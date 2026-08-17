using Godot;

// godot-abi-grid probe — docs/analysis.md §8.9
//
// Two jobs:
//   1. Force a .NET build so the managed -> native bridge exists at all
//      (§4.6: managed GodotObject -> NativePtr -> raw engine struct).
//   2. Publish a STATIC root the calibrator can reach with nothing but a type
//      name and a static field name, exactly like `NGame.Instance` in §4.6.
//
// Deliberately plain `public static` FIELDS, not auto-properties: an
// auto-property hides its storage in a `<Name>k__BackingField`, which would
// make the harness test our metadata reader's name-mangling tolerance instead
// of testing the bridge. Keep the target boring.
//
// Probe.gd is the GDScript twin used for `binding=gdscript` cells; keep the
// ready-file contract identical between the two.
public partial class Probe : Control
{
    public const string ReadyFileEnv = "GRID_READY_FILE";
    public const string ExitAfterEnv = "GRID_EXIT_AFTER";
    public const string ContractVersion = "godot-abi-grid/ready.v2";

    // --- statics the calibrator resolves ------------------------------------
    public static Probe Instance;
    public static Node HarnessRoot;
    public static int WalkCount;
    public static string BuildTag = "godot-abi-grid";

    // --- instance fields worth reading through the bridge -------------------
    public string ProbeAscii = "GridProbe ASCII 0123";
    public string ProbeUnicode = "héllo ✦ 日本語";
    public int ProbeInt32 = 613227;
    public long ProbeInt64 = 887313409151L;
    public float ProbeFloat = 40.9151f;
    public bool ProbeBool = true;

    private double _uptime;
    private double _exitAfter;

    public override void _Ready()
    {
        Instance = this;
        HarnessRoot = this;
        WalkCount = CountNodes(this);

        _exitAfter = 300.0;
        string exitAfter = OS.GetEnvironment(ExitAfterEnv);
        if (!string.IsNullOrEmpty(exitAfter) && double.TryParse(exitAfter, out double parsed))
        {
            _exitAfter = parsed;
        }

        WriteReadyFile();
        GD.Print("[grid] probe ready, ", WalkCount, " nodes under ", GetPath());
    }

    public override void _Process(double delta)
    {
        // Hold the process open long enough for the harness to attach, but never
        // leak a window forever if the harness dies. 0 or negative disables.
        if (_exitAfter <= 0.0)
        {
            return;
        }

        _uptime += delta;
        if (_uptime > _exitAfter)
        {
            GetTree().Quit(0);
        }
    }

    public static int CountNodes(Node node)
    {
        int total = 1;
        foreach (Node child in node.GetChildren())
        {
            total += CountNodes(child);
        }

        return total;
    }

    // ------------------------------------------------------------------------
    // The RAW child list, internal children included (ready.v2).
    //
    // Node.GetChildren() hides children added with INTERNAL_MODE_FRONT/BACK, but
    // a calibrator walking Node::data.children in memory CANNOT hide them: they
    // are ordinary entries in the same intrusive list. RichTextLabel builds a
    // VScrollBar this way, so ZetaRich has one child in memory and none in the
    // authored scene.
    //
    // Ground truth generated from Main.tscn therefore cannot describe what the
    // walk actually sees, and stating a node count of 20 to a calibrator that
    // will find 21 does not merely fail the cell -- it makes the CORRECT layout
    // unacceptable and pushes the search onto a wrong one. Only the engine knows
    // its own internal children, and their names (@VScrollBar@2) carry an
    // instantiation counter that no static file could predict, so the engine
    // reports them here and the harness reconciles.
    // ------------------------------------------------------------------------
    public static Godot.Collections.Array RawTree(Node root)
    {
        var rows = new Godot.Collections.Array();
        AppendRawTree(root, root.Name, rows);
        return rows;
    }

    private static void AppendRawTree(Node node, string path, Godot.Collections.Array rows)
    {
        int count = node.GetChildCount(true);
        var childNames = new Godot.Collections.Array();
        for (int i = 0; i < count; i++)
        {
            childNames.Add(node.GetChild(i, true).Name.ToString());
        }

        rows.Add(new Godot.Collections.Dictionary
        {
            { "path", path },
            { "name", node.Name.ToString() },
            { "class", node.GetClass() },
            { "children", childNames },
            { "internal", node.GetParent() != null && !node.GetParent().GetChildren().Contains(node) },
        });

        for (int i = 0; i < count; i++)
        {
            Node child = node.GetChild(i, true);
            AppendRawTree(child, path + "/" + child.Name, rows);
        }
    }

    private void WriteReadyFile()
    {
        string path = OS.GetEnvironment(ReadyFileEnv);
        if (string.IsNullOrEmpty(path))
        {
            path = "user://grid-ready.json";
        }

        Godot.Collections.Array rawTree = RawTree(this);

        var info = new Godot.Collections.Dictionary
        {
            { "contract", ContractVersion },
            { "pid", OS.GetProcessId() },
            { "engineVersion", (string)Engine.GetVersionInfo()["string"] },
            { "binding", "dotnet" },
            { "templateVariant", OS.HasFeature("template_debug") ? "debug" : "release" },
            { "precision", OS.HasFeature("double") ? "double" : "single" },
            { "isTemplate", OS.HasFeature("template") },
            { "isEditor", OS.HasFeature("editor") },
            { "hasMonoFeature", OS.HasFeature("mono") },
            { "walkRootPath", GetPath().ToString() },
            { "walkCount", WalkCount },
            { "rawWalkCount", rawTree.Count },
            { "rawTree", rawTree },
            { "executable", OS.GetExecutablePath() },
            { "userDataDir", OS.GetUserDataDir() },
            { "staticRootType", nameof(Probe) },
            { "staticRootField", nameof(Instance) },
            { "buildTag", BuildTag },
        };

        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError("[grid] could not write ready file to " + path + ": " + FileAccess.GetOpenError());
            return;
        }

        file.StoreString(Json.Stringify(info, "  "));
        file.Flush();
    }
}
