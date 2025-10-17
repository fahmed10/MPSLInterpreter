using static MPSLInterpreter.TokenType;

namespace MPSLInterpreter;

public record class TokenizerError(int Line, int Column, string Message);

internal static class Tokenizer
{
    private static string code = "";
    private static List<Token> tokens = [];
    private static List<TokenizerError> errors = [];
    private static int start;
    private static int current;
    private static int line;
    private static int lastLine;

    private static string CurrentString => code[start..current];

    private static readonly Dictionary<char, TokenType> operators = new()
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
        { '!', EXCLAMATION },
        { '&', AMPERSAND },
        { '|', PIPE },
    };

    private static readonly Dictionary<string, TokenType> compoundOperators = new()
    {
        { "..", DOT_DOT },
        { "->", ARROW },
        { "=>", THICK_ARROW },
        { "!=", EXCLAMATION_EQUAL },
        { ">=", GREATER_EQUAL },
        { "<=", LESSER_EQUAL },
        { "::", COLON_COLON },
    };

    internal static readonly Dictionary<string, TokenType> keywords = new()
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
        { "each", EACH },
        { "null", NULL },
        { "use", USE },
        { "group", GROUP },
        { "public", PUBLIC },
    };

    private static readonly Dictionary<char, char> escapeSequences = new()
    {
        { 'n', '\n' },
        { 'r', '\r' },
        { 't', '\t' },
        { '"', '"' },
        { '\\', '\\' }
    };

    private static void Reset()
    {
        tokens = [];
        errors = [];
        start = current = line = lastLine = 0;
        line++;
    }

    public static IList<Token> GetTokens(string code, out IList<TokenizerError> errors)
    {
        Reset();
        code = code.ReplaceLineEndings("\n");
        Tokenizer.code = code;

        while (current < code.Length)
        {
            ReadToken();
            start = current;
        }

        tokens.Add(new(EOF, "", line, current - lastLine, start));
        errors = Tokenizer.errors;

        return tokens;
    }

    private static void ReadToken()
    {
        char c = code[current];
        if (c is '@')
        {
            current++;

            if (current >= code.Length)
            {
                AddToken(AT);
            }
            else if (code[current] == '"')
            {
                ReadInterpolated();
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
            ReadString();
        }
        else if (compoundOperators.TryGetValue($"{c}{NextChar()}", out TokenType compoundType))
        {
            current += 2;
            AddToken(compoundType);
        }
        else if (char.IsAsciiLetter(c) || c is '_')
        {
            AdvanceWhile(c => char.IsAsciiLetterOrDigit(c) || c is '_');
            if (keywords.TryGetValue(CurrentString, out TokenType tokenType))
            {
                AddToken(tokenType);
            }
            else
            {
                AddToken(IDENTIFIER);
            }
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
        else if (operators.TryGetValue(c, out TokenType type))
        {
            current++;

            if (type is PAREN_RIGHT or SQUARE_RIGHT && IsLastToken(EOL))
            {
                tokens.RemoveAt(tokens.Count - 1);
            }

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
                    current = code.Length;
                }

                AddToken(COMMENT);
            }
            else
            {
                AdvanceWhile(c => c is not '\n');
                AddToken(COMMENT);
            }
        }
        else if (c is '\n')
        {
            if (tokens.Count > 0 && !IsLastToken(EOL, CURLY_LEFT, PAREN_LEFT, SQUARE_LEFT, COMMENT))
            {
                AddToken(EOL);
            }

            line++;
            current++;
            lastLine = current;
        }
        else if (c is ' ' or '\r' or '\t')
        {
            current++;
        }
        else
        {
            current++;
            ReportError($"Unexpected character '{c}'.");
        }
    }

    private static void ReadString()
    {
        current++;
        AdvanceWhile((c, next, i) =>
        {
            if (c is '\\')
            {
                if (escapeSequences.ContainsKey(next))
                {
                    current++;
                }
                else
                {
                    ReportError($"Invalid escape sequence '\\{next}'.");
                }
            }

            return c is not '"';
        });
        current++;

        if (current > code.Length)
        {
            ReportError("Unterminated string literal.");
            current = code.Length;
            AddToken(STRING, ReplaceEscapeSequences(CurrentString[1..]));
        }
        else
        {
            AddToken(STRING, ReplaceEscapeSequences(CurrentString[1..^1]));
        }
    }

    private static void ReadInterpolated()
    {
        current++;
        AddToken(INTERPOLATED_STRING_START);
        start = current;

        AdvanceWhile((c, next, i) =>
        {
            if (c is '\\')
            {
                if (escapeSequences.ContainsKey(next))
                {
                    current++;
                }
                else
                {
                    ReportError($"Invalid escape sequence '\\{next}'.");
                }

                return true;
            }
            else if ((c is '{' && next is '{') || (c is '}' && next is '}'))
            {
                current++;
            }
            else if (c is '}')
            {
                current++;
                ReportError("Encountered '}' with no opening '{' in interpolated string.");
            }
            else if (c == '{')
            {
                if (i != 0)
                {
                    AddToken(INTERPOLATED_TEXT, ReplaceEscapeSequences(CurrentString.Replace("{{", "{").Replace("}}", "}")));
                    start = current;
                }
                current++;
                start++;
                while (current < code.Length && code[current] is not '}')
                {
                    ReadToken();
                    start = current;
                }

                if (current >= code.Length)
                {
                    return false;
                }

                start++;

                if (code[current] is '{')
                {
                    AddToken(INTERPOLATED_TEXT, "");
                }
            }

            if (code[current] is '"')
            {
                if (start != current)
                {
                    AddToken(INTERPOLATED_TEXT, ReplaceEscapeSequences(CurrentString.Replace("{{", "{").Replace("}}", "}")));
                    start = current;
                }
                return false;
            }

            return true;
        });

        if (current < code.Length)
        {
            current++;
        }

        AddToken(INTERPOLATED_STRING_END);
    }

    private static string ReplaceEscapeSequences(string str)
    {
        foreach (KeyValuePair<char, char> pair in escapeSequences)
        {
            str = str.Replace($"\\{pair.Key}", pair.Value.ToString());
        }

        return str;
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
        errors.Add(new TokenizerError(line, current - lastLine, message));
    }

    private static void AddToken(TokenType type, object? value = null)
    {
        tokens.Add(new Token(type, code[start..current], line, current - lastLine, start, value));
    }

    private static char NextChar()
    {
        return current + 1 < code.Length ? code[current + 1] : '\0';
    }
}
