using System.Collections.Immutable;
using ManiaScriptSharp.Generator.Statements;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ManiaScriptSharp.Generator;

public class ManiaScriptBodyBuilder
{
    public INamedTypeSymbol ScriptSymbol { get; }
    public SemanticModel SemanticModel { get; }
    public TextWriter Writer { get; }
    public ManiaScriptHead Head { get; }
    public GeneratorHelper Helper { get; }

    public bool IsBuildingEventHandling { get; private set; }
    public bool IsBuildingLoop { get; private set; }
    public Queue<string> BlockLineQueue { get; } = new();
    public Queue<string> AfterBlockLineQueue { get; } = new();

    public ManiaScriptBodyBuilder(
        INamedTypeSymbol scriptSymbol,
        SemanticModel semanticModel,
        TextWriter writer,
        ManiaScriptHead head,
        GeneratorHelper helper)
    {
        ScriptSymbol = scriptSymbol;
        SemanticModel = semanticModel;
        Writer = writer;
        Head = head;
        Helper = helper;
    }

    public ManiaScriptBody AnalyzeAndBuild()
    {
        WriteSettingsChangeDetectors(Head.Settings);

        var methods = ScriptSymbol.GetMembers()
            .OfType<IMethodSymbol>();

        var hasContextSymbol = ScriptSymbol.Interfaces.Any(i => i.Name == "IContext");

        var functionsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
        
        var mainMethodSymbol = default(IMethodSymbol);
        var loopMethodSymbol = default(IMethodSymbol);
        var constructorSymbol = default(IMethodSymbol);

        foreach (var method in methods)
        {
            if (hasContextSymbol)
            {
                switch (method.Name)
                {
                    case "Main":
                        mainMethodSymbol = method;
                        continue;
                    case "Loop":
                        loopMethodSymbol = method;
                        continue;
                }
            }

            if (method.MethodKind == MethodKind.Constructor)
            {
                constructorSymbol = method;
            }
            else if (method.MethodKind is not MethodKind.PropertyGet and not MethodKind.PropertySet)
            {
                functionsBuilder.Add(method);
            }
        }

        var functions = functionsBuilder.ToImmutable();
        
        WriteFunctions(functions);

        var addMain = functions.Length > 0 || Head.Netreads.Length > 0 || Head.Netwrites.Length > 0 || Head.Locals.Length > 0;

        var indent = addMain ? 1 : 0;

        if (hasContextSymbol)
        {
            _ = mainMethodSymbol ?? throw new Exception("Main method not found");
            _ = loopMethodSymbol ?? throw new Exception("Loop method not found");
            _ = constructorSymbol ?? throw new Exception("Constructor not found");

            var constructorAnalysis = ConstructorAnalysis.Analyze(constructorSymbol, SemanticModel, Helper);

            var mainDocBuilder = new DocumentationBuilder(this);
            mainDocBuilder.WriteDocumentation(0, mainMethodSymbol);

            if (addMain)
            {
                Writer.WriteLine("main() {");
            }

            WriteGlobalInitializers(indent);
            WriteBindingInitializers(indent);

            WriteFunctionBody(indent, new FunctionIdentifier(mainMethodSymbol));

            var loopDocBuilder = new DocumentationBuilder(this);
            loopDocBuilder.WriteDocumentation(indent, loopMethodSymbol);

            Writer.WriteLine(indent, "while (True) {");
            WriteLoopContents(indent + 1, functions, constructorAnalysis, loopMethodSymbol);
            Writer.WriteLine(indent, "}");

            if (addMain)
            {
                Writer.WriteLine("}");
            }
        }

        return new();
    }

    private void WriteSettingsChangeDetectors(ImmutableArray<IFieldSymbol> settings)
    {
        if (!ScriptSymbol.GetAttributes().Any(x => x.AttributeClass?.Name is "SettingsChangeDetectorsAttribute"))
        {
            return;
        }

        Writer.WriteLine("***Settings***");
        Writer.WriteLine("***");

        foreach (var setting in settings)
        {
            Writer.Write("declare ");
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(setting.Type, null));
            Writer.Write(" Previous");
            Writer.Write(Standardizer.StandardizeName(setting.Name));
            Writer.Write(" = ");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.WriteLine(";");
        }

        Writer.WriteLine();

        foreach (var setting in settings)
        {
            Writer.Write("log(\"");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.Write(": \" ^ ");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.WriteLine(");");
        }

        Writer.WriteLine();

