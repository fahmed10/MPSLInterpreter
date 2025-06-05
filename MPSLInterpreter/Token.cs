namespace MPSLInterpreter;

public class Token(TokenType Type, string Lexeme, int Line, int Column, int Start, object? Value = null)
{
    public TokenType Type { get; } = Type;
    public string Lexeme { get; } = Lexeme;
    public int Line { get; } = Line;
    public int Column { get; } = Column;
    public int Start { get; } = Start;
    public int End => Start + Lexeme.Length;
    public object? Value { get; } = Value;

    public override string ToString()
    {
        return $"({Type}, {Lexeme})";
    }
}

public enum TokenType
{
    PAREN_LEFT, PAREN_RIGHT, CURLY_LEFT, CURLY_RIGHT,
    SQUARE_LEFT, SQUARE_RIGHT, COMMA, SLASH, ASTERISK,
    PLUS, MINUS, EQUAL, COLON, AMPERSAND, PIPE,
    AT, DOT_DOT, INTERPOLATED_STRING_START, INTERPOLATED_STRING_END,

    EXCLAMATION, EXCLAMATION_EQUAL,
    GREATER, GREATER_EQUAL,
    LESSER, LESSER_EQUAL,
    ARROW, THICK_ARROW,

    IDENTIFIER, STRING, NUMBER, COMMAND, INTERPOLATED_TEXT, COMMENT,

    TRUE, FALSE, IF, ELSE, WHILE, FN, VAR, BREAK, MATCH, TYPE, EACH, NULL, USE,

    EOL, EOF
}
