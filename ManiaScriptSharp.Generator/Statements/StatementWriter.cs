using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public abstract class StatementWriter : SyntaxWriter
{
    protected StatementWriterUtils? Utils { get; private set; }

    protected int Ident => Utils?.Ident ?? throw new InvalidOperationException();
    protected TextWriter Writer => Utils?.Writer ?? throw new InvalidOperationException();
    protected ImmutableArray<ParameterSyntax> Parameters => Utils?.Parameters ?? throw new InvalidOperationException();
    protected override ManiaScriptBodyBuilder BodyBuilder => Utils?.BodyBuilder ?? throw new InvalidOperationException();

    public static void WriteSyntax(StatementWriterUtils utils)
    {        
        var statementWriter = utils.GetStatementWriter();
        var w = utils.Writer;

        if (statementWriter is null)
        {
            w.Write(utils.Ident, "/* ");
            w.Write(utils.Statement.GetType().Name);
            w.WriteLine(" */");
        }
        else
        {
            statementWriter.Utils = utils;
            statementWriter.Write();
        }
    }

    protected void WriteSyntax(StatementSyntax statement, int identOffset = 0)
    {
        WriteSyntax(new StatementWriterUtils(Ident + identOffset, statement, Parameters, BodyBuilder));
    }

    protected bool WriteSyntax(ExpressionSyntax expression)
    {
        return ExpressionWriter.WriteSyntax(new ExpressionWriterUtils(Ident, expression, Parameters, BodyBuilder));
    }

    protected void WriteLocationComment()
    {
        if (Utils is null)
        {
            throw new InvalidOperationException();
        }

        Writer.Write("/* ");

        var loc = Utils.Statement.GetLocation();
        var line = loc.GetLineSpan();
        Writer.Write('[');
        Writer.Write(line.StartLinePosition.Line + 1);
        Writer.Write(',');
        Writer.Write(line.StartLinePosition.Character + 1);
        Writer.Write("] */");
    }

    protected ISymbol? GetSymbol()
    {
        return GetSymbol(Utils?.Statement ?? throw new InvalidOperationException());
    }
}