        foreach (var setting in settings)
        {
            Writer.Write("declare netwrite ");
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(setting.Type, null));
            Writer.Write(" Net_");
            Writer.Write(Standardizer.StandardizeName(setting.Name));
            Writer.WriteLine(" for Teams[0];");
            Writer.Write("Net_");
            Writer.Write(Standardizer.StandardizeName(setting.Name));
            Writer.Write(" = ");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.WriteLine(";");
        }

        Writer.WriteLine("***");
        Writer.WriteLine();
        Writer.WriteLine("***UpdateSettings***");
        Writer.WriteLine("***");

        foreach (var setting in settings)
        {
            var att = setting.GetAttributes()
                .First(x => x.AttributeClass?.Name == NameConsts.SettingAttribute);

            var reloadOnChange = att.NamedArguments.FirstOrDefault(x => x.Key == "ReloadOnChange").Value.Value as bool? ?? false;
            var callOnChange = att.NamedArguments.FirstOrDefault(x => x.Key == "CallOnChange").Value.Value as string;

            Writer.Write("if (");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.Write(" != Previous");
            Writer.Write(Standardizer.StandardizeName(setting.Name));
            Writer.WriteLine(") {");
            Writer.Write("    Previous");
            Writer.Write(Standardizer.StandardizeName(setting.Name));
            Writer.Write(" = ");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.WriteLine(";");
            Writer.Write("    Net_");
            Writer.Write(Standardizer.StandardizeName(setting.Name));
            Writer.Write(" = ");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.WriteLine(";");

            if (reloadOnChange)
            {
                Writer.WriteLine("    G_Reload = True;");
            }

            if (callOnChange is not null)
            {
                var methodSymbol = ScriptSymbol.GetMembers(callOnChange)
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault();

                if (methodSymbol is null)
                {
                    Helper.Context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                        "MSSG008", "Method not found", "Method {0} not found", "ManiaScriptSharp",
                        DiagnosticSeverity.Error, true), setting.Locations.FirstOrDefault()));
                }

                Writer.Write("    ");

                if (methodSymbol is not null)
                {
                    if (methodSymbol.DeclaredAccessibility == Accessibility.Private)
                    {
                        Writer.Write("Private_");
                    }
                }

                Writer.Write(callOnChange);
                Writer.WriteLine("();");
            }

            Writer.Write("    log(\"");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.Write(": \" ^ ");
            Writer.Write(Standardizer.StandardizeSettingName(setting.Name));
            Writer.WriteLine(");");
            Writer.WriteLine("}");
        }

        /*
        if(S_UseLadder != PreviousUseLadder) {
	        PreviousUseLadder = S_UseLadder;
	        Net_S_UseLadder = S_UseLadder;
	        Log("Envimix", "S_UseLadder: " ^ S_UseLadder);
        }
         */
        Writer.WriteLine("***");
        Writer.WriteLine();
    }

    private void WriteFunctions(ImmutableArray<IMethodSymbol> functions)
    {
        foreach (var functionSymbol in functions)
        {
            if (functionSymbol.IsVirtual || functionSymbol.IsOverride)
            {
                if (functionSymbol.DeclaringSyntaxReferences.Length <= 0 ||
                    functionSymbol.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax methodSyntax)
                {
                    continue;
                }

                if (methodSyntax.Body?.Statements.Count == 0 || methodSyntax.ExpressionBody is not null)
                {
                    continue;
                }
            }

            var docBuilder = new DocumentationBuilder(this);
            docBuilder.WriteDocumentation(indent: 0, functionSymbol);

            var knownStructNames = new HashSet<string>(Head.Structs.Select(x => x.Name));

            if (functionSymbol.IsVirtual || functionSymbol.IsOverride)
            {
                Writer.Write("***");
            }
            else
            {
                Writer.Write(Standardizer.CSharpTypeToManiaScriptType(functionSymbol.ReturnType, knownStructNames));
                Writer.Write(' ');
            }

            if (functionSymbol.DeclaredAccessibility == Accessibility.Private)
            {
                Writer.Write("Private_");
            }
            
            Writer.Write(Standardizer.CSharpTypeToManiaScriptType(functionSymbol.Name));

            if (functionSymbol.IsVirtual || functionSymbol.IsOverride)
            {
                Writer.WriteLine("***");
                Writer.WriteLine("***");
                WriteFunctionBody(indent: 0, new FunctionIdentifier(functionSymbol));
                Writer.WriteLine("***");
                Writer.WriteLine();
                continue;
            }

            Writer.Write('(');

            var isFirst = true;

            foreach (var parameter in functionSymbol.Parameters)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    Writer.Write(", ");
                }

                Writer.Write(Standardizer.CSharpTypeToManiaScriptType(parameter.Type, knownStructNames));
                Writer.Write(' ');
                Writer.Write(Standardizer.StandardizeUnderscoreName(parameter.Name));
            }

            Writer.WriteLine(") {");
            WriteFunctionBody(indent: 1, new FunctionIdentifier(functionSymbol));
            Writer.WriteLine("}");
            Writer.WriteLine();
        }
    }

    private void WriteGlobalInitializers(int indent)
    {
        if (Head.Globals.Length <= 0)
        {
            return;
        }

        foreach (var global in Head.Globals.Where(x => x.ContainingType.Equals(ScriptSymbol, SymbolEqualityComparer.Default)))
        {
            var equalsSyntax = global.DeclaringSyntaxReferences[0].GetSyntax() switch
            {
                PropertyDeclarationSyntax propertyDeclarationSyntax => propertyDeclarationSyntax.Initializer,
                VariableDeclaratorSyntax variableDeclaratorSyntax => variableDeclaratorSyntax.Initializer,
                _ => throw new NotSupportedException("Unknown global declaration syntax")
            };

            if (equalsSyntax is null)
            {
                continue;
            }

            Writer.WriteIndent(indent);
            Writer.Write(Standardizer.StandardizeGlobalName(global.Name));
            Writer.Write(" = ");
            Writer.Write(Standardizer.StandardizeName(equalsSyntax.Value.ToString()));
            Writer.WriteLine(";");
        }

        Writer.WriteLine();
    }

    private void WriteBindingInitializers(int indent)
    {
        if (Head.Bindings.Length <= 0)
        {
            return;
        }

        foreach (var binding in Head.Bindings)
        {
            var manialinkControlAtt = binding.GetAttributes()
                .First(x => x.AttributeClass?.Name == NameConsts.ManialinkControlAttribute);

            var controlId = manialinkControlAtt.ConstructorArguments.Length == 0
                ? binding.Name
                : manialinkControlAtt
                    .ConstructorArguments[0]
                    .Value?
                    .ToString();

            if (controlId is null)
            {
                continue;
            }

            var type = binding switch
            {
                IPropertySymbol prop => prop.Type,
                IFieldSymbol field => field.Type,
                _ => throw new Exception("This should never happen")
            };

            Writer.WriteIndent(indent);

            Writer.Write(binding.Name);
            Writer.Write(" = (Page.GetFirstChild(\"");
            Writer.Write(controlId);
            Writer.Write("\") as ");
            Writer.Write(type.Name);
            Writer.WriteLine(");");
        }

        Writer.WriteLine();
    }

    private void WriteLoopContents(int indent, ImmutableArray<IMethodSymbol> functions,
        ConstructorAnalysis constructorAnalysis, IMethodSymbol loopMethodSymbol)
    {
        Writer.WriteLine(indent, "yield;");

        IsBuildingEventHandling = true;
        var eventForeachBuilder = new EventForeachBuilder(this);
        eventForeachBuilder.Write(indent, functions, constructorAnalysis);
        IsBuildingEventHandling = false;

        IsBuildingLoop = true;
        WriteFunctionBody(indent, new FunctionIdentifier(loopMethodSymbol));
        IsBuildingLoop = false;
    }

    public void WriteFunctionBody(int indent, Function function)
    {
        BlockSyntax block;
        ImmutableArray<ParameterSyntax> parameters;
        
        switch (function)
        {
            case FunctionIdentifier functionIdentifier:
            {
                if (functionIdentifier.Method.DeclaringSyntaxReferences.Length <= 0 ||
                    functionIdentifier.Method.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax methodSyntax)
                {
                    return;
                }
            
                // get block from methodSyntax.ExpressionBody
                if (methodSyntax.Body is not null)
                {
                    block = methodSyntax.Body;
                }
                else if (methodSyntax.ExpressionBody is not null)
                {
                    block = SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(methodSyntax.ExpressionBody.Expression));
                }
                else
                {
                    return;
                }
                
                parameters = methodSyntax.ParameterList.Parameters.ToImmutableArray();
                break;
            }
            case FunctionAnonymous functionAnonymous:
                block = functionAnonymous.Block;
                parameters = functionAnonymous.Parameters;
                break;
            default:
                throw new NotSupportedException("Unknown function type");
        }

        foreach (var statement in block.Statements)
        {
            StatementWriter.WriteSyntax(new(indent, statement, parameters, this));
        }
    }
}