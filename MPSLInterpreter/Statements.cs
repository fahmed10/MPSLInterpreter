namespace MPSLInterpreter;

public abstract record class Statement
{
    public interface IVisitor<T>
    {
        T VisitBreak(Break statement);
        T VisitExpressionStatement(ExpressionStatement statement);
        T VisitIf(If statement);
        T VisitWhile(While statement);
        T VisitEach(Each statement);
        T VisitFunctionDeclaration(FunctionDeclaration statement);
        T VisitUse(Use statement);
    }

    public record ExpressionStatement(Expression expression) : Statement
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitExpressionStatement(this);
        public override string ToString() => $"{expression};";
    }
    public record If(IList<(Expression condition, Expression.Block body)> statements, Expression.Block? elseBlock) : Statement
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitIf(this);
    }
    public record While(Expression condition, Expression.Block body) : Statement
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitWhile(this);
    }
    public record Each(Token variableName, Expression collection, Expression.Block body) : Statement
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitEach(this);
    }
    public record FunctionDeclaration(Token name, IList<Token> parameters, Expression.Block body) : Statement
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitFunctionDeclaration(this);
    }
    public record Break(Token keyword) : Statement
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBreak(this);
    }
    public record Use(Token path) : Statement
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitUse(this);
    }

    public abstract T Accept<T>(IVisitor<T> visitor);
}
