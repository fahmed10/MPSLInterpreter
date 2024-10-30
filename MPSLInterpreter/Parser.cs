using static MPSLInterpreter.TokenType;

namespace MPSLInterpreter;

internal record class ParserError(string Message);
internal class ParseException : Exception;

internal static class Parser
{
    private static IList<Token> tokens = null!;
    private static List<ParserError> errors = new();
    private static int current;

    private static TokenType[][] binaryOperators =
    {
        [PIPE], // Or
        [AMPERSAND], // And
        [EQUAL, EXCLAMATION_EQUAL], // Equality
        [LESSER, GREATER, LESSER_EQUAL, GREATER_EQUAL], // Comparison
        [PLUS, MINUS], // Sum
        [ASTERISK, SLASH], // Factor
    };

    public static IList<Statement> Parse(IList<Token> tokens, out IList<ParserError> errors)
    {
        Parser.tokens = tokens;
        Parser.errors.Clear();
        current = 0;

        List<Statement> statements = new();

        while (!IsNextToken(EOF))
        {
            try
            {
                statements.Add(StatementRule());
            }
            catch (ParseException e)
            {
#if DEBUG
                if (e.StackTrace != null)
                {
                    Utils.WriteLineColored($"Parser Error: {e.StackTrace.TrimTo(2000)}\n", ConsoleColor.DarkGray);
                }
#endif

                Synchronize();
            }

            while (MatchNextToken(EOL)) { }

            if (current >= tokens.Count)
            {
                break;
            }
        }

        errors = Parser.errors;

        return statements;
    }

    private static Statement StatementRule()
    {
        while (MatchNextToken(EOL)) { }

        if (IsNextToken(VAR)) return new Statement.ExpressionStatement(VariableRule());
        if (MatchNextToken(IF)) return IfRule();
        if (MatchNextToken(WHILE)) return WhileRule();
        if (MatchNextToken(EACH)) return EachRule();
        if (IsNextToken(CURLY_LEFT)) return new Statement.ExpressionStatement(BlockRule());
        if (MatchNextToken(BREAK)) return BreakRule();
        if (MatchNextToken(FN)) return FunctionRule();
        if (MatchNextToken(USE)) return UseRule();

        return ExpressionStatementRule();
    }

    private static Statement UseRule()
    {
        Token path = RequireMatchNext(STRING, "Expected path to file to use as string.");
        return new Statement.Use(path);
    }

    private static Statement IfRule()
    {
        List<(Expression condition, Expression.Block body)> statements = new();
        Expression.Block? elseBlock = null;

        statements.Add((ExpressionRule(), StatementOrBlockRule()));

        while (MatchNextToken(ELSE))
        {
            if (MatchNextToken(IF))
            {
                statements.Add((ExpressionRule(), StatementOrBlockRule()));
            }
            else
            {
                elseBlock = StatementOrBlockRule();
                break;
            }
        }

        return new Statement.If(statements, elseBlock);
    }

    private static Statement EachRule()
    {
        Token variableName = RequireMatchNext(IDENTIFIER, "Expected identifier.");
        RequireMatchNext(COLON, "Expected ':'.");
        return new Statement.Each(variableName, ExpressionRule(), StatementOrBlockRule());
    }

    private static Statement WhileRule()
    {
        return new Statement.While(ExpressionRule(), StatementOrBlockRule());
    }

    private static Statement BreakRule()
    {
        Token keyword = PreviousToken();
        RequireMatchNext([EOL, EOF], "Expected <EOL>");
        return new Statement.Break(keyword);
    }

    private static Expression.Block StatementOrBlockRule()
    {
        MatchNextToken(EOL);

        Expression.Block block;
        if (PeekToken().Type == CURLY_LEFT)
        {
            block = BlockRule();
        }
        else
        {
            RequireMatchNext(THICK_ARROW, "Expected '=>' or '{'.");
            block = new Expression.Block([StatementRule()]);
        }

        while (MatchNextToken(EOL)) { }
        return block;
    }

    private static Expression.Block BlockRule()
    {
        RequireMatchNext(CURLY_LEFT, "Expected '{'.");

        List<Statement> statements = new();

        while (!MatchNextToken(CURLY_RIGHT))
        {
            while (MatchNextToken(EOL)) { }
            statements.Add(StatementRule());
            while (MatchNextToken(EOL)) { }
        }

        return new Expression.Block(statements);
    }

