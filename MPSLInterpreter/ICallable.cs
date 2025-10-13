using System.Collections.Immutable;

namespace MPSLInterpreter;

public interface ICallable
{
    public int ArgumentCount { get; }
    public ImmutableList<string> ParameterNames { get; }
    
    internal object? Call(Interpreter interpreter, params object?[] args);
}
