// mitm-listener - PC-side MITM for the Xbox: PATH-based gate + full passthru.
//
//   /IsSystemUpdateAvailable (and update-check family) -> file-driven responses (gate key)
//   EVERYTHING ELSE -> transparent passthru to the real endpoint via DoH(1.1.1.1)-resolved
//   upstream IPs (dodges our own DNS override). Console stays fully functional:
//   enumeration, manifests, licenses, profile sync all relay untouched.
//
// Usage:  dotnet tools/mitm-listener/bin/Release/net10.0/mitm-listener.dll <server.pfx>
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var pfx = args.Length > 0 ? args[0]
        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "mitm-pki", "server2.pfx"));
if (!File.Exists(pfx)) { Console.WriteLine($"server.pfx not found: {pfx}"); return 1; }
var cert = new X509Certificate2(pfx, "", X509KeyStorageFlags.DefaultKeySet);
Console.WriteLine($"cert: {cert.Subject} (thumb {cert.Thumbprint})");

var logPath = Path.Combine(Path.GetDirectoryName(pfx)!, "capture.log");
var proxyLog = Path.Combine(Path.GetDirectoryName(pfx)!, "passthru.log");
var manifestDir = Path.Combine(Path.GetDirectoryName(pfx)!, "manifests");
Directory.CreateDirectory(manifestDir);
void LogC(string msg) { Console.WriteLine(msg); File.AppendAllText(logPath, msg + Environment.NewLine); }
void LogP(string msg) { Console.WriteLine(msg); File.AppendAllText(proxyLog, msg + Environment.NewLine); }

