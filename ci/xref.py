# Prints the machine code around every place a Mach-O x86_64 binary refers to the given C strings or addresses.
# Used to see what Unity's editor checks before it registers a platform module.
# Usage: python3 xref.py <binary> <text or 0xSTART-0xEND> ...
# A text matches every C string that contains it; an address range matches any RIP-relative access inside it.
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
ranges = []
for s in sys.argv[2:]:
    if s.startswith("0x") and "-" in s:
        lo, hi = (int(x, 16) for x in s.split("-"))
        ranges.append((lo, hi, s))
        continue
    i = cdata.find(s.encode())
    while i >= 0:
        start = cdata.rfind(b"\x00", 0, i) + 1
        end = cdata.find(b"\x00", i)
        targets[cstr.virtual_address + start] = cdata[start:end].decode("latin-1")
        i = cdata.find(s.encode(), end)
print("string addresses:", {hex(k): v for k, v in targets.items()})


def target(addr):
    if addr in targets:
        return targets[addr]
    for lo, hi, name in ranges:
        if lo <= addr <= hi:
            return "%s (+%#x)" % (name, addr - lo)
    return None


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
    if "rip" not in ins.op_str:
        continue
    try:
        disp = int(ins.op_str.split("rip")[1].split("]")[0].replace(" ", "") or "0", 16)
    except ValueError:
        continue
    hit = target(ins.address + ins.size + disp)
    if hit is None:
        continue
    print("\n==== %r referenced at %#x" % (hit, ins.address))
    for j in range(max(0, idx - 120), min(len(insns), idx + 25)):
        k = insns[j]
        extra = ""
        if k.mnemonic == "call" and k.op_str.startswith("0x"):
            extra = "  ; " + label(int(k.op_str, 16))
        print("%#x: %-6s %s%s%s" % (k.address, k.mnemonic, k.op_str, extra, "   <<<<" if j == idx else ""))
