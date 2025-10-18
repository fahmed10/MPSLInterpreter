using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static MPSLInterpreter.StdLibrary.LibFFI;

namespace MPSLInterpreter.StdLibrary;

internal static class FFI
{
    public static MPSLEnvironment GetEnvironment()
    {
        MPSLEnvironment environment = new();
        environment.DefineFunction("load_library", new(LoadLibrary));
        environment.DefineFunction("unload_library", new(UnloadLibrary));
        environment.DefineFunction("get_platform", new(GetPlatform));
        environment.DefineFunction("get_arch", new(GetArch));
        environment.DefineFunction("call", new(CallNative));

        MPSLEnvironment pointerGroup = new();
        pointerGroup.DefineFunction("create", new(CreatePointer));
        pointerGroup.DefineFunction("create_of", new(CreatePointerTo));
        pointerGroup.DefineFunction("create_of_array", new(CreatePointerToArray));
        pointerGroup.DefineFunction("create_of_string", new(CreatePointerToString));
        pointerGroup.DefineFunction("read_of", new(ReadPointerAs));
        pointerGroup.DefineFunction("read_of_offset", new(ReadPointerAsOffset));
        pointerGroup.DefineFunction("read_of_array", new(ReadPointerAsArray));
        pointerGroup.DefineFunction("read_of_string", new(ReadPointerAsString));
        pointerGroup.DefineFunction("write_of", new(WritePointerOf));
        pointerGroup.DefineFunction("write_of_offset", new(WritePointerOfOffset));
        pointerGroup.DefineFunction("free", new(FreePointer));

        MPSLEnvironment typeGroup = new();
        typeGroup.DefineVariable("void", ffi_type.Void);
        typeGroup.DefineVariable("pointer", ffi_type.Pointer);
        typeGroup.DefineVariable("float", ffi_type.Float);
        typeGroup.DefineVariable("double", ffi_type.Double);
        typeGroup.DefineVariable("int8", ffi_type.SInt8);
        typeGroup.DefineVariable("int16", ffi_type.SInt16);
        typeGroup.DefineVariable("int32", ffi_type.SInt32);
        typeGroup.DefineVariable("int64", ffi_type.SInt64);
        typeGroup.DefineVariable("uint8", ffi_type.UInt8);
        typeGroup.DefineVariable("uint16", ffi_type.UInt16);
        typeGroup.DefineVariable("uint32", ffi_type.UInt32);
        typeGroup.DefineVariable("uint64", ffi_type.UInt64);

        environment.DefineGroup("Pointer", new(pointerGroup));
        environment.DefineGroup("Type", new(typeGroup));
        return environment;
    }

    private static nint LoadLibrary(string path)
    {
        if (NativeLibrary.TryLoad(path, out nint handle))
        {
            return handle;
        }

        return 0;
    }

