using System.Diagnostics.CodeAnalysis;
using System.Text;
using static MPSLInterpreter.TokenType;

namespace MPSLInterpreter;

internal class InterpretException(string message) : Exception(message);

internal class Interpreter : Expression.IVisitor<object?>, Statement.IVisitor<object?>
{
    private readonly MPSLEnvironment globalEnvironment;
    public bool breakCalled = false;
    private Token breakToken = null!;
    private bool errorOccurred = false;
    private bool declaringPublic = false;
    public MPSLEnvironment environment;

    public Interpreter(MPSLEnvironment environment)
    {
        globalEnvironment = environment;
        this.environment = globalEnvironment;
    }

    /// <summary>
    /// Interprets the given statements.
    /// </summary>
    /// <param name="statements">The statements to interpret.</param>
    /// <returns>True if no errors occurred at runtime, otherwise false.</returns>
    public bool Interpret(IEnumerable<Statement> statements)
    {
        try
        {
            foreach (Statement statement in statements)
            {
                Execute(statement);
                if (breakCalled)
                {
                    ReportError(breakToken, "Cannot use break outside of a loop or function body.");
                }
            }
        }
        catch (InterpretException e)
        {
            errorOccurred = true;
            Utils.WriteLineColored(e.Message, ConsoleColor.Red);
        }

        return !errorOccurred;
    }

    object? Execute(Statement statement)
    {
        return statement.Accept(this);
    }

    object? Evaluate(Expression expression)
    {
        return expression.Accept(this);
    }

    public static bool IsTruthy(object? obj)
    {
        return obj switch
        {
            null => false,
            bool b => b,
            _ => true
        };
    }

    bool IsEqual(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null)
        {
            return false;
        }

        if (left is double d && right is double d2)
        {
            return d == d2;
        }

