namespace MPSLInterpreter;

/// <summary>
/// Stores variables and functions that were declared in its block in MPSL code.
/// </summary>
/// <param name="parent">The parent of this environment.</param>
public class MPSLEnvironment(MPSLEnvironment? parent = null)
{
    internal readonly Dictionary<string, (bool @public, object? value)> variables = [];
    readonly Dictionary<string, (bool @public, ICallable function)> functions = [];
    readonly Dictionary<string, (bool @public, MPSLGroup group)> groups = [];
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

        variables[name] = (false, value);
    }

    internal void DefineVariable(Token token, object? value, bool @public)
    {
        if (variables.ContainsKey(token.Lexeme))
        {
            Interpreter.ReportError(token, $"Variable '{token.Lexeme}' has already been defined.");
        }
        else if (groups.ContainsKey(token.Lexeme))
        {
            Interpreter.ReportError(token, $"Cannot define a variable with the same name as the group '{token.Lexeme}'.");
        }

        variables[token.Lexeme] = (@public, value);
    }

    public void DefineFunction(string name, NativeFunction function)
    {
        if (functions.ContainsKey(name))
        {
            throw new ArgumentException($"Function '{name}' has already been defined.");
        }

        functions[name] = (false, function);
    }

    internal void DefineFunction(Token token, ICallable function, bool @public)
    {
        if (functions.ContainsKey((string)token.Value!))
        {
            Interpreter.ReportError(token, $"Function '{token.Lexeme}' has already been defined.");
        }

        functions[(string)token.Value!] = (@public, function);
    }

    internal ICallable GetFunction(Token name)
    {
        string functionName = (string)name.Value!;

        if (functions.TryGetValue(functionName, out (bool @public, ICallable function) result))
        {
            return result.function;
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

    internal void DefineGroup(Token token, MPSLGroup group, bool @public)
    {
        if (groups.ContainsKey(token.Lexeme))
        {
            Interpreter.ReportError(token, $"Group '{token.Lexeme}' has already been defined.");
        }
        else if (variables.ContainsKey(token.Lexeme))
        {
            Interpreter.ReportError(token, $"Cannot define a group with the same name as the variable '{token.Lexeme}'.");
        }

        groups[token.Lexeme] = (@public, group);
    }

    public void AssignVariable(string name, object value)
    {
        if (variables.TryGetValue(name, out (bool @public, object? value) result))
        {
            variables[name] = (result.@public, value);
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
        if (variables.TryGetValue(name.Lexeme, out (bool @public, object? value) result))
        {
            variables[name.Lexeme] = (result.@public, value);
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
        if (variables.TryGetValue(name, out (bool @public, object? value) result))
        {
            return result.value;
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        throw new ArgumentException(name, $"Undefined variable '{name}'.");
    }

    internal object? GetVariableValue(Token name)
    {
        if (variables.TryGetValue(name.Lexeme, out (bool @public, object? value) result))
        {
            return result.value;
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        Interpreter.ReportError(name, $"Undefined variable '{name.Lexeme}'.");
        return null;
    }

    internal object? GetGroup(Token name)
    {
        if (groups.TryGetValue(name.Lexeme, out (bool @public, MPSLGroup value) result))
        {
            return result.value;
        }
        else if (parent != null)
        {
            return parent.GetGroup(name);
        }

        Interpreter.ReportError(name, $"Undefined group '{name.Lexeme}'.");
        return null;
    }

    internal object? Get(Token name)
    {
        if (name.Type == TokenType.COMMAND)
        {
            return GetFunction(name);
        }
        if (groups.ContainsKey(name.Lexeme))
        {
            return GetGroup(name);
        }

        return GetVariableValue(name);
    }
}
