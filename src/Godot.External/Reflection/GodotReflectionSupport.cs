namespace Godot.External.Reflection;

/// <summary>
/// Whether the getter-code / <c>ClassDB</c> reflection route applies to a given Godot build at all,
/// and the structure layouts that route depends on.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a gate, not a hint.</b> Every fact the route rests on is version- and platform-scoped,
/// and each one fails differently: a wrong platform decodes the wrong registers, a wrong version
/// walks the wrong container. Refusing up front is the only way the rest of the module gets to
/// assume what it assumes.
/// </para>
/// <para>
/// Known boundaries, from Godot's own source:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Windows only.</b> <c>platform/windows/detect.py</c> defines <c>TYPED_METHOD_BIND</c>; no other
/// platform does. Without it the <c>MethodBind</c> holds no typed member pointer to disassemble.
/// </item>
/// <item>
/// <b>4.6 converts three <c>ClassInfo</c> maps to <c>AHashMap</c></b> — <c>constant_map</c>,
/// <c>signal_map</c> and <c>property_setget</c> — whose storage is a dense array plus an
/// open-addressed index, not the intrusive doubly-linked <c>HashMapElement</c> chain. That is now
/// <em>supported</em> rather than refused: <see cref="AHashMapWalk"/> reads them and
/// <see cref="ClassDbLayout.HasAHashMaps"/> says which version needs it. The map this route
/// actually depends on, <c>ClassInfo::method_map</c>, is still a <c>HashMap</c> at 4.6 — but it
/// moves to <c>+0x28</c>, because <c>ClassInfo</c> gains <c>const GDType *gdtype</c>.
/// </item>
/// <item>
/// <b>4.7 removes the names this route reads.</b> <c>ClassInfo</c> at 4.7 drops
/// <c>StringName name</c> and <c>StringName inherits</c> entirely, folding them into <c>GDType</c>
/// (<c>class_db.h:120-157</c>), so a walker that reads a class name out of a <c>ClassInfo</c> reads
/// two unrelated members instead. 4.6 is the transitional tag carrying both, which is why the gate
/// opens exactly one version and no further. <b>This is a source reading, not a measurement</b> —
/// no 4.7 template has been run here.
/// </item>
/// <item>
/// <b><c>CowData</c>'s header grew at 4.6.</b> The element count moves from <c>buffer-8</c> to
/// <c>buffer-0x10</c>, so a 4.5-shaped string reader decodes <em>nothing</em> on a 4.6 target — see
/// <see cref="ClassDbLayout.CowDataSizeBackOffset"/>. It fails silently and in the most misleading
/// possible direction: every class and method name comes back empty, which reads as "this engine
/// has no such class" rather than as a layout change.
/// </item>
/// <item>
/// <b><c>HashMap</c> shrank 48 → 40 bytes between 4.4 and 4.5</b> (4.3/4.4 hold the allocator as a
/// member, 4.5+ inherit it privately and empty-base-optimization erases it), so any stride computed
/// for one is wrong for the other. <c>head_element</c> moves with it, <c>+0x18</c> → <c>+0x10</c>.
/// Both numbers are live-measured; see <see cref="ClassDbLayout.HashMapSize"/> and
/// <see cref="ClassDbLayout.HashMapHeadElement"/>, and do not conflate them.
/// </item>
/// <item>
/// <b><c>StringName::_Data</c> dropped <c>cname</c> and <c>idx</c> in 4.5</b>, which moves every
/// field after them. A 4.3 reader must check <c>cname</c> before <c>name</c> — not as a formality:
/// on 4.3 the <c>method_map</c> keys carry their text in <c>cname</c> with <c>name</c> empty, so a
/// <c>name</c>-only reader resolves no method at all. A 4.5 reader must not look for it. See
/// <see cref="ClassDbLayout.StringNameHasCompileTimeName"/> for the measurement.
/// </item>
/// </list>
/// <para>
/// The <c>property_list</c> / <c>property_map</c> / <c>property_setget</c> members themselves are
/// safe to rely on in shipped games: <c>class_db.h</c> declares them <em>outside</em> the
/// <c>DEBUG_ENABLED</c> block, so they exist in release export templates.
/// </para>
/// <para>
/// <b>TODO — deliberately not built here.</b> Two further routes to the same offsets were scoped and
/// left for a later pass, both of which avoid disassembly entirely and would make a third and fourth
/// independent derivation for <see cref="OffsetCrossCheck"/> to weigh:
/// </para>
/// <list type="number">
/// <item>
/// <b>GDScript <c>member_indices</c></b> — fully data-driven. A script's member table maps property
/// names to indices with no machine code involved, so it has none of this module's
/// codegen-shape fragility. It covers script-declared members rather than engine fields, which makes
/// it complementary to this route rather than a replacement.
/// </item>
/// <item>
/// <b><c>ObjectDB::object_slots</c> as an anchor</b> — a way to reach a live <c>Object</c> without
/// scanning, which is precisely the seed <see cref="ClassDbElementWalk"/> currently lacks.
/// </item>
/// </list>
/// </remarks>
internal static class GodotReflectionSupport
{
    /// <summary>The first minor version whose <c>ClassDB</c> this module cannot walk.</summary>
    /// <remarks>
    /// Was 6, on the premise that <c>AHashMap</c> ended the walk. It did not — <see cref="AHashMapWalk"/>
    /// reads it, and <c>ClassDB::classes</c> and <c>ClassInfo::method_map</c> are both still
    /// <c>HashMap</c> at 4.6. 4.7 is a different problem and a real one: <c>ClassInfo</c> no longer
    /// carries <c>name</c> or <c>inherits</c> at all.
    /// </remarks>
    public const int FirstUnsupportedMinor = 7;

