using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ManiaScriptSharp.McpServer.Reflection;

/// <summary>Read-only MCP tools for exploring the ManiaScriptSharp API surface.</summary>
[McpServerToolType]
public sealed class ManiaScriptApiTools(ReflectionApiCatalog catalog)
{
    [McpServerTool(Name = "maniascript_api_list_assemblies")]
    [Description("Lists the ManiaScriptSharp assemblies available for API reflection. Use an assembly id with the other tools to disambiguate game-specific types.")]
    public string ListAssemblies() => catalog.ListAssemblies();

    [McpServerTool(Name = "maniascript_api_search")]
    [Description("Searches public ManiaScriptSharp types and their declared public members. Use this first to locate an API type or member. Results are capped to keep responses concise.")]
    public string Search(
        [Description("Case-insensitive text to find in a type's namespace, name, or a declared public member name.")] string query,
        [Description("Optional assembly id from maniascript_api_list_assemblies, for example 'trackmania'.")] string? assembly = null,
        [Description("Maximum matches to return, from 1 through 200. Defaults to 50.")] int limit = 50) =>
        catalog.Search(query, assembly, limit);

    [McpServerTool(Name = "maniascript_api_get_type")]
    [Description("Returns a public API type's C# declaration, base type, interfaces, and all declared public constructors, properties, fields, events, and methods. If the name exists in more than one game API, supply assembly.")]
    public string GetType(
        [Description("The full type name or simple type name, such as 'ManiaScriptSharp.CTmMode' or 'CTmMode'.")] string typeName,
        [Description("Optional assembly id from maniascript_api_list_assemblies, for example 'maniaplanet'.")] string? assembly = null) =>
        catalog.GetType(typeName, assembly);

    [McpServerTool(Name = "maniascript_api_get_members")]
    [Description("Returns the overloads and metadata for one declared public member of a public API type. Use this after maniascript_api_get_type when a type has many members.")]
    public string GetMembers(
        [Description("The full type name or simple type name.")] string typeName,
        [Description("The exact member name, for example 'PendingEvents' or 'GetPlayer'.")] string memberName,
        [Description("Optional assembly id from maniascript_api_list_assemblies, for example 'maniaplanet3'.")] string? assembly = null) =>
        catalog.GetMembers(typeName, memberName, assembly);
}
