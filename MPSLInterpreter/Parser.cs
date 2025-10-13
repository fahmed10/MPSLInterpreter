using static MPSLInterpreter.TokenType;

namespace MPSLInterpreter;

public record class ParserError(Token Token, string Message);
internal class ParseException : Exception;

internal static class Parser
{
    private static IList<Token> tokens = null!;
    private static List<ParserError> errors = [];
    private static int current;

    private static readonly TokenType[][] binaryOperators = [
        [PIPE], // Or
        [AMPERSAND], // And
        [EQUAL, EXCLAMATION_EQUAL], // Equality
        [LESSER, GREATER, LESSER_EQUAL, GREATER_EQUAL], // Comparison
        [PLUS, MINUS], // Sum
        [ASTERISK, SLASH], // Factor
    ];

    private static readonly TokenType[] literalTokens = [NUMBER, STRING, TRUE, FALSE, NULL];

    public static IList<Statement> Parse(IList<Token> tokens, out IList<ParserError> errors)
    {
        Parser.tokens = tokens;
        Parser.errors = [];
        current = 0;

        List<Statement> statements = [];

        while (!IsNextToken(EOF))
        {
            SynchronizationBoundary(() => statements.Add(DeclarationRule(false)));

            if (statements.LastOrDefault() is Statement.ExpressionStatement expressionStatement)
            {
                RequireExpressionStatement(expressionStatement.expression);
            }

            if (current >= tokens.Count)
            {
                break;
            }
        }

        errors = Parser.errors;

        return statements;
    }

    private static Statement DeclarationRule(bool required)
    {
        if (MatchNextToken(PUBLIC))
        {
            return new Statement.Public(PreviousToken(), DeclarationStatementRule(true));
        }

        return DeclarationStatementRule(required);
    }

    private static Statement DeclarationStatementRule(bool required)
    {
        if (IsNextToken(VAR)) return new Statement.ExpressionStatement(VariableRule());
        if (MatchNextToken(FN)) return FunctionRule();
        if (MatchNextToken(GROUP)) return GroupRule();

        if (required)
        {
            Statement.ExpressionStatement statement = ExpressionStatementRule();

            if (statement.expression is not Expression.Assign assign || assign.target is not Expression.VariableDeclaration)
            {
                ReportError(PreviousToken(), "Expected variable, function, or group declaration.");
            }

            return statement;
        }
        else
        {
            return StatementRule();
        }
    }

    private static Statement StatementRule()
    {
        if (MatchNextToken(IF)) return IfRule();
        if (MatchNextToken(WHILE)) return WhileRule();
        if (MatchNextToken(EACH)) return EachRule();
        if (IsNextToken(CURLY_LEFT)) return new Statement.ExpressionStatement(BlockRule());
        if (MatchNextToken(BREAK)) return BreakRule();
        if (MatchNextToken(USE)) return UseRule();

        return ExpressionStatementRule();
    }

    private static Statement.GroupDeclaration GroupRule()
    {
        Token groupToken = PreviousToken();
        Token name = RequireMatchNext(IDENTIFIER, "Expected group name.");

        Token start = RequireMatchNext(CURLY_LEFT, "Expected '{'.");
        List<Statement> statements = [];

        while (!MatchNextToken(CURLY_RIGHT) && !IsNextToken(EOF))
        {
            SynchronizationBoundary(() =>
            {
                statements.Add(DeclarationRule(true));
                MatchNextToken(EOL);
            });
        }

        if (PreviousToken().Type != CURLY_RIGHT && IsNextToken(EOF))
        {
            ReportError(PeekToken(), "Expected '}'.");
        }

        Expression.Block body = new(statements, start, PreviousToken().End);
        return new Statement.GroupDeclaration(groupToken, name, body);
    }

    private static Statement.Use UseRule()
    {
        Token useToken = PreviousToken();
        Token target = RequireMatchNext([STRING, IDENTIFIER], "Expected file path as string or name of built-in group.");
        return new Statement.Use(useToken, target);
    }

    private static Statement.If IfRule()
    {
        Token ifToken = PreviousToken();

        List<(Expression condition, Expression.Block body)> statements = [];
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

        return new Statement.If(ifToken, statements, elseBlock);
    }

    private static Statement.Each EachRule()
    {
        Token eachToken = PreviousToken();
        Token variableName = RequireMatchNext(IDENTIFIER, "Expected identifier.");
        RequireMatchNext(COLON, "Expected ':'.");
        return new Statement.Each(eachToken, variableName, ExpressionRule(), StatementOrBlockRule());
    }

