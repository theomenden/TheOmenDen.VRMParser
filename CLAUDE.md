# TheOmenDen.VRMParser

A .NET library for parsing — and round-tripping — **VRM** avatar files (VRoid / VTuber model
format) built on the **glTF 2.0** container. Strongly-typed models are source-generated from the
official JSON Schemas via **Corvus.Text.Json** (v5); the library adds the binary `.glb`/`.vrm` parsing,
VRM 0.x / VRM 1.0 extension handling, and serialization on top.

Distributed as a **NuGet package** (class library, no entry point).

## Tech stack

| Concern            | Choice                                                              |
|--------------------|--------------------------------------------------------------------|
| Target framework   | `net10.0` (SDK pinned via `global.json`, `rollForward: latestMinor`) |
| Language           | C# 14, `ImplicitUsings` + `Nullable` enabled                       |
| Model generation   | Corvus.Text.Json v5 (`Corvus.Text.Json` + `.SourceGenerator` + `.CodeGeneration`); namespace is `Corvus.Text.Json` |
| Concurrency        | `System.Threading.Channels` (in the net10 shared framework — no package ref needed) |
| Logging            | `Microsoft.Extensions.Logging` abstractions only                  |
| Analyzers          | Roslynator, Roslynator.CodeAnalysis, SonarAnalyzer.CSharp (treat warnings seriously) |
| Testing            | **TUnit** + TUnit Mocks + **Shouldly** + **Bogus** (see Testing)   |

## Architecture — feature-grouped library

Organize by **VRM/glTF capability**, not by technical layer. Each feature area owns its parsing,
its models, and its serialization. The JSON Schemas under `JsonSchemas/` are the source of truth
for the data shapes; hand-written code wraps the generated types.

```
TheOmenDen.VRMParser/
  JsonSchemas/            # Schema source of truth (AdditionalFiles → Corvus codegen)
  Glb/                    # Binary GLB container layer (feature folder)
    GlbDocument.cs        # Parse/write the .glb/.vrm container: header + JSON/BIN chunks; round-trips byte-stable
    GlbFormatException.cs # Thrown on malformed GLB (bad magic/version, misaligned/truncated chunks)
  Models/
    Records/              # Corvus [JsonSchemaTypeGenerator] partial structs, one per schema area
      GltfRoot.cs, Gltf*.cs        # glTF 2.0 core
      Vrm0.cs                      # VRM 0.x extension
      Vrm1Root.cs, Vrmc*.cs        # VRM 1.0 extensions (VRMC_*)
      PathingConstants.cs          # Centralized schema path constants
```

`GlbDocument` (namespace `TheOmenDen.VRMParser.Glb`) is the entry point: `GlbDocument.Parse(bytes)`
/ `TryParse` → `Json` + `Binary` chunk payloads, and `ToBytes()` / `WriteTo(stream)` back out. It keeps
chunk payloads verbatim so a parse → write cycle is byte-stable for compliant input. The typed glTF
model is reached via the internal `ParseGltf()` bridge (`ParsedJsonDocument<GltfRoot>`), since the
generated model is `internal`; the test project sees it through `InternalsVisibleTo`.

As more parsing/serialization lands, add sibling **feature folders** — `Gltf/` (core glTF parse +
emit), `Vrm0/`, `Vrm1/`. Keep each self-contained: a feature's reader, writer, and helpers live
together. Avoid a single god-class parser and avoid horizontal `Parsers/`, `Serializers/` buckets
that split one feature across folders.

### Read + write (round-trip)

This library is **bidirectional**: it parses `.glb`/`.vrm` → typed model, and serializes the model
back to bytes. Design with that in mind:

- Preserve unknown/unmodeled glTF extensions and `extras` on read so they survive a write
  (don't silently drop data you didn't model — round-trip fidelity matters for avatar files).
- Keep binary chunk layout (JSON chunk + BIN chunk, 4-byte alignment, padding) correct on write.
- Parsing and serialization for a feature are two halves of one contract — co-locate and test them
  together (parse → serialize → re-parse should be stable).

## Conventions (detected from existing code)

- **File-scoped namespaces** everywhere (`namespace TheOmenDen.VRMParser.Models.Records;`).
- Generated model wrappers are `internal readonly partial struct` decorated with
  `[JsonSchemaTypeGenerator(...)]` (from `using Corvus.Text.Json;`). Default Corvus accessibility is
  `Internal` (`CorvusTextJsonDefaultAccessibility`); optional members map to `NullOrUndefined`
  (`CorvusTextJsonOptionalAsNullable`). Public API
  surface is deliberately small — expose only what consumers need, keep generated types internal.
- Schema paths are referenced through **`PathingConstants`** constants, not string literals. When you
  add a schema, add its path constant there and an `AdditionalFiles` entry in the `.csproj`.
- Public, consumer-facing types (e.g. `GlbDocument`) are `public sealed`. Seal by default.
- Prefer modern C#: primary constructors, collection expressions, pattern matching, `Span<T>`/
  `ReadOnlySpan<byte>` for binary parsing hot paths (this is a byte-pushing library — avoid
  unnecessary allocations and copies; parse over spans, not `byte[]` slices).