        return left.Equals(right);
    }

    void CheckNumericValue(Token token, [NotNull] object? value, string? errorMessage = null)
    {
        if (value is null)
        {
            ReportError(token, "Value cannot be null.");
        }

        if (value is double num)
        {
            if (double.IsNaN(num))
            {
                ReportError(token, "Value cannot be NaN.");
            }
        }
        else
        {
            ReportError(token, errorMessage ?? "Value must be a number.");
        }
    }

    void CheckStringValue(Token token, [NotNull] object? value)
    {
        if (value is null)
        {
            ReportError(token, "Value cannot be null.");
        }

        if (value is string)
        {
            return;
        }

        ReportError(token, "Value must be a string.");
    }

    public static string ToMPSLString(object? obj)
    {
        return obj?.ToString() ?? "null";
    }

    public static string ToMPSLDebugString(object? obj)
    {
        return obj switch
        {
            string => $"\"{ToMPSLString(obj)}\"",
            _ => ToMPSLString(obj)
        };
    }

    [DoesNotReturn]
    public static void ReportError(Token token, string message)
    {
        throw new InterpretException($"[L{token.Line}, C{token.Column}] Runtime Error: {message}");
    }

    [DoesNotReturn]
    public static void ReportErrorRaw(string message)
    {
        throw new InterpretException(message);
    }

    public object? VisitBinary(Expression.Binary expression)
    {
        object? left = Evaluate(expression.left);
        object? right = Evaluate(expression.right);

        if (expression.operatorToken.Type == PLUS)
        {
            if (left is double d1 && right is double d2)
            {
                CheckNumericValue(expression.operatorToken, left);
                CheckNumericValue(expression.operatorToken, right);
                return d1 + d2;
            }
            if (left is string s1 && right is string s2)
            {
                CheckStringValue(expression.operatorToken, left);
                CheckStringValue(expression.operatorToken, right);
                return s1 + s2;
            }

            ReportError(expression.operatorToken, "Operands must be two numbers or two strings.");
        }

        if (expression.operatorToken.Type is EQUAL or EXCLAMATION_EQUAL)
        {
            return expression.operatorToken.Type switch
            {
                EQUAL => IsEqual(left, right),
                EXCLAMATION_EQUAL => !IsEqual(left, right),
                _ => null
            };
        }

        if (expression.operatorToken.Type is AMPERSAND)
        {
            return IsTruthy(left) && IsTruthy(right);
        }

        if (expression.operatorToken.Type is PIPE)
        {
            return IsTruthy(left) || IsTruthy(right);
        }

        CheckNumericValue(expression.operatorToken, left);
        CheckNumericValue(expression.operatorToken, right);

        if (expression.operatorToken.Type is SLASH)
        {
            if ((double)right == 0)
            {
                ReportError(expression.operatorToken, "Cannot divide by zero.");
            }
            return (double)left / (double)right;
        }

        return expression.operatorToken.Type switch
        {
            MINUS => (double)left - (double)right,
            ASTERISK => (double)left * (double)right,
            GREATER => (double)left > (double)right,
            GREATER_EQUAL => (double)left >= (double)right,
            LESSER => (double)left < (double)right,
            LESSER_EQUAL => (double)left <= (double)right,
            _ => throw new NotImplementedException(),
        };
    }

    public object? VisitUnary(Expression.Unary expression)
    {
        object? value = Evaluate(expression.right);

        if (expression.operatorToken.Type is MINUS)
        {
            CheckNumericValue(expression.operatorToken, value);
            return -(double)value;
        }

        return expression.operatorToken.Type switch
        {
            EXCLAMATION => !IsTruthy(value),
            _ => throw new NotImplementedException()
        };
    }

    public object? VisitLiteral(Expression.Literal expression)
    {
        return expression.value;
    }

    public object? VisitGrouping(Expression.Grouping expression)
    {
        return Evaluate(expression.expression);
    }

    public object? VisitVariable(Expression.Variable expression)
    {
        return environment.GetVariableValue(expression.name);
    }

    public object? VisitVariableDeclaration(Expression.VariableDeclaration expression)
    {
        environment.DefineVariable(expression.name, null, declaringPublic);
        return null;
    }

    public object? VisitAssign(Expression.Assign expression)
    {
        object? returnValue;
        object? oldContext = environment.contextValue;

        if (expression.target is Expression.VariableDeclaration declaration)
        {
            VisitVariableDeclaration(declaration);
            environment.AssignVariable(declaration.name, Evaluate(expression.value));
        }
        else if (expression.target is Expression.Access access)
        {
            object? value = Evaluate(access.expression);
            if (value is MPSLArray array)
            {
                int index = GetIndexValue(access);
                environment.contextValue = array[index];
                array[index] = Evaluate(expression.value);
            }
            else if (value is MPSLObject obj)
            {
                object? key = Evaluate(access.indexExpression);

                if (key == null)
                {
                    ReportError(access.start, "Cannot index object with null key.");
                }
                if (!obj.TryGetValue(key, out object? keyValue))
                {
                    ReportError(access.start, $"Object does not contain key '{key}'.");
                }

                environment.contextValue = keyValue;
                obj[key] = Evaluate(expression.value);
            }
            else
            {
                ReportError(access.start, "Only arrays can be assigned to with an access expression.");
            }
        }
        else if (expression.target is Expression.GroupAccess groupAccess)
        {
            MPSLGroup group = (MPSLGroup)Evaluate(groupAccess.group)!;
            group.Environment.AssignVariable(groupAccess.accessName, Evaluate(expression.value));
        }
        else if (expression.target is Expression.Variable variable)
        {
            environment.contextValue = environment.GetVariableValue(variable.name);
            environment.AssignVariable(variable.name, Evaluate(expression.value));
        }
        else if (expression.target is Expression.Call call)
        {
            ReportError(call.callee.FirstToken, "Cannot assign to function.");
        }
        else
        {
            ReportError(expression.target.FirstToken, "Invalid assignment target.");
        }

        returnValue = expression.value;
        environment.contextValue = oldContext;
        return returnValue;
    }

    public object? VisitCall(Expression.Call expression)
    {
        List<object?> arguments = [];
        foreach (Expression argument in expression.arguments)
        {
            arguments.Add(Evaluate(argument));
        }

        ICallable function = (ICallable)Evaluate(expression.callee)!;

        if (function.ArgumentCount != arguments.Count)
        {
            ReportError(expression.callee.FirstToken, $"Expected {function.ArgumentCount} argument(s), but got {arguments.Count} argument(s).");
        }

        try
        {
            return function.Call(this, arguments.ToArray());
        }
        catch (Exception e)
        {
            ReportErrorRaw($"In function '{expression.callee}', called from [L{expression.callee.FirstToken.Line}, C{expression.callee.FirstToken.Column}]:\n{(e.InnerException ?? e).Message}");
            return null;
        }
    }

    public object? VisitBreak(Statement.Break statement)
    {
        breakCalled = true;
        breakToken = statement.keyword;
        return null;
    }

    public object? VisitExpressionStatement(Statement.ExpressionStatement statement)
    {
        return Evaluate(statement.expression);
    }

    public object? InterpretBlock(Expression.Block block, MPSLEnvironment? blockEnvironment = null)
    {
        MPSLEnvironment previous = environment;
        object? lastValue = Invalid.Value;

        try
        {
            environment = blockEnvironment ?? new(environment);

            foreach (Statement statement in block.statements)
            {
                object? newValue = Execute(statement);
                if (newValue is not Invalid and not null)
                {
                    lastValue = newValue;
                }

                if (breakCalled)
                {
                    break;
                }
            }
        }
        finally
        {
            environment = previous;
        }

        return lastValue is Invalid ? null : lastValue;
    }

    public object? VisitBlock(Expression.Block expression)
    {
        return InterpretBlock(expression);
    }

    public object? VisitIf(Statement.If statement)
    {
        foreach ((Expression condition, Expression.Block body) in statement.statements)
        {
            if (IsTruthy(Evaluate(condition)))
            {
                return VisitBlock(body);
            }
        }

        if (statement.elseBlock is not null)
        {
            return VisitBlock(statement.elseBlock);
        }

        return null;
    }

    public object? VisitWhile(Statement.While statement)
    {
        object? value = null;

        while (IsTruthy(Evaluate(statement.condition)))
        {
            value = VisitBlock(statement.body);

            if (breakCalled)
            {
                breakCalled = false;
                break;
            }
        }

        return value;
    }

    public object? VisitEach(Statement.Each statement)
    {
        object? blockValue = null;
        object? collection = Evaluate(statement.collection);

        if (collection is not IEnumerable<object> and not string)
        {
            ReportError(statement.variableName, "Collection must be a string or array.");
        }

        IEnumerable<object?> collectionValues = null!;
        if (collection is IEnumerable<object?> values)
        {
            collectionValues = values;
        }
        else if (collection is string str)
        {
            collectionValues = str.AsEnumerable().Cast<object?>();
        }

        foreach (object? value in collectionValues)
        {
            MPSLEnvironment blockEnvironment = new(environment);
            blockEnvironment.DefineVariable(statement.variableName, value, false);
            blockValue = InterpretBlock(statement.body, blockEnvironment);

            if (breakCalled)
            {
                breakCalled = false;
                break;
            }
        }

        return blockValue;
    }

    public object? VisitMatch(Expression.Match expression)
    {
        object? value = Evaluate(expression.value);
        foreach ((Expression condition, Expression.Block body) in expression.statements)
        {
            environment.contextValue = value;
            if (IsTruthy(Evaluate(condition)))
            {
                environment.contextValue = Invalid.Value;
                return VisitBlock(body);
            }
        }

        environment.contextValue = Invalid.Value;
        if (expression.elseBlock is not null)
        {
            return VisitBlock(expression.elseBlock);
        }

        return null;
    }

    public object? VisitContextValue(Expression.ContextValue expression)
    {
        if (environment.contextValue is Invalid)
        {
            ReportError(expression.token, "'@' has no value here.");
        }

        return environment.contextValue;
    }

    public object? VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        environment.DefineFunction(statement.name, new MPSLFunction(statement.parameters, statement.body, environment), declaringPublic);
        return null;
    }

    public object? VisitArray(Expression.Array expression)
    {
        MPSLArray array = [];

        foreach (var item in expression.items)
        {
            object? value = Evaluate(item.expression);
            if (item.spread)
            {
                if (value is MPSLArray a)
                {
                    array.AddRange(a);
                }
                else
                {
                    ReportError(expression.start, "Can only spread array types in an array expression.");
                }
            }
            else
            {
                array.Add(value);
            }
        }

        return array;
    }

    public object? VisitObject(Expression.Object expression)
    {
        MPSLObject obj = [];

        foreach (var item in expression.items)
        {
            object? value = Evaluate(item.valueExpression);
            if (item is Expression.Object.Item.Spread)
            {
                if (value is MPSLObject o)
                {
                    foreach (var pair in o)
                    {
                        obj[pair.Key] = pair.Value;
                    }
                }
                else
                {
                    ReportError(item.valueExpression.FirstToken, "Can only spread object types in an object expression.");
                }
            }
            else if (item is Expression.Object.Item.KeyValue keyValueItem)
            {
                object? key = Evaluate(keyValueItem.keyExpression);

                if (key == null)
                {
                    ReportError(keyValueItem.keyExpression.token, "Cannot have null key on object.");
                }

                obj[key] = value;
            }
        }

        return obj;
    }

    private int GetIndexValue(Expression.Access expression)
    {
        object? index = Evaluate(expression.indexExpression);

        CheckNumericValue(expression.start, index, "Index of access expression must evaluate to a number.");
        if (!double.IsInteger((double)index))
        {
            ReportError(expression.start, "Index of access expression must evaluate to a whole number.");
        }

        return (int)(double)index;
    }

    public object? VisitAccess(Expression.Access expression)
    {
        object? value = Evaluate(expression.expression);

        if (value is MPSLArray array)
        {
            return array[GetIndexValue(expression)];
        }
        else if (value is string str)
        {
            return str[GetIndexValue(expression)].ToString();
        }
        else if (value is MPSLObject obj)
        {
            object? key = Evaluate(expression.indexExpression);

            if (key == null)
            {
                ReportError(expression.start, "Cannot index object with null key.");
            }
            if (!obj.TryGetValue(key, out object? keyValue))
            {
                ReportError(expression.start, $"Object does not contain key '{key}'.");
            }

            return keyValue;
        }
        else
        {
            ReportError(expression.start, value is null ? "Cannot index a null value." : "Only arrays and strings can be indexed with an access expression.");
            return null;
        }
    }

    public object? VisitPush(Expression.Push expression)
    {
        object? variable = environment.GetVariableValue(expression.target);
        object? value = Evaluate(expression.value);

        if (variable is MPSLArray array)
        {
            array.Add(value);
        }
        else
        {
            ReportError(expression.target, "Can only push into an array.");
        }

        return value;
    }

    public object? VisitInterpolatedString(Expression.InterpolatedString interpolatedString)
    {
        StringBuilder str = new();

        foreach (Expression expression in interpolatedString.expressions)
        {
            str.Append(Evaluate(expression));
        }

        return str.ToString();
    }

    public object? VisitUse(Statement.Use statement)
    {
        if (statement.target.Type == IDENTIFIER)
        {
            if (StdLibrary.BuiltInGroups.groups.TryGetValue(statement.target.Lexeme, out MPSLGroup? group))
            {
                environment.AddEnvironment(statement.target, group.Environment);
            }
            else
            {
                ReportError(statement.target, $"Built-in group '{statement.target.Lexeme}' does not exist.");
            }

            return null;
        }

        string path = Path.GetFullPath((string)statement.target.Value!);

        if (!File.Exists(path))
        {
            ReportError(statement.target, $"File at '{path}' does not exist.");
        }

        MPSLEnvironment env = new();
        MPSL.RunFile(path, env);

        if (env is null)
        {
            ReportError(statement.target, $"Failed to use '{statement.target.Value}'.");
            return null;
        }

        environment.AddEnvironment(statement.target, env);
        return null;
    }

    public object? VisitGroupDeclaration(Statement.GroupDeclaration statement)
    {
        MPSLEnvironment groupEnvironment = new(environment);
        InterpretBlock(statement.body, groupEnvironment);
        environment.DefineGroup(statement.name, new(groupEnvironment), declaringPublic);
        return null;
    }

    public object? VisitPublic(Statement.Public statement)
    {
        declaringPublic = true;
        object? value = statement.statement.Accept(this);
        declaringPublic = false;
        return value;
    }

    public object? VisitGroupAccess(Expression.GroupAccess expression)
    {
        MPSLGroup? group = (MPSLGroup?)Evaluate(expression.group);

        if (group is null)
        {
            ReportError(expression.group.FirstToken, "Cannot access a null group.");
            return null;
        }

        return group.Environment.Get(expression.accessName);
    }

    public object? VisitGroup(Expression.Group expression)
    {
        return environment.GetGroup(expression.name);
    }

    public object? VisitFunction(Expression.Function expression)
    {
        return environment.GetFunction(expression.name);
    }
}
