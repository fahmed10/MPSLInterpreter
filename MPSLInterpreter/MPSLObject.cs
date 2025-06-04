namespace MPSLInterpreter;

public class MPSLObject : Dictionary<object, object?>
{
    public MPSLObject() { }

    public override string ToString()
    {
        return $"({string.Join(", ", this.Select(p => $"{Interpreter.ToMPSLDebugString(p.Key)}: {Interpreter.ToMPSLDebugString(p.Value)}"))})";
    }
}