    private static Statement.While WhileRule()
    {
        return new Statement.While(PreviousToken(), ExpressionRule(), StatementOrBlockRule());
    }

    private static Statement.Break BreakRule()
    {
        Token keyword = PreviousToken();
        RequireEndOfLineOrFile("Expected <EOL>");
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

            if (IsNextToken(EOL))
            {
                ReportError(PeekToken(), "Expected expression.");
                block = new Expression.Block([], PeekToken(), PeekToken().End);
            }
            else if (IsNextToken(VAR))
            {
                ReportError(PeekToken(), "Cannot declare variable inside '=>' block.");
                Statement variableStatement = new Statement.ExpressionStatement(VariableRule());
                block = new Expression.Block([variableStatement], variableStatement.FirstToken, variableStatement.End);
            }
            else
            {
                Statement statement = StatementRule();
                if (statement is Statement.ExpressionStatement expressionStatement && expressionStatement.expression is Expression.Assign assign && assign.target is Expression.VariableDeclaration)
                {
                    ReportError(PreviousPreviousToken(), "Cannot declare variable inside '=>' block.");
                }
                block = new Expression.Block([statement], statement.FirstToken, statement.End);
            }
        }

        MatchNextToken(EOL);
        return block;
    }

    private static Expression.Block BlockRule()
    {
        Token start = RequireMatchNext(CURLY_LEFT, "Expected '{'.");

        List<Statement> statements = [];

        while (!MatchNextToken(CURLY_RIGHT) && !IsNextToken(EOF))
        {
            SynchronizationBoundary(() => statements.Add(DeclarationStatementRule(false)));

            if (!IsNextToken(CURLY_RIGHT, EOF) && statements.LastOrDefault() is Statement.ExpressionStatement expressionStatement)
            {
                RequireExpressionStatement(expressionStatement.expression);
            }
        }

        if (PreviousToken().Type != CURLY_RIGHT && IsNextToken(EOF))
        {
            ReportError(PeekToken(), "Expected '}'.");
        }

        return new Expression.Block(statements, start, PreviousToken().End);
    }

    private static Statement.ExpressionStatement ExpressionStatementRule()
    {
        Expression expression = ExpressionRule();

        if (!IsNextToken(CURLY_RIGHT))
        {
            RequireEndOfLineOrFile("Expected <EOL>.");
        }

        return new Statement.ExpressionStatement(expression);
    }

    private static void RequireExpressionStatement(Expression expression)
    {
        if (expression is Expression.Assign or Expression.Call or Expression.VariableDeclaration or Expression.Push or Expression.Block or Expression.Match)
        {
            return;
        }

        ReportError(expression.FirstToken, "Only assign, call, and match expressions can be used as statements.");
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
                IList<Statement> statements = [new Statement.ExpressionStatement(expression), new Statement.Break(PreviousToken())];
                return new Expression.Block(statements, statements[0].FirstToken, statements[1].End);
            }
            else if (MatchNextToken(SQUARE_LEFT))
            {
                Token name = RequireMatchNext(IDENTIFIER, "Expected variable name.");
                RequireMatchNext(SQUARE_RIGHT, "Expected ']'.");
                return new Expression.Push(name, expression, PreviousToken());
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
            Token varToken = PreviousToken();
            Token name = RequireMatchNext(IDENTIFIER, "Expected variable name.");
            return new Expression.VariableDeclaration(varToken, name);
        }
        else
        {
            if (!IsNextToken(IDENTIFIER))
            {
                throw ReportError(ReadToken(), PreviousToken().Type == COMMAND ? "Cannot assign to function." : "Expected variable name.");
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
        if (MatchNextToken(EXCLAMATION, MINUS))
        {
            Token operatorToken = PreviousToken();
            Expression right = UnaryRule();
            return new Expression.Unary(operatorToken, right);
        }

        return AccessRule();
    }

    private static Expression AccessRule()
    {
        Expression expression = GroupAccessRule();

        while (MatchNextToken(SQUARE_LEFT))
        {
            Token start = PreviousToken();
            Expression indexExpression = ExpressionRule();
            RequireMatchNext(SQUARE_RIGHT, "Expected ']'.");
            expression = new Expression.Access(expression, indexExpression, start, PreviousToken());
        }

        return expression;
    }

    private static Expression GroupAccessRule()
    {
        Expression expression = PrimaryRule();

        while (MatchNextToken(COLON_COLON))
        {
            expression = new Expression.GroupAccess(expression, TryRequireMatchNext([IDENTIFIER, COMMAND], "Expected identifier.") ? PreviousToken() : null!);

            if (PreviousToken().Type == COMMAND)
            {
                expression = new Expression.Call(expression, ReadCallArguments());
            }
        }

        return expression;
    }

    private static Expression PrimaryRule()
    {
        if (IsNextToken(literalTokens))
        {
            return LiteralRule();
        }

        return ReadToken().Type switch
        {
            IDENTIFIER when IsNextToken(COLON_COLON) => new Expression.Group(PreviousToken()),
            IDENTIFIER => new Expression.Variable(PreviousToken()),
            AT => new Expression.ContextValue(PreviousToken()),
            SQUARE_LEFT => ArrayLiteralRule(),
            COMMAND => CallRule(),
            MATCH => MatchRule(),
            PAREN_LEFT when IsNextToken(DOT_DOT, PAREN_RIGHT) || (IsNextToken([.. literalTokens, IDENTIFIER]) && IsNextNextToken(COLON)) => ObjectLiteralRule(),
            PAREN_LEFT => GroupingRule(),
            INTERPOLATED_STRING_START => InterpolatedStringRule(),
            _ => throw ReportError(PreviousToken(), "Expected expression.")
        };
    }

    private static Expression.Literal LiteralRule()
    {
        return ReadToken().Type switch
        {
            FALSE => new Expression.Literal(false, PreviousToken()),
            TRUE => new Expression.Literal(true, PreviousToken()),
            NULL => new Expression.Literal(null, PreviousToken()),
            NUMBER or STRING => new Expression.Literal(PreviousToken().Value, PreviousToken()),
            _ => throw ReportError(PreviousToken(), "Expected literal value.")
        };
    }

    private static Expression.InterpolatedString InterpolatedStringRule()
    {
        Token start = PreviousToken();
        List<Expression> expressions = [];

        while (!MatchNextToken(INTERPOLATED_STRING_END))
        {
            if (MatchNextToken(INTERPOLATED_TEXT))
            {
                expressions.Add(new Expression.Literal(PreviousToken().Value, PreviousToken()));
            }
            else
            {
                expressions.Add(ExpressionRule());
            }
        }

        return new Expression.InterpolatedString(expressions, start, PreviousToken());
    }

    private static Expression.Grouping GroupingRule()
    {
        Token start = PreviousToken();
        Expression expression = NonAssignExpressionRule();
        RequireMatchNext(PAREN_RIGHT, "Expected ')'.");
        return new Expression.Grouping(expression, start, PreviousToken());
    }

    private static Expression.Object ObjectLiteralRule()
    {
        Token start = PreviousToken();

        if (MatchNextToken(PAREN_RIGHT))
        {
            return new Expression.Object(start, [], PreviousToken());
        }

        List<Expression.Object.Item> items = [];

        do
        {
            if (MatchNextToken(DOT_DOT))
            {
                items.Add(new Expression.Object.Item.Spread(ExpressionRule()));
            }
            else
            {
                Expression.Literal keyExpression;

                if (MatchNextToken(IDENTIFIER))
                {
                    keyExpression = new Expression.Literal(PreviousToken().Lexeme, PreviousToken());
                }
                else if (IsNextToken(literalTokens))
                {
                    keyExpression = LiteralRule();
                }
                else
                {
                    throw ReportError(ReadToken(), "Expected object key name.");
                }

                RequireMatchNext(COLON, "Expected ':' after key.");
                items.Add(new Expression.Object.Item.KeyValue(keyExpression, ExpressionRule()));
            }
        }
        while (MatchNextToken(COMMA));

        Token end = RequireMatchNext(PAREN_RIGHT, "Expected ')'.");
        return new Expression.Object(start, items, end);
    }

    private static Expression.Array ArrayLiteralRule()
    {
        Token start = PreviousToken();

        if (MatchNextToken(SQUARE_RIGHT))
        {
            return new Expression.Array(start, [], PreviousToken());
        }

        List<(Expression, bool)> items = [];

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

        Token end = RequireMatchNext(SQUARE_RIGHT, "Expected ']'.");
        return new Expression.Array(start, items, end);
    }

    private static Expression.Match MatchRule()
    {
        Token start = PreviousToken();

        Expression value = ExpressionRule();
        RequireMatchNext(CURLY_LEFT, "Expected '{'.");

        List<(Expression condition, Expression.Block body)> statements = [];
        Expression.Block? elseStatement = null;

        while (!MatchNextToken(CURLY_RIGHT, ELSE))
        {
            statements.Add((ExpressionRule(), StatementOrBlockRule()));
        }

        if (PreviousToken().Type == ELSE)
        {
            elseStatement = StatementOrBlockRule();
            try
            {
                RequireMatchNext(CURLY_RIGHT, "Expected '}'. An else match must be the last match in a match expression.");
            }
            catch (ParseException)
            {
                ReadToPairClosing(CURLY_LEFT, CURLY_RIGHT);
            }
        }

        Token end = PreviousToken();
        return new Expression.Match(value, statements, elseStatement, start, end);
    }

    private static Statement.FunctionDeclaration FunctionRule()
    {
        Token fnToken = PreviousToken();
        Token name = RequireMatchNext(COMMAND, "Function names must start with an '@' character.", new() {
            { AT, "Expected function name after '@' character." },
            { EOL, "Expected function name." },
            { EOF, "Expected function name." }
        });
        List<Token> parameters = [];

        if (IsNextToken(EOL, EOF))
        {
            throw ReportError(PeekToken(), "Expected parameter name, '{', or '=>'.");
        }

        if (PeekToken().Type is not (CURLY_LEFT or THICK_ARROW))
        {
            do
            {
                parameters.Add(RequireMatchNext(IDENTIFIER, "Expected parameter name."));
            }
            while (MatchNextToken(COMMA));
        }

        return new Statement.FunctionDeclaration(fnToken, name, parameters, StatementOrBlockRule());
    }

    private static Expression.Call CallRule()
    {
        Token command = PreviousToken();
        return new Expression.Call(new Expression.Function(command), ReadCallArguments());
    }

    private static List<Expression> ReadCallArguments()
    {
        List<Expression> args = [];
        TokenType[] callEndTokens = [CURLY_LEFT, ARROW, EOL, EOF, COMMA, PAREN_RIGHT, SQUARE_RIGHT];

        while (!(IsNextNextToken([.. binaryOperators.SelectMany(t => t), .. callEndTokens]) && MatchNextToken(EXCLAMATION)) && !IsNextToken(callEndTokens))
        {
            args.Add(NonAssignExpressionRule());

            if (!MatchNextToken(COMMA))
            {
                MatchNextToken(EXCLAMATION);
                break;
            }

            if (IsNextNextToken(COLON))
            {
                Backtrack();
                break;
            }
        }

        return args;
    }

    private static void SynchronizationBoundary(Action action)
    {
        try
        {
            action.Invoke();
        }
        catch (ParseException)
        {
            Synchronize();
        }

        MatchNextToken(EOL);
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

    private static void ReadToPairClosing(TokenType pairStart, TokenType pairEnd)
    {
        int pairDepth = 1;
        while (current < tokens.Count && pairDepth > 0)
        {
            Token token = ReadToken();
            if (token.Type == pairStart)
            {
                pairDepth++;
            }
            else if (token.Type == pairEnd)
            {
                pairDepth--;
            }
        }
    }

    private static Token ReadToken()
    {
        Token token = tokens[current];
        current++;
        return token;
    }

    private static void Backtrack()
    {
        current--;
    }

    private static Token RequireMatchNext(TokenType type, string errorMessage, Dictionary<TokenType, string>? customErrorMessages = null) => RequireMatchNext([type], errorMessage, customErrorMessages);

    private static Token RequireMatchNext(TokenType[] types, string errorMessage, Dictionary<TokenType, string>? customErrorMessages = null)
    {
        if (!MatchNextToken(types))
        {
            string? customError = null;
            if (current < tokens.Count)
            {
                customErrorMessages?.TryGetValue(PeekToken().Type, out customError);
            }
            throw ReportError(PeekToken(), customError ?? errorMessage);
        }

        return PreviousToken();
    }

    private static bool TryRequireMatchNext(TokenType[] types, string errorMessage)
    {
        if (!MatchNextToken(types))
        {
            ReportError(PeekToken(), errorMessage);
            return false;
        }

        return true;
    }

    private static void RequireEndOfLineOrFile(string errorMessage)
    {
        if (!MatchNextToken(EOL) && !IsNextToken(EOF))
        {
            ReportError(PeekToken(), errorMessage);
        }
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

    private static bool IsNextNextToken(params TokenType[] types)
    {
        if (current + 1 >= tokens.Count)
        {
            return false;
        }

        return types.Any(type => type == tokens[current + 1].Type);
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

    private static Token PreviousPreviousToken()
    {
        return tokens[current - 2];
    }

    private static ParseException ReportError(Token token, string message)
    {
        errors.Add(new ParserError(token, message));
        return new ParseException();
    }
}
