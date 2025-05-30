namespace MPSLInterpreter;

/// <summary>
/// Stores variables and functions that were declared in its block in MPSL code.
/// </summary>
/// <param name="parent">The parent of this environment.</param>
public class MPSLEnvironment(MPSLEnvironment? parent = null)
{
    readonly Dictionary<string, object?> variables = [];
    readonly Dictionary<string, ICallable> functions = [];
    internal object? contextValue = Invalid.Value;

    internal void AddEnvironment(Token useToken, MPSLEnvironment other)
    {
        foreach (var pair in other.variables)
        {
            if (!variables.TryAdd(pair.Key, pair.Value))
            {
                Interpreter.ReportError(useToken, $"Variable '{pair.Key}' has already been defined.");
            }
        }

        foreach (var pair in other.functions)
        {
            if (!functions.TryAdd(pair.Key, pair.Value))
            {
                Interpreter.ReportError(useToken, $"Function '{pair.Key}' has already been defined.");
            }
        }
    }

    public void DefineVariable(string name, object? value)
    {
        if (variables.ContainsKey(name))
        {
            throw new ArgumentException($"Variable '{name}' has already been defined in this environment.");
        }

        variables[name] = value;
    }

    internal void DefineVariable(Token token, object? value)
    {
        if (variables.ContainsKey(token.Lexeme))
        {
            Interpreter.ReportError(token, $"Variable '{token.Lexeme}' has already been defined.");
        }

        variables[token.Lexeme] = value;
    }

    public void DefineFunction(string name, NativeFunction function)
    {
        if (functions.ContainsKey(name))
        {
            throw new ArgumentException($"Function '{name}' has already been defined.");
        }

        functions[name] = function;
    }

    internal void DefineFunction(Token token, ICallable function)
    {
        if (functions.ContainsKey((string)token.Value!))
        {
            Interpreter.ReportError(token, $"Function '{token.Lexeme}' has already been defined.");
        }

        functions[(string)token.Value!] = function;
    }

    internal ICallable GetFunction(Token name)
    {
        string functionName = (string)name.Value!;

        if (functions.TryGetValue(functionName, out ICallable? function))
        {
            return function;
        }
        else if (BuiltInFunctions.functions.TryGetValue(functionName, out NativeFunction? nativeFunction))
        {
            return nativeFunction;
        }
        else if (parent != null)
        {
            return parent.GetFunction(name);
        }

        Interpreter.ReportError(name, $"Undefined function '{name.Lexeme}'.");
        return null;
    }

    public void AssignVariable(string name, object value)
    {
        if (variables.ContainsKey(name))
        {
            variables[name] = value;
            return;
        }
        else if (parent != null)
        {
            parent.AssignVariable(name, value);
            return;
        }

        throw new ArgumentException($"Undefined variable '{name}'.");
    }

    internal void AssignVariable(Token name, object? value)
    {
        if (variables.ContainsKey(name.Lexeme))
        {
            variables[name.Lexeme] = value;
            return;
        }
        else if (parent != null)
        {
            parent.AssignVariable(name, value);
            return;
        }

        Interpreter.ReportError(name, $"Undefined variable '{name.Lexeme}'.");
    }

    public object? GetVariableValue(string name)
    {
        if (variables.TryGetValue(name, out object? value))
        {
            return value;
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        throw new ArgumentException(name, $"Undefined variable '{name}'.");
    }

    internal object? GetVariableValue(Token name)
    {
        if (variables.TryGetValue(name.Lexeme, out object? value))
        {
            return value;
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        Interpreter.ReportError(name, $"Undefined variable '{name.Lexeme}'.");
        return null;
    }
}
