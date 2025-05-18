namespace MPSLInterpreter;

/// <summary>
/// The result of running MPSL code.
/// </summary>
/// <param name="Success">True if no errors occurred attempting to run the code, otherwise false.</param>
/// <param name="TokenizerErrors">The list of errors reported by the tokenizer while attempting to tokenize the code.</param>
/// <param name="ParserErrors">The list of errors reported by the parser while attempting to parse the code.</param>
public record struct MPSLRunResult(bool Success, IList<TokenizerError> TokenizerErrors, IList<ParserError> ParserErrors);

/// <summary>
/// The result of tokenizing and parsing MPSL code.
/// </summary>
/// <param name="Tokens">The list of tokens read from the MPSL code.</param>
/// <param name="Statements">The list of statements parsed from the MPSL code.</param>
/// <param name="TokenizerErrors">The list of errors reported by the tokenizer while attempting to tokenize the code.</param>
/// <param name="ParserErrors">The list of errors reported by the parser while attempting to parse the code.</param>
public record struct MPSLCheckResult(IList<Token> Tokens, IList<Statement> Statements, IList<TokenizerError> TokenizerErrors, IList<ParserError> ParserErrors)
{
    /// <summary>
    /// True if the code has no errors, otherwise false.
    /// </summary>
    public readonly bool Valid => TokenizerErrors.Count == 0 && ParserErrors.Count == 0;
}