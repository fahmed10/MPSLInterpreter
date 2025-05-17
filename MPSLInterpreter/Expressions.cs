namespace MPSLInterpreter;

public abstract record class Expression
{
    public abstract int Start { get; }
    public abstract int End { get; }

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

    public interface IVisitor
    {
        void VisitBinary(Binary expression) { }
        void VisitUnary(Unary expression) { }
        void VisitLiteral(Literal expression) { }
        void VisitGrouping(Grouping expression) { }
        void VisitVariable(Variable expression) { }
        void VisitVariableDeclaration(VariableDeclaration expression) { }
        void VisitAssign(Assign expression) { }
        void VisitCall(Call expression) { }
        void VisitMatch(Match expression) { }
        void VisitContextValue(ContextValue expression) { }
        void VisitBlock(Block expression) { }
        void VisitArray(Array expression) { }
        void VisitAccess(Access expression) { }
        void VisitPush(Push expression) { }
        void VisitInterpolatedString(InterpolatedString expression) { }
    }

    public record Binary(Token operatorToken, Expression left, Expression right) : Expression
    {
        public override int Start => left.Start;
        public override int End => right.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBinary(this);
        public override void Accept(IVisitor visitor) => visitor.VisitBinary(this);
        public override string ToString() => $"({operatorToken.Lexeme} {left} {right})";
    }
    public record Unary(Token operatorToken, Expression right) : Expression
    {
        public override int Start => operatorToken.Start;
        public override int End => right.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitUnary(this);
        public override void Accept(IVisitor visitor) => visitor.VisitUnary(this);
        public override string ToString() => $"({operatorToken.Lexeme} {right})";
    }
    public record Literal(object? value, Token token) : Expression
    {
        public override int Start => token.Start;
        public override int End => token.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitLiteral(this);
        public override void Accept(IVisitor visitor) => visitor.VisitLiteral(this);
        public override string ToString() => $"<{value}>";
    }
    public record Grouping(Expression expression, Token start, Token end) : Expression
    {
        public override int Start => start.Start;
        public override int End => end.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitGrouping(this);
        public override void Accept(IVisitor visitor) => visitor.VisitGrouping(this);
        public override string ToString() => $"({expression})";
    }
    public record Variable(Token name) : Expression
    {
        public override int Start => name.Start;
        public override int End => name.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitVariable(this);
        public override void Accept(IVisitor visitor) => visitor.VisitVariable(this);
    }
    public record VariableDeclaration(Token start, Token name) : Expression
    {
        public override int Start => start.Start;
        public override int End => name.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitVariableDeclaration(this);
        public override void Accept(IVisitor visitor) => visitor.VisitVariableDeclaration(this);
    }
    public record Assign(Expression target, Expression value) : Expression
    {
        public override int Start => value.Start;
        public override int End => target.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitAssign(this);
        public override void Accept(IVisitor visitor) => visitor.VisitAssign(this);
        public override string ToString() => $"({value} -> {target})";
    }
    public record Call(Token callee, IList<Expression> arguments) : Expression
    {
        public override int Start => callee.Start;
        public override int End => arguments.LastOrDefault()?.End ?? callee.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitCall(this);
        public override void Accept(IVisitor visitor) => visitor.VisitCall(this);
        public override string ToString() => $"({callee.Lexeme} {arguments.Select(e => e.ToString()).Aggregate((a, b) => $"{a}, {b}")})";
    }
    public record Match(Expression value, IList<(Expression condition, Block body)> statements, Block? elseBlock, Token start, Token end) : Expression
    {
        public override int Start => start.Start;
        public override int End => end.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitMatch(this);
        public override void Accept(IVisitor visitor) => visitor.VisitMatch(this);
    }
    public record ContextValue(Token token) : Expression
    {
        public override int Start => token.Start;
        public override int End => token.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitContextValue(this);
        public override void Accept(IVisitor visitor) => visitor.VisitContextValue(this);
    }
    public record Block(IList<Statement> statements, int start, int end) : Expression
    {
        public override int Start => start;
        public override int End => end;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBlock(this);
        public override void Accept(IVisitor visitor) => visitor.VisitBlock(this);
    }
    public record Array(Token start, IList<(Expression expression, bool spread)> items, Token end) : Expression
    {
        public override int Start => start.Start;
        public override int End => end.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitArray(this);
        public override void Accept(IVisitor visitor) => visitor.VisitArray(this);
    }
    public record InterpolatedString(IList<Expression> expressions) : Expression
    {
        public override int Start => expressions.First().Start;
        public override int End => expressions.Last().End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitInterpolatedString(this);
        public override void Accept(IVisitor visitor) => visitor.VisitInterpolatedString(this);
    }
    public record Access(Expression expression, Expression indexExpression, Token start, Token end) : Expression
    {
        public override int Start => expression.Start;
        public override int End => end.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitAccess(this);
        public override void Accept(IVisitor visitor) => visitor.VisitAccess(this);
    }
    public record Push(Token target, Expression value, Token end) : Expression
    {
        public override int Start => value.Start;
        public override int End => end.End;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitPush(this);
        public override void Accept(IVisitor visitor) => visitor.VisitPush(this);
    }

    public abstract T Accept<T>(IVisitor<T> visitor);
    public abstract void Accept(IVisitor visitor);
}
