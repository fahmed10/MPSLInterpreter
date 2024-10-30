﻿using System.Text.RegularExpressions;

namespace MPSLInterpreter;

internal static class BuiltInFunctions
{
    public static Dictionary<string, NativeFunction> functions = new()
    {
        { "time", new(0, Time) },
        { "print", new(1, Print) },
        { "write", new(1, Write) },
        { "read", new(0, Read) },
        { "read_key", new(0, ReadKey) },
        { "parse_num", new(1, ParseNum) },
        { "clear", new(0, Clear) },
        { "wait", new(1, Wait) },
        { "length", new(1, Length) },
        { "fill_array", new(1, FillArray) },
        { "range", new(2, Range) },
        { "range_to", new(1, RangeTo) },
        { "set_color", new(1, SetColor) },
        { "set_bg_color", new(1, SetBgColor) },
        { "read_file", new (1, ReadFile) },
        { "write_file", new (2, WriteFile) },
        { "del_file", new (1, DeleteFile) },
        { "del_dir", new (1, DeleteDirectory) },
        { "make_dir", new (1, MakeDirectory) },
        { "read_dir", new (1, ReadDirectory) },
        { "replace", new (3, Replace) },
        { "regex_replace", new (3, RegexReplace) },
        { "regex_match", new (2, RegexMatch) },
        { "regex_matches", new (2, RegexMatches) },
        { "if", new (3, If) },
    };

    private static double Time() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000d;
    private static void Print(object value) => Console.WriteLine(Interpreter.ToMPSLString(value));
    private static void Write(object value) => Console.Write(Interpreter.ToMPSLString(value));
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
    private static double Length(object value)
    {
        return value switch
        {
            string s => s.Length,
            MPSLArray a => a.Count,
            _ => throw new ArgumentException("Can only get the length of a string or array.")
        };
    }
    private static MPSLArray FillArray(double size)
    {
        if (size >= 0)
        {
            object?[] array = new object?[(int)size];
            Array.Fill(array, null);
            return new MPSLArray(array);
        }
        else
        {
            throw new ArgumentException("Size cannot be negative.");
        }
    }
    private static MPSLArray Range(double from, double to) => new(Enumerable.Range((int)from, (int)to - (int)from).Cast<object>());
    private static MPSLArray RangeTo(double to) => Range(0, to);
    private static void SetColor(double color) => Console.ForegroundColor = (ConsoleColor)color;
    private static void SetBgColor(double color) => Console.BackgroundColor = (ConsoleColor)color;
    private static string ReadFile(string path) => File.ReadAllText(path);
    private static void WriteFile(string path, string str) => File.WriteAllText(path, str);
    private static void DeleteFile(string path) => File.Delete(path);
    private static void DeleteDirectory(string path) => Directory.Delete(path, true);
    private static void MakeDirectory(string path) => Directory.CreateDirectory(path);
    private static MPSLArray ReadDirectory(string path) => new(Directory.GetFiles(path));
    private static string Replace(string s, string p, string r) => s.Replace(p, r);
    private static string RegexReplace(string s, string p, string r) => Regex.Replace(s, p, r);
    private static bool RegexMatch(string s, string p) => Regex.Match(s, p).Success;
    private static MPSLArray RegexMatches(string s, string p) => new(Regex.Matches(s, p).Select(m => m.Value));
    private static object? If(object? condition, object? ifTrue, object? ifFalse) => Interpreter.IsTruthy(condition) ? ifTrue : ifFalse;
}