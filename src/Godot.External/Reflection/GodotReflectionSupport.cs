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
/// <b>4.6 / master breaks the walk.</b> <c>ClassInfo::property_setget</c> becomes an
/// <c>AHashMap</c>, whose element storage is not the intrusive doubly-linked
/// <c>HashMapElement</c> chain this module walks. A different walker is needed; guessing with this
/// one would traverse unrelated memory.
/// </item>
/// <item>
/// <b><c>HashMap</c> shrank 48 → 40 bytes between 4.3 and 4.5</b>, so any stride computed for one is
/// wrong for the other.
/// </item>
/// <item>
/// <b><c>StringName::_Data</c> dropped <c>cname</c> and <c>idx</c> in 4.5</b>, which moves every
/// field after them. A 4.3 reader must check <c>cname</c> before <c>name</c>; a 4.5 reader must not
/// look for it.
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
    public const int FirstUnsupportedMinor = 6;

    /// <summary>The oldest minor version whose layout differences are accounted for.</summary>
    public const int OldestSupportedMinor = 3;

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
            reason = $"Godot {major}.x is outside everything this module was checked against (4.3, 4.5)";
            return false;
        }

        if (minor >= FirstUnsupportedMinor)
        {
            reason = $"Godot 4.{minor}: ClassInfo::property_setget is an AHashMap from 4.6 onward, " +
                "which does not use the intrusive HashMapElement chain this walker follows";
            return false;
        }

        if (minor < OldestSupportedMinor)
        {
            reason = $"Godot 4.{minor} predates the oldest layout accounted for (4.{OldestSupportedMinor})";
            return false;
        }

        layout = minor == OldestSupportedMinor ? ClassDbLayout.Godot43 : ClassDbLayout.Godot45;
        reason = string.Empty;
        return true;
    }
}
