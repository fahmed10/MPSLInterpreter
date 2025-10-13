using System.Collections.Immutable;

namespace MPSLInterpreter;

public record class NativeFunction(int ArgumentCount, Delegate Function) : ICallable
{
    public ImmutableList<string> ParameterNames => parameterNames;
    readonly ImmutableList<string> parameterNames = [.. Function.Method.GetParameters().Select(p => p.Name!)];

    object? ICallable.Call(Interpreter interpreter, object?[] args) => Function.DynamicInvoke(args);
}
