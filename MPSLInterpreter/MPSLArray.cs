namespace MPSLInterpreter;

public class MPSLArray : List<object?>
{
    public MPSLArray() { }
    public MPSLArray(int capacity) : base(capacity) { }
    public MPSLArray(IEnumerable<object?> items) : base(items) { }

    public override string ToString()
    {
        return $"[{this.Select(Interpreter.ToMPSLString).Aggregate((a, b) => $"{a}, {b}")}]";
    }
}
