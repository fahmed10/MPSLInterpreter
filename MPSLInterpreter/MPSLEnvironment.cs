using System.Collections.Immutable;

namespace MPSLInterpreter;

/// <summary>
/// Stores variables, functions, and groups that were declared in its scope in MPSL code.
/// </summary>
/// <param name="parent">The parent of this environment.</param>
public class MPSLEnvironment(MPSLEnvironment? parent = null)
{
    record struct Item<T>(bool Public, bool Visible, T Value);

    public ImmutableList<string> Variables => [.. variables.Keys];
    public ImmutableList<string> Functions => [.. functions.Keys.Select(name => "@" + name)];
    public ImmutableList<string> Groups => [.. groups.Keys];
    readonly Dictionary<string, Item<(object? value, int position)>> variables = [];
    readonly Dictionary<string, Item<ICallable>> functions = [];
    readonly Dictionary<string, Item<MPSLGroup>> groups = [];
    internal object? contextValue = Invalid.Value;

    private MPSLEnvironment(MPSLEnvironment environment, MPSLEnvironment? parent) : this(parent)
    {
        variables = new(environment.variables);
        functions = new(environment.functions);
        groups = new(environment.groups);
        contextValue = environment.contextValue;
    }

    internal void AddEnvironment(Token useToken, MPSLEnvironment other)
    {
        foreach (var pair in other.variables)
        {
            if (!variables.TryAdd(pair.Key, new(false, pair.Value.Public, (pair.Value.Value.value, -1))))
            {
                Interpreter.ReportError(useToken, $"Variable '{pair.Key}' has already been defined.");
            }
        }

        foreach (var pair in other.functions)
        {
            if (!functions.TryAdd(pair.Key, new(false, pair.Value.Public, pair.Value.Value)))
            {
                Interpreter.ReportError(useToken, $"Function '{pair.Key}' has already been defined.");
            }
        }

        foreach (var pair in other.groups)
        {
            if (!groups.TryAdd(pair.Key, new(false, pair.Value.Public, PrepareGroupForUse(pair.Value.Value))))
            {
                Interpreter.ReportError(useToken, $"Group '{pair.Key}' has already been defined.");
            }
        }
    }

    private MPSLGroup PrepareGroupForUse(MPSLGroup group)
    {
        MPSLEnvironment env = group.Environment.DeepCopy();

        foreach (var pair in env.variables)
        {
            env.variables[pair.Key] = new(pair.Value.Public, pair.Value.Public, (pair.Value.Value.value, -1));
        }

        foreach (var pair in env.functions)
        {
            env.functions[pair.Key] = new(pair.Value.Public, pair.Value.Public, pair.Value.Value);
        }

        foreach (var pair in env.groups)
        {
            env.groups[pair.Key] = new(pair.Value.Public, pair.Value.Public, PrepareGroupForUse(pair.Value.Value));
        }

        return new(env);
    }

    public void DefineVariable(string name, object? value)
    {
        if (variables.ContainsKey(name))
        {
            throw new ArgumentException($"Variable '{name}' has already been defined in this environment.");
        }

        variables[name] = new(true, true, (value, -1));
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

        variables[token.Lexeme] = new(@public, true, (value, token.Start));
    }

    public void DefineFunction(string name, NativeFunction function)
    {
        if (functions.ContainsKey(name))
        {
            throw new ArgumentException($"Function '{name}' has already been defined.");
        }

        functions[name] = new(true, true, function);
    }

    internal void DefineFunction(Token token, ICallable function, bool @public)
    {
        if (functions.ContainsKey((string)token.Value!))
        {
            Interpreter.ReportError(token, $"Function '{token.Lexeme}' has already been defined.");
        }

        functions[(string)token.Value!] = new(@public, true, function);
    }

    internal ICallable GetFunction(Token name)
    {
        string functionName = (string)name.Value!;

        if (functions.TryGetValue(functionName, out Item<ICallable> result))
        {
            if (!result.Visible)
            {
                Interpreter.ReportError(name, $"Function '{name.Lexeme}' is inaccessible as it is not public.");
            }

            return result.Value;
        }
        else if (parent != null)
        {
            return parent.GetFunction(name);
        }
        else if (StdLibrary.GlobalFunctions.functions.TryGetValue(functionName, out NativeFunction? nativeFunction))
        {
            return nativeFunction;
        }

        Interpreter.ReportError(name, $"Undefined function '{name.Lexeme}'.");
        return null;
    }

    public ICallable GetFunction(string name)
    {
        string functionName = name.Length > 0 && name[0] == '@' ? name[1..] : name;

        if (functions.TryGetValue(functionName, out Item<ICallable> result))
        {
            return result.Value;
        }
        else if (parent != null)
        {
            return parent.GetFunction(functionName);
        }
        else if (StdLibrary.GlobalFunctions.functions.TryGetValue(functionName, out NativeFunction? nativeFunction))
        {
            return nativeFunction;
        }

        throw new ArgumentException($"Undefined function '{name}'.");
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

        groups[token.Lexeme] = new(@public, true, group);
    }

    public void DefineGroup(string name, MPSLGroup group)
    {
        if (groups.ContainsKey(name))
        {
            throw new ArgumentException($"Group '{name}' has already been defined.");
        }
        else if (variables.ContainsKey(name))
        {
            throw new ArgumentException($"Cannot define a group with the same name as the variable '{name}'.");
        }

        groups[name] = new(true, true, group);
    }

    public void AssignVariable(string name, object value)
    {
        if (variables.TryGetValue(name, out Item<(object? value, int position)> result))
        {
            variables[name] = new(result.Public, result.Visible, (value, result.Value.position));
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
        if (variables.TryGetValue(name.Lexeme, out Item<(object? value, int position)> result))
        {
            if (!result.Visible)
            {
                Interpreter.ReportError(name, $"Variable '{name.Lexeme}' is inaccessible as it is not public.");
            }

            variables[name.Lexeme] = new(result.Public, result.Visible, (value, result.Value.position));
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
        if (variables.TryGetValue(name, out Item<(object? value, int position)> result))
        {
            return result.Value.value;
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        throw new ArgumentException(name, $"Undefined variable '{name}'.");
    }

    internal object? GetVariableValue(Token name)
    {
        if (variables.TryGetValue(name.Lexeme, out Item<(object? value, int position)> result) && result.Value.position < name.Start)
        {
            if (!result.Visible)
            {
                Interpreter.ReportError(name, $"Variable '{name.Lexeme}' is inaccessible as it is not public.");
            }

            return result.Value.value;
        }
        else if (parent != null)
        {
            return parent.GetVariableValue(name);
        }

        Interpreter.ReportError(name, $"Undefined variable '{name.Lexeme}'.");
        return null;
    }

    internal MPSLGroup GetGroup(Token name)
    {
        if (groups.TryGetValue(name.Lexeme, out Item<MPSLGroup> result))
        {
            if (!result.Visible)
            {
                Interpreter.ReportError(name, $"Group '{name.Lexeme}' is inaccessible as it is not public.");
            }

            return result.Value;
        }
        else if (parent != null)
        {
            return parent.GetGroup(name);
        }

        Interpreter.ReportError(name, $"Undefined group '{name.Lexeme}'.");
        return null;
    }

    public MPSLGroup GetGroup(string name)
    {
        if (groups.TryGetValue(name, out Item<MPSLGroup> result))
        {
            return result.Value;
        }
        else if (parent != null)
        {
            return parent.GetGroup(name);
        }

        throw new ArgumentException($"Undefined group '{name}'.");
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

    internal MPSLEnvironment DeepCopy() => new(this, parent);
}
