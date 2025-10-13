using System.Collections.Frozen;

namespace MPSLInterpreter;

/// <summary>
/// The main class for handling and running MPSL programs.
/// </summary>
public static class MPSL
{
    /// <summary>
    /// A dictionary of MPSL keywords to their token type.
    /// </summary>
    public static FrozenDictionary<string, TokenType> Keywords { get; } = Tokenizer.keywords.ToFrozenDictionary();

    private static void Main(string[] args)
    {
        if (args.Length == 1)
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("""
                MPSL Interpreter Help
                
                Arguments:
                --help: Displays this text.
                --license: Displays license information.
                """);
            }
            else if (args[0] == "--license")
            {
                Console.WriteLine("""
                This software is licensed under the MIT license:

                    Copyright (c) 2025 fahmed10

                    Permission is hereby granted, free of charge, to any person obtaining a copy
                    of this software and associated documentation files (the "Software"), to deal
                    in the Software without restriction, including without limitation the rights
                    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                    copies of the Software, and to permit persons to whom the Software is
                    furnished to do so, subject to the following conditions:

                    The above copyright notice and this permission notice shall be included in all
                    copies or substantial portions of the Software.

                    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                    SOFTWARE.
                """);
            }
            else
            {
                RunFile(args[0], new());
            }
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
        IList<Statement> statements = Parser.Parse(tokens.Where(t => t.Type != TokenType.COMMENT).ToList(), out IList<ParserError> parserErrors);

        return new(tokens, statements, tokenizerErrors, parserErrors);
    }
}
