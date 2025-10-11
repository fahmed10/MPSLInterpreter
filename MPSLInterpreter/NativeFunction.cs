namespace MPSLInterpreter;

public record class NativeFunction(int ArgumentCount, Delegate Function) : ICallable
{
    object? ICallable.Call(Interpreter interpreter, object?[] args) => Function.DynamicInvoke(args);
}
