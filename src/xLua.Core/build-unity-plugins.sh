#!/bin/bash
# Builds the XLua.Core variants the Unity package ships and copies them into it.
#
#   Managed/XLua.Core.dll                  native library loaded by name: desktop, Android, UWP, the editor
#   Managed/Static/XLua.Core.dll           native library linked into the executable: iOS, tvOS
#   Managed/StaticNoSocket/XLua.Core.dll   the same without luasocket: WebGL
#
# Run it after changing anything under src/xLua.Core and commit the DLLs with the change;
# a package installed from a git URL cannot build them.

set -eu
HERE="$(cd "$(dirname "$0")" && pwd)"
PLUGINS="$HERE/../xLua.Unity/Assets/xLua/Plugins/Managed"
OUT="$HERE/obj/unity-plugins"

build() { # <output subfolder> <extra msbuild properties...>
    local name="$1"; shift
    dotnet build "$HERE/XLua.Core.csproj" -c Release -nologo -v q -o "$OUT/$name" "$@"
}

build default
build static -p:XLuaStaticLink=true
build static-no-socket -p:XLuaStaticLink=true -p:XLuaNoSocket=true

mkdir -p "$PLUGINS/Static" "$PLUGINS/StaticNoSocket"
cp "$OUT/default/XLua.Core.dll" "$PLUGINS/XLua.Core.dll"
cp "$OUT/static/XLua.Core.dll" "$PLUGINS/Static/XLua.Core.dll"
cp "$OUT/static-no-socket/XLua.Core.dll" "$PLUGINS/StaticNoSocket/XLua.Core.dll"
echo "copied three XLua.Core variants into $PLUGINS"
