#!/usr/bin/env python3
"""Pre-commit scrub gate for xbone-deep-analysis.

Run against staged files (or a path list) before committing. Exits non-zero
if any file looks like a banned artifact or contains identifier-shaped data
that was not deliberately redacted.

Usage:
  python scripts/scrub_check.py            # checks `git diff --cached --name-only`
  python scripts/scrub_check.py PATH...    # checks explicit paths
"""
import os
import re
import subprocess
import sys

# 1b) Piracy-adjacent links: no torrent/magnet/IA/mirror LINKS anywhere.
# Patterns are link-shaped (URLs/URIs), not bare words, so policy prose and
# this script's own rule text do not trip the gate.
LINK_PATTERNS = [
    (r"(?i)https?://\S*torrent|[\w/\\.-]+\.torrent\b", "torrent link"),
    (r"(?i)magnet:\?xt=", "magnet link"),
    (r"(?i)(https?://|\b[\w.-]*\b)?\b(archive\.org|web\.archive\.org|archive\.ph|archive\.today)\b|\bwayback\.\S+",
     "Internet Archive / Wayback link"),
    (r"(?i)https?://\S*(1337x|piratebay|nyaa|libgen|annas[- ]?archive|z-?lib)",
     "mirror/piracy site link"),
]

# 1) Filenames that must never be committed (artifact classes).
BANNED_FILE_PATTERNS = [
    r"(?i)\.hive$",
    r"(?i)\.(bin|pcap|pcapng|etl|pfx|p12|key|cer|crt|csr|srl)$",
    r"(?i)(^|/)license\.cs$",
    r"(?i)\.(xvd|xvc|msixvc)$",
    r"(?i)\.(7z|zip|rar|exe|dll|pdb)$",
    r"(?i)(^|/)tokens?\.json$",
    r"(?i)(lic(req|resp)|renew(req|resp)|collreq|userroot|update_xsts)",
    r"(?i)(^|/)users\.csv$",
    r"(?i)-decomp\.(c|h)$",
    r"(?i)spire-ghidra-.*\.c$",
    r"(?i)capture\.log",
    r"(?i)pasthru\.log|passthru\.log",
    r"(?i)listener-(out|err)\.log",
    r"(?i)console-mitm-captures-",
]

# 2) Identifier-shaped content that requires redaction. These regexes match
#    strings as they would appear UNREDACTED; after scrubbing they should not
#    match. A match is a hard failure unless the line contains an explicit
#    `# redacted` / `<!-- redacted -->` marker.
ID_PATTERNS = [
    (r"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\b",
     "GUID"),
    (r"\b[0-9A-Fa-f]{40,}\b", "long-hex-blob (40+ hex chars; token/key-shaped)"),
    (r"\b(?:\d[ :\-]?){15,}\b", "long digit run (serial/ID-shaped)"),
    # MAC-ish
    (r"\b[0-9A-Fa-f]{2}(?::[0-9A-Fa-f]{2}){5}\b", "MAC-address-shaped"),
    # Email-ish
    (r"\b[\w.+-]+@[\w-]+\.[\w.-]+\b", "email-shaped"),
]

REDACTION_MARKER = re.compile(r"redacted", re.IGNORECASE)

# Allowlist of literal strings that are format constants, not identifiers
# (e.g. registry storage GUIDs that are the same on every console).
# One literal per line; lines containing them are exempt from ID checks.
ALLOWLIST_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "scrub_allowlist.txt")


def load_allowlist():
    try:
        return [l.strip() for l in open(ALLOWLIST_FILE, encoding="utf-8") if l.strip() and not l.startswith("#")]
    except OSError:
        return []


def staged_files():
    out = subprocess.run(
        ["git", "diff", "--cached", "--name-only", "--diff-filter=ACM"],
        capture_output=True, text=True,
    )
    return [f for f in out.stdout.splitlines() if f.strip()]


def check_file(path):
    problems = []
    for pat in BANNED_FILE_PATTERNS:
        if re.search(pat, path.replace("\\", "/")):
            problems.append(f"banned file pattern: {pat}")
    # The gate script itself is the trusted transform: skip its own content.
    if os.path.abspath(path) == os.path.abspath(__file__):
        return []
    try:
        text = open(path, encoding="utf-8", errors="replace").read()
    except OSError as e:
        return [f"unreadable: {e}"]
    allowlist = load_allowlist()
    for line_no, line in enumerate(text.splitlines(), 1):
        if REDACTION_MARKER.search(line):
            continue
        if any(a in line for a in allowlist):
            continue
        if re.match(r"^From [0-9a-f]{40} ", line):  # git format-patch header (local commit SHA)
            continue
        for pat, label in ID_PATTERNS:
            if re.search(pat, line):
                problems.append(f"line {line_no}: unredacted {label}: {line.strip()[:100]}")
        for pat, label in LINK_PATTERNS:
            if re.search(pat, line):
                problems.append(f"line {line_no}: forbidden {label}: {line.strip()[:100]}")
    return problems


def main():
    paths = sys.argv[1:] or staged_files()
    if not paths:
        print("nothing staged to check")
        return 0
    failures = 0
    for p in paths:
        probs = check_file(p)
        if probs:
            failures += 1
            print(f"FAIL {p}")
            for pr in probs:
                print(f"     {pr}")
    if failures:
        print(f"\n{failures} file(s) failed the scrub gate. Fix or redact before committing.")
        return 1
    print(f"scrub gate: {len(paths)} file(s) clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
