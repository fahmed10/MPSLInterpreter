namespace MPSLInterpreter;

public abstract record class Statement : INode
{
    public abstract int Start { get; }
    public abstract int End { get; }
    public abstract Token FirstToken { get; }

    public interface IVisitor<T>
    {
        T VisitBreak(Break statement);
        T VisitExpressionStatement(ExpressionStatement statement);
        T VisitIf(If statement);
        T VisitWhile(While statement);
        T VisitEach(Each statement);
        T VisitFunctionDeclaration(FunctionDeclaration statement);
        T VisitUse(Use statement);
        T VisitGroup(Group statement);
        T VisitPublic(Public statement);
    }

    public interface IVisitor
    {
        void VisitBreak(Break statement) { }
        void VisitExpressionStatement(ExpressionStatement statement) { }
        void VisitIf(If statement) { }
        void VisitWhile(While statement) { }
        void VisitEach(Each statement) { }
        void VisitFunctionDeclaration(FunctionDeclaration statement) { }
        void VisitUse(Use statement) { }
        void VisitGroup(Group statement) { }
        void VisitPublic(Public statement) { }
    }

    public record ExpressionStatement(Expression expression) : Statement
    {
        public override int Start => expression.Start;
        public override int End => expression.End;
        public override Token FirstToken => expression.FirstToken;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitExpressionStatement(this);
        public override void Accept(IVisitor visitor) => visitor.VisitExpressionStatement(this);
        public override string ToString() => $"{expression};";
    }
    public record If(Token start, IList<(Expression condition, Expression.Block body)> statements, Expression.Block? elseBlock) : Statement
    {
        public override int Start => start.Start;
        public override int End => elseBlock?.End ?? statements.Last().body.End;
        public override Token FirstToken => start;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitIf(this);
        public override void Accept(IVisitor visitor) => visitor.VisitIf(this);
    }
    public record While(Token start, Expression condition, Expression.Block body) : Statement
    {
        public override int Start => start.Start;
        public override int End => body.End;
        public override Token FirstToken => start;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitWhile(this);
        public override void Accept(IVisitor visitor) => visitor.VisitWhile(this);
    }
    public record Each(Token start, Token variableName, Expression collection, Expression.Block body) : Statement
    {
        public override int Start => start.Start;
        public override int End => body.End;
        public override Token FirstToken => start;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitEach(this);
        public override void Accept(IVisitor visitor) => visitor.VisitEach(this);
    }
    public record FunctionDeclaration(Token start, Token name, IList<Token> parameters, Expression.Block body) : Statement
    {
        public override int Start => start.Start;
        public override int End => body.End;
        public override Token FirstToken => start;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitFunctionDeclaration(this);
        public override void Accept(IVisitor visitor) => visitor.VisitFunctionDeclaration(this);
    }
    public record Break(Token keyword) : Statement
    {
        public override int Start => keyword.Start;
        public override int End => keyword.End;
        public override Token FirstToken => keyword;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBreak(this);
        public override void Accept(IVisitor visitor) => visitor.VisitBreak(this);
    }
    public record Use(Token start, Token target) : Statement
    {
        public override int Start => start.Start;
        public override int End => target.End;
        public override Token FirstToken => start;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitUse(this);
        public override void Accept(IVisitor visitor) => visitor.VisitUse(this);
    }
    public record Group(Token start, Token name, Expression.Block body) : Statement
    {
        public override int Start => start.Start;
        public override int End => body.End;
        public override Token FirstToken => start;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitGroup(this);
        public override void Accept(IVisitor visitor) => visitor.VisitGroup(this);
    }
    public record Public(Token start, Statement statement) : Statement
    {
        public override int Start => start.Start;
        public override int End => statement.End;
        public override Token FirstToken => start;

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitPublic(this);
        public override void Accept(IVisitor visitor) => visitor.VisitPublic(this);
    }

    public abstract T Accept<T>(IVisitor<T> visitor);
    public abstract void Accept(IVisitor visitor);
}
