using Godot.External.Calibrator.Protocol;

namespace Godot.External.Tests;

/// <summary>
/// The authored ground truth of <c>tools/godot-abi-grid/project/Main.tscn</c>, transcribed from that
/// harness's <c>expected.json</c>.
/// </summary>
/// <remarks>
/// Every number is load-bearing and is copied rather than invented. <c>613×227</c>, <c>887×313</c>
/// and friends are odd and mutually distinct because §12.5's <c>200×50</c> control produced four
/// false candidates; <c>409×151</c> appears twice so a calibrator keying nodes by geometry is caught
/// collapsing them; and <c>AnchoredWide</c> carries anchors of <c>0.5</c> with offsets that are not
/// its rect, which is the only thing separating <c>Data.offset[4]</c> from <c>Data.anchor[4]</c>.
/// </remarks>
internal static class GridScene
{
    /// <summary>Walk root path, as the harness states it in <c>walkRootPath</c>.</summary>
    public const string WalkRootPath = "/root/RootHarness";

    /// <summary>The twenty nodes, parents before children.</summary>
    public static IReadOnlyList<GridNode> Nodes { get; } =
    [
        Node("RootHarness", [1920, 1080], [0, 0], [1, 1], [0, 0, 1920, 1080]),
        Node("RootHarness/AlphaPanel", [613, 227], [37, 53], [1.25, 0.75], [37, 53, 650, 280]),
        Node("RootHarness/AlphaPanel/AlphaLeaf", [409, 151], [11, 13], [1, 1], [11, 13, 420, 164]),
        Node("RootHarness/AlphaPanel/BetaBranch", [887, 313], [23, 29], [0.5, 2], [23, 29, 910, 342]),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest", [409, 151], [7, 11], [1, 1], [7, 11, 416, 162]),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore", [283, 97], [5, 3], [1, 1], [5, 3, 288, 100]),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore", [151, 67], [2, 1], [1, 1], [2, 1, 153, 68]),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaLabelAscii", [331, 89], [0, 0], [1, 1], [0, 0, 331, 89], text: "GridProbe ASCII 0123", nodeClass: "Label"),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaLabelUnicode", [347, 83], [0, 95], [1, 1], [0, 95, 347, 178], text: "héllo ✦ 日本語", nodeClass: "Label"),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaRich", [401, 96], [0, 185], [1, 1], [0, 185, 401, 281], text: "ρich ✦ テキスト 𝄞 RTL", rich: true, nodeClass: "RichTextLabel"),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonSibling", [85, 59], [160, 1], [1, 1], [160, 1, 245, 60]),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaSiblingOne", [49, 39], [300, 5], [1, 1], [300, 5, 349, 44], nodeClass: "Node"),
        Node("RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaSiblingTwo", [67, 63], [300, 50], [1, 1], [300, 50, 367, 113]),
        Node("RootHarness/AlphaPanel/BetaBranch/VisibleTwin", [271, 139], [30, 350], [1, 1], [30, 350, 301, 489]),
        Node("RootHarness/AlphaPanel/BetaBranch/HiddenTwin", [359, 181], [320, 350], [1, 1], [320, 350, 679, 531], visible: false),
        Node("RootHarness/AlphaPanel/BetaBranch/AnchoredWide", [613, 151], [12.5, -40.5], [1, 1], [-431, -197, 182, -46], anchors: [0.5, 0.5, 0.5, 0.5]),
        Node("RootHarness/AlphaPanel/BetaBranch/AnchoredWide/AnchoredChild", [53, 41], [3, 7], [1, 1], [3, 7, 56, 48]),
        Node("RootHarness/AlphaPanel/AlphaSibling", [79, 67], [450, 5], [1, 1], [450, 5, 529, 72]),
        Node("RootHarness/OmegaPanel", [289, 161], [1000, 600], [2.5, 1.75], [1000, 600, 1289, 761]),
        Node("RootHarness/OmegaPanel/OmegaChild", [101, 63], [9, 9], [1, 1], [9, 9, 110, 72], nodeClass: "ColorRect"),
    ];

    /// <summary>
    /// The request the harness builds for this scene — values only, never an offset.
    /// </summary>
    public static DriverRequest Request(string binding = "dotnet") => new()
    {
        Contract = DriverRequest.ContractId,
        Pid = 4242,
        Cell = new CellAxes($"4.5.1-release-single-{binding}", "4.5.1", "release", "single", binding),
        EngineVersion = "4.5.1.stable",
        WalkRootPath = WalkRootPath,
        NodeCount = Nodes.Count,
        Names = [.. Nodes.Select(n => n.Name)],
        Sizes =
        [
            new SizeAnchor("RootHarness/AlphaPanel/BetaBranch", 887, 313),
            new SizeAnchor("RootHarness/AlphaPanel", 613, 227),
            new SizeAnchor("RootHarness/AlphaPanel/BetaBranch/AnchoredWide", 613, 151),
            new SizeAnchor("RootHarness/AlphaPanel/BetaBranch/HiddenTwin", 359, 181),
            new SizeAnchor("RootHarness/OmegaPanel", 289, 161),
            new SizeAnchor("RootHarness/AlphaPanel/BetaBranch/VisibleTwin", 271, 139),
        ],
        Visibility = new VisibilityAnchor(
            "RootHarness/AlphaPanel/BetaBranch/VisibleTwin",
            "RootHarness/AlphaPanel/BetaBranch/HiddenTwin"),
        ManagedStatic = binding == "dotnet" ? new ManagedStaticAnchor("Probe", "Instance") : null,
    };

    private static GridNode Node(
        string path,
        double[] size,
        double[] position,
        double[] scale,
        double[] offsets,
        double[]? anchors = null,
        bool visible = true,
        string? text = null,
        bool rich = false,
        string nodeClass = "Control")
        => new(
            path,
            path[(path.LastIndexOf('/') + 1)..],
            size,
            position,
            scale,
            offsets,
            anchors ?? [0, 0, 0, 0],
            visible,
            text,
            rich,
            nodeClass);
}
