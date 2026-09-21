using System.Reflection;
using System.Text.Json;
using ManiaScriptSharp;

namespace ManiaScriptSharp.McpServer.Reflection;

/// <summary>
/// Builds a read-only, reflection-backed view of the public ManiaScriptSharp API assemblies.
/// </summary>
public sealed class ReflectionApiCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IReadOnlyDictionary<string, ApiAssembly> assemblies;

    public ReflectionApiCatalog()
    {
        assemblies = new[]
        {
            new ApiAssembly("core", "ManiaScriptSharp", typeof(IContext).Assembly, "Runtime helpers, attributes, and built-in ManiaScript stubs."),
            new ApiAssembly("maniaplanet", "ManiaScriptSharp.ManiaPlanet", LoadAssembly("ManiaScriptSharp.ManiaPlanet"), "ManiaPlanet (TM2/SM) API surface."),
            new ApiAssembly("maniaplanet3", "ManiaScriptSharp.ManiaPlanet3", LoadAssembly("ManiaScriptSharp.ManiaPlanet3"), "ManiaPlanet 3 API surface."),
            new ApiAssembly("trackmania", "ManiaScriptSharp.Trackmania", LoadAssembly("ManiaScriptSharp.Trackmania"), "Trackmania (2020) API surface."),
        }.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Serializes the known API assemblies and their public type counts.</summary>
    public string ListAssemblies()
    {
        var result = assemblies.Values
            .OrderBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                PublicTypeCount = GetPublicTypes(x).Count,
            });

        return Serialize(result);
    }

    /// <summary>Finds matching types and declared public members.</summary>
    public string Search(string query, string? assembly, int limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Error("query must contain at least one non-whitespace character.");
        }

        if (!TryGetAssemblies(assembly, out var selectedAssemblies, out var error))
        {
            return Error(error!);
        }

        limit = Math.Clamp(limit, 1, 200);
        var matches = selectedAssemblies
            .SelectMany(apiAssembly => GetPublicTypes(apiAssembly).Select(type => new { apiAssembly, type }))
            .SelectMany(x => FindMatches(x.apiAssembly, x.type, query))
            .OrderBy(x => x.Assembly, StringComparer.Ordinal)
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ThenBy(x => x.Member, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();

        return Serialize(new
        {
            Query = query,
            Assembly = assembly,
            Limit = limit,
            Matches = matches,
        });
    }

    /// <summary>Gets a type's public reflection metadata.</summary>
    public string GetType(string typeName, string? assembly)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return Error("typeName must contain a full or simple type name.");
        }

        if (!TryFindTypes(typeName, assembly, out var matches, out var error))
        {
            return Error(error!);
        }

        return Serialize(matches.Select(x => DescribeType(x.ApiAssembly, x.Type)));
    }

    /// <summary>Gets a type member's public reflection metadata.</summary>
    public string GetMembers(string typeName, string memberName, string? assembly)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return Error("memberName must contain an exact member name.");
        }

        if (!TryFindTypes(typeName, assembly, out var matches, out var error))
        {
            return Error(error!);
        }

        var result = matches.Select(x => new
        {
            Assembly = x.ApiAssembly.Id,
            Type = GetTypeName(x.Type),
            Members = GetDeclaredPublicMembers(x.Type)
                .Where(member => string.Equals(member.Name, memberName, StringComparison.OrdinalIgnoreCase))
                .Select(DescribeMember)
                .ToArray(),
        });

        return Serialize(result);
    }

    private static Assembly LoadAssembly(string name) => Assembly.Load(new AssemblyName(name));

    private static IReadOnlyList<Type> GetPublicTypes(ApiAssembly assembly) =>
        assembly.Assembly.GetExportedTypes()
            .Where(type => !type.IsNested && !type.IsSpecialName)
            .OrderBy(GetTypeName, StringComparer.Ordinal)
            .ToArray();

    private bool TryGetAssemblies(string? assembly, out IEnumerable<ApiAssembly> selectedAssemblies, out string? error)
    {
        if (string.IsNullOrWhiteSpace(assembly))
        {
            selectedAssemblies = assemblies.Values;
            error = null;
            return true;
        }

        if (assemblies.TryGetValue(assembly, out var selected))
        {
            selectedAssemblies = [selected];
            error = null;
            return true;
        }

        selectedAssemblies = [];
        error = $"Unknown assembly '{assembly}'. Call maniascript_api_list_assemblies for valid assembly ids.";
        return false;
    }

    private bool TryFindTypes(
        string typeName,
        string? assembly,
        out IReadOnlyList<(ApiAssembly ApiAssembly, Type Type)> matches,
        out string? error)
    {
        if (!TryGetAssemblies(assembly, out var selectedAssemblies, out error))
        {
            matches = [];
            return false;
        }

        matches = selectedAssemblies
            .SelectMany(apiAssembly => GetPublicTypes(apiAssembly)
                .Where(type => TypeNameMatches(type, typeName))
                .Select(type => (apiAssembly, type)))
            .OrderBy(x => x.apiAssembly.Id, StringComparer.Ordinal)
            .ToArray();

        if (matches.Count > 0)
        {
            return true;
        }

        error = $"No public API type named '{typeName}' was found.";
        return false;
    }

    private static bool TypeNameMatches(Type type, string typeName) =>
        string.Equals(type.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(GetTypeName(type), typeName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<SearchMatch> FindMatches(ApiAssembly assembly, Type type, string query)
    {
        if (Contains(type.FullName, query) || Contains(GetTypeName(type), query))
        {
            yield return new SearchMatch(
                assembly.Id,
                GetTypeName(type),
                Member: null,
                Kind: "type",
                FormatTypeDeclaration(type));
        }

        foreach (var member in GetDeclaredPublicMembers(type).Where(member => Contains(member.Name, query)))
        {
            yield return new SearchMatch(
                assembly.Id,
                GetTypeName(type),
                member.Name,
                GetMemberKind(member),
                FormatMember(member));
        }
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) is true;

    private static object DescribeType(ApiAssembly assembly, Type type) => new
    {
        Assembly = assembly.Id,
        FullName = type.FullName,
        Declaration = FormatTypeDeclaration(type),
        BaseType = type.BaseType is null ? null : FormatType(type.BaseType),
        Interfaces = type.GetInterfaces().Select(FormatType).OrderBy(x => x, StringComparer.Ordinal),
        Members = GetDeclaredPublicMembers(type).Select(DescribeMember),
    };

    private static object DescribeMember(MemberInfo member) => new
    {
        Name = member.Name,
        Kind = GetMemberKind(member),
        Declaration = FormatMember(member),
        Attributes = member.GetCustomAttributesData().Select(FormatAttribute).ToArray(),
    };

    private static IEnumerable<MemberInfo> GetDeclaredPublicMembers(Type type) =>
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MemberInfo>()
            .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Concat(type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName))
            .OrderBy(GetMemberKind, StringComparer.Ordinal)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ThenBy(FormatMember, StringComparer.Ordinal);

    private static string GetMemberKind(MemberInfo member) => member switch
    {
        ConstructorInfo => "constructor",
        PropertyInfo => "property",
        FieldInfo => "field",
        EventInfo => "event",
        MethodInfo => "method",
        _ => "member",
    };

    private static string FormatTypeDeclaration(Type type)
    {
        var modifiers = type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct" : "class";
        return $"public {modifiers} {GetTypeName(type)}";
    }

    private static string FormatMember(MemberInfo member) => member switch
    {
        ConstructorInfo constructor => $"public {GetTypeName(constructor.DeclaringType!)}({FormatParameters(constructor.GetParameters())})",
        PropertyInfo property => FormatProperty(property),
        FieldInfo field => $"public {(field.IsStatic ? "static " : string.Empty)}{FormatType(field.FieldType)} {field.Name}",
        EventInfo @event => $"public event {FormatType(@event.EventHandlerType!)} {@event.Name}",
        MethodInfo method => $"public {(method.IsStatic ? "static " : string.Empty)}{FormatType(method.ReturnType)} {method.Name}{FormatGenericArguments(method)}({FormatParameters(method.GetParameters())})",
        _ => member.Name,
    };

    private static string FormatProperty(PropertyInfo property)
    {
        var accessors = new List<string>();
        if (property.GetMethod?.IsPublic is true) accessors.Add("get;");
        if (property.SetMethod?.IsPublic is true) accessors.Add("set;");
        return $"public {(property.GetMethod?.IsStatic is true ? "static " : string.Empty)}{FormatType(property.PropertyType)} {property.Name} {{ {string.Join(' ', accessors)} }}";
    }

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters) =>
        string.Join(", ", parameters.Select(parameter =>
        {
            var modifier = parameter.GetCustomAttribute<ParamArrayAttribute>() is not null
                ? "params "
                : parameter.IsOut
                    ? "out "
                    : parameter.ParameterType.IsByRef
                        ? "ref "
                        : string.Empty;
            var defaultValue = parameter.HasDefaultValue ? $" = {FormatDefaultValue(parameter.DefaultValue)}" : string.Empty;
            return $"{modifier}{FormatType(parameter.ParameterType)} {parameter.Name}{defaultValue}";
        }));

    private static string FormatDefaultValue(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        char character => $"'{character}'",
        bool boolean => boolean ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
    };

    private static string FormatGenericArguments(MethodInfo method) => method.IsGenericMethod
        ? $"<{string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name))}>"
        : string.Empty;

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
        {
            return FormatType(type.GetElementType()!);
        }

        if (type.IsArray)
        {
            return $"{FormatType(type.GetElementType()!)}[]";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(short)) return "short";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(int)) return "int";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(long)) return "long";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(object)) return "object";

        if (type.IsGenericType)
        {
            var name = type.Name[..type.Name.IndexOf('`')];
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FormatType))}>";
        }

        return type.Name;
    }

    private static string GetTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name))}>";
    }

    private static string FormatAttribute(CustomAttributeData attribute)
    {
        var name = attribute.AttributeType.Name.EndsWith("Attribute", StringComparison.Ordinal)
            ? attribute.AttributeType.Name[..^"Attribute".Length]
            : attribute.AttributeType.Name;
        var arguments = attribute.ConstructorArguments
            .Select(argument => FormatAttributeArgument(argument.Value))
            .Concat(attribute.NamedArguments.Select(argument => $"{argument.MemberName} = {FormatAttributeArgument(argument.TypedValue.Value)}"));
        return $"[{name}({string.Join(", ", arguments)})]";
    }

    private static string FormatAttributeArgument(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        char character => $"'{character}'",
        Type type => $"typeof({FormatType(type)})",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
    };

    private static string Error(string message) => Serialize(new { Error = message });

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private sealed record ApiAssembly(string Id, string Name, Assembly Assembly, string Description);

    private sealed record SearchMatch(string Assembly, string Type, string? Member, string Kind, string Declaration);
}