    private static Statement ExpressionStatementRule()
    {
        Expression expression = ExpressionRule();
        RequireMatchNext([EOL, EOF], "Expected <EOL>.");

        return new Statement.ExpressionStatement(expression);
    }

    private static Expression ExpressionRule() => AssignmentRule();

    private static Expression NonAssignExpressionRule(int precedence = 0)
    {
        if (precedence >= binaryOperators.Length - 1)
        {
            return BinaryRule(UnaryRule, binaryOperators[precedence]);
        }

        return BinaryRule(() => NonAssignExpressionRule(precedence + 1), binaryOperators[precedence]);
    }

    private static Expression AssignmentRule()
    {
        Expression expression = NonAssignExpressionRule();
        if (MatchNextToken(ARROW))
        {
            if (MatchNextToken(BREAK))
            {
                return new Expression.Block([new Statement.ExpressionStatement(expression), new Statement.Break(PreviousToken())]);
            }
            else if (MatchNextToken(SQUARE_LEFT))
            {
                Token name = RequireMatchNext(IDENTIFIER, "Expected variable name.");
                RequireMatchNext(SQUARE_RIGHT, "Expected ']'.");
                return new Expression.Push(name, expression);
            }
            else
            {
                return new Expression.Assign(VariableRule(), expression);
            }
        }
        return expression;
    }

    private static Expression VariableRule()
    {
        if (MatchNextToken(VAR))
        {
            Token name = RequireMatchNext(IDENTIFIER, "Expected variable name.");
            return new Expression.VariableDeclaration(name);
        }
        else
        {
            if (!IsNextToken(IDENTIFIER))
            {
                ReportError(ReadToken(), "Expected variable name.");
            }

            return AccessRule();
        }
    }

    private static Expression BinaryRule(Func<Expression> leftRule, params TokenType[] types)
    {
        Expression expression = leftRule.Invoke();

        while (MatchNextToken(types))
        {
            Token @operator = PreviousToken();
            Expression right = leftRule.Invoke();
            expression = new Expression.Binary(@operator, expression, right);
        }

        return expression;
    }

    private static Expression UnaryRule()
    {
        if (MatchNextToken(EXCLAMATION, MINUS, DOLLAR))
        {
            Token operatorToken = PreviousToken();
            Expression right = UnaryRule();
            return new Expression.Unary(operatorToken, right);
        }

        return AccessRule();
    }

    private static Expression AccessRule()
    {
        Expression expression = PrimaryRule();

        while (MatchNextToken(SQUARE_LEFT))
        {
            Token start = PreviousToken();
            Expression indexExpression = ExpressionRule();
            RequireMatchNext(SQUARE_RIGHT, "Expected ']'.");
            expression = new Expression.Access(expression, indexExpression, start);
        }

        return expression;
    }

    private static Expression PrimaryRule()
    {
        return ReadToken().Type switch
        {
            FALSE => new Expression.Literal(false),
            TRUE => new Expression.Literal(true),
            NULL => new Expression.Literal(null),
            NUMBER or STRING => new Expression.Literal(PreviousToken().Value),
            IDENTIFIER => new Expression.Variable(PreviousToken()),
            AT => new Expression.ContextValue(),
            SQUARE_LEFT => ArrayLiteralRule(),
            COMMAND => CallRule(),
            MATCH => MatchRule(),
            PAREN_LEFT => GroupingRule(),
            INTERPOLATED_STRING_MARKER => InterpolatedStringRule(),
            EOF => throw ReportError(PreviousToken(), "Expected expression."),
            _ => throw ReportError(PeekToken(), "Expected expression.")
        };
    }

    private static Expression InterpolatedStringRule()
    {
        List<Expression> expressions = new();

        while (!MatchNextToken(INTERPOLATED_STRING_MARKER))
        {
            if (MatchNextToken(INTERPOLATED_TEXT))
            {
                expressions.Add(new Expression.Literal(PreviousToken().Value));
            }
            else
            {
                expressions.Add(ExpressionRule());
            }
        }

        return new Expression.InterpolatedString(expressions);
    }

