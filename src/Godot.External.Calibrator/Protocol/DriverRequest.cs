using System.Text.Json;

namespace Godot.External.Calibrator.Protocol;

/// <summary>One authored control whose size the harness is willing to tell us.</summary>
/// <remarks>
/// This is the whole of the numeric ground truth in a <c>driver.v1</c> request: a path and a size.
/// No offsets, ever — see <c>tools/godot-abi-grid/lib/driver.mjs</c>.
/// </remarks>
public sealed record SizeAnchor(string Path, double Width, double Height);

/// <summary>The two nodes whose visibility is known, one of each.</summary>
public sealed record VisibilityAnchor(string VisiblePath, string HiddenPath);

/// <summary>A managed static the calibrator may reach for on a <c>binding=dotnet</c> cell.</summary>
public sealed record ManagedStaticAnchor(string Type, string Field);

/// <summary>The grid cell's four axes, as the harness labels the directory.</summary>
public sealed record CellAxes(string Name, string Version, string Template, string Precision, string Binding);

/// <summary>
/// One <c>godot-abi-grid/driver.v1</c> request, parsed defensively.
/// </summary>
/// <remarks>
/// Everything is optional in the parser even where the contract says it is not: a driver that
/// throws on a missing field reports "error" to the harness, which is reserved for a crashed
/// driver. A missing anchor should degrade into "that offset was not derivable", which is a
/// verdict.
/// </remarks>
public sealed record DriverRequest
{
    /// <summary>The contract this driver implements.</summary>
    public const string ContractId = "godot-abi-grid/driver.v1";

    /// <summary>Contract string as the request declared it, whatever it was.</summary>
    public string? Contract { get; init; }

    /// <summary>Target process id, or <see langword="null"/> when the harness did not launch one.</summary>
    public int? Pid { get; init; }

    /// <summary>Path of the built target, for diagnostics only.</summary>
    public string? Executable { get; init; }

    /// <summary>The cell axes.</summary>
    public CellAxes Cell { get; init; } = new("unknown", "unknown", "release", "single", "dotnet");

    /// <summary>The engine's own version string, from the target's ready file.</summary>
    public string? EngineVersion { get; init; }

    /// <summary>The target's own in-process node count, when it reported one.</summary>
    public int? RuntimeWalkCount { get; init; }

    /// <summary>Absolute scene path of the walk root, e.g. <c>/root/RootHarness</c>.</summary>
    public string? WalkRootPath { get; init; }

    /// <summary>Known sizes, the anchors for the §12.5 intersection.</summary>
    public IReadOnlyList<SizeAnchor> Sizes { get; init; } = [];

    /// <summary>The visible/hidden twins.</summary>
    public VisibilityAnchor? Visibility { get; init; }

    /// <summary>How many nodes the authored scene has.</summary>
    public int? NodeCount { get; init; }

    /// <summary>Every node name in the scene, unordered with respect to the tree.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>The managed static root, on .NET cells only.</summary>
    public ManagedStaticAnchor? ManagedStatic { get; init; }

    /// <summary>Derivation method the harness demands for structural offsets.</summary>
    public string RequiredStructuralMethod { get; init; } = "pointer-identity";

    /// <summary>Derivation method the harness demands for semantic offsets.</summary>
    public string RequiredSemanticMethod { get; init; } = "known-value-intersection";

    /// <summary>Name of the walk root node, i.e. the last segment of <see cref="WalkRootPath"/>.</summary>
    public string? WalkRootName
    {
        get
        {
            string? path = WalkRootPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            int slash = path.LastIndexOf('/');
            string name = slash >= 0 ? path[(slash + 1)..] : path;
            return name.Length == 0 ? null : name;
        }
    }

    /// <summary>Whether this cell has a managed side at all.</summary>
    public bool IsDotNetCell => string.Equals(Cell.Binding, "dotnet", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the build's <c>real_t</c> is 8 bytes wide.</summary>
    public bool IsDoublePrecision => string.Equals(Cell.Precision, "double", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Names known to be children of the walk root, recovered from the anchor paths.
    /// </summary>
    /// <remarks>
    /// The harness never states the hierarchy, but <c>anchors.sizes[].path</c> is a path, and a
    /// two-segment path rooted at the walk root names a child of it. That is the "known child" the
    /// §12.5 child-list derivation needs, and it is ground truth rather than an assumption.
    /// </remarks>
    public IReadOnlyList<string> KnownRootChildNames()
    {
        string? root = WalkRootName;
        if (root is null)
        {
            return [];
        }

        List<string> children = [];
        foreach (SizeAnchor anchor in Sizes)
        {
            string[] segments = anchor.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2 && segments[0] == root && !children.Contains(segments[1]))
            {
                children.Add(segments[1]);
            }
        }

        return children;
    }

    /// <summary>Parses a request. Unknown members are ignored; missing members degrade.</summary>
    public static DriverRequest Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        JsonElement anchors = Member(root, "anchors");
        JsonElement runtime = Member(root, "runtime");
        JsonElement require = Member(root, "require");
        JsonElement cell = Member(root, "cell");

        return new DriverRequest
        {
            Contract = String(root, "contract"),
            Pid = Int(root, "pid"),
            Executable = String(root, "executable"),
            Cell = new CellAxes(
                String(cell, "name") ?? "unknown",
                String(cell, "version") ?? "unknown",
                String(cell, "template") ?? "release",
                String(cell, "precision") ?? "single",
                String(cell, "binding") ?? "dotnet"),
            EngineVersion = String(runtime, "engineVersion"),
            RuntimeWalkCount = Int(runtime, "walkCount"),
            WalkRootPath = String(anchors, "walkRootPath") ?? String(runtime, "walkRootPath"),
            Sizes = ParseSizes(Member(anchors, "sizes")),
            Visibility = ParseVisibility(Member(anchors, "visible")),
            NodeCount = Int(anchors, "nodeCount"),
            Names = ParseNames(Member(anchors, "names")),
            ManagedStatic = ParseManagedStatic(Member(anchors, "managedStatic")),
            RequiredStructuralMethod = String(require, "structuralMethod") ?? "pointer-identity",
            RequiredSemanticMethod = String(require, "semanticMethod") ?? "known-value-intersection",
        };
    }

    private static IReadOnlyList<SizeAnchor> ParseSizes(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<SizeAnchor> anchors = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            string? path = String(item, "path");
            double[]? size = JsonVector.Read(Member(item, "size"), 2);
            if (path is not null && size is not null)
            {
                anchors.Add(new SizeAnchor(path, size[0], size[1]));
            }
        }

        return anchors;
    }

    private static VisibilityAnchor? ParseVisibility(JsonElement element)
    {
        string? visible = String(element, "visiblePath");
        string? hidden = String(element, "hiddenPath");
        return visible is null || hidden is null ? null : new VisibilityAnchor(visible, hidden);
    }

    private static IReadOnlyList<string> ParseNames(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> names = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? value = item.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    names.Add(value);
                }
            }
        }

        return names;
    }

    private static ManagedStaticAnchor? ParseManagedStatic(JsonElement element)
    {
        string? type = String(element, "type");
        string? field = String(element, "field");
        return type is null || field is null ? null : new ManagedStaticAnchor(type, field);
    }

    private static JsonElement Member(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out JsonElement value)
            ? value
            : default;

    private static string? String(JsonElement parent, string name)
    {
        JsonElement value = Member(parent, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? Int(JsonElement parent, string name)
    {
        JsonElement value = Member(parent, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int parsed) ? parsed : null;
    }
}
