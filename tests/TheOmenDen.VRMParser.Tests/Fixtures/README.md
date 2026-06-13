# Test fixtures

Binary GLB / VRM files used by the integration tests.

| File | Source | License |
|------|--------|---------|
| `Box.glb` | [Khronos glTF-Sample-Assets — Box](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/Box) | **CC0 1.0** (public domain) |
| `MinimalVrm1.vrm` | Synthesized by `generate_fixtures.py` | This repository's license |
| `MinimalVrm0.vrm` | Synthesized by `generate_fixtures.py` | This repository's license |

## Box.glb
A real, exporter-produced GLB (JSON + BIN chunks). Khronos publishes it under CC0, so it is safe to
redistribute here. It exercises the binary container parser against genuine real-world output.

## MinimalVrm0.vrm / MinimalVrm1.vrm
**Not real avatars.** Minimal, schema-conformant VRM documents (VRM 0.x `VRM` extension and VRM 1.0
`VRMC_vrm` extension, each with the required `meta` and the 15 required `humanoid` bones) wrapped in a
spec-compliant GLB container. Authored by this project, so they carry this repository's license with no
third-party obligations.

Regenerate with:

```bash
python generate_fixtures.py
```

Chunks are padded exactly as `GlbDocument` writes them (JSON with `0x20`, BIN with `0x00`, 4-byte
aligned), so a `Parse → ToBytes` cycle is byte-identical.

## Opt-in real-world model (`RealWorldVrmTests`)
Full-size avatar exports (a ~37 MB UniVRM/VRoid `.vrm`, for example) are too large and usually too
licence-encumbered to commit. `RealWorldVrmTests` instead reads a model from a path you supply via the
`VRMPARSER_REAL_VRM_PATH` environment variable, and **skips entirely** when that variable is unset or
the file is missing — so a fresh clone and CI stay green without the asset.

Point it at any real `.vrm`/`.glb` to exercise parse, async-vs-sync parity, typed binding, and a
byte-for-byte round-trip at full scale:

```bash
# bash
VRMPARSER_REAL_VRM_PATH="/path/to/Model.vrm" dotnet test
```

```powershell
# PowerShell
$env:VRMPARSER_REAL_VRM_PATH = "C:\path\to\Model.vrm"; dotnet test
```
