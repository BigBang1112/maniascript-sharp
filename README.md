# ManiaScriptSharp

## `.csproj` example

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="ManiaScriptSharp" />
    <Using Static="true" Include="ManiaScriptSharp.ManiaScript" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="ManiaScriptSharp.Generator\ManiaScriptSharp.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    <ProjectReference Include="ManiaScriptSharp.ManiaPlanet\ManiaScriptSharp.ManiaPlanet.csproj" />
  </ItemGroup>
	
  <ItemGroup>
    <AdditionalFiles Include="buildsettings.yml" /> <!-- Optional build settings tweaking -->
    <AdditionalFiles Include="manialink_v3_ns.xsd" /> <!-- Optional Manialink XML validation -->
  </ItemGroup>

</Project>
```

## `buildsettings.yml` example

```yaml
OutputDir: C:/MyManiaPlanetServer/UserData # Build root (default is the relative folder 'out')
Packed: false # If the output will be packed into a folder with the name of the project
```