    private static Expression GroupingRule()
    {
        Expression expression = NonAssignExpressionRule();
        RequireMatchNext(PAREN_RIGHT, "Expected ')'.");
        return new Expression.Grouping(expression);
    }

    private static Expression ArrayLiteralRule()
    {
        Token start = PreviousToken();

        if (MatchNextToken(SQUARE_RIGHT))
        {
            return new Expression.Array(start, []);
        }

        List<(Expression, bool)> items = new();

        do
        {
            if (MatchNextToken(DOT_DOT))
            {
                items.Add((ExpressionRule(), true));
            }
            else
            {
                items.Add((ExpressionRule(), false));
            }
        }
        while (MatchNextToken(COMMA));

        RequireMatchNext(SQUARE_RIGHT, "Expected ']'.");

        return new Expression.Array(start, items);
    }

    private static Expression MatchRule()
    {
        Expression value = ExpressionRule();
        RequireMatchNext(CURLY_LEFT, "Expected '{'.");

        List<(Expression condition, Expression.Block body)> statements = new();
        Expression.Block? elseStatement = null;

        while (!MatchNextToken(CURLY_RIGHT, ELSE))
        {
            statements.Add((ExpressionRule(), StatementOrBlockRule()));
        }

        if (PreviousToken().Type == ELSE)
        {
            elseStatement = StatementOrBlockRule();
            RequireMatchNext(CURLY_RIGHT, "Expected '}'. An else match must be the last match statement in a match expression.");
        }

        return new Expression.Match(value, statements, elseStatement);
    }

    private static Statement FunctionRule()
    {
        Token name = RequireMatchNext(COMMAND, "Function names must start with an '@' character.");
        List<Token> parameters = new();

        if (PeekToken().Type is not CURLY_LEFT and not THICK_ARROW)
        {
            do
            {
                parameters.Add(RequireMatchNext(IDENTIFIER, "Expected parameter name."));
            }
            while (MatchNextToken(COMMA));
        }

        return new Statement.FunctionDeclaration(name, parameters, StatementOrBlockRule());
    }

    private static Expression CallRule()
    {
        Token command = PreviousToken();
        List<Expression> args = new List<Expression>();
        string functionName = (string)command.Value!;

        while (!MatchNextToken(EXCLAMATION) && !IsNextToken(CURLY_LEFT, EOL))
        {
            args.Add(NonAssignExpressionRule());
            if (!MatchNextToken(COMMA))
            {
                MatchNextToken(EXCLAMATION);
                break;
            }
        }

        return new Expression.Call(command, args);
    }

    private static void Synchronize()
    {
        if (current >= tokens.Count)
        {
            return;
        }

        if (PeekToken().Type != EOF)
        {
            ReadToken();
        }

        while (PeekToken().Type != EOF)
        {
            if (PreviousToken().Type == EOL)
            {
                return;
            }

            if (PeekToken().Type is FN or VAR or IF or WHILE or BREAK)
            {
                return;
            }

            ReadToken();
        }
    }

    private static Token ReadToken()
    {
        Token token = tokens[current];
        current++;
        return token;
    }

    private static Token RequireMatchNext(TokenType type, string errorMessage) => RequireMatchNext([type], errorMessage);

    private static Token RequireMatchNext(TokenType[] types, string errorMessage)
    {
        if (!MatchNextToken(types))
        {
            ReportError(ReadToken(), errorMessage);
        }

        return PreviousToken();
    }

    private static Token PeekToken()
    {
        return tokens[current];
    }

    private static bool IsNextToken(params TokenType[] types)
    {
        if (current >= tokens.Count)
        {
            return false;
        }

        return types.Any(type => type == PeekToken().Type);
    }

    private static bool MatchNextToken(params TokenType[] types)
    {
        if (IsNextToken(types))
        {
            ReadToken();
            return true;
        }

        return false;
    }

    private static Token PreviousToken()
    {
        return tokens[current - 1];
    }

    private static Exception ReportError(Token token, string message)
    {
        errors.Add(new ParserError($"[L{token.Line}, C{token.Column}] {message}"));
        return new ParseException();
    }
}
