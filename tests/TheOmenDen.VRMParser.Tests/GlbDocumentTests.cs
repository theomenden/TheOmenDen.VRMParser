using Shouldly;
using TheOmenDen.VRMParser.Models;

namespace TheOmenDen.VRMParser.Tests;

/// <summary>
/// Starter smoke tests for <see cref="GlbDocument"/>. As parse/serialize logic lands,
/// replace these with real fixture-backed round-trip tests
/// (parse .glb/.vrm -> model -> serialize -> re-parse should be stable).
/// </summary>
public sealed class GlbDocumentTests
{
    [Test]
    public async Task GlbDocument_CanBeConstructed()
    {
        var document = new GlbDocument();

        document.ShouldNotBeNull();

        // Keeps TUnit's async assertion surface exercised until real assertions exist.
        await Assert.That(document).IsNotNull();
    }
}
