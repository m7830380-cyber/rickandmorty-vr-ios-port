#!/bin/bash
# Runs the Unity editor in batch mode with a watchdog so a hung editor cannot stall the job.
#   ci/unity.sh <log file> <timeout seconds> <done regex or ""> <unity args...>
# Returns Unity's exit code; 0 if the done regex appeared in the log (Unity is then killed if it lingers);
# 124 on timeout. The log is followed live so the job output shows progress.
LOG="$1"; LIMIT="$2"; DONE="$3"; shift 3
# UNITY_BIN: another editor to run (the licensing editor); defaults to Unity 5.4.
UNITY="${UNITY_BIN:-/Applications/Unity/Unity.app/Contents/MacOS/Unity}"
# Unity 5.4 is Intel-only: run it natively on Intel runners, under Rosetta on Apple silicon.
ARCH=""; [ "$(uname -m)" = arm64 ] && ARCH="arch -x86_64"
# UNITY_DYLD: libraries to inject (ci/dirshim). Passed here, not exported earlier, because macOS strips
# DYLD_* variables whenever a system binary such as /bin/bash starts.
INJECT=""; [ -n "$UNITY_DYLD" ] && INJECT="env DYLD_INSERT_LIBRARIES=$UNITY_DYLD"
rm -f "$LOG"; touch "$LOG"
$ARCH $INJECT "$UNITY" -batchmode -nographics -logFile "$LOG" "$@" &
PID=$!
# The job log is public: serial numbers and email addresses are blanked before printing.
tail -n +1 -f "$LOG" > >(grep --line-buffered -iE "licens|error|exception|build|\[RMVR|compil|exiting" |
  sed -l -E 's/[A-Z0-9]{2}-[A-Z0-9]{4}-[A-Z0-9X-]{4,}/<serial>/g; s/[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/<email>/g' |
  cut -c1-300) &
TAILPID=$!
start=$SECONDS; rc=""
while kill -0 $PID 2>/dev/null; do
  if [ -n "$DONE" ] && grep -qE "$DONE" "$LOG"; then
    echo "[unity.sh] done marker seen after $((SECONDS-start))s"
    for i in $(seq 1 30); do kill -0 $PID 2>/dev/null || break; sleep 1; done
    kill -9 $PID 2>/dev/null; rc=0; break
  fi
  if [ $((SECONDS-start)) -ge "$LIMIT" ]; then
    echo "[unity.sh] TIMEOUT after ${LIMIT}s - killing Unity"; kill -9 $PID 2>/dev/null; rc=124; break
  fi
  sleep 5
done
if [ -z "$rc" ]; then wait $PID; rc=$?; fi
if [ -n "$DONE" ] && [ "$rc" != 0 ] && grep -qE "$DONE" "$LOG"; then rc=0; fi
sleep 1; kill $TAILPID 2>/dev/null
echo "[unity.sh] exit $rc after $((SECONDS-start))s"
exit $rc