    /// <summary>The oldest minor version whose layout differences are accounted for.</summary>
    public const int OldestSupportedMinor = 3;

    /// <summary>
    /// The last minor version that uses the 4.3-family layout — 48-byte <c>HashMap</c> with
    /// <c>head_element</c> at <c>+0x18</c>, and a <c>StringName::_Data</c> that still carries
    /// <c>cname</c>.
    /// </summary>
    /// <remarks>
    /// docs/analysis.md §15.3: the <c>HashMap</c> 48→40 shrink and the <c>cname</c> removal both
    /// happen at 4.4→4.5, so <b>4.4 belongs with 4.3 and not with 4.5</b>. §15.7 found this gate
    /// sending 4.4 to the 4.5 layout, which is wrong in both members at once; it cost nothing only
    /// because the calibrator never reaches this code.
    /// </remarks>
    public const int LastFourThreeFamilyMinor = 4;

    /// <summary>
    /// Decides whether reflection may be attempted against a target build.
    /// </summary>
    /// <param name="major">Engine major version, e.g. 4.</param>
    /// <param name="minor">Engine minor version, e.g. 5.</param>
    /// <param name="isWindows">Whether the target is a Windows export template.</param>
    /// <param name="layout">The layout constants for that build; only valid when supported.</param>
    /// <param name="reason">Why it was refused. Empty when supported.</param>
    /// <returns><see langword="false"/> whenever anything about the build is outside what was verified.</returns>
    public static bool TryResolve(
        int major,
        int minor,
        bool isWindows,
        out ClassDbLayout layout,
        out string reason)
    {
        layout = ClassDbLayout.Godot45;

        if (!isWindows)
        {
            reason = "the reflection route requires TYPED_METHOD_BIND, which Godot defines only in " +
                "platform/windows/detect.py; on other platforms a pointer-to-member is an Itanium " +
                "{ptr, adjustment} pair and there is no code address to decode";
            return false;
        }

        if (major != 4)
        {
            reason = $"Godot {major}.x is outside everything this module was checked against (4.3, 4.5, 4.6)";
            return false;
        }

        if (minor >= FirstUnsupportedMinor)
        {
            reason = $"Godot 4.{minor}: ClassInfo drops StringName name and StringName inherits from 4.7 " +
                "onward, folding both into GDType, so there is no class name in a ClassInfo for this " +
                "walker to read; the route must be re-plumbed through ClassInfo::gdtype first";
            return false;
        }

        if (minor < OldestSupportedMinor)
        {
            reason = $"Godot 4.{minor} predates the oldest layout accounted for (4.{OldestSupportedMinor})";
            return false;
        }

        // Three layouts, not two, and the boundaries are not where a reader would guess: 4.4 sits with
        // 4.3 (§15.3), and 4.6 differs from 4.5 in ClassInfo and CowData while sharing its HashMap.
        layout = minor switch
        {
            <= LastFourThreeFamilyMinor => ClassDbLayout.Godot43,
            5 => ClassDbLayout.Godot45,
            _ => ClassDbLayout.Godot46,
        };

        reason = string.Empty;
        return true;
    }
}
