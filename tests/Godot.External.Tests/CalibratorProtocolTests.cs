using System.Text.Json;
using Godot.External.Calibrator.Protocol;

namespace Godot.External.Tests;

/// <summary>
/// The <c>godot-abi-grid/driver.v1</c> boundary, from both sides.
/// </summary>
/// <remarks>
/// The harness and the calibrator are developed independently on purpose, so the encoding is the
/// only thing holding them together and it is worth testing as an artefact rather than as an
/// implementation detail. The rules that matter are stated in <c>tools/godot-abi-grid/README.md</c>:
/// pointers must be strings, offsets may be hex, vectors may arrive in three shapes, and a driver
/// that reports <c>usedProfile</c> fails outright.
/// </remarks>
public sealed class CalibratorProtocolTests
{
    private const string SampleRequest = """
    {
      "contract": "godot-abi-grid/driver.v1",
      "pid": 12345,
      "executable": "out/4.5.1-release-single-dotnet/grid.exe",
      "cell": { "name": "4.5.1-release-single-dotnet", "version": "4.5.1",
                "template": "release", "precision": "single", "binding": "dotnet" },
      "runtime": { "engineVersion": "4.5.1.stable", "walkCount": 20 },
      "anchors": {
        "walkRootPath": "/root/RootHarness",
        "sizes": [ { "size": [887, 313], "path": "RootHarness/AlphaPanel/BetaBranch" },
                   { "size": [613, 227], "path": "RootHarness/AlphaPanel" },
                   { "size": [289, 161], "path": "RootHarness/OmegaPanel" } ],
        "visible": { "visiblePath": "RootHarness/VisibleTwin", "hiddenPath": "RootHarness/HiddenTwin" },
        "nodeCount": 20,
        "names": ["RootHarness", "AlphaPanel", "BetaBranch", "OmegaPanel"],
        "managedStatic": { "type": "Probe", "field": "Instance" }
      },
      "require": { "structuralMethod": "pointer-identity", "semanticMethod": "known-value-intersection" }
    }
    """;

    [Fact]
    public void ParsesAWholeRequest()
    {
        DriverRequest request = DriverRequest.Parse(SampleRequest);

        Assert.Equal(DriverRequest.ContractId, request.Contract);
        Assert.Equal(12345, request.Pid);
        Assert.Equal("4.5.1", request.Cell.Version);
        Assert.True(request.IsDotNetCell);
        Assert.False(request.IsDoublePrecision);
        Assert.Equal("RootHarness", request.WalkRootName);
        Assert.Equal(20, request.NodeCount);
        Assert.Equal(3, request.Sizes.Count);
        Assert.Equal(887, request.Sizes[0].Width);
        Assert.Equal("Probe", request.ManagedStatic!.Type);
        Assert.Equal("4.5.1.stable", request.EngineVersion);
    }

    [Fact]
    public void RecoversTheHierarchyTheAnchorPathsImply()
    {
        DriverRequest request = DriverRequest.Parse(SampleRequest);

        // The harness never states the tree, but a two-segment anchor path names a child of the
        // walk root — and one known parent/child pair is all the structural derivation needs.
        Assert.Equal(new[] { "AlphaPanel", "OmegaPanel" }, request.KnownRootChildNames());
    }

    [Fact]
    public void DegradesRatherThanThrowsOnAHollowRequest()
    {
        DriverRequest request = DriverRequest.Parse("""{"contract":"godot-abi-grid/driver.v1"}""");

        Assert.Null(request.Pid);
        Assert.Null(request.WalkRootName);
        Assert.Empty(request.Sizes);
        Assert.Empty(request.Names);
        Assert.Null(request.ManagedStatic);
        Assert.Empty(request.KnownRootChildNames());
    }

    [Theory]
    [InlineData("""{"size":[8,9],"path":"p"}""")]
    [InlineData("""{"size":{"x":8,"y":9},"path":"p"}""")]
    [InlineData("""{"size":{"0":8,"1":9},"path":"p"}""")]
    public void AcceptsAllThreeVectorShapes(string anchor)
    {
        DriverRequest request = DriverRequest.Parse("{\"anchors\":{\"sizes\":[" + anchor + "]}}");

        Assert.Equal(8, request.Sizes[0].Width);
        Assert.Equal(9, request.Sizes[0].Height);
    }

    [Fact]
    public void EmitsOffsetsAsHexAndPointersAsStrings()
    {
        DriverResult result = new()
        {
            WalkCount = 1,
            Derivation = new Derivation
            {
                Structural = new StructuralDerivation
                {
                    Offsets = new Dictionary<string, string> { ["node.childListHead"] = Wire.Offset(0x148) },
                },
            },
            Nodes = [new NodeRecord { Name = "RootHarness", NativePtr = Wire.Pointer(0x1a9204c5580) }],
        };

        using JsonDocument document = JsonDocument.Parse(result.ToJson());
        JsonElement root = document.RootElement;

        Assert.Equal("0x148", root.GetProperty("derivation").GetProperty("structural")
            .GetProperty("offsets").GetProperty("node.childListHead").GetString());

        // A 64-bit pointer through a JS Number is a silent corruption; the harness rejects unsafe
        // integers rather than guessing, so this must be a string on the wire.
        JsonElement pointer = root.GetProperty("nodes")[0].GetProperty("nativePtr");
        Assert.Equal(JsonValueKind.String, pointer.ValueKind);
        Assert.Equal("0x1a9204c5580", pointer.GetString());
    }

    [Fact]
    public void AlwaysReportsUsedProfileFalse()
    {
        using JsonDocument document = JsonDocument.Parse(new DriverResult().ToJson());

        // §8.9: usedProfile true fails calibration.unaided whatever else the run scored, so the
        // flag is a constant with no setter rather than a configurable one.
        Assert.False(document.RootElement.GetProperty("usedProfile").GetBoolean());
        Assert.Equal(DerivationMethods.Structural, document.RootElement.GetProperty("derivation")
            .GetProperty("structural").GetProperty("method").GetString());
    }

    [Fact]
    public void OmitsNullsSoAnUnderivedFieldIsAbsentRatherThanZero()
    {
        DriverResult result = new() { Nodes = [new NodeRecord { Name = "X", NativePtr = "0x1" }] };

        using JsonDocument document = JsonDocument.Parse(result.ToJson());
        JsonElement node = document.RootElement.GetProperty("nodes")[0];

        Assert.False(node.TryGetProperty("size", out _));
        Assert.False(node.TryGetProperty("visible", out _));
        Assert.False(node.TryGetProperty("parentPtr", out _));
    }
}
