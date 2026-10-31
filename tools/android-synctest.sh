#!/usr/bin/env bash
# Runs Mandible's --synctest on a connected Android device or emulator and saves the checksum trail.
# Usage: bash tools/android-synctest.sh path/to/Mandible.apk [seconds]
set -u
APK="$1"
SECONDS_TO_PLAY="${2:-600}"
PKG=com.mandible.warbeneaththegrass

adb install -r "$APK"
# phones have no command line: hand the arguments over in a file inside the app's own storage
adb shell "run-as $PKG sh -c 'mkdir -p files && echo --synctest=$SECONDS_TO_PLAY > files/test_args.txt'"
adb logcat -c
# start it exactly as tapping the home-screen icon does (the launcher activity's class name varies)
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1

for i in $(seq 1 110); do
    sleep 15
    lines=$(adb logcat -d | grep -c "\[synctest\]" || true)
    echo "after $((i * 15))s: $lines sync lines; latest: $(adb logcat -d | grep -oE '\[(args|synctest)\].*' | tail -n 1)"
    if [ "$i" -eq 4 ]; then adb logcat -d | grep -iE "godot|mono|dotnet|fatal|crash" | tail -n 40; fi
    if adb logcat -d | grep -q "synctest\] done"; then break; fi
    if ! adb shell pidof "$PKG" > /dev/null; then echo "the app is no longer running"; break; fi
done

adb logcat -d | grep -oE "\[synctest\].*" | tee synctest-android.txt
adb logcat -d | grep -iE "godot|mono|fatal|crash" | tail -n 120 > logcat-tail.txt || true
grep -q "synctest\] done" synctest-android.txt
