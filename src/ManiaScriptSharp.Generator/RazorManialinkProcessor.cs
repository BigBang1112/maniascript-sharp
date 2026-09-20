using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

/// <summary>
/// Parses a component-style Razor file into an outer ManiaApp context and a dynamic Manialink
/// template. Nested <c>IContext</c> classes are left in the outer class so the generator can emit
/// one of them into the template's script element.
/// </summary>
internal static class RazorManialinkProcessor
{
    private const string ExpressionTokenPrefix = "__MSS_RAZOR_EXPRESSION_";

    private static readonly RazorProjectEngine Engine = RazorProjectEngine.Create(
        RazorConfiguration.Default,
        RazorProjectFileSystem.Create("/"),
        static builder =>
        {
            NamespaceDirective.Register(builder);
            InheritsDirective.Register(builder);
            ComponentCodeDirective.Register(builder);
            builder.AddDirective(DirectiveDescriptor.CreateDirective(
                "implements",
                DirectiveKind.SingleLine,
                static directive => directive.AddTypeToken()));
        });

    public static RazorManialinkDocument Process(string path, string source)
    {
        var sourceDocument = RazorSourceDocument.Create(source, path, Encoding.UTF8);
        var codeDocument = Engine.Process(
            sourceDocument,
            FileKinds.Component,
            Array.Empty<RazorSourceDocument>(),
            Array.Empty<TagHelperDescriptor>());
        var csharpDocument = codeDocument.GetCSharpDocument();

        if (csharpDocument.Diagnostics.Count > 0)
        {
            var errors = string.Join("; ", csharpDocument.Diagnostics.Select(static d => d.ToString()));
            throw new InvalidOperationException($"Razor parsing failed: {errors}");
        }

        var generatedRoot = CSharpSyntaxTree.ParseText(csharpDocument.GeneratedCode).GetCompilationUnitRoot();
        var razorClass = generatedRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(static c => c.Members.OfType<MethodDeclarationSyntax>()
                .Any(static m => m.Identifier.ValueText == "BuildRenderTree"));
        if (razorClass is null)
            throw new InvalidOperationException("Razor did not generate a page class for the template.");

        var baseType = ReadDirective(source, "inherits");
        var interfaces = ReadDirectives(source, "implements").ToArray();
        if (string.IsNullOrWhiteSpace(baseType))
            throw new InvalidOperationException("The Razor page must declare its ManiaScript context with @inherits.");
        if (!interfaces.Any(static i =>
                i.Equals("IContext", StringComparison.Ordinal)
                || i.EndsWith(".IContext", StringComparison.Ordinal)))
            throw new InvalidOperationException("The Razor page must declare @implements ManiaScriptSharp.IContext.");

        var className = Path.GetFileNameWithoutExtension(path);
        if (!SyntaxFacts.IsValidIdentifier(className))
            throw new InvalidOperationException(
                $"Razor filename '{Path.GetFileName(path)}' does not produce a valid C# class name.");

        var buildRenderTree = razorClass.Members.OfType<MethodDeclarationSyntax>()
            .Single(static m => m.Identifier.ValueText == "BuildRenderTree");
        var (xml, expressions) = ExtractMarkup(buildRenderTree);
        if (string.IsNullOrWhiteSpace(xml))
            throw new InvalidOperationException("The Razor file does not contain Manialink XML markup.");

        var baseTypes = new[] { baseType! }
            .Concat(interfaces)
            .Select(static name => SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(name)));
        var userMembers = razorClass.Members
            .Where(static member => member is not MethodDeclarationSyntax { Identifier.ValueText: "BuildRenderTree" });
        var contextClass = SyntaxFactory.ClassDeclaration(className)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SeparatedList<BaseTypeSyntax>(baseTypes)))
            .WithMembers(SyntaxFactory.List(userMembers));

        var usings = generatedRoot.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(static u => !u.Name!.ToString().StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal))
            .Select(static u => u.WithoutTrivia().ToFullString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new RazorManialinkDocument(
            BuildClassSource(usings, ReadDirective(source, "namespace"), contextClass),
            xml,
            expressions,
            className);
    }

    internal static string AddRenderMethod(RazorManialinkDocument document, string mergedXml)
    {
        var renderedXml = mergedXml;
        for (var i = 0; i < document.Expressions.Count; i++)
            renderedXml = renderedXml.Replace(
                ExpressionToken(i),
                InterpolationPlaceholder(i));

        var maxBraceRun = MaxRun(renderedXml, '{');
        var dollarCount = Math.Max(2, maxBraceRun + 1);
        for (var i = 0; i < document.Expressions.Count; i++)
            renderedXml = renderedXml.Replace(
                InterpolationPlaceholder(i),
                new string('{', dollarCount) + document.Expressions[i] + new string('}', dollarCount));

        var quoteCount = Math.Max(3, MaxRun(renderedXml, '"') + 1);
        var dollars = new string('$', dollarCount);
        var quotes = new string('"', quoteCount);
        var renderMethod = CSharpSyntaxTree.ParseText(
                $"class C {{ public string Render() => {dollars}{quotes}\n{renderedXml}\n{quotes}; }}")
            .GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();

        var root = CSharpSyntaxTree.ParseText(document.CSharpSource).GetCompilationUnitRoot();
        var outerClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == document.ClassName);
        var updatedRoot = root.ReplaceNode(outerClass, outerClass.AddMembers(renderMethod));
        return updatedRoot.ToFullString();
    }

    private static string BuildClassSource(
        IReadOnlyList<string> usings,
        string? namespaceName,
        ClassDeclarationSyntax contextClass)
    {
        var source = new StringBuilder();
        foreach (var usingDirective in usings)
            source.AppendLine(usingDirective);

        if (!string.IsNullOrWhiteSpace(namespaceName))
            source.Append("namespace ").Append(namespaceName).AppendLine(";").AppendLine();

        source.Append(contextClass.NormalizeWhitespace().ToFullString());
        return source.ToString();
    }

    private static (string Xml, IReadOnlyList<string> Expressions) ExtractMarkup(
        MethodDeclarationSyntax buildRenderTree)
    {
        if (buildRenderTree.DescendantNodes().Any(static node =>
                node is IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax
                    or WhileStatementSyntax or SwitchStatementSyntax))
            throw new InvalidOperationException(
                "Razor control-flow blocks in Manialink markup are not supported; use value expressions only.");

        var xml = new StringBuilder();
        var expressions = new List<string>();
        var elements = new Stack<ElementState>();

        void CloseStartTag()
        {
            if (elements.Count == 0 || !elements.Peek().StartTagOpen) return;
            xml.Append('>');
            elements.Peek().StartTagOpen = false;
        }

        string AddExpression(ExpressionSyntax expression)
        {
            var index = expressions.Count;
            expressions.Add(expression.WithoutTrivia().ToFullString());
            return ExpressionToken(index);
        }

        foreach (var invocation in buildRenderTree.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                continue;

            var arguments = invocation.ArgumentList.Arguments;
            switch (memberAccess.Name.Identifier.ValueText)
            {
                case "AddMarkupContent" when arguments.Count >= 2:
                    CloseStartTag();
                    xml.Append(StringLiteral(arguments[1].Expression));
                    break;

                case "OpenElement" when arguments.Count >= 2:
                    CloseStartTag();
                    var elementName = StringLiteral(arguments[1].Expression);
                    xml.Append('<').Append(elementName);
                    elements.Push(new ElementState(elementName));
                    break;

                case "AddAttribute" when arguments.Count >= 3:
                    if (elements.Count == 0 || !elements.Peek().StartTagOpen)
                        throw new InvalidOperationException("Razor generated an attribute outside an XML start tag.");
                    var attributeName = StringLiteral(arguments[1].Expression);
                    xml.Append(' ').Append(attributeName).Append("=\"");
                    if (arguments[2].Expression is LiteralExpressionSyntax literal)
                        xml.Append(EscapeXml(literal.Token.ValueText));
                    else
                        xml.Append(AddExpression(arguments[2].Expression));
                    xml.Append('"');
                    break;

                case "AddContent" when arguments.Count >= 2:
                    CloseStartTag();
                    if (arguments[1].Expression is LiteralExpressionSyntax contentLiteral)
                        xml.Append(EscapeXml(contentLiteral.Token.ValueText));
                    else
                        xml.Append(AddExpression(arguments[1].Expression));
                    break;

                case "CloseElement":
                    if (elements.Count == 0)
                        throw new InvalidOperationException("Razor generated an unmatched XML closing element.");
                    var element = elements.Pop();
                    if (element.StartTagOpen)
                        xml.Append(" />");
                    else
                        xml.Append("</").Append(element.Name).Append('>');
                    break;
            }
        }

        if (elements.Count > 0)
            throw new InvalidOperationException("Razor generated unclosed XML elements.");

        return (xml.ToString().Trim(), expressions);
    }

    private static string StringLiteral(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : throw new InvalidOperationException("Razor generated a non-literal XML name or markup fragment.");

    private static string EscapeXml(string value)
        => value.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");

    private static string? ReadDirective(string source, string name)
        => ReadDirectives(source, name).FirstOrDefault();

    private static IEnumerable<string> ReadDirectives(string source, string name)
    {
        using var reader = new StringReader(source);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            var prefix = "@" + name + " ";
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
                yield return trimmed.Substring(prefix.Length).Trim();
        }
    }

    private static int MaxRun(string value, char character)
    {
        var maximum = 0;
        var current = 0;
        foreach (var c in value)
        {
            current = c == character ? current + 1 : 0;
            maximum = Math.Max(maximum, current);
        }
        return maximum;
    }

    private static string ExpressionToken(int index) => ExpressionTokenPrefix + index + "__";
    private static string InterpolationPlaceholder(int index) => "__MSS_INTERPOLATION_" + index + "__";

    private sealed class ElementState
    {
        public string Name { get; }
        public bool StartTagOpen { get; set; } = true;

        public ElementState(string name) => Name = name;
    }
}

internal sealed class RazorManialinkDocument
{
    public string CSharpSource { get; }
    public string XmlTemplate { get; }
    public IReadOnlyList<string> Expressions { get; }
    public string ClassName { get; }

    public RazorManialinkDocument(
        string csharpSource,
        string xmlTemplate,
        IReadOnlyList<string> expressions,
        string className)
    {
        CSharpSource = csharpSource;
        XmlTemplate = xmlTemplate;
        Expressions = expressions;
        ClassName = className;
    }
}
