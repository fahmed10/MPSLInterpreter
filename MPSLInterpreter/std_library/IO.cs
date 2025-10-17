namespace MPSLInterpreter.StdLibrary;

internal static class IO
{
    public static MPSLEnvironment GetEnvironment()
    {
        MPSLEnvironment environment = new();
        environment.DefineFunction("read_file", new(ReadFile));
        environment.DefineFunction("write_file", new(WriteFile));
        environment.DefineFunction("del_file", new(DeleteFile));
        environment.DefineFunction("del_dir", new(DeleteDirectory));
        environment.DefineFunction("make_dir", new(MakeDirectory));
        environment.DefineFunction("read_dir", new(ReadDirectory));
        return environment;
    }

    private static string ReadFile(string path) => File.ReadAllText(path);
    private static void WriteFile(string path, string str) => File.WriteAllText(path, str);
    private static void DeleteFile(string path) => File.Delete(path);
    private static void DeleteDirectory(string path) => Directory.Delete(path, true);
    private static void MakeDirectory(string path) => Directory.CreateDirectory(path);
    private static MPSLArray ReadDirectory(string path) => new(Directory.GetFiles(path));
}