    private static void UnloadLibrary(nint handle) => NativeLibrary.Free(handle);

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsLinux()) return "linux";

        return "other";
    }

    private static string GetArch() => RuntimeInformation.ProcessArchitecture.ToString();
    private static nint CreatePointer(double size) => Marshal.AllocHGlobal((int)size);
    private static nint CreatePointerTo(ffi_type type) => Marshal.AllocHGlobal((int)type.size);

    private static unsafe nint CreatePointerToArray(MPSLArray array, ffi_type type)
    {
        byte* pointer = (byte*)Marshal.AllocHGlobal(array.Count * (int)type.size);

        for (int i = 0; i < array.Count; i++)
        {
            WritePointerOfOffset((nint)pointer, type, array[i], i * (int)type.size);
        }

        return (nint)pointer;
    }

    private static unsafe nint CreatePointerToString(string str)
    {
        int size = Encoding.UTF8.GetByteCount(str) + 1;
        byte* pointer = (byte*)Marshal.AllocHGlobal(size);
        Span<byte> span = new(pointer, size);
        Encoding.UTF8.TryGetBytes(str.AsSpan(), span, out _);
        span[size - 1] = 0;
        return (nint)pointer;
    }

    private static object? ReadPointerAs(nint pointer, ffi_type type) => ReadPointerAsOffset(pointer, type, 0);
    private static unsafe object? ReadPointerAsOffset(nint pointer, ffi_type type, double offset)
    {
        T Read<T>() where T : unmanaged => MemoryMarshal.Read<T>(new ReadOnlySpan<byte>((byte*)pointer + nint.CreateChecked(offset), (int)type.size));

        if (type == ffi_type.UInt8) return (double)Read<byte>();
        else if (type == ffi_type.UInt16) return (double)Read<ushort>();
        else if (type == ffi_type.UInt32) return (double)Read<uint>();
        else if (type == ffi_type.UInt64) return (double)Read<ulong>();
        else if (type == ffi_type.SInt8) return (double)Read<sbyte>();
        else if (type == ffi_type.SInt16) return (double)Read<short>();
        else if (type == ffi_type.SInt32) return (double)Read<int>();
        else if (type == ffi_type.SInt64) return (double)Read<long>();
        else if (type == ffi_type.Float) return (double)Read<float>();
        else if (type == ffi_type.Double) return Read<double>();
        else if (type == ffi_type.Pointer) return Read<nint>();
        else throw new ArgumentException("Invalid pointer type.");
    }

    private static unsafe string ReadPointerAsString(nint pointer)
    {
        int length = 0;

        while (((byte*)pointer)[length] != '\0')
        {
            length++;
        }

        ReadOnlySpan<byte> span = new((byte*)pointer, length);
        return Encoding.UTF8.GetString(span);
    }

    private static unsafe MPSLArray ReadPointerAsArray(nint pointer, ffi_type type, double elements)
    {
        MPSLArray array = [];

        for (int i = 0; i < elements; i++)
        {
            array.Add(ReadPointerAsOffset(pointer, type, (int)type.size * i));
        }

        return array;
    }

    private static void WritePointerOf(nint pointer, ffi_type type, object? value) => WritePointerOfOffset(pointer, type, value, 0);
    private static unsafe void WritePointerOfOffset(nint pointer, ffi_type type, object? value, double offset)
    {
        void Write<T, TFrom>() where T : unmanaged, INumber<T> where TFrom : INumber<TFrom>
        {
            MemoryMarshal.Write<T>(new((byte*)pointer + nint.CreateChecked(offset), (int)type.size), T.CreateChecked((TFrom)value!));
        }

        if (type == ffi_type.UInt8) Write<byte, double>();
        else if (type == ffi_type.UInt16) Write<ushort, double>();
        else if (type == ffi_type.UInt32) Write<uint, double>();
        else if (type == ffi_type.UInt64) Write<ulong, double>();
        else if (type == ffi_type.SInt8) Write<sbyte, double>();
        else if (type == ffi_type.SInt16) Write<short, double>();
        else if (type == ffi_type.SInt32) Write<int, double>();
        else if (type == ffi_type.SInt64) Write<long, double>();
        else if (type == ffi_type.Float) Write<float, double>();
        else if (type == ffi_type.Double) Write<double, double>();
        else if (type == ffi_type.Pointer) Write<nint, nint>();
        else throw new ArgumentException("Invalid pointer type.");
    }

    private static void FreePointer(nint pointer) => Marshal.FreeHGlobal(pointer);

    private static unsafe object? CallNative(nint handle, string name, MPSLArray args, MPSLArray argTypes, ffi_type returnType)
    {
        void* symbol = (void*)NativeLibrary.GetExport(handle, name);

        var argTypesList = stackalloc ffi_type[argTypes.Count];
        var argTypesArray = stackalloc ffi_type*[argTypes.Count];

        for (int i = 0; i < argTypes.Count; i++)
        {
            argTypesList[i] = (ffi_type)argTypes[i]!;
            argTypesArray[i] = &argTypesList[i];
        }

        ulong ret;

        ffi_cif cif = new();
        ffi_status status = ffi_prep_cif(&cif, ffi_abi.FFI_DEFAULT_ABI, (uint)args.Count, &returnType, argTypesArray);

        void** argValues = stackalloc void*[argTypes.Count];

        for (int i = 0; i < argTypes.Count; i++)
        {
            nint arg = Marshal.AllocHGlobal((int)argTypesList[i].size);
            WritePointerOf(arg, argTypesList[i], args[i]);
            argValues[i] = (void*)arg;
        }

        ffi_call(&cif, symbol, &ret, argValues);

        for (int i = 0; i < argTypes.Count; i++)
        {
            Marshal.FreeHGlobal((nint)argValues[i]);
        }

        if (returnType == ffi_type.Void)
        {
            return null;
        }

        return ReadPointerAs((nint)(&ret), returnType);
    }
}