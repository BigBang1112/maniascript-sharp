using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class RazorManialinkProcessorTests
{
    private const string RazorSource = """
        @using ManiaScriptSharp
        @using static ManiaScriptSharp.ManiaScript
        @namespace Example
        @inherits CManiaApp
        @implements IContext

        <manialink version="3">
            <label id="LabelHello" text="@Title" data-note="Hello @@ world" />
        </manialink>

        @code {
            private string Title = "Hello";

            public void Main()
            {
                var layer = UILayerCreate();
                layer.ManialinkPage = Render();
            }

            public class PageScript : CMlScriptIngame, IContext
            {
                [ManialinkControl]
                public required CMlLabel LabelHello;

                public void Main()
                {
                    LabelHello.Value = "Ready";
                }
            }
        }
        """;

    [Fact]
    public void Process_ExtractsOuterContextNestedScriptAndMarkupExpressions()
    {
        var result = RazorManialinkProcessor.Process("HelloPage.razor", RazorSource);

        Assert.Equal("HelloPage", result.ClassName);
        Assert.Contains("<manialink version=\"3\">", result.XmlTemplate);
        Assert.Contains("Hello @ world", result.XmlTemplate);
        Assert.Contains("__MSS_RAZOR_EXPRESSION_0__", result.XmlTemplate);
        Assert.Equal("Title", Assert.Single(result.Expressions));
        Assert.DoesNotContain("@code", result.XmlTemplate);

        var root = CSharpSyntaxTree.ParseText(result.CSharpSource).GetCompilationUnitRoot();
        Assert.Equal("Example", root.Members.OfType<FileScopedNamespaceDeclarationSyntax>().Single().Name.ToString());
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
        Assert.Equal("HelloPage", classes[0].Identifier.ValueText);
        Assert.Equal("CManiaApp, IContext", classes[0].BaseList!.Types.ToString());
        Assert.Contains(classes, static c => c.Identifier.ValueText == "PageScript");
        Assert.DoesNotContain(classes[0].Members, static m =>
            m is MethodDeclarationSyntax { Identifier.ValueText: "BuildRenderTree" });
    }

    [Fact]
    public void AddRenderMethod_CreatesRawInterpolatedMarkupMethod()
    {
        var document = RazorManialinkProcessor.Process("HelloPage.razor", RazorSource);
        var source = RazorManialinkProcessor.AddRenderMethod(document, document.XmlTemplate);
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

        var render = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(static m => m.Identifier.ValueText == "Render");
        var interpolated = Assert.IsType<InterpolatedStringExpressionSyntax>(
            Assert.IsType<ArrowExpressionClauseSyntax>(render.ExpressionBody).Expression);
        Assert.Contains(interpolated.Contents, static c =>
            c is InterpolationSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "Title" } });
    }

    [Fact]
    public void Process_RequiresOuterContextDirectives()
    {
        const string source = "<manialink version=\"3\" />";

        var exception = Assert.Throws<InvalidOperationException>(
            () => RazorManialinkProcessor.Process("NoContext.razor", source));

        Assert.Contains("@inherits", exception.Message);
    }

    [Fact]
    public void Process_RejectsMarkupControlFlow()
    {
        const string source = """
            @using ManiaScriptSharp
            @inherits CManiaApp
            @implements IContext
            <manialink version="3">
                @if (ShowLabel) { <label text="Hello" /> }
            </manialink>
            @code { private bool ShowLabel = true; public void Main() { } }
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => RazorManialinkProcessor.Process("DynamicPage.razor", source));

        Assert.Contains("control-flow", exception.Message);
    }
}
