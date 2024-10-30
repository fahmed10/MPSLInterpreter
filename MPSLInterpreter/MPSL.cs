namespace MPSLInterpreter;

public static class MPSL
{
    private static void Main(string[] args)
    {
        if (args.Length == 1)
        {
            RunFile(args[0], new());
        }
        else
        {
            Console.WriteLine("Usage: mpsl <file>");
        }
    }

    /// <summary>
    /// Runs the MPSL code in the file at the given path. Also sets the location of the file as the current directory.
    /// </summary>
    /// <param name="path">The path to the file to run.</param>
    /// <param name="environment">The global environment to use while running the code.</param>
    public static void RunFile(string path, MPSLEnvironment environment)
    {
        string file = File.ReadAllText(path);
        Directory.SetCurrentDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Run(file, environment);
    }

    /// <summary>
    /// Runs the given code.
    /// </summary>
    /// <param name="source">The MPSL code to run.</param>
    /// <param name="environment">The global environment to use while running the code.</param>
    public static void Run(string source, MPSLEnvironment environment)
    {
        IList<Token> tokens = Tokenizer.GetTokens(source, out IList<TokenizerError> tokenizerErrors);

        foreach (TokenizerError error in tokenizerErrors)
        {
            Utils.WriteLineColored(error.Message, ConsoleColor.Red);
        }

        IList<Statement> statements = Parser.Parse(tokens, out IList<ParserError> parserErrors);

        foreach (ParserError error in parserErrors)
        {
            Utils.WriteLineColored(error.Message, ConsoleColor.Red);
        }

        if (tokenizerErrors.Count > 0 || parserErrors.Count > 0)
        {
            return;
        }

        Interpreter interpreter = new(environment);
        interpreter.Interpret(statements);
        return;
    }
}
