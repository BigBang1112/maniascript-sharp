using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Statements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Expressions;

public abstract class ExpressionWriter : SyntaxWriter
{
    protected ExpressionWriterUtils? Utils { get; private set; }

    protected int Indent => Utils?.Indent ?? throw new InvalidOperationException();
    protected TextWriter Writer => Utils?.Writer ?? throw new InvalidOperationException();
    protected ImmutableArray<ParameterSyntax> Parameters => Utils?.Parameters ?? throw new InvalidOperationException();
    public override ManiaScriptBodyBuilder BodyBuilder => Utils?.BodyBuilder ?? throw new InvalidOperationException();

    public static bool WriteSyntax(ExpressionWriterUtils utils)
    {
        var expressionWriter = utils.GetExpressionWriter();
        var w = utils.Writer;

        if (expressionWriter is null)
        {
            w.Write("/* ");
            w.Write(utils.Expression.GetType().Name);
            w.Write("[");
            w.Write(utils.Expression);
            w.Write("] */");

            return false;
        }
        
        expressionWriter.Utils = utils;
        
        try
        {
            expressionWriter.Write();
        }
        catch (ExpressionException ex)
        {
            w.Write(ex.Message);
        }

        return true;
    }

    protected void WriteSyntax(ExpressionSyntax expression)
    {
        WriteSyntax(new ExpressionWriterUtils(Indent, expression, Parameters, BodyBuilder));
    }

    protected void WriteSyntax(StatementSyntax statement)
    {
        StatementWriter.WriteSyntax(new StatementWriterUtils(Indent, statement, Parameters, BodyBuilder));
    }

    protected ISymbol? GetSymbol()
    {
        return GetSymbol(Utils?.Expression ?? throw new InvalidOperationException());
    }
}