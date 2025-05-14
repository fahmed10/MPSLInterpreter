using static MPSLInterpreter.TokenType;

namespace MPSLInterpreter;

public record class TokenizerError(string Message);

internal static class Tokenizer
{
    private static string code = "";
    private static readonly List<Token> tokens = [];
    private static readonly List<TokenizerError> errors = [];
    private static int start;
    private static int current;
    private static int line;
    private static int lastLine;

    private static string CurrentString => code[start..current];

    private static Dictionary<char, TokenType> operators = new()
    {
        { '+', PLUS },
        { '-', MINUS },
        { '*', ASTERISK },
        { '/', SLASH },
        { '>', GREATER },
        { '<', LESSER },
        { '=', EQUAL },
        { ',', COMMA },
        { ':', COLON },
        { '{', CURLY_LEFT },
        { '}', CURLY_RIGHT },
        { '(', PAREN_LEFT },
        { ')', PAREN_RIGHT },
        { '[', SQUARE_LEFT },
        { ']', SQUARE_RIGHT },
        { '$', DOLLAR },
        { '!', EXCLAMATION },
        { '&', AMPERSAND },
        { '|', PIPE },
    };

    private static Dictionary<string, TokenType> keywords = new()
    {
        { "true", TRUE },
        { "false", FALSE },
        { "if", IF },
        { "else", ELSE },
        { "while", WHILE },
        { "var", VAR },
        { "break", BREAK },
        { "match", MATCH },
        { "fn", FN },
        { "type", TYPE },
        { "each", EACH },
        { "null", NULL },
        { "use", USE }
    };

    private static void Reset()
    {
        tokens.Clear();
        errors.Clear();
        start = current = line = lastLine = 0;
        line++;
    }

    public static IList<Token> GetTokens(string code, out IList<TokenizerError> errors)
    {
        Reset();
        Tokenizer.code = code;

        while (current < code.Length)
        {
            ReadToken();
            start = current;
        }

        tokens.Add(new Token(EOF, "", line, current - lastLine));
        errors = Tokenizer.errors;

        return tokens;
    }

    private static void ReadToken()
    {
        char c = code[current];
        if (c is '@')
        {
            current++;

            if (code[current] == '"')
            {
                ReadInterpolated('"', '{', '}', INTERPOLATED_STRING_MARKER, INTERPOLATED_STRING_MARKER);
            }
            else if (!char.IsAsciiLetterOrDigit(code[current]))
            {
                AddToken(AT);
            }
            else
            {
                AdvanceWhile(c => char.IsAsciiLetterOrDigit(c) || c is '_');
                AddToken(COMMAND, CurrentString[1..]);
            }
        }
        else if (c is '"')
        {
            current++;
            AdvanceWhile(c => c is not '"');
            current++;

            if (current > code.Length)
            {
                ReportError("Unterminated string literal.");
                current = code.Length;
            }

            AddToken(STRING, CurrentString[1..^1]);
        }
        else if (c is '<' && !IsLastToken(IDENTIFIER, NUMBER, STRING, PAREN_RIGHT, SQUARE_RIGHT, AT))
        {
            ReadInterpolated('>', '{', '}', ANGLE_LEFT, ANGLE_RIGHT);
        }
        else if (char.IsAsciiLetter(c) || c is '_')
        {
            AdvanceWhile(c => char.IsAsciiLetterOrDigit(c) || c is '_');
            AddToken(keywords.ContainsKey(CurrentString.ToLower()) ? keywords[CurrentString.ToLower()] : IDENTIFIER);
        }
        else if (c is '.' && NextChar() is '.')
        {
            current += 2;
            AddToken(DOT_DOT);
        }
        else if (c is '.' || char.IsAsciiDigit(c))
        {
            current++;
            AdvanceWhile(c => c is '.' || char.IsAsciiDigit(c));
            if (double.TryParse(CurrentString, out double value))
            {
                AddToken(NUMBER, value);
            }
            else
            {
                ReportError($"Invalid number '{CurrentString}'.");
            }
        }
        else if (c is '-' && NextChar() is '>')
        {
            current += 2;
            AddToken(ARROW);
        }
        else if (c is '=' && NextChar() is '>')
        {
            current += 2;
            AddToken(THICK_ARROW);
        }
        else if (c is '!' && NextChar() is '=')
        {
            current += 2;
            AddToken(EXCLAMATION_EQUAL);
        }
        else if (c is '>' && NextChar() is '=')
        {
            current += 2;
            AddToken(GREATER_EQUAL);
        }
        else if (c is '<' && NextChar() is '=')
        {
            current += 2;
            AddToken(LESSER_EQUAL);
        }
        else if (operators.TryGetValue(c, out TokenType type))
        {
            current++;
            AddToken(type);
        }
        else if (c is '#')
        {
            if (NextChar() is '#')
            {
                current++;
                AdvanceWhile(c => !(c is '#' && NextChar() is '#'));
                current += 2;

                if (current > code.Length)
                {
                    ReportError("Expected '##', got <EOF>.");
                }
            }
            else
            {
                AdvanceWhile(c => c is not '\n');
            }
        }
        else if (c is '\n')
        {
            if (tokens.Count > 0 && !IsLastToken(EOL, CURLY_LEFT, PAREN_LEFT, SQUARE_LEFT, COMMA))
            {
                AddToken(EOL);
            }

            line++;
            lastLine = current;
            current++;
        }
        else if (c is ' ' or '\r' or '\t')
        {
            current++;
        }
        else
        {
            ReportError("Unknown character.");
            current++;
        }
    }

