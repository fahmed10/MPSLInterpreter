namespace MPSLInterpreter;

/// <summary>
/// Stores variables and functions that were declared in its block in MPSL code.
/// </summary>
public class MPSLEnvironment
{
    /// <summary>
    /// The parent of this environment.
    /// </summary>
    public MPSLEnvironment? parent;
    Dictionary<string, object?> variables = new();
    Dictionary<string, ICallable> functions = new();
    internal object? contextValue = Invalid.Value;

    /// <summary>
    /// Initializes a new instance of the <c>MPSLEnvironment</c> class.
    /// </summary>
    /// <param name="parent">The parent of this environment.</param>
    public MPSLEnvironment(MPSLEnvironment? parent = null)
    {
        this.parent = parent;
    }

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

        if (functions.ContainsKey(functionName))
        {
            return functions[functionName];
        }
        else if (BuiltInFunctions.functions.ContainsKey(functionName))
        {
            return BuiltInFunctions.functions[functionName];
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
        if (variables.ContainsKey(name))
        {
            return variables[name];
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        throw new ArgumentException(name, $"Undefined variable '{name}'.");
    }

    internal object? GetVariableValue(Token name)
    {
        if (variables.ContainsKey(name.Lexeme))
        {
            return variables[name.Lexeme];
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        Interpreter.ReportError(name, $"Undefined variable '{name.Lexeme}'.");
        return null;
    }
}
