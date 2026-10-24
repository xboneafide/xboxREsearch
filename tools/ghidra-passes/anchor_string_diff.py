# Ghidra post-script: anchor-string-driven partial decompile diff.
# Decompile all functions referencing a set of anchor strings/imports, and
# dump the function list with sizes. Suited to small binaries (tens of KB)
# where a full sweep is cheap but you only want the functions that matter
# for a targeted question (e.g. which code touches a mount API family).
#
# Edit NEEDLES to the string family under investigation.
# @category Analysis
from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor
import os

NEEDLES = [b'XCrdMount\x00', b'XCrdMountContentType\x00', b'XCrdBuildPath\x00',
           b'XCrdQueryXvdInformation\x00', b'XCrdCreate\x00', b'g:',
           b'MountVolume', b'XCrd']

# Output path from script args (first arg) or the user temp dir.
tag = getScriptArgs()[0] if getScriptArgs() else 'unknown'
outdir = getScriptArgs()[1] if len(getScriptArgs()) > 1 else os.path.join(
    os.environ.get('TEMP', os.environ.get('TMP', '.')), 'ghidra-decomp')
OUT = os.path.join(outdir, '%s-decomp.c' % tag)
os.makedirs(outdir, exist_ok=True)

monitor = ConsoleTaskMonitor()
di = DecompInterface()
di.openProgram(currentProgram)

# find anchor strings (ASCII, embedded in the binary)
anchors = []
minA = currentProgram.getAddressFactory().getDefaultAddressSpace().getMinAddress()
for pat in NEEDLES:
    patstr = '\\x' + '\\x'.join('%02x' % c for c in bytearray(pat))
    a = findBytes(minA, patstr)
    while a is not None:
        anchors.append(a.toString())
        a = findBytes(a.add(1), patstr)

print('%s anchors: %d' % (tag, len(anchors)))

# collect functions referencing the anchors
rf = currentProgram.getReferenceManager()
funcs = {}
for addr_str in anchors:
    try:
        addr = currentProgram.getAddressFactory().getAddress(addr_str)
    except Exception:
        continue
    for ref in rf.getReferencesTo(addr):
        f = getFunctionContaining(ref.getFromAddress())
        if f is not None:
            funcs[str(f.getEntryPoint())] = f

# fallback: if no refs resolved, decompile everything (small binaries only)
if not funcs:
    for f in currentProgram.getFunctionManager().getFunctions(True):
        funcs[str(f.getEntryPoint())] = f

print('%s functions to decompile: %d' % (tag, len(funcs)))
with open(OUT, 'w') as fh:
    fh.write('// %s anchor-string decompile\n' % tag)
    for key, f in sorted(funcs.items()):
        try:
            res = di.decompileFunction(f, 120, monitor)
            fh.write('\n// ===== FUNC %s @ %s (size %d) =====\n' % (
                f.getName(), key, f.getBody().getNumAddresses()))
            fh.write(res.getDecompiledFunction().getC() if res.decompileCompleted()
                     else '// failed: %s\n' % res.getErrorMessage())
        except Exception as e:
            fh.write('// exception: %s\n' % (key, e))
print('wrote %s' % OUT)
