namespace MPSLInterpreter;

internal record class MPSLFunction(int ArgumentCount, IList<Token> parameters, Expression.Block block) : ICallable
{
    public object? Call(Interpreter interpreter, object?[] args)
    {
        MPSLEnvironment previous = interpreter.environment;
        interpreter.environment = new MPSLEnvironment(interpreter.environment);

        for (int i = 0; i < ArgumentCount; i++)
        {
            interpreter.environment.DefineVariable(parameters[i], args[i]);
        }

        object? value = interpreter.InterpretBlock(block, null, null);
        interpreter.breakCalled = false;
        interpreter.environment = previous;
        return value;
    }
}