// resolve hostname via DoH (bypasses our own LAN DNS override - self-loop guard)
async Task<IPAddress?> ResolveRealAsync(string host)
{
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("accept", "application/dns-json");
        var json = await http.GetStringAsync($"https://1.1.1.1/dns-query?name={host}&type=A");
        var i = json.IndexOf("\"data\":\"");
        while (i >= 0)
        {
            var start = i + 8;   // "data":" is 8 chars - off-by-one made every value empty
            var end = json.IndexOf('"', start);
            var val = json[start..end];
            if (IPAddress.TryParse(val, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
                return ip;
            i = json.IndexOf("\"data\":\"", end);
        }
    }
    catch (Exception ex) { LogP($"[doh] resolve {host} failed: {ex.Message}"); }
    return null;
}

var passthruClient = new HttpClient(new SocketsHttpHandler
{
    ConnectCallback = async (ctx, ct) =>
    {
        var host = ctx.DnsEndPoint.Host;
        var ip = await ResolveRealAsync(host) ?? throw new Exception($"DoH resolve failed for {host}");
        LogP($"[doh] {host} -> {ip}");
        var sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        await sock.ConnectAsync(new IPEndPoint(ip, ctx.DnsEndPoint.Port), ct);
        return new NetworkStream(sock, ownsSocket: true);
    },
    AutomaticDecompression = System.Net.DecompressionMethods.None,
});
passthruClient.DefaultRequestHeaders.ExpectContinue = false;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(443, lo =>
    {
        lo.Protocols = HttpProtocols.Http2 | HttpProtocols.Http1;
        lo.UseHttps(new HttpsConnectionAdapterOptions
        {
            ServerCertificate = cert,
            ClientCertificateMode = ClientCertificateMode.AllowCertificate,
        });
    });
    o.ListenAnyIP(80);
    o.Limits.MaxRequestBodySize = 4 * 1024 * 1024;
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var app = builder.Build();

app.Run(async ctx =>
{
    var stamp = DateTime.Now.ToString("HH:mm:ss");
    var remote = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
    var host = ctx.Request.Host.Host ?? "";
    var requestPath = ctx.Request.Path.ToString();

    var sb = new StringBuilder();
    sb.AppendLine($"[{stamp}] {ctx.Request.Method} https://{host}{requestPath} from {remote} proto={ctx.Request.Protocol}");
    foreach (var h in ctx.Request.Headers) sb.AppendLine($"  H {h.Key}: {h.Value}");
    string body = "";
    try
    {
        using var r = new StreamReader(ctx.Request.Body, Encoding.UTF8, false, 8192, leaveOpen: true);
        body = await r.ReadToEndAsync();
    }
    catch { }
    sb.AppendLine($"  BODY: {body}");
    LogC(sb.ToString());

    // ---- PATH-based gate: only update-check endpoints are impersonated
    var isGatePath = requestPath.Contains("IsSystemUpdateAvailable") ||
                     requestPath.Contains("IsUpdateAvailable") ||
                     requestPath.Contains("GetSpecificSystemVersion") ||
                     requestPath.Contains("/SLS/");

    if (isGatePath)
    {
        var respDir = Path.GetDirectoryName(pfx)!;
        var p204 = Path.Combine(respDir, "response.204");
        var pBody = Path.Combine(respDir, "response.body");
        var pStat = Path.Combine(respDir, "response.status");
        var pType = Path.Combine(respDir, "response.ctype");

        if (File.Exists(p204)) { ctx.Response.StatusCode = 204; LogC("  <- 204"); return; }
        var status = 200;
        if (int.TryParse(File.Exists(pStat) ? File.ReadAllText(pStat).Trim() : "", out var s)) status = s;

        byte[] respBody;
        try
        {
            string cid = "", vid = "";
            var cidIdx = body.IndexOf("\"ContentId\":\"");
            if (cidIdx >= 0) { var s2 = cidIdx + 13; var e2 = body.IndexOf("\"", s2); if (e2 > s2) cid = body.Substring(s2, e2 - s2); }
            var vidIdx = body.IndexOf("\"VersionId\":\"");
            if (vidIdx >= 0) { var s2 = vidIdx + 13; var e2 = body.IndexOf("\"", s2); if (e2 > s2) vid = body.Substring(s2, e2 - s2); }

            // MODE FILE: mitm-pki\response.mode = noupdate | sameversion | mandatory
            // Switchable without rebuild. Captured real schemas:
            //  - noupdate   = exact mirror of real server's "already current" response (329B)
            //  - sameversion= UpdateAvailable:true targeting console's own build, Files:[]
            //  - mandatory  = same as sameversion but IsMandatory:true
            var mode = "noupdate";
            var pMode = Path.Combine(respDir, "response.mode");
            if (File.Exists(pMode)) mode = File.ReadAllText(pMode).Trim().ToLowerInvariant();

            if (!string.IsNullOrEmpty(cid) && !string.IsNullOrEmpty(vid))
            {
                string dyn = mode switch
                {
                    "sameversion" => $"{{\"ContentId\":\"{cid}\",\"TargetVersionId\":\"{vid}\",\"IncomingVersionId\":\"{vid}\",\"Files\":[],\"IsMandatory\":false,\"AvailabilityDate\":\"2024-06-01T00:00:00\",\"ExpirationDate\":\"9999-12-31T23:59:59\",\"SequenceDate\":\"2024-06-01T00:00:00\",\"BuildName\":\"Durango 25398.4908\",\"UpdateAvailable\":true,\"EstimatedDownloadSize\":0,\"UpdateType\":2}}",
                    "mandatory" => $"{{\"ContentId\":\"{cid}\",\"TargetVersionId\":\"{vid}\",\"IncomingVersionId\":\"{vid}\",\"Files\":[],\"IsMandatory\":true,\"AvailabilityDate\":\"2024-06-01T00:00:00\",\"ExpirationDate\":\"9999-12-31T23:59:59\",\"SequenceDate\":\"2024-06-01T00:00:00\",\"BuildName\":\"Durango 25398.4908\",\"UpdateAvailable\":true,\"EstimatedDownloadSize\":0,\"UpdateType\":2}}",
                    _ => $"{{\"ContentId\":\"{cid}\",\"TargetVersionId\":null,\"IncomingVersionId\":\"{vid}\",\"Files\":[],\"IsMandatory\":false,\"AvailabilityDate\":null,\"ExpirationDate\":null,\"SequenceDate\":null,\"BuildName\":null,\"UpdateAvailable\":false,\"EstimatedDownloadSize\":0,\"UpdateType\":0}}"
                };
                respBody = Encoding.UTF8.GetBytes(dyn);
            }
            else
            {
                respBody = File.Exists(pBody) ? File.ReadAllBytes(pBody) : Encoding.UTF8.GetBytes("{\"UpdateAvailable\":false,\"IsMandatory\":false}");
            }
        }
        catch
        {
            respBody = File.Exists(pBody) ? File.ReadAllBytes(pBody) : Encoding.UTF8.GetBytes("{\"UpdateAvailable\":false,\"IsMandatory\":false}");
        }
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = File.Exists(pType) ? File.ReadAllText(pType).Trim() : "application/json";
        ctx.Response.ContentLength = respBody.Length;
        LogC($"  <- {status} ({respBody.Length}B)");
        await ctx.Response.Body.WriteAsync(respBody);
        return;
    }

    // ---- everything else: transparent passthru to real upstream
    try
    {
        // PATCH: capture full request for licensing/collections (device ticket hunt)
        if (host.EndsWith("licensing.mp.microsoft.com") || host.EndsWith("collections.mp.microsoft.com"))
        {
            var licDir = Path.Combine(Path.GetDirectoryName(pfx)!, "license-capture");
            Directory.CreateDirectory(licDir);
            var licSb = new StringBuilder();
            licSb.AppendLine($"[{stamp}] {ctx.Request.Method} https://{host}{requestPath}{ctx.Request.QueryString}");
            foreach (var h in ctx.Request.Headers) licSb.AppendLine($"  H {h.Key}: {h.Value}");
            licSb.AppendLine("  BODY:");
            licSb.AppendLine(body);
            File.WriteAllText(Path.Combine(licDir, $"req_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt"), licSb.ToString());
            LogP($"[{stamp}] LICENSING-CAPTURE {ctx.Request.Method} {requestPath} ({body.Length}B body)");
        }
        var fwd = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), $"https://{host}{requestPath}{ctx.Request.QueryString}");
        foreach (var h in ctx.Request.Headers)
        {
            if (h.Key is "Host" or "Connection" or "Keep-Alive" or "Transfer-Encoding" or "Upgrade" or "Content-Type" or "Content-Length") continue;
            try { fwd.Headers.Add(h.Key, h.Value.ToArray()); } catch { }
        }
        if (body.Length > 0)
        {
            fwd.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            if (ctx.Request.ContentType != null && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(ctx.Request.ContentType, out var mt))
                fwd.Content.Headers.ContentType = mt;
        }
        LogP($"[{stamp}] PASSTHRU {ctx.Request.Method} {host}{requestPath}");
        var resp = await passthruClient.SendAsync(fwd);
        var respBody = await resp.Content.ReadAsByteArrayAsync();
        LogP($"[{stamp}] PASSTHRU <- {(int)resp.StatusCode} ({respBody.Length}B)");
        // save manifest dump
        try
        {
            var safeHost = string.Join("_", host.Split(Path.GetInvalidFileNameChars()));
            var safePath = string.Join("_", requestPath.Replace('/','_').Trim('_').Split(Path.GetInvalidFileNameChars()));
            var fn = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{safeHost}_{safePath}_{respBody.Length}b.bin";
            File.WriteAllBytes(Path.Combine(manifestDir, fn), respBody);
        }
        catch { }
        // PATCH: capture licensing response (device ticket validation result)
        if (host.EndsWith("licensing.mp.microsoft.com") || host.EndsWith("collections.mp.microsoft.com"))
        {
            var licDir2 = Path.Combine(Path.GetDirectoryName(pfx)!, "license-capture");
            var sb2 = new StringBuilder();
            sb2.AppendLine($"[{stamp}] <- {(int)resp.StatusCode} {requestPath}");
            foreach (var h in resp.Headers) sb2.AppendLine($"  H {h.Key}: {h.Value}");
            sb2.AppendLine("  BODY:");
            sb2.AppendLine(Encoding.UTF8.GetString(respBody));
            File.WriteAllText(Path.Combine(licDir2, $"resp_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt"), sb2.ToString());
        }
        ctx.Response.StatusCode = (int)resp.StatusCode;
        ctx.Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        ctx.Response.ContentLength = respBody.Length;
        await ctx.Response.Body.WriteAsync(respBody);
    }
    catch (Exception ex)
    {
        LogP($"[{stamp}] PASSTHRU-ERR {host}: {ex.InnerException?.Message ?? ex.Message}");
        ctx.Response.StatusCode = 503;
    }
});

app.Run();
return 0;