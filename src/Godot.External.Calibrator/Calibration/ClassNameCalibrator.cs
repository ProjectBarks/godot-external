using System.Buffers.Binary;
using System.Text;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>Each node's engine class, and the vtable that identified it.</summary>
public sealed record ClassNameMap(
    string Method,
    IReadOnlyDictionary<ulong, string> Names,
    IReadOnlyDictionary<ulong, ulong> Vtables,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Ancestry)
{
    /// <summary>
    /// Whether <paramref name="node"/>'s class is, or descends from, <paramref name="baseClass"/>.
    /// <see langword="null"/> when the hierarchy could not be read — which is not the same as "no".
    /// </summary>
    public bool? DescendsFrom(ulong node, string baseClass)
    {
        if (!Names.TryGetValue(node, out string? className))
        {
            return null;
        }

        if (className == baseClass)
        {
            return true;
        }

        if (!Ancestry.TryGetValue(className, out IReadOnlyList<string>? chain) || chain.Count == 0)
        {
            return null;
        }

        return chain.Contains(baseClass);
    }

    /// <summary>Every node whose engine class is exactly <paramref name="className"/>.</summary>
    public HashSet<ulong> Instances(string className)
        => [.. Names.Where(kv => kv.Value == className).Select(kv => kv.Key)];

    /// <summary>Distinct classes seen.</summary>
    public int ClassCount => Names.Values.Distinct().Count();
}

/// <summary>
/// Reads each node's class from its vtable, through Itanium RTTI. Nothing is calibrated.
/// </summary>
/// <remarks>
/// <para>
/// This replaces reading <c>Object::_class_name_ptr</c>, which cannot work and never could on
/// 4.2–4.4: <c>_postinitialize()</c> assigns it and nulls it again two lines later, so for the
/// entire observable lifetime of every node the slot reads zero. That is why a <c>cname</c>
/// fallback, a coverage threshold and name corroboration all produced no change whatsoever — there
/// was nothing there to derive. Godot PR #105099 fixed it for 4.5 only.
/// </para>
/// <para>
/// The vtable route has no such version dependence, and no offsets to derive:
/// </para>
/// <code>
/// vptr     = u64[node + 0]     Object is polymorphic with no base, so the vptr is at offset 0
/// offs_top = i64[vptr - 16]    0 for a primary-base object — a free validity check
/// tinfo    = u64[vptr -  8]    std::type_info *
/// name     = cstring(u64[tinfo + 8])   "5Label" -> length-prefixed, so "Label"
/// </code>
/// <para>
/// Every one of those is an ABI constant. Official Godot Windows templates are built with
/// MinGW-GCC — not MSVC, as had been assumed — so Itanium RTTI is the right shape, and it is
/// enabled (no <c>-fno-rtti</c> anywhere in the build). A custom MSVC-built template is detected and
/// declined rather than decoded as garbage.
/// </para>
/// <para>
/// The vtable pointer is also used as the grouping key <em>before</em> any name is resolved: it
/// partitions the scene into exact class-equivalence sets for free, and the name then costs one
/// resolution per distinct class rather than one per node.
/// </para>
/// <para>
/// <b>This identity is unauthenticated, and it is now the single point of failure for text
/// correctness.</b> Text publication is gated on the class named here, so a target that presents a
/// forged vtable — pointing at a type_info whose name says <c>Label</c> — gets that name back, and
/// nothing downstream disputes it. <see cref="Corroborated"/> only cross-checks nodes Godot itself
/// named after their class, which a forger controls too. That is an acceptable trade for reading a
/// cooperating, unmodified game, which is what this library is for; it is not a security boundary,
/// and it should not be relied on as one against a target that has any reason to lie.
/// </para>
/// </remarks>
public static class ClassNameCalibrator
{
    /// <summary>Longest accepted class name.</summary>
    public const int MaxNameLength = 64;

    /// <summary>How <c>Names</c> was obtained, for the result.</summary>
    public const string ItaniumRtti = "vtable-itanium-rtti";

    /// <summary>Reads every node's class, or explains why it could not.</summary>
    public static ClassNameMap? Derive(IMemoryReader reader, IReadOnlyList<ulong> nodes, out string diagnosis)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(nodes);

        diagnosis = "not attempted";
        if (nodes.Count < 3)
        {
            diagnosis = "too few walked nodes to identify classes";
            return null;
        }

        Dictionary<ulong, ulong> vtables = [];
        foreach (ulong node in nodes)
        {
            if (reader.TryReadPointer(node, out ulong vptr) && vptr != 0 && (vptr & 7) == 0)
            {
                vtables[node] = vptr;
            }
        }

        // Most, not every: a node whose window is short says nothing about the ones that answered.
        int required = (nodes.Count * 3 / 4) + 1;
        if (vtables.Count < required)
        {
            diagnosis = $"only {vtables.Count} of {nodes.Count} nodes had a readable vtable pointer at offset 0";
            return null;
        }

        Dictionary<ulong, string> byVtable = [];
        string? abiRefusal = null;
        foreach (ulong vptr in vtables.Values.Distinct())
        {
            if (TryReadClassName(reader, vptr, out string name, out string why))
            {
                byVtable[vptr] = name;
            }
            else if (why.Length > 0)
            {
                abiRefusal ??= why;
            }
        }

        Dictionary<ulong, string> named = [];
        foreach ((ulong node, ulong vptr) in vtables)
        {
            if (byVtable.TryGetValue(vptr, out string? name))
            {
                named[node] = name;
            }
        }

        if (named.Count < required)
        {
            diagnosis = abiRefusal
                ?? $"only {named.Count} of {nodes.Count} nodes resolved a class name through their vtable; "
                 + "this build may have been compiled without RTTI";
            return null;
        }

        if (named.Values.Distinct().Count() < 2)
        {
            diagnosis = $"every walked node resolved to the same class (\"{named.Values.First()}\"), which cannot "
                      + "be right for a scene tree and suggests the pointer is not a vtable";
            return null;
        }

        Dictionary<string, IReadOnlyList<string>> ancestry = [];
        foreach ((ulong vptr, string name) in byVtable)
        {
            ancestry[name] = ReadBaseChain(reader, vptr);
        }

        diagnosis = $"{named.Count}/{nodes.Count} nodes named across {named.Values.Distinct().Count()} classes";
        return new ClassNameMap(ItaniumRtti, named, vtables, ancestry);
    }

    /// <summary>
    /// Walks a class's base chain, so the hierarchy comes from the target rather than a hardcoded
    /// list of Godot class names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Itanium's <c>__si_class_type_info</c> — the single-inheritance case, which is every node class
    /// in Godot — carries <c>__base_type</c> at <c>+16</c>, so the chain is a pointer walk with no
    /// offsets to derive. A root class uses plain <c>__class_type_info</c> instead, which has nothing
    /// there, so each step is validated as a type_info in its own right before being believed and the
    /// walk simply stops when it stops validating.
    /// </para>
    /// <para>
    /// This exists because class NAMES alone cannot answer "does this node have a CanvasItem?", and
    /// guessing it from geometry cannot either: a bare <c>Node</c> read through Control offsets
    /// yields all-zeros, which is perfectly plausible geometry. Publishing <c>visible=true</c> for an
    /// object with no <c>CanvasItem</c> base is the same category of error as publishing text for a
    /// non-Label.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> ReadBaseChain(IMemoryReader reader, ulong vtable, int maxDepth = 16)
    {
        ArgumentNullException.ThrowIfNull(reader);

        List<string> chain = [];
        if (vtable < 32 || !reader.TryReadPointer(vtable - 8, out ulong typeInfo) || typeInfo == 0)
        {
            return chain;
        }

        HashSet<ulong> seen = [typeInfo];
        for (int depth = 0; depth < maxDepth; depth++)
        {
            if (!reader.TryReadPointer(typeInfo + 16, out ulong baseInfo)
                || baseInfo == 0
                || (baseInfo & 7) != 0
                || !seen.Add(baseInfo)
                || !TryReadTypeInfoName(reader, baseInfo, out string baseName))
            {
                break;
            }

            chain.Add(baseName);
            typeInfo = baseInfo;
        }

        return chain;
    }

    /// <summary>Resolves one vtable to its class name.</summary>
    public static bool TryReadClassName(IMemoryReader reader, ulong vtable, out string name, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(reader);

        name = string.Empty;
        refusal = string.Empty;

        if (vtable < 32
            || !TryReadInt64(reader, vtable - 16, out long offsetToTop)
            || offsetToTop != 0
            || !reader.TryReadPointer(vtable - 8, out ulong typeInfo)
            || typeInfo == 0
            || (typeInfo & 7) != 0)
        {
            return false;
        }

        // ABI probe. Itanium: type_info's first qword is its own vtable pointer, a canonical
        // read-only address. MSVC: the slot holds a CompleteObjectLocator whose first DWORD is 0 or
        // 1. Declining is the point — a custom MSVC template must degrade, not emit garbage.
        if (!reader.TryReadPointer(typeInfo, out ulong typeInfoVtable))
        {
            return false;
        }

        if (typeInfoVtable < 0x10000 || typeInfoVtable >= 1UL << 48)
        {
            // Deliberately does not name MSVC. The probe reliably DECLINES a non-Itanium block, but
            // it cannot reliably say what the block is: only when the COL's object-offset dword is
            // zero does the qword look small enough to recognise, and otherwise this is simply
            // "not the shape expected". Declining is a measurement; the cause is not.
            refusal = $"the type-info block at 0x{typeInfo:x} is not shaped like Itanium RTTI (its first qword, "
                    + $"0x{typeInfoVtable:x}, is not a canonical vtable pointer). This reader decodes Itanium RTTI "
                    + "only, as emitted by the MinGW-GCC toolchain the official Windows templates are built with; "
                    + "a template built with another toolchain, or without RTTI, lands here.";
            return false;
        }

        return TryReadTypeInfoName(reader, typeInfo, out name);
    }

    /// <summary>Reads and demangles the name out of a <c>std::type_info</c>, validating its shape.</summary>
    public static bool TryReadTypeInfoName(IMemoryReader reader, ulong typeInfo, out string name)
    {
        ArgumentNullException.ThrowIfNull(reader);

        name = string.Empty;

        if (!reader.TryReadPointer(typeInfo, out ulong typeInfoVtable)
            || typeInfoVtable < 0x10000
            || typeInfoVtable >= 1UL << 48)
        {
            return false;
        }

        if (!reader.TryReadPointer(typeInfo + 8, out ulong mangledPointer)
            || mangledPointer == 0
            || !TryReadAscii(reader, mangledPointer, out string mangled))
        {
            return false;
        }

        return TryDemangle(mangled, out name);
    }

    /// <summary>
    /// Turns an Itanium mangled type name into a class name: <c>"5Label"</c> to <c>"Label"</c>.
    /// </summary>
    /// <remarks>
    /// Godot's node classes are all in the global namespace, so the length-prefixed form is the only
    /// one that needs decoding. A nested name arrives as <c>N…E</c> and is refused outright rather
    /// than guessed at — a wrong class name produces a phantom exactly as a wrong offset would.
    /// </remarks>
    public static bool TryDemangle(string mangled, out string name)
    {
        ArgumentNullException.ThrowIfNull(mangled);

        name = string.Empty;
        ReadOnlySpan<char> span = mangled.AsSpan();

        if (span.Length > 0 && span[0] == '*')
        {
            span = span[1..];
        }

        if (span.Length == 0 || span[0] == 'N')
        {
            return false; // nested/namespaced: not a Godot node class, and not decoded here
        }

        int digits = 0;
        while (digits < span.Length && char.IsAsciiDigit(span[digits]))
        {
            digits++;
        }

        if (digits == 0 || !int.TryParse(span[..digits], out int length) || length <= 0)
        {
            return false;
        }

        ReadOnlySpan<char> rest = span[digits..];
        if (rest.Length != length)
        {
            return false; // trailing template or qualifier junk: not a plain class
        }

        name = rest.ToString();
        return IsClassIdentifier(name);
    }

    /// <summary>Whether a decoded string is shaped like a Godot class name.</summary>
    public static bool IsClassIdentifier(string value)
    {
        if (value.Length is < 2 or > MaxNameLength || !char.IsAsciiLetter(value[0]))
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether at least one derived class name is confirmed by a node name Godot generated.
    /// </summary>
    /// <remarks>
    /// Godot names its own internal children after their class — <c>@VScrollBar@2</c> is a
    /// <c>VScrollBar</c> — so wherever the walk contains one, the node name corroborates the class
    /// name for free and with no scene knowledge at all.
    /// </remarks>
    public static bool Corroborated(
        IReadOnlyDictionary<ulong, string> classes,
        IReadOnlyDictionary<ulong, string>? nodeNames)
    {
        if (nodeNames is null)
        {
            return true;
        }

        bool sawInternal = false;
        foreach ((ulong node, string name) in nodeNames)
        {
            if (name.Length < 3 || name[0] != '@')
            {
                continue;
            }

            int tail = name.IndexOf('@', 1);
            string declared = tail > 1 ? name[1..tail] : name[1..];
            if (!IsClassIdentifier(declared))
            {
                continue;
            }

            sawInternal = true;
            if (classes.TryGetValue(node, out string? derived) && derived == declared)
            {
                return true;
            }
        }

        return !sawInternal;
    }

    private static bool TryReadInt64(IMemoryReader reader, ulong address, out long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        if (!reader.TryRead(address, buffer))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt64LittleEndian(buffer);
        return true;
    }

    private static bool TryReadAscii(IMemoryReader reader, ulong address, out string value)
    {
        value = string.Empty;

        Span<byte> buffer = stackalloc byte[MaxNameLength + 8];
        if (!reader.TryRead(address, buffer))
        {
            return false;
        }

        StringBuilder builder = new();
        foreach (byte b in buffer)
        {
            if (b == 0)
            {
                value = builder.ToString();
                return value.Length > 0;
            }

            if (b is < 0x20 or > 0x7E)
            {
                return false;
            }

            builder.Append((char)b);
        }

        return false;
    }
}
