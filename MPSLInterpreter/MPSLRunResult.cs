namespace MPSLInterpreter;

/// <summary>
/// The result of running MPSL code.
/// </summary>
/// <param name="Success">True if no errors occurred attempting to run the code, otherwise false.</param>
/// <param name="TokenizerErrors">The list of errors reported by the tokenizer while attempting to run the code.</param>
/// <param name="ParserErrors">The list of errors reported by the parser while attempting to run the code.</param>
public record struct MPSLRunResult(bool Success, IList<TokenizerError> TokenizerErrors, IList<ParserError> ParserErrors);