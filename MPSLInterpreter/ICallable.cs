namespace MPSLInterpreter;

internal interface ICallable
{
    public int ArgumentCount { get; }
    internal object? Call(Interpreter interpreter, params object?[] args);
}
