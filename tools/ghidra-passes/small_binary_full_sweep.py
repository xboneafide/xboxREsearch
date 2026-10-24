# Ghidra post-script: full decompile sweep tuned for SMALL binaries
# (tens of KB — e.g. OS service components and era binaries). Same idea as
# full_decomp.py but with a longer per-function timeout and a per-program
# output file named after the program.
# @category Analysis
from ghidra.app.decompiler import DecompInterface
from ghidra.util.task import ConsoleTaskMonitor
import os

monitor = ConsoleTaskMonitor()
di = DecompInterface()
di.openProgram(currentProgram)
fm = currentProgram.getFunctionManager()

# Output dir from script args or the user temp dir; one file per program.
outdir = getScriptArgs()[0] if getScriptArgs() else os.path.join(
    os.environ.get('TEMP', os.environ.get('TMP', '.')), 'ghidra-decomp')
os.makedirs(outdir, exist_ok=True)
name = currentProgram.getName().replace('.exe', '').replace('.dll', '')
OUT = os.path.join(outdir, '%s-decomp.c' % name)

print('analyzing %s' % currentProgram.getName())

count = 0
errors = 0
with open(OUT, 'w') as fh:
    fh.write('// %s\n' % currentProgram.getName())
    for f in fm.getFunctions(True):
        if monitor.isCancelled():
            break
        count += 1
        try:
            res = di.decompileFunction(f, 120, monitor)
            fh.write('\n// ===== FUNC %s @ %s =====\n' % (f.getName(), f.getEntryPoint()))
            if res.decompileCompleted():
                fh.write(res.getDecompiledFunction().getC())
            else:
                errors += 1
                fh.write('// decompile failed: %s\n' % res.getErrorMessage())
        except Exception as e:
            fh.write('// exception: %s\n' % e)
print('decompiled %d funcs (%d errors) -> %s' % (count, errors, OUT))