    private static void ReadInterpolated(char endChar, char interpolateStart, char interpolateEnd, TokenType startType, TokenType endType)
    {
        current++;
        AddToken(startType);
        start = current;

        AdvanceWhile((c, next, i) =>
        {
            if (c == interpolateStart && next == interpolateStart)
            {
                current++;
            }
            else if (c == interpolateStart && next != interpolateStart)
            {
                if (i != 0)
                {
                    AddToken(INTERPOLATED_TEXT, CurrentString.Replace(interpolateStart.ToString() + interpolateStart, interpolateStart.ToString()).Replace(interpolateEnd.ToString() + interpolateEnd, interpolateEnd.ToString()));
                    start = current;
                }
                current++;
                start++;
                while (code[current] != interpolateEnd)
                {
                    ReadToken();
                    start = current;
                }

                start++;

                if (code[current] == interpolateStart)
                {
                    AddToken(INTERPOLATED_TEXT, "");
                }
            }

            if (code[current] == endChar)
            {
                if (start != current)
                {
                    AddToken(INTERPOLATED_TEXT, CurrentString.Replace("{{", "{").Replace("}}", "}"));
                    start = current;
                }
                return false;
            }

            return true;
        });

        current++;
        AddToken(endType);
    }

    private static bool IsLastToken(params TokenType[] types)
    {
        if (tokens.Count == 0)
        {
            return false;
        }

        return types.Any(t => t == tokens.Last().Type);
    }

    private static void AdvanceWhile(Func<char, bool> func)
    {
        while (current < code.Length && func.Invoke(code[current]))
        {
            if (code[current] is '\n')
            {
                line++;
                lastLine = current;
            }
            current++;
        }
    }

    private static void AdvanceWhile(Func<char, char, int, bool> func)
    {
        int index = 0;
        while (current < code.Length && func.Invoke(code[current], current + 1 >= code.Length ? '\0' : code[current + 1], index))
        {
            if (code[current] is '\n')
            {
                line++;
                lastLine = current;
            }
            current++;
            index++;
        }
    }

    private static void ReportError(string message)
    {
        errors.Add(new TokenizerError($"[L{line}, C{current - lastLine}] {message}"));
    }

    private static void AddToken(TokenType type, object? value = null)
    {
        tokens.Add(new Token(type, code[start..current], line, current - lastLine, value));
    }

    private static char NextChar()
    {
        return current + 1 < code.Length ? code[current + 1] : '\0';
    }
}
