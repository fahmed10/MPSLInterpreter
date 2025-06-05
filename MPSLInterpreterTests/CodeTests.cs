using System.Text.RegularExpressions;
using MPSLInterpreter;

namespace MPSLInterpreterTests;

public partial class CodeTests
{
    private static readonly string[] NEWLINE_STRINGS = ["\r\n", "\r", "\n"];

    private static string[] RunTests(string testsDirectory)
    {
        string[] filePaths = Directory.GetFiles(testsDirectory, "*.mpsl", SearchOption.AllDirectories);
        return filePaths;
    }

    [TestCaseSource(nameof(RunTests), new object[] { "tests" })]
    public void TestCode(string filePath)
    {
        TestCodeFile(filePath, true);
        TestCodeFile(filePath, false);
    }

    private static void TestCodeFile(string filePath, bool trimCode)
    {
        string code = File.ReadAllText(filePath);
        TextWriter standardOut = Console.Out;
        using StringWriter stringWriter = new();
        Console.SetOut(stringWriter);
        bool success = MPSL.Run(trimCode ? StripTestComments().Replace(code, "").TrimEnd() : code, new()).Success;
        Console.SetOut(standardOut);
        string[] outputLines = stringWriter.ToString().Split(NEWLINE_STRINGS, StringSplitOptions.None);

        string[] lines = code
            .Split(NEWLINE_STRINGS, StringSplitOptions.None)
            .SkipWhile(l => !l.StartsWith("# @EXPECT"))
            .Select(l => l.Length >= 2 ? l[2..] : l[1..]) // Strip comment marker and space from each line
            .ToArray();

        if (lines.Length == 0)
        {
            throw new InvalidDataException("Invalid format for test file. Expected line starting with '# @EXPECT RUN' or '# @EXPECT ERROR'");
        }

        outputLines = outputLines[..^1];

        if (lines[0].EndsWith("RUN"))
        {
            lines = lines[1..];

            if (!success)
            {
                Assert.Fail("Expected code to RUN, but code errored:\n" + string.Join('\n', outputLines));
                return;
            }

            Assert.That(outputLines, Is.EquivalentTo(lines));
        }
        else if (lines[0].EndsWith("ERROR"))
        {
            lines = lines[1..];

            if (success)
            {
                Assert.Fail("Expected code to ERROR, but code ran.");
                return;
            }
            if (!trimCode)
            {
                outputLines = [.. outputLines.Select(line => StripErrorLocation().Replace(line, ""))];
                lines = [.. lines.Select(line => StripErrorLocation().Replace(line, ""))];
            }

            Assert.That(outputLines, Is.EquivalentTo(lines));
        }
        else
        {
            throw new InvalidDataException($"Expected line starting with '# @EXPECT RUN' or '# @EXPECT ERROR', but got '{lines[0]}'");
        }
    }

    [GeneratedRegex(@"# @[A-Z]+(?:.|\n)+")]
    private static partial Regex StripTestComments();

    [GeneratedRegex(@"\[L\d+, C\d+\]")]
    private static partial Regex StripErrorLocation();
}
