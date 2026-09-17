#!/usr/bin/env python3
"""Package a built NearbyChests.dll into release zips.

Usage: package.py <version> [bepinex-pack.zip]

Writes to release/:
  NearbyChests-<version>.zip               the mod only
  NearbyChests-<version>-with-BepInEx.zip  the mod plus BepInEx, if a pack zip is given
  NearbyChests-<version>-thunderstore.zip  the Thunderstore upload, if icon.png exists
"""
import json
import os
import struct
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DLL = os.path.join(ROOT, "bin", "Release", "NearbyChests.dll")
README = os.path.join(ROOT, "README.md")
CHANGELOG = os.path.join(ROOT, "CHANGELOG.md")
PLUGIN_DIR = "BepInEx/plugins/NearbyChests/"
PACK_PREFIX = "BepInExPack_Valheim/"
# Thunderstore wants a 256x256 PNG at the root of the package.
ICON = os.path.join(ROOT, "icon.png")
BEPINEX_DEPENDENCY = "denikson-BepInExPack_Valheim-5.4.2350"


def thunderstore_manifest(version):
    return {
        # Letters, numbers and underscores only, and Thunderstore shows underscores as spaces.
        # It can't be changed after the first upload.
        "name": "Nearby_Chests",
        "version_number": version,
        "website_url": "https://robododd.com",
        # 250 characters at most.
        "description": "Craft, build and fuel stations from nearby chests. "
                       "Stack sorts your items into the right chests, and Tidy cleans a chest up.",
        "dependencies": [BEPINEX_DEPENDENCY],
    }


def check_thunderstore(manifest):
    """Fail here rather than at the upload form."""
    if len(manifest["description"]) > 250:
        sys.exit(f"manifest description is {len(manifest['description'])} characters, the limit is 250")
    with open(ICON, "rb") as f:
        header = f.read(24)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        sys.exit(f"{ICON} is not a PNG")
    size = struct.unpack(">II", header[16:24])
    if size != (256, 256):
        sys.exit(f"{ICON} is {size[0]}x{size[1]}, Thunderstore needs 256x256")
    for path in (README, CHANGELOG):
        with open(path, encoding="utf-8") as f:
            f.read()


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

    if os.path.exists(ICON):
        manifest = thunderstore_manifest(version)
        check_thunderstore(manifest)
        ts_zip = os.path.join(out, f"NearbyChests-{version}-thunderstore.zip")
        with zipfile.ZipFile(ts_zip, "w", zipfile.ZIP_DEFLATED) as z:
            # Thunderstore reads these four from the zip root. Mod managers install the rest.
            z.writestr("manifest.json", json.dumps(manifest, indent=2))
            z.write(ICON, "icon.png")
            z.write(README, "README.md")
            z.write(CHANGELOG, "CHANGELOG.md")
            z.write(DLL, "plugins/NearbyChests.dll")
        print(ts_zip)
    else:
        print(f"skipping the Thunderstore zip - {ICON} not found", file=sys.stderr)

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
