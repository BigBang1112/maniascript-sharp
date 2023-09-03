using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class SwitchStatementWriter : StatementWriter<SwitchStatementSyntax>
{
    public override void Write(SwitchStatementSyntax statement)
    {
        var hasPatternSwitchLabel = statement.Sections
            .SelectMany(x => x.Labels)
            .Any(x => x is CasePatternSwitchLabelSyntax);

        if (hasPatternSwitchLabel)
        {
            // use if statement
            WriteIf(statement);
            return;
        }

        WriteSwitch(statement);
    }

    private void WriteIf(SwitchStatementSyntax statement)
    {
        var first = true;

        foreach (var section in statement.Sections)
        {
            Writer.WriteIndent(Indent);

            if (first)
            {
                first = false;
            }
            else
            {
                Writer.Write("else ");
            }

            Writer.Write("if (");
            WriteSyntax(statement.Expression);

            var firstLabel = true;

            foreach (var label in section.Labels)
            {
                if (firstLabel)
                {
                    firstLabel = false;
                }
                else
                {
                    Writer.Write(" || ");
                }

                switch (label)
                {
                    case CaseSwitchLabelSyntax caseSwitchLabel:
                        Writer.Write(" == ");
                        WriteSyntax(caseSwitchLabel.Value);
                        break;
                    case CasePatternSwitchLabelSyntax casePatternSwitchLabel:
                        if (casePatternSwitchLabel.Pattern is RelationalPatternSyntax relational)
                        {
                            Writer.Write(' ');
                            Writer.Write(relational.OperatorToken.Text);
                            Writer.Write(' ');
                            WriteSyntax(relational.Expression);
                        }
                        else
                        {
                            Writer.Write($"/* CasePatternSwitchLabelSyntax {casePatternSwitchLabel.Pattern.GetType().Name} */");
                        }

                        break;
                    case DefaultSwitchLabelSyntax:
                        // Handled at different place
                        break;
                    default:
                        Writer.Write("/* ");
                        Writer.Write(label.GetType().Name);
                        Writer.Write(" */");
                        break;
                }
            }

            Writer.WriteLine(") {");

            var hasBreakAtEnd = section.Statements.LastOrDefault(x => x is BreakStatementSyntax) is not null;

            foreach (var sectionStatement in section.Statements.Take(section.Statements.Count - (hasBreakAtEnd ? 1 : 0)))
            {
                WriteSyntax(sectionStatement, indentOffset: 1);
            }

            Writer.WriteLine(Indent, "}");
        }
    }

    private void WriteSwitch(SwitchStatementSyntax statement)
    {
        Writer.Write(Indent, "switch (");
        WriteSyntax(statement.Expression);
        Writer.WriteLine(") {");

        foreach (var section in statement.Sections)
        {
            foreach (var label in section.Labels)
            {
                switch (label)
                {
                    case CaseSwitchLabelSyntax caseSwitchLabel:
                        Writer.Write(Indent + 1, "case ");
                        WriteSyntax(caseSwitchLabel.Value);
                        break;
                    case DefaultSwitchLabelSyntax defaultSwitchLabel:
                        Writer.Write(Indent + 1, "default");
                        break;
                    default:
                        Writer.Write("/* ");
                        Writer.Write(label.GetType().Name);
                        Writer.Write(" */");
                        break;
                }

                Writer.WriteLine(": {");
            }

            var hasBreakAtEnd = section.Statements.LastOrDefault(x => x is BreakStatementSyntax) is not null;

            foreach (var sectionStatement in section.Statements.Take(section.Statements.Count - (hasBreakAtEnd ? 1 : 0)))
            {
                WriteSyntax(sectionStatement, indentOffset: 2);
            }

            Writer.WriteLine(Indent + 1, "}");
        }

        Writer.WriteLine(Indent, "}");
    }
}
