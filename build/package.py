#!/usr/bin/env python3
"""Package a built NearbyChests.dll into release zips.

Usage: package.py <version> [bepinex-pack.zip]

Writes to release/:
  NearbyChests-<version>.zip               the mod only
  NearbyChests-<version>-with-BepInEx.zip  the mod plus BepInEx, if a pack zip is given
"""
import os
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DLL = os.path.join(ROOT, "bin", "Release", "NearbyChests.dll")
README = os.path.join(ROOT, "README.md")
CHANGELOG = os.path.join(ROOT, "CHANGELOG.md")
PLUGIN_DIR = "BepInEx/plugins/NearbyChests/"
PACK_PREFIX = "BepInExPack_Valheim/"


def add_mod(z):
    z.write(DLL, PLUGIN_DIR + "NearbyChests.dll")
    z.write(README, PLUGIN_DIR + "README.md")
    z.write(CHANGELOG, PLUGIN_DIR + "CHANGELOG.md")


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    version = sys.argv[1]
    bepinex_pack = sys.argv[2] if len(sys.argv) > 2 else None

    if not os.path.exists(DLL):
        sys.exit(f"{DLL} not found - run 'dotnet build -c Release' first")

    out = os.path.join(ROOT, "release")
    os.makedirs(out, exist_ok=True)

    mod_zip = os.path.join(out, f"NearbyChests-{version}.zip")
    with zipfile.ZipFile(mod_zip, "w", zipfile.ZIP_DEFLATED) as z:
        add_mod(z)
    print(mod_zip)

    if bepinex_pack:
        full_zip = os.path.join(out, f"NearbyChests-{version}-with-BepInEx.zip")
        with zipfile.ZipFile(bepinex_pack) as src, zipfile.ZipFile(full_zip, "w", zipfile.ZIP_DEFLATED) as z:
            # The Thunderstore pack keeps the game-folder files under BepInExPack_Valheim/.
            for info in src.infolist():
                if info.is_dir() or not info.filename.startswith(PACK_PREFIX):
                    continue
                z.writestr(info.filename[len(PACK_PREFIX):], src.read(info))
            add_mod(z)
        print(full_zip)


if __name__ == "__main__":
    main()
