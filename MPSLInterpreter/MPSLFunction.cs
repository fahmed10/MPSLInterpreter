namespace MPSLInterpreter;

internal record class MPSLFunction(int ArgumentCount, IList<Token> Parameters, Expression.Block Block, MPSLEnvironment Closure) : ICallable
{
    public object? Call(Interpreter interpreter, object?[] args)
    {
        MPSLEnvironment blockEnvironment = new(Closure);

        for (int i = 0; i < ArgumentCount; i++)
        {
            blockEnvironment.DefineVariable(Parameters[i], args[i], false);
        }

        object? value = interpreter.InterpretBlock(Block, blockEnvironment);
        interpreter.breakCalled = false;
        return value;
    }
}
