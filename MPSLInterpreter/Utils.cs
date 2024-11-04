using System.Diagnostics.CodeAnalysis;

namespace MPSLInterpreter;

internal static class Utils
{
    public static void WriteColored(string value, ConsoleColor color)
    {
        ConsoleColor oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(value);
        Console.ForegroundColor = oldColor;
    }

    public static void WriteLineColored(string value, ConsoleColor color)
    {
        WriteColored(value + Environment.NewLine, color);
    }

    public static string TrimTo(this string str, int maxLength)
    {
        return str[..Math.Min(maxLength, str.Length)];
    }
}

internal sealed class Void
{
    private Void() { }
}

internal sealed class Invalid
{
    public static Invalid Value = new();

    private Invalid() { }

    [DoesNotReturn]
    public override bool Equals(object? obj) => throw new NotImplementedException();

    [DoesNotReturn]
    public override int GetHashCode() => throw new NotImplementedException();

    [DoesNotReturn]
    public override string? ToString() => throw new NotImplementedException();
}
