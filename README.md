# ManiaScriptSharp

ManiaScriptSharp is a way to type ManiaScript with C# features. C# code is compiled into both .NET library and a set of ManiaScript files.

### 4 reasons why ManiaScriptSharp exists

1. Easier syntax
2. Accurate auto-complete
3. Flexible accessibility
4. Unit-testability

This project **does not guarantee anything**. It was written while practicing source generators as a beginner, and the code is **admittedly low-effort, hacky, and not maintainable.** It can take hundreds of hours to rewrite it into something cleaner, but this **should be preferred** if this project hooks wider interest. Let me know if you're interested in doing something like this, I can offer a variety of help or even collaborate.

## Usage

1. Create a C# library project (any other project type is not supported)
2. Update the project settings (`.csproj`) with:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ManiaScriptSharp" Version="0.2.0" /> <!-- attributes, markers, ... -->
    <PackageReference Include="ManiaScriptSharp.Generator" Version="0.2.0" /> <!-- generator of ManiaScript -->
    <PackageReference Include="ManiaScriptSharp.ManiaPlanet" Version="0.2.0" /> <!-- use ManiaPlanet (TM2/SM) scripting API -->
  </ItemGroup>

  <ItemGroup>
    <Using Include="ManiaScriptSharp" /> <!-- to implicitly import all classes -->
    <Using Static="true" Include="ManiaScriptSharp.ManiaScript" /> <!-- to easily use log(), assert(), etc. -->
  </ItemGroup>

  <ItemGroup>
    <!-- <AdditionalFiles Include="buildsettings.yml" /> --> <!-- optional settings for building (will change in the future) -->
    <!-- <AdditionalFiles Include="manialink_v3_ns.xsd" /> --> <!-- optional manialink XML validator: https://github.com/reaby/manialink-xsd/blob/main/manialink_v3_ns.xsd -->
  </ItemGroup>
</Project>
```
3. Pick one of the context classes like you would in ManiaScript (`CMapEditorPlugin`, `CTmMode`, `CTmMlScriptIngame`, ...)
4. Create directories based on where these scripts should lay (`CTmMode` would lay in `Scripts/Modes/TrackMania` for example)
5. Create a C# class there and start with this skeleton:
```csharp
namespace MyProject;

public class MyGamemode : CTmMode, IContext
{
    public void Main()
    {
        
    }

    public void Loop()
    {
        
    }
}
```
6. After running Build, ManiaScript will generate into the `out` folder next to the source code, following the directory path.
7. To redirect the output, create `buildsettings.yml` in the root project folder, and write this content:
```yaml
OutputDir: C:/MyManiaPlanetServer/UserData # Build root (default is the relative folder 'out')
Packed: false # If the output will be packed into a folder with the name of the project
```

## Techniques

### Contexts

`IContext` generates `main()` and `while(x)`. Given C# methods are `Main()` and `Loop()`.

### Inheritance (`#RequireContext` and `#Extends`)

`#RequireContext` and `#Extends` are generated based on C# class inheritance. If the class is "official" (part of `ManiaScriptSharp` namespace), `#RequireContext` is used, otherwise it's `#Extends`.

`#Extends` is generated by taking the class namespace as a directory path, and the class name as a file name without the extension.

### Library inclusions

Standard library inclusions are hardcoded.

Custom library inclusions are defined with the `IncludeAttribute`.

### Constants

C#:
```csharp
const int Constant = 5;
```
ManiaScript:
```
#Const Constant 5
```

### Settings

### Globals

### Bindings

### Functions

### Event handling (optional feature)

### Labels (`***` and `+++`)

The closest C# feature to labels is virtual methods.

### Netwrites

### Netreads

### Locals

### Pattern matching

ManiaScriptSharp provides a powerful set of snippet generators for the pattern matching with the C#'s `is` keyword.

### Switch statement

#### Manialink XML
