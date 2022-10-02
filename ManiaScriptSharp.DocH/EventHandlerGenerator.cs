using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ManiaScriptSharp.DocH;

[Generator]
public class EventHandlerGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        if (false && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var typeSymbolList = context.Compilation
            .GlobalNamespace
            .GetNamespaceMembers()
            .First(x => x.Name == "ManiaScriptSharp")
            .GetTypeMembers();

        foreach (var typeSymbol in typeSymbolList)
        {
            foreach (var delegateSymbol in typeSymbol.GetTypeMembers().Where(x => x.DelegateInvokeMethod is not null))
            {
                var method = delegateSymbol.DelegateInvokeMethod!;
                var eventName = delegateSymbol.Name.Substring(0, delegateSymbol.Name.Length - 12);
                
                var builder = new StringBuilder();
                
                builder.AppendLine("using System;");
                builder.AppendLine();
                builder.AppendLine("namespace ManiaScriptSharp;");
                builder.AppendLine();
                builder.Append("public partial class ");
                builder.AppendLine(typeSymbol.Name);
                builder.AppendLine("{");
                builder.Append("    public event ");
                builder.Append(delegateSymbol.Name);
                builder.Append("? ");
                builder.Append(eventName);
                builder.AppendLine(";");
                builder.AppendLine();
                builder.Append("    protected virtual void On");
                builder.Append(eventName);
                builder.Append('(');

                var isFirstParam = true;
                
                foreach (var param in method.Parameters)
                {
                    if (isFirstParam)
                    {
                        isFirstParam = false;
                    }
                    else
                    {
                        builder.Append(", ");
                    }
                    
                    builder.Append(param.Type.ToDisplayString());
                    builder.Append(' ');
                    builder.Append(param.Name);
                }
                
                builder.AppendLine(")");
                builder.AppendLine("    {");
                builder.Append("        ");
                builder.Append(eventName);
                builder.Append("?.Invoke(");
                
                isFirstParam = true;
                
                foreach (var param in method.Parameters)
                {
                    if (isFirstParam)
                    {
                        isFirstParam = false;
                    }
                    else
                    {
                        builder.Append(", ");
                    }

                    builder.Append(param.Name);
                }
                
                builder.AppendLine(");");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                
                context.AddSource($"{typeSymbol.Name}.{delegateSymbol.Name}.cs", builder.ToString());
            }
        }
    }
}
