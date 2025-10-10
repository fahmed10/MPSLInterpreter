using RegExpr = System.Text.RegularExpressions.Regex;

namespace MPSLInterpreter.StdLibrary;

internal static class Regex
{
    public static MPSLEnvironment GetEnvironment()
    {
        MPSLEnvironment environment = new();
        environment.DefineFunction("match", new(2, RegexMatch));
        environment.DefineFunction("matches", new(2, RegexMatches));
        environment.DefineFunction("replace", new(3, RegexReplace));
        return environment;
    }

    private static string RegexMatch(string str, string pattern) => RegExpr.Match(str, pattern).Value;
    private static MPSLArray RegexMatches(string str, string pattern) => new(RegExpr.Matches(str, pattern).Select(m => m.Value));
    private static string RegexReplace(string str, string pattern, string replacement) => RegExpr.Replace(str, pattern, replacement);
}