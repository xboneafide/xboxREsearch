# Ghidra post-script: full decompile sweep of all functions in the current
# program. Writes one big C corpus for offline grep.
# @category Analysis
from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor
import os

# Output path is taken from script args (second arg) or a default under the
# user's temp dir; never hardcode machine-local paths.
tag = getScriptArgs()[0] if getScriptArgs() else 'prog'
outdir = getScriptArgs()[1] if len(getScriptArgs()) > 1 else os.path.join(
    os.environ.get('TEMP', os.environ.get('TMP', '.')), 'ghidra-decomp')
OUT = os.path.join(outdir, '%s-full.c' % tag)
os.makedirs(outdir, exist_ok=True)

monitor = ConsoleTaskMonitor()
di = DecompInterface()
di.openProgram(currentProgram)

fmgr = currentProgram.getFunctionManager()
total = 0
with open(OUT, 'w') as fh:
    for f in fmgr.getFunctions(True):
        if monitor.isCancelled():
            break
        try:
            res = di.decompileFunction(f, 90, monitor)
            fh.write('\n// ===== FUNC %s @ %s =====\n' % (f.getName(), f.getEntryPoint()))
            fh.write(res.getDecompiledFunction().getC() if res.decompileCompleted()
                     else '// failed: %s\n' % res.getErrorMessage())
            total += 1
        except Exception as e:
            fh.write('// exception %s: %s\n' % (f.getName(), e))
print('wrote %s (%d functions)' % (OUT, total))
