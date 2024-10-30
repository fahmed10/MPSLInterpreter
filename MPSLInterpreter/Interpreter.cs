using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static MPSLInterpreter.TokenType;

namespace MPSLInterpreter;

internal class InterpretException(string message) : Exception(message);

internal class Interpreter : Expression.IVisitor<object?>, Statement.IVisitor<object?>
{
    public readonly MPSLEnvironment globalEnvironment;
    public bool breakCalled = false;
    public MPSLEnvironment environment;

    public Interpreter(MPSLEnvironment environment)
    {
        globalEnvironment = environment;
        this.environment = globalEnvironment;
    }

    public void Interpret(IEnumerable<Statement> statements)
    {
        try
        {
            foreach (Statement statement in statements)
            {
                Execute(statement);
                if (breakCalled)
                {
                    ReportErrorRaw("Cannot use break outside of a loop or function body.");
                }
            }
        }
        catch (InterpretException e)
        {
            Utils.WriteLineColored(e.Message, ConsoleColor.Red);

#if DEBUG
            if (e.StackTrace != null)
            {
                Utils.WriteLineColored(e.StackTrace + '\n', ConsoleColor.DarkGray);
            }
#endif
        }
    }

    private object? Execute(Statement statement)
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

    void CheckNumericValue(Token token, [NotNull] object? value)
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
            ReportError(token, "Value must be a number.");
        }
    }

    void CheckStringValue(Token token, [NotNull] object? value)
    {
        if (value is null)
        {
            ReportError(token, "Value cannot be nil.");
        }

        if (value is string)
        {
            return;
        }

        ReportError(token, "Value must be a string.");
    }

    public static string ToMPSLString(object? obj)
    {
        return obj switch
        {
            null => "null",
            _ => obj.ToString() ?? "null"
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
            if (left is double && right is double)
            {
                CheckNumericValue(expression.operatorToken, left);
                CheckNumericValue(expression.operatorToken, right);
                return (double)left + (double)right;
            }
            if (left is string && right is string)
            {
                CheckStringValue(expression.operatorToken, left);
                CheckStringValue(expression.operatorToken, right);
                return (string)left + (string)right;
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
            DOLLAR => ToMPSLString(value),
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
        environment.DefineVariable(expression.name, null);
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
            Expression.Variable variable = (Expression.Variable)access.expression;
            int index = Convert.ToInt32(Evaluate(access.indexExpression));
            MPSLArray array = (MPSLArray)environment.GetVariableValue(variable.name);
            environment.contextValue = array[index];
            array[index] = Evaluate(expression.value);
        }
        else
        {
            Expression.Variable variable = (Expression.Variable)expression.target;
            environment.contextValue = environment.GetVariableValue(variable.name);
            environment.AssignVariable(variable.name, Evaluate(expression.value));
        }

        returnValue = expression.value;
        environment.contextValue = oldContext;
        return returnValue;
    }

    public object? VisitCall(Expression.Call expression)
    {
        List<object?> arguments = new();
        foreach (Expression argument in expression.arguments)
        {
            arguments.Add(Evaluate(argument));
        }

        ICallable function = environment.GetFunction(expression.callee);

        if (function.ArgumentCount != arguments.Count)
        {
            ReportError(expression.callee, $"Expected {function.ArgumentCount} argument(s), but got {arguments.Count} argument(s).");
        }

        try
        {
            return function.Call(this, arguments.ToArray());
        }
        catch (Exception e)
        {
            ReportErrorRaw($"In function '{expression.callee.Lexeme}', called from [L{expression.callee.Line}, C{expression.callee.Column}]:\n{(e.InnerException ?? e).Message}");
            return null;
        }
    }

    public object? VisitBreak(Statement.Break statement)
    {
        breakCalled = true;
        return null;
    }

    public object? VisitExpressionStatement(Statement.ExpressionStatement statement)
    {
        return Evaluate(statement.expression);
    }

    public object? InterpretBlock(Expression.Block block, object? contextValue, Action<MPSLEnvironment>? environmentAction)
    {
        MPSLEnvironment previous = environment;
        object? lastValue = Invalid.Value;
        try
        {
            environment = new MPSLEnvironment(environment);
            environment.contextValue = contextValue;
            environmentAction?.Invoke(environment);

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

        return lastValue;
    }

    public object? VisitBlock(Expression.Block expression)
    {
        return InterpretBlock(expression, Invalid.Value, null);
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
            blockValue = InterpretBlock(statement.body, Invalid.Value, e => e.DefineVariable(statement.variableName, value));

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
        return environment.contextValue;
    }

    public object? VisitFunctionDeclaration(Statement.FunctionDeclaration statement)
    {
        environment.DefineFunction(statement.name, new MPSLFunction(statement.parameters.Count, statement.parameters, statement.body));
        return null;
    }

    public object? VisitArray(Expression.Array expression)
    {
        MPSLArray array = new MPSLArray();

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

    public object? VisitAccess(Expression.Access expression)
    {
        object? value = Evaluate(expression.expression);
        object? index = Evaluate(expression.indexExpression);

        CheckNumericValue(expression.start, index);
        if (!double.IsInteger((double)index))
        {
            ReportError(expression.start, "Access expression must evaluate to a whole number.");
        }

        if (value is MPSLArray a)
        {
            return a[(int)(double)index];
        }
        else if (value is string s)
        {
            return s[(int)(double)index].ToString();
        }
        else
        {
            ReportError(expression.start, "Only arrays and strings can be accessed with an access expression.");
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
        string path = Path.GetFullPath((string)statement.path.Value!);

        if (!File.Exists(path))
        {
            ReportError(statement.path, $"File {path} does not exist.");
        }

        MPSLEnvironment env = new();
        MPSL.RunFile(path, env);

        if (env is null)
        {
            ReportError(statement.path, $"Failed to use '{statement.path.Value}'.");
            return null;
        }

        environment.AddEnvironment(statement.path, env);
        return null;
    }
}
