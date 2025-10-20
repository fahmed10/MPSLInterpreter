using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace MPSLInterpreter.StdLibrary;

internal static unsafe partial class LibFFI
{
    public enum ffi_status : uint
    {
        OK,
        FFI_BAD_TYPEDEF,
        FFI_BAD_ABI,
        FFI_BAD_ARGTYPE
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ffi_cif
    {
        ffi_abi abi;
        uint nargs;
        ffi_type** arg_types;
        ffi_type* rtype;
        uint bytes;
        uint flags;
    }

    public enum ffi_abi : uint
    {
        FFI_FIRST_ABI,
        FFI_WIN64,
        FFI_GNUW64,
        FFI_LAST_ABI,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ffi_type(ushort size, ushort type)
    {
        public ulong size = size;
        ushort alignment = size;
        ushort type = type;
        public ffi_type** elements = (ffi_type**)0;

        public static readonly ffi_type Void = new(1, 0);
        public static readonly ffi_type Pointer = new((ushort)nint.Size, 14);
        public static readonly ffi_type Struct = new(0, 13);

        public static readonly ffi_type Float = new(sizeof(float), 2);
        public static readonly ffi_type Double = new(sizeof(double), 3);

        public static readonly ffi_type SInt8 = new(sizeof(sbyte), 6);
        public static readonly ffi_type SInt16 = new(sizeof(short), 8);
        public static readonly ffi_type SInt32 = new(sizeof(int), 10);
        public static readonly ffi_type SInt64 = new(sizeof(long), 12);

        public static readonly ffi_type UInt8 = new(sizeof(byte), 5);
        public static readonly ffi_type UInt16 = new(sizeof(ushort), 7);
        public static readonly ffi_type UInt32 = new(sizeof(uint), 9);
        public static readonly ffi_type UInt64 = new(sizeof(ulong), 11);

        public static bool operator ==(ffi_type a, ffi_type b) => a.type == b.type;
        public static bool operator !=(ffi_type a, ffi_type b) => a.type != b.type;

        public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is ffi_type t && t.type == type;
        public override readonly int GetHashCode() => type;
    }

    [LibraryImport("libffi-8")]
    public static partial ffi_status ffi_prep_cif(ffi_cif* cif, ffi_abi abi, uint nargs, ffi_type* rtype, ffi_type** argtypes);

    [LibraryImport("libffi-8")]
    public static partial void ffi_call(ffi_cif* cif, void* fn, void* rvalue, void** avalues);
}