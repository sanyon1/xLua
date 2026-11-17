#!/bin/bash
# Runs the Unity verification suites against the package in this repository.
#
#   WORK=<empty scratch dir> [UNITY=<Unity.exe>] [UNITY6=<Unity.exe>] ./run.sh [hotfix] [unit] [consumer]
#
# Without arguments all three suites run. Each suite copies what it needs into WORK, so the
# repository's own Unity project is never opened or modified. See README.md.

set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
: "${WORK:?set WORK to an empty scratch directory}"
UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/2021.2.1f1/Editor/Unity.exe}"
UNITY6="${UNITY6:-/c/Program Files/Unity/Hub/Editor/6000.2.6f2/Editor/Unity.exe}"
SUITES="${*:-hotfix unit consumer}"
mkdir -p "$WORK"

batch() { # <unity> <project> <method> <log> [extra args]
    local unity="$1" project="$2" method="$3" log="$4"; shift 4
    "$unity" -batchmode -quit -nographics -projectPath "$project" -executeMethod "$method" -logFile "$log" "$@"
}

copy_project() { # <target dir>: the repository's Unity project without tests and generated code
    local target="$1"
    mkdir -p "$target"
    cp -r "$REPO/src/xLua.Unity/Assets" "$REPO/src/xLua.Unity/Packages" "$REPO/src/xLua.Unity/ProjectSettings" "$target/"
    rm -rf "${target:?}/Assets/xLua/Gen" "${target:?}/Assets/xLua/Gen.meta"
}

errors() { grep -h "error CS" "$@" 2>/dev/null | sed 's/^.*Assets/Assets/' | sort -u | cut -c1-220 | head -12; }

hotfix() {
    local P="$WORK/hotfix"
    echo "=== hotfix: injection, hotfix calls and generated code spread over two assemblies"
    (cd "$HERE/injector" && dotnet build inj.csproj -nologo -v q -o "$WORK/injector" > "$WORK/injector.log" 2>&1) || { echo "injector build failed, see $WORK/injector.log"; return 1; }
    copy_project "$P"
    rm -rf "${P:?}/Assets/Tests" "${P:?}/Assets/Tests.meta"      # the unit tests only compile in the editor
    mkdir -p "$P/Tools" "$P/Assets/HotfixSmoke" "$P/Assets/Editor"
    cp "$WORK/injector/"* "$P/Tools/"
    cp "$HERE/hotfix/"*.cs "$P/Assets/HotfixSmoke/"
    cp -r "$HERE/hotfix/HfAsm" "$P/Assets/HfAsm"
    cp "$HERE/hotfix/Editor/"*.cs "$P/Assets/Editor/"

    batch "$UNITY" "$P" HotfixRunner.SetDefine "$P/define.log"; echo "define exit=$?"
    batch "$UNITY" "$P" HotfixRunner.Gen "$P/gen.log"; echo "gen exit=$?"
    timeout 600 "$UNITY" -batchmode -projectPath "$P" -executeMethod HotfixRunner.Run -logFile "$P/test.log"; echo "editor run exit=$?"
    batch "$UNITY" "$P" HotfixBuild.Il2cpp "$P/build.log"; echo "il2cpp build exit=$?"
    # the IntKey id maps are written during the build, after the assets were packed
    cp "$P"/Assets/xLua/Gen/Resources/hotfix_id_map*.lua.txt "$P/Build/hf/" 2>/dev/null
    (cd "$P/Build/hf" 2>/dev/null && timeout 60 ./hf.exe -batchmode -nographics -logFile player.log > /dev/null 2>&1)

    errors "$P/define.log" "$P/gen.log" "$P/test.log" "$P/build.log"
    echo "-- generated into:"; find "$P/Assets" -name XLuaGenAutoRegister.cs | sed "s#$P/##"
    echo "-- editor"; grep -h "TEST.*\(FAIL\|SUMMARY\|gen assemblies\)\|inject finish" "$P/test.log" | sed 's/^.*ScriptAssemblies.//' | sort -u | cut -c1-200
    echo "-- il2cpp player"; grep -h "BUILD_RESULT" "$P/build.log" | sort -u; grep -h "TEST.*\(FAIL\|SUMMARY\|gen assemblies\)" "$P/Build/hf/player.log" 2>/dev/null | sort -u | cut -c1-200
}

unit() {
    local P="$WORK/unit"
    echo "=== unit: the xLua unit tests with generated code, in editor play mode"
    copy_project "$P"
    # the tests expect to sit directly under Assets
    mv "$P/Assets/Tests/UnitTest/xLuaTest" "$P/Assets/xLuaTest"
    mv "$P/Assets/Tests/UnitTest/xLuaTest.meta" "$P/Assets/xLuaTest.meta"
    mv "$P/Assets/Tests/UnitTest/StreamingAssets" "$P/Assets/StreamingAssets"
    mv "$P/Assets/Tests/UnitTest/StreamingAssets.meta" "$P/Assets/StreamingAssets.meta"
    mkdir -p "$P/Assets/Editor"
    cp "$HERE/unit/Editor/"*.cs "$P/Assets/Editor/"

    batch "$UNITY" "$P" RunXLuaTests.Gen "$P/gen.log"; echo "gen exit=$?"
    timeout 600 "$UNITY" -batchmode -projectPath "$P" -executeMethod RunXLuaTests.Run -logFile "$P/test.log"; echo "editor run exit=$?"

    errors "$P/gen.log" "$P/test.log"
    grep -h "XLUATEST gen_flag\|PASSED  \]\|FAILED  \]" "$P/test.log" | sort | uniq -c | cut -c1-120
    for f in ltest_case_list.txt ltest_case_failed.txt; do
        [ -f "$P/Assets/xLuaTest/$f" ] && echo "$f: $(wc -l < "$P/Assets/xLuaTest/$f") lines"
    done
}

consumer() {
    local P="$WORK/consumer"
    echo "=== consumer: a project that installs the package, three assemblies, IL2CPP player on Unity 6"
    mkdir -p "$P/Packages" "$P/ProjectSettings"
    cp -r "$HERE/consumer/Assets" "$P/"
    cat > "$P/Packages/manifest.json" <<EOF
{
  "dependencies": {
    "com.sanyoni.xlua": "file:$(cygpath -m "$REPO" 2>/dev/null || echo "$REPO")/src/xLua.Unity/Assets/xLua",
    "com.unity.modules.jsonserialize": "1.0.0"
  }
}
EOF
    batch "$UNITY6" "$P" Build.Gen "$P/gen.log"; echo "gen exit=$?"
    batch "$UNITY6" "$P" BuildIl2cpp.Win "$P/build.log" -buildTarget StandaloneWindows64; echo "il2cpp build exit=$?"
    (cd "$P/Build/WinIl2cpp" 2>/dev/null && timeout 120 ./smoke.exe -batchmode -nographics -logFile player.log > /dev/null 2>&1)

    errors "$P/gen.log" "$P/build.log"
    echo "-- generated into:"; find "$P/Assets" -name XLuaGenAutoRegister.cs | sed "s#$P/##"
    grep -h "BUILD_RESULT" "$P/build.log" | sort -u
    cat "$P/Build/WinIl2cpp/smoke.txt" 2>/dev/null || echo "no smoke.txt: the player did not finish"
}

for suite in $SUITES; do
    case "$suite" in
        hotfix|unit|consumer) "$suite" ;;
        *) echo "unknown suite: $suite" ;;
    esac
done
