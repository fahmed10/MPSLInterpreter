namespace MPSLInterpreter;

/// <summary>
/// The main class for handling and running MPSL programs.
/// </summary>
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
    /// Runs the MPSL code in the file at the given path, setting the directory of the file as the current working directory.
    /// </summary>
    /// <param name="path">The path to the file to run.</param>
    /// <param name="environment">The global environment to use while running the code.</param>
    /// <returns>The result of running the code.</returns>
    public static MPSLRunResult RunFile(string path, MPSLEnvironment environment)
    {
        string? file = null;

        try
        {
            file = File.ReadAllText(path);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        }
        catch
        {
            Utils.WriteLineColored($"An error occurred while trying to read the path '{path}'.", ConsoleColor.Red);
        }

        if (file != null)
        {
            return Run(file, environment);
        }

        return new(false, [], []);
    }

    /// <summary>
    /// Runs the given code.
    /// </summary>
    /// <param name="source">The MPSL code to run.</param>
    /// <param name="environment">The global environment to use while running the code.</param>
    /// <returns>The result of running the code.</returns>
    public static MPSLRunResult Run(string source, MPSLEnvironment environment)
    {
        MPSLCheckResult result = Check(source);

        foreach (TokenizerError error in result.TokenizerErrors)
        {
            Utils.WriteLineColored($"[L{error.Line}, C{error.Column}] {error.Message}", ConsoleColor.Red);
        }

        foreach (ParserError error in result.ParserErrors)
        {
            Utils.WriteLineColored($"[L{error.Token.Line}, C{error.Token.Column}] {error.Message}", ConsoleColor.Red);
        }

        if (!result.Valid)
        {
            return new(false, result.TokenizerErrors, result.ParserErrors);
        }

        Interpreter interpreter = new(environment);
        bool success = interpreter.Interpret(result.Statements);
        return new(success, [], []);
    }

    /// <summary>
    /// Checks the given code, tokenizing and parsing it but not running it.
    /// </summary>
    /// <param name="source">The MPSL code to check.</param>
    /// <returns>The result of checking the code.</returns>
    public static MPSLCheckResult Check(string source)
    {
        IList<Token> tokens = Tokenizer.GetTokens(source, out IList<TokenizerError> tokenizerErrors);
        IList<Statement> statements = Parser.Parse(tokens, out IList<ParserError> parserErrors);

        return new(tokens, statements, tokenizerErrors, parserErrors);
    }
}
