#!/bin/bash
# Stands in for Unity 5.4's 32-bit "mono" programs (Contents/Mono/bin/mono and
# Contents/MonoBleedingEdge/bin/mono), which macOS 10.15+ cannot run, using the 64-bit mono-sgen that
# Unity ships in MonoBleedingEdge.
# Script compiles (smcs.exe) are redirected to MonoBleedingEdge's mcs.exe, told to build against Unity's
# own "unity" (.NET 2.0) libraries so the result still matches the editor's runtime.
C="$(cd "$(dirname "$0")/../.." && pwd)"
SGEN="$C/MonoBleedingEdge/bin/mono-sgen"
PROFILE="$C/Mono/lib/mono/unity"
# Unity points these at its 2.0 corlib, which the newer runtime refuses to load.
unset MONO_PATH MONO_CFG_DIR
if [[ "$1" == *smcs.exe ]]; then
  shift
  MCS="$(ls "$C"/MonoBleedingEdge/lib/mono/4.5/mcs.exe "$C"/MonoBleedingEdge/lib/mono/4.0/mcs.exe 2>/dev/null | head -n 1)"
  exec "$SGEN" "$MCS" -noconfig -nostdlib -lib:"$PROFILE" \
    -r:mscorlib.dll -r:System.dll -r:System.Core.dll -r:System.Xml.dll "$@"
fi
exec "$SGEN" "$@"
