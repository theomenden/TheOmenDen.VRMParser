#!/usr/bin/env python3
"""Generates the synthesized VRM fixtures (MinimalVrm0.vrm, MinimalVrm1.vrm).

These are NOT real avatars. They are minimal, schema-conformant VRM documents wrapped in a
spec-compliant GLB container, authored by this project (so they carry this repo's license).
Chunks are padded exactly like GlbDocument (JSON with spaces, BIN with zeros, 4-byte aligned),
so a parse -> write cycle through GlbDocument is byte-identical.

Run from this directory:  python generate_fixtures.py
"""
import json
import struct
from pathlib import Path

GLB_MAGIC = 0x46546C67
VERSION = 2
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942


def align4(n: int) -> int:
    return (n + 3) & ~3


def build_glb(gltf: dict, binary: bytes | None) -> bytes:
    json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    json_padded = json_bytes + b" " * (align4(len(json_bytes)) - len(json_bytes))

    total = 12 + 8 + len(json_padded)
    bin_padded = b""
    if binary is not None:
        bin_padded = binary + b"\x00" * (align4(len(binary)) - len(binary))
        total += 8 + len(bin_padded)

    out = bytearray()
    out += struct.pack("<III", GLB_MAGIC, VERSION, total)
    out += struct.pack("<II", len(json_padded), JSON_CHUNK) + json_padded
    if binary is not None:
        out += struct.pack("<II", len(bin_padded), BIN_CHUNK) + bin_padded
    return bytes(out)


# 15 VRM 1.0 required human bones, mapped to node indices 1..15 (node 0 is the scene root).
REQUIRED_BONES = [
    "hips", "spine", "head",
    "leftUpperLeg", "leftLowerLeg", "leftFoot",
    "rightUpperLeg", "rightLowerLeg", "rightFoot",
    "leftUpperArm", "leftLowerArm", "leftHand",
    "rightUpperArm", "rightLowerArm", "rightHand",
]


def base_gltf() -> dict:
    # Scene root (node 0) parents the 15 bone nodes (1..15). A tiny 4-byte buffer
    # is referenced as the GLB binary chunk so the BIN path is exercised.
    nodes = [{"name": "Root", "children": list(range(1, 16))}]
    nodes += [{"name": bone} for bone in REQUIRED_BONES]
    return {
        "asset": {"version": "2.0", "generator": "TheOmenDen.VRMParser test fixtures"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": nodes,
        "buffers": [{"byteLength": 4}],
        "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 4}],
    }


def vrm1() -> bytes:
    gltf = base_gltf()
    gltf["extensionsUsed"] = ["VRMC_vrm"]
    gltf["extensions"] = {
        "VRMC_vrm": {
            "specVersion": "1.0",
            "meta": {
                "name": "TheOmenDen Test Avatar",
                "authors": ["TheOmenDen"],
                "licenseUrl": "https://vrm.dev/licenses/1.0/",
            },
            "humanoid": {
                "humanBones": {bone: {"node": i + 1} for i, bone in enumerate(REQUIRED_BONES)},
            },
        }
    }
    return build_glb(gltf, b"\x00\x00\x00\x00")


def vrm0() -> bytes:
    gltf = base_gltf()
    gltf["extensionsUsed"] = ["VRM"]
    gltf["extensions"] = {
        "VRM": {
            "exporterVersion": "TheOmenDen.VRMParser-0.0",
            "specVersion": "0.0",
            "meta": {
                "title": "TheOmenDen Test Avatar (VRM 0.x)",
                "author": "TheOmenDen",
                "licenseName": "CC0",
            },
            "humanoid": {
                "humanBones": [{"bone": bone, "node": i + 1} for i, bone in enumerate(REQUIRED_BONES)],
            },
        }
    }
    return build_glb(gltf, b"\x00\x00\x00\x00")


def main() -> None:
    here = Path(__file__).parent
    (here / "MinimalVrm1.vrm").write_bytes(vrm1())
    (here / "MinimalVrm0.vrm").write_bytes(vrm0())
    print("wrote MinimalVrm1.vrm and MinimalVrm0.vrm")


if __name__ == "__main__":
    main()
