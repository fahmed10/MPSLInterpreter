using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static MPSLInterpreter.StdLibrary.LibFFI;

namespace MPSLInterpreter.StdLibrary;

internal static class FFI
{
    public static MPSLEnvironment GetEnvironment()
    {
        MPSLEnvironment environment = new();
        environment.DefineFunction("load_library", new(1, LoadLibrary));
        environment.DefineFunction("unload_library", new(1, UnloadLibrary));
        environment.DefineFunction("get_platform", new(0, GetPlatform));
        environment.DefineFunction("get_arch", new(0, GetArch));
        environment.DefineFunction("call", new(5, CallNative));
        environment.DefineVariable("void", ffi_type.Void);
        environment.DefineVariable("pointer", ffi_type.Pointer);
        environment.DefineVariable("float", ffi_type.Float);
        environment.DefineVariable("double", ffi_type.Double);
        environment.DefineVariable("int8", ffi_type.SInt8);
        environment.DefineVariable("int16", ffi_type.SInt16);
        environment.DefineVariable("int32", ffi_type.SInt32);
        environment.DefineVariable("int64", ffi_type.SInt64);
        environment.DefineVariable("uint8", ffi_type.UInt8);
        environment.DefineVariable("uint16", ffi_type.UInt16);
        environment.DefineVariable("uint32", ffi_type.UInt32);
        environment.DefineVariable("uint64", ffi_type.UInt64);
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

    private static unsafe object? CallNative(nint handle, string name, MPSLArray args, MPSLArray argTypes, ffi_type returnType)
    {
        void* symbol = (void*)NativeLibrary.GetExport(handle, name);

        var argTypesList = stackalloc ffi_type[argTypes.Count];
        var argTypesArray = new ffi_type*[argTypes.Count];

        for (int i = 0; i < argTypes.Count; i++)
        {
            argTypesList[i] = (ffi_type)argTypes[i]!;
            argTypesArray[i] = &argTypesList[i];
        }

        ulong ret;

        fixed (ffi_type** argTypesPtr = argTypesArray)
        {
            ffi_cif cif = new();
            ffi_status status = ffi_prep_cif(&cif, ffi_abi.FFI_DEFAULT_ABI, (uint)args.Count, &returnType, argTypesPtr);

            void** argValues = stackalloc void*[argTypes.Count];

            for (int i = 0; i < argTypes.Count; i++)
            {
                unsafe void AddArgument<T>() where T : unmanaged, INumber<T>
                {
                    T* arg = (T*)Marshal.AllocHGlobal((int)argTypesList[i].size);
                    *arg = T.CreateChecked((double)args[i]!);
                    argValues[i] = arg;
                }

                if (argTypesList[i] == ffi_type.UInt8) AddArgument<byte>();
                else if (argTypesList[i] == ffi_type.UInt16) AddArgument<ushort>();
                else if (argTypesList[i] == ffi_type.UInt32) AddArgument<uint>();
                else if (argTypesList[i] == ffi_type.UInt64) AddArgument<ulong>();
                else if (argTypesList[i] == ffi_type.SInt8) AddArgument<sbyte>();
                else if (argTypesList[i] == ffi_type.SInt16) AddArgument<short>();
                else if (argTypesList[i] == ffi_type.SInt32) AddArgument<int>();
                else if (argTypesList[i] == ffi_type.SInt64) AddArgument<long>();
                else if (argTypesList[i] == ffi_type.Float) AddArgument<float>();
                else if (argTypesList[i] == ffi_type.Double) AddArgument<double>();
                else if (argTypesList[i] == ffi_type.Pointer)
                {
                    nint* arg = (nint*)Marshal.AllocHGlobal((int)argTypesList[i].size);
                    *arg = (nint)args[i]!;
                    argValues[i] = arg;
                }
            }

            ffi_call(&cif, symbol, &ret, argValues);

            for (int i = 0; i < argTypes.Count; i++)
            {
                Marshal.FreeHGlobal((nint)argValues[i]);
            }
        }

        if (returnType == ffi_type.UInt8) return (double)Unsafe.As<ulong, byte>(ref ret);
        else if (returnType == ffi_type.UInt16) return (double)Unsafe.As<ulong, ushort>(ref ret);
        else if (returnType == ffi_type.UInt32) return (double)Unsafe.As<ulong, uint>(ref ret);
        else if (returnType == ffi_type.UInt64) return (double)ret;
        else if (returnType == ffi_type.SInt8) return (double)Unsafe.As<ulong, sbyte>(ref ret);
        else if (returnType == ffi_type.SInt16) return (double)Unsafe.As<ulong, short>(ref ret);
        else if (returnType == ffi_type.SInt32) return (double)Unsafe.As<ulong, int>(ref ret);
        else if (returnType == ffi_type.SInt64) return (double)Unsafe.As<ulong, long>(ref ret);
        else if (returnType == ffi_type.Float) return (double)Unsafe.As<ulong, float>(ref ret);
        else if (returnType == ffi_type.Double) return Unsafe.As<ulong, double>(ref ret);
        else if (returnType == ffi_type.Pointer) return Unsafe.As<ulong, nint>(ref ret);
        else if (returnType == ffi_type.Void) return null;
        else throw new ArgumentException("Invalid return type.");
    }
}