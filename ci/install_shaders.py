# Replaces the placeholder shaders AssetRipper writes (the game ships compiled DirectX shaders only) with real
# ones, keeping each file's .meta so every material still points at it:
#   - game/Shaders/*.shader   our replacements for Owlchemy's own shaders (same names and properties)
#   - Unity's built-in shader source for copies of built-in shaders (particles, mobile, sprites, skybox...)
#   - TextMeshPro's shaders for the old TMPro ones
# Usage: python3 install_shaders.py <project dir> <our shaders dir> <builtin_shaders dir> <textmeshpro package dir>
import glob
import os
import re
import shutil
import sys

project, ours, builtin, tmp = sys.argv[1:5]
assets = os.path.join(project, "Assets")
MARK = "DummyShaderTextExporter"

placeholders = [p for p in glob.glob(os.path.join(assets, "**", "*.shader"), recursive=True)
                if MARK in open(p, encoding="utf-8", errors="replace").read()]
print("placeholder shaders:", len(placeholders))


def index(root, pattern="*.shader"):
    out = {}
    for p in glob.glob(os.path.join(root, "**", pattern), recursive=True):
        out.setdefault(os.path.basename(p), p)
    return out


our_shaders = index(ours)
builtin_shaders = index(builtin)
tmp_shaders = index(tmp)
ALIASES = {"Shader.shader": "Standard.shader"}  # a renamed copy of Unity's Standard shader


def tmp_name(name):
    # old TextMeshPro: TMPro_SDF-Mobile.shader -> package 3.x: TMP_SDF-Mobile.shader
    return re.sub(r"^TMPro_", "TMP_", name)


def copy_includes(src_dir, dst_dir):
    for inc in glob.glob(os.path.join(src_dir, "*.cginc")):
        shutil.copyfile(inc, os.path.join(dst_dir, os.path.basename(inc)))


done, left = 0, []
for path in placeholders:
    name = os.path.basename(path)
    src, kind = None, None
    if name in our_shaders:
        src, kind = our_shaders[name], "ours"
    elif ALIASES.get(name, name) in builtin_shaders:
        src, kind = builtin_shaders[ALIASES.get(name, name)], "builtin"
    elif tmp_name(name) in tmp_shaders:
        src, kind = tmp_shaders[tmp_name(name)], "tmp"
    if src is None:
        left.append(name)
        continue
    text = open(src, encoding="utf-8").read()
    if kind == "tmp":
        # keep the old TMPro shader names the game's scripts look up
        text = text.replace('Shader "TextMeshPro/', 'Shader "TMPro/')
    open(path, "w", encoding="utf-8").write(text)
    copy_includes(os.path.dirname(src), os.path.dirname(path))
    done += 1
    print("  %-8s %s" % (kind, name))

print("replaced %d, still placeholders: %d %s" % (done, len(left), sorted(left)))
