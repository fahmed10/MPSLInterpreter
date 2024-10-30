namespace MPSLInterpreter;

public abstract record class Expression
{
    public interface IVisitor<T>
    {
        T VisitBinary(Binary expression);
        T VisitUnary(Unary expression);
        T VisitLiteral(Literal expression);
        T VisitGrouping(Grouping expression);
        T VisitVariable(Variable expression);
        T VisitVariableDeclaration(VariableDeclaration expression);
        T VisitAssign(Assign expression);
        T VisitCall(Call expression);
        T VisitMatch(Match expression);
        T VisitContextValue(ContextValue expression);
        T VisitBlock(Block expression);
        T VisitArray(Array expression);
        T VisitAccess(Access expression);
        T VisitPush(Push expression);
        T VisitInterpolatedString(InterpolatedString expression);
    }

    public record Binary(Token operatorToken, Expression left, Expression right) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBinary(this);
        public override string ToString() => $"({operatorToken.Lexeme} {left} {right})";
    }
    public record Unary(Token operatorToken, Expression right) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitUnary(this);
        public override string ToString() => $"({operatorToken.Lexeme} {right})";
    }
    public record Literal(object? value) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitLiteral(this);
        public override string ToString() => $"<{value}>";
    }
    public record Grouping(Expression expression) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitGrouping(this);
        public override string ToString() => $"({expression})";
    }
    public record Variable(Token name) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitVariable(this);
    }
    public record VariableDeclaration(Token name) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitVariableDeclaration(this);
    }
    public record Assign(Expression target, Expression value) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitAssign(this);
        public override string ToString() => $"({value} -> {target})";
    }
    public record Call(Token callee, IList<Expression> arguments) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitCall(this);
        public override string ToString() => $"({callee.Lexeme} {arguments.Select(e => e.ToString()).Aggregate((a, b) => $"{a}, {b}")})";
    }
    public record Match(Expression value, IList<(Expression condition, Block body)> statements, Block? elseBlock) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitMatch(this);
    }
    public record ContextValue() : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitContextValue(this);
    }
    public record Block(IList<Statement> statements) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBlock(this);
    }
    public record Array(Token start, IList<(Expression expression, bool spread)> items) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitArray(this);
    }
    public record InterpolatedString(IList<Expression> expressions) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitInterpolatedString(this);
    }
    public record Access(Expression expression, Expression indexExpression, Token start) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitAccess(this);
    }
    public record Push(Token target, Expression value) : Expression
    {
        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitPush(this);
    }

    public abstract T Accept<T>(IVisitor<T> visitor);
}
