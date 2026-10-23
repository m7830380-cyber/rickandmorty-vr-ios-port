# Prints the machine code around every place a Mach-O x86_64 binary loads the given C strings.
# Used to see what Unity's editor checks before it registers a platform module.
# Usage: python3 xref.py <binary> <string> [<string> ...]
import sys
import lief
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

binary = lief.parse(sys.argv[1])
if hasattr(binary, "take"):
    binary = binary.take(lief.MachO.Header.CPU_TYPE.X86_64)
text = binary.get_section("__text")
code = bytes(text.content)
base = text.virtual_address

cstr = binary.get_section("__cstring")
cdata = bytes(cstr.content)
targets = {}
for s in sys.argv[2:]:
    i = cdata.find(s.encode() + b"\0")
    while i >= 0:
        if i == 0 or cdata[i - 1] == 0:
            targets[cstr.virtual_address + i] = s
        i = cdata.find(s.encode() + b"\0", i + 1)
print("string addresses:", {hex(k): v for k, v in targets.items()})

md = Cs(CS_ARCH_X86, CS_MODE_64)
md.skipdata = True
insns = list(md.disasm(code, base))
print("instructions:", len(insns))
names = {}
for sym in binary.symbols:
    if sym.value:
        names[sym.value] = sym.name

def label(addr):
    return names.get(addr, "")

for idx, ins in enumerate(insns):
    if ins.mnemonic != "lea" or "rip" not in ins.op_str:
        continue
    try:
        disp = int(ins.op_str.split("rip")[1].split("]")[0].replace(" ", ""), 16)
    except ValueError:
        continue
    addr = ins.address + ins.size + disp
    if addr not in targets:
        continue
    print("\n==== %s referenced at %#x" % (targets[addr], ins.address))
    for j in range(max(0, idx - 120), min(len(insns), idx + 25)):
        k = insns[j]
        extra = ""
        if k.mnemonic == "call" and k.op_str.startswith("0x"):
            extra = "  ; " + label(int(k.op_str, 16))
        print("%#x: %-6s %s%s%s" % (k.address, k.mnemonic, k.op_str, extra, "   <<<<" if j == idx else ""))
