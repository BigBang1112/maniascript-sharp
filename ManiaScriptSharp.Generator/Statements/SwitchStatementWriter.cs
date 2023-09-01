using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator.Statements;

public class SwitchStatementWriter : StatementWriter<SwitchStatementSyntax>
{
    public override void Write(SwitchStatementSyntax statement)
    {
        Writer.Write(Indent, "switch (");
        WriteSyntax(statement.Expression);
        Writer.WriteLine(") {");

        foreach (var section in statement.Sections)
        {
            foreach (var label in section.Labels)
            {
                Writer.Write(Indent + 1, "case ");

                switch (label)
                {
                    case CasePatternSwitchLabelSyntax casePatternSwitchLabel:
                        Writer.Write("/* CasePatternSwitchLabelSyntax */");
                        break;
                    case CaseSwitchLabelSyntax caseSwitchLabel:
                        WriteSyntax(caseSwitchLabel.Value);
                        break;
                    case DefaultSwitchLabelSyntax defaultSwitchLabel:
                        Writer.Write("/* DefaultSwitchLabelSyntax */");
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
