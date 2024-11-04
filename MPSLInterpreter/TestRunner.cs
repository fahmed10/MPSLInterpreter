namespace MPSLInterpreter;

internal static class TestRunner
{
    private static readonly string[] NEWLINE_STRINGS = ["\r\n", "\r", "\n"];

    public static void RunTests(string testsDirectory)
    {
        string[] filePaths = Directory.GetFiles(testsDirectory, "*.mpsl", SearchOption.AllDirectories);

        Console.WriteLine("Test Output:");

        foreach (string file in filePaths)
        {
            TestCode(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
        }
    }

    private static void TestCode(string testName, string code)
    {
        void WriteTestFail(string message)
        {
            Utils.WriteLineColored($"[{testName.ToUpper()}: FAIL] {message}", ConsoleColor.Red);
        }

        void WriteTestSuccess()
        {
            Utils.WriteLineColored($"[{testName.ToUpper()}: SUCCESS]", ConsoleColor.Green);
        }

        TextWriter standardOut = Console.Out;
        using StringWriter stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        bool success = MPSL.Run(code, new());
        Console.SetOut(standardOut);
        string[] outputLines = stringWriter.ToString().Split(NEWLINE_STRINGS, StringSplitOptions.None);

        string[] lines = code
            .Split(NEWLINE_STRINGS, StringSplitOptions.None)
            .SkipWhile(l => !l.StartsWith("# @EXPECT"))
            .Select(l => l[2..]) // Strip comment marker and space from each line
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
                WriteTestFail("Expected code to RUN, but code errored.");
                return;
            }

            if (outputLines.SequenceEqual(lines))
            {
                WriteTestSuccess();
            }
            else
            {
                WriteTestFail("Output did not match expected output.");
                PrintDiffs(lines, outputLines);
            }
        }
        else if (lines[0].EndsWith("ERROR"))
        {
            lines = lines[1..];

            if (success)
            {
                WriteTestFail("Expected code to ERROR, but code ran.");
                return;
            }

            if (outputLines.SequenceEqual(lines))
            {
                WriteTestSuccess();
            }
            else
            {
                WriteTestFail("Output did not match expected output.");
                PrintDiffs(lines, outputLines);
            }
        }
        else
        {
            throw new InvalidDataException($"Expected line starting with '# @EXPECT RUN' or '# @EXPECT ERROR', but got '{lines[0]}'");
        }
    }

    private static void PrintDiffs(string[] expectedLines, string[] outputLines)
    {
        int maxLength = Math.Max(expectedLines.Length, outputLines.Length);

        for (int i = 0; i < maxLength; i++)
        {
            string? expected = i < expectedLines.Length ? expectedLines[i] : null;
            string? output = i < outputLines.Length ? outputLines[i] : null;

            if (expected == null)
            {
                Utils.WriteLineColored($">>>> [Line {i + 1}] Unexpected output: '{output}'", ConsoleColor.Red);
            }
            else if (output == null)
            {
                Utils.WriteLineColored($">>>> [Line {i + 1}] Expected output: '{expected}'", ConsoleColor.Red);
            }
            else if (expected != output)
            {
                Utils.WriteColored($">>>> [Line {i + 1}] '{output}' != ", ConsoleColor.Red);
                Utils.WriteLineColored($"'{expected}'", ConsoleColor.Green);
            }
        }
    }
}
