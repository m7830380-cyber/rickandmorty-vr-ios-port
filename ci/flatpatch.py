# Turns the game's SteamVR player rig into the flat rig by pointing components at flat replacement scripts.
# Only the script reference (m_Script guid) changes: the component keeps its fileID and serialized fields, so
# everything in the game that references it keeps working. Run on the composed project before Unity opens it.
# Usage: python3 flatpatch.py <project dir>
# Prints only counts and script names (the CI log is public).
import glob
import os
import re
import sys

SWAPS = {
    # game script                      -> replacement
    "SteamVR_HMDAndInputController": "Flat_HMDAndInputController",
    "SteamVR_TrackedInputController": "Flat_TrackedInputController",
    "ScreenFader_SteamVR": "ScreenFader",
}

assets = os.path.join(sys.argv[1], "Assets")
by_name = {}
for meta in glob.glob(os.path.join(assets, "**", "*.cs.meta"), recursive=True):
    name = os.path.basename(meta)[:-len(".cs.meta")]
    m = re.search(r"^guid: ([0-9a-f]{32})", open(meta, encoding="utf-8").read(), re.M)
    if m:
        by_name.setdefault(name, []).append(m.group(1))

pairs = []
for old, new in SWAPS.items():
    for n in (old, new):
        if len(by_name.get(n, [])) != 1:
            sys.exit("flatpatch: expected exactly one script named %s, found %d" % (n, len(by_name.get(n, []))))
    pairs.append((by_name[old][0], by_name[new][0], old, new))

files = glob.glob(os.path.join(assets, "**", "*.prefab"), recursive=True) + \
        glob.glob(os.path.join(assets, "**", "*.unity"), recursive=True)
counts = {p[2]: 0 for p in pairs}
changed = 0
for path in files:
    with open(path, encoding="utf-8", newline="") as f:
        text = f.read()
    orig = text
    for old_guid, new_guid, old, _ in pairs:
        pattern = "m_Script: {fileID: 11500000, guid: %s, type: 3}" % old_guid
        n = text.count(pattern)
        if n:
            counts[old] += n
            text = text.replace(pattern, "m_Script: {fileID: 11500000, guid: %s, type: 3}" % new_guid)
    if text != orig:
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(text)
        changed += 1

for old_guid, new_guid, old, new in pairs:
    print("flatpatch: %s -> %s: %d component(s)" % (old, new, counts[old]))
print("flatpatch: %d file(s) changed" % changed)
if counts["SteamVR_HMDAndInputController"] == 0 or counts["SteamVR_TrackedInputController"] == 0:
    sys.exit("flatpatch: player rig not found")
