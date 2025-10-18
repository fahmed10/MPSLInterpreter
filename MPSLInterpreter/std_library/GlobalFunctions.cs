using System.Collections.Frozen;
using System.Diagnostics;

namespace MPSLInterpreter.StdLibrary;

public static class GlobalFunctions
{
    public static readonly FrozenDictionary<string, NativeFunction> functions = new Dictionary<string, NativeFunction>()
    {
        { "time", new(Time) },
        { "print", new(Print) },
        { "write", new(Write) },
        { "read", new(Read) },
        { "read_key", new(ReadKey) },
        { "parse_num", new(ParseNum) },
        { "clear", new(Clear) },
        { "wait", new(Wait) },
        { "length", new(Length) },
        { "fill_array", new(FillArray) },
        { "insert", new(Insert) },
        { "remove_at", new(RemoveAt) },
        { "range", new(Range) },
        { "range_to", new(RangeTo) },
        { "set_color", new(SetColor) },
        { "set_bg_color", new(SetBgColor) },
        { "replace", new(Replace) },
        { "split", new(Split) },
        { "if", new(If) },
        { "mod", new(Mod) },
        { "run_process", new(Run) },
        { "str", new(ToStr) },
        { "type", new(GetMPSLType) }
    }.ToFrozenDictionary();

    private static double Time() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static void Print(object? value) => Console.WriteLine(Interpreter.ToMPSLString(value));
    private static void Write(object? value) => Console.Write(Interpreter.ToMPSLString(value));
    private static string? Read() => Console.ReadLine();
    private static string? ReadKey() => Console.ReadKey(true).KeyChar.ToString();
    private static double? ParseNum(string value)
    {
        if (double.TryParse(value, out double d))
        {
            return d;
        }

        return null;
    }
    private static void Clear() => Console.Clear();
    private static void Wait(double time)
    {
        Task.Delay(TimeSpan.FromSeconds(time)).Wait();
    }
    private static double Length(object? value)
    {
        return value switch
        {
            string s => s.Length,
            MPSLArray a => a.Count,
            null => throw new ArgumentException("Cannot get the length of null."),
            _ => throw new ArgumentException("Can only get the length of a string or array.")
        };
    }
    private static MPSLArray FillArray(double size)
    {
        if (size >= 0)
        {
            object?[] array = new object?[(int)size];
            Array.Fill(array, null);
            return new(array);
        }
        else
        {
            throw new ArgumentException("Size cannot be negative.");
        }
    }
    private static void Insert(MPSLArray array, double index, object? value)
    {
        if (index >= 0)
        {
            array.Insert((int)index, value);
        }
        else
        {
            throw new ArgumentException("Index cannot be negative.");
        }
    }
    private static void RemoveAt(MPSLArray array, double index)
    {
        if (index >= 0)
        {
            array.RemoveAt((int)index);
        }
        else
        {
            throw new ArgumentException("Index cannot be negative.");
        }
    }
    private static MPSLArray Range(double from, double to) => new(Enumerable.Range((int)from, (int)to - (int)from).Select(n => (double)n).Cast<object>());
    private static MPSLArray RangeTo(double to) => Range(0, to);
    private static void SetColor(double color) => Console.ForegroundColor = (ConsoleColor)color;
    private static void SetBgColor(double color) => Console.BackgroundColor = (ConsoleColor)color;
    private static string Replace(string str, string pattern, string replacement) => str.Replace(pattern, replacement);
    private static MPSLArray Split(string str, string delimiter) => new(str.Split(delimiter));
    private static object? If(object? condition, object? ifTrue, object? ifFalse) => Interpreter.IsTruthy(condition) ? ifTrue : ifFalse;
    private static double Mod(double number, double by) => number % by;
    private static void Run(string path, MPSLArray args) => Process.Start(new ProcessStartInfo(path, args.Select(Interpreter.ToMPSLString)));
    private static string ToStr(object? value) => Interpreter.ToMPSLString(value);
    private static string GetMPSLType(object? value) => Interpreter.GetMPSLType(value);
}
