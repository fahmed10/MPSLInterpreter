namespace MPSLInterpreter;

public interface INode
{
    public abstract int Start { get; }
    public abstract int End { get; }
}