## Adding a new schema-backed model

1. Drop the `*.schema.json` into `JsonSchemas/`.
2. In `TheOmenDen.VRMParser.csproj`, add both a `<None Remove=...>` and an
   `<AdditionalFiles Include=...>` entry for it (the `None Remove` stops it being treated as content).
3. Add a path constant to `PathingConstants.cs`.
4. Create a `readonly partial struct` with `[JsonSchemaTypeGenerator($"...{PathingConstants.XPath}{PathingConstants.SchemaJsonSuffix}")]`.
5. Build — Corvus generates the type. Inspect generated output under `obj/.../generated/` if needed.

## Testing

Uses **TUnit** (source-generated, runs on **Microsoft.Testing.Platform** — *not* VSTest/xUnit).

Test project setup (`tests/TheOmenDen.VRMParser.Tests/`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>          <!-- required by Microsoft.Testing.Platform -->
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.5*" />
    <PackageReference Include="TUnit.Mocks" Version="1.53.0" />
    <PackageReference Include="Shouldly" Version="4.3.0" />
    <PackageReference Include="Bogus" Version="35.6.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\TheOmenDen.VRMParser\TheOmenDen.VRMParser.csproj" />
  </ItemGroup>
</Project>
```

Rules:
- **Do NOT** add `Microsoft.NET.Test.Sdk`, `coverlet.collector`, or `coverlet.msbuild` — they break
  TUnit's discovery on the Testing Platform. Code coverage and TRX reporting are built into TUnit.
- **`dotnet test` on the .NET 10 SDK requires opting into MTP** — this is enabled repo-wide by the
  `test.runner` key in `global.json` (already configured). Do **not** add the legacy
  `TestingPlatformDotnetTestSupport` property; it's the .NET 9 mechanism and is now unnecessary.
- Scaffold with `dotnet new install TUnit.Templates` then `dotnet new TUnit` (delete the generated
  `Calculator`/demo files).
- Use `[Test]` / `[Arguments(...)]` (TUnit attributes), **Shouldly** for assertions (`result.ShouldBe(...)`),
  **TUnit.Mocks** for fakes, and **Bogus** to generate randomized glTF/VRM model data.
- Run with `dotnet run --project tests/...` or `dotnet test`. **Both work**; pass TUnit/MTP flags
  *after* a `--` separator, e.g. `dotnet test -- --coverage --report-trx`.

What to cover for a parser:
- **Fixtures:** live in `tests/TheOmenDen.VRMParser.Tests/Fixtures/` (copied to output via the test
  csproj). `Box.glb` is real CC0 Khronos exporter output; `MinimalVrm{0,1}.vrm` are synthesized by
  `generate_fixtures.py` (schema-real VRM 0.x / 1.0 in a GLB container). See `Fixtures/README.md` for
  provenance/licensing. `.glb`/`.vrm` are marked `binary` in `.gitattributes`.
- **Round-trip:** parse → serialize → re-parse must be byte- or model-stable; assert unknown
  extensions/`extras` survive.
- **Malformed input:** truncated chunks, bad magic/version, misaligned padding → clear failures, no
  crashes or silent corruption.

## Build & verify

```bash
dotnet build                # builds via TheOmenDen.VRMParser.slnx; Corvus codegen + analyzers run
dotnet build -c Release
dotnet test                 # solution-level; MTP runner picked up from global.json
dotnet run --project tests/TheOmenDen.VRMParser.Tests   # equivalent, easier flag passing

# Performance + runtime diagnostics (BenchmarkDotNet; Release only)
dotnet run -c Release --project benchmarks/TheOmenDen.VRMParser.Benchmarks -- --filter '*'
# GC-pause / memory-pressure harness over a real avatar (DotNext GCNotification)
dotnet run -c Release --project benchmarks/TheOmenDen.VRMParser.Benchmarks -- diagnostics
```

The solution is **`.slnx`** (XML format) — there is no `.sln`. The SDK floor is **.NET 10**
(`global.json`, `rollForward: latestMinor`).

The **`benchmarks/`** feature folder holds the BenchmarkDotNet suite (read/write/bind paths, with
`MemoryDiagnoser` + `ThreadingDiagnoser`) and a DotNext `GCNotification` GC-pause harness. Point
`VRMPARSER_REAL_VRM_PATH` at a full-size `.vrm` for realistic IO/GC numbers; `--hw` adds Windows ETW
hardware counters (admin only). See `benchmarks/TheOmenDen.VRMParser.Benchmarks/README.md`.

CI (`.github/workflows/dotnet.yml`) is `workflow_dispatch` only and uses `dotnet test` on .NET 10.x
— which now works because of the `global.json` MTP runner. Consider enabling `on: push` /
`pull_request`.

## Tooling notes

- The **Roslyn navigator MCP server** (`cwm-roslyn-navigator`) is available via the dotnet-claude-kit
  plugin — prefer it (`find_symbol`, `find_references`, `get_diagnostics`, `detect_antipatterns`) over
  raw text search when navigating or reviewing C#. No project-level `.mcp.json` is required.
- A large amount of code is **source-generated** at build time; if a type "doesn't exist," build first
  rather than assuming it's missing.
