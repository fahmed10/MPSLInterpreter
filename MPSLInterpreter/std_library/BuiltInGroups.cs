using System.Collections.Frozen;

namespace MPSLInterpreter.StdLibrary;

public static class BuiltInGroups
{
    public static readonly FrozenDictionary<string, MPSLGroup> groups = new Dictionary<string, MPSLGroup>()
    {
        { "Regex", new(Regex.GetEnvironment()) },
        { "IO", new(IO.GetEnvironment()) },
        { "FFI", new(FFI.GetEnvironment()) }
    }.ToFrozenDictionary();
}