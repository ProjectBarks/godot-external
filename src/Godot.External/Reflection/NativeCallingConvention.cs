namespace Godot.External.Reflection;

/// <summary>
/// Which platform ABI a getter body was compiled for. The decoder needs this because "the
/// <c>this</c> pointer" is not a portable concept — it is a register assignment fixed by the ABI.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists so the Windows assumption is stated rather than assumed.</b> The whole getter-code
/// route rests on <c>TYPED_METHOD_BIND</c>, which Godot defines in <c>platform/windows/detect.py</c>
/// and <em>not</em> on Linux. Under MSVC a pointer-to-member of a single-inheritance,
/// no-virtual-bases class (which every <c>Object</c> subclass is) is a plain 8-byte code address, so
/// the <c>MethodBind</c> holds something we can disassemble. Under the Itanium C++ ABI the same
/// pointer is a <c>{ptr, adjustment}</c> pair and <c>this</c> arrives in RDI, so the same bytes read
/// as a different function.
/// </para>
/// <para>
/// Only <see cref="MsvcX64"/> is implemented. <see cref="SystemVX64"/> is present precisely so that
/// a caller targeting Linux gets a <em>refusal</em> naming the reason instead of a plausible number
/// decoded under the wrong register convention.
/// </para>
/// </remarks>
public enum NativeCallingConvention
{
    /// <summary>
    /// Windows x64 (MSVC). Integer arguments in RCX, RDX, R8, R9; a return value too large for RAX
    /// is written through a hidden pointer passed in RCX, which shifts <c>this</c> to RDX.
    /// </summary>
    MsvcX64 = 0,

    /// <summary>
    /// System V x86-64 (Linux/macOS). <b>Not supported</b> — see the remarks on this enum. Passing it
    /// yields <see cref="FieldOffsetDecodeStatus.UnsupportedCallingConvention"/>.
    /// </summary>
    SystemVX64 = 1,
}
