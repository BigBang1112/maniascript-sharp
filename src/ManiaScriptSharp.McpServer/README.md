# ManiaScriptSharp MCP Server

An HTTP MCP server that exposes the public ManiaScriptSharp API through reflection. It never executes the API stubs; the available MCP tools only return metadata.

## Run locally

```powershell
dotnet run --project src/ManiaScriptSharp.McpServer
```

The Streamable HTTP MCP endpoint is `http://127.0.0.1:5108/mcp`. A health check is available at `GET /health`.

Configure an MCP client with that endpoint. The server offers these read-only tools:

- `maniascript_api_list_assemblies` — available API assemblies and their ids.
- `maniascript_api_search` — finds public types and members by name.
- `maniascript_api_get_type` — returns a type's declaration, interfaces, base type, and members.
- `maniascript_api_get_members` — returns one member's overloads and attributes.

The reflected assemblies are `core`, `maniaplanet`, `maniaplanet3`, and `trackmania`. Supply an assembly id when a type is present in more than one game API.

The development profile binds only to loopback. Before making the server reachable beyond the local machine, put it behind authentication and transport security appropriate for the client and deployment environment.
