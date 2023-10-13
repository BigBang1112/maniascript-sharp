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
```cs
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
6. After running Build, ManiaScript will generate into the `out` folder next to the source code, following the directory path. If it does not happen or you get weird warnings, restart your IDE or try different build options.
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

Standard library inclusions are hardcoded (may change in the future).

Custom library inclusions are defined with the `IncludeAttribute` (also may change in the future).

### Types

The mappings are as following:

| C# | ManiaScript |
|---|---|
| `void` | `Void` |
| `int` | `Integer` |
| `float` | `Real` |
| `double` | `Real` |
| `bool` | `Boolean` |
| `string` | `Text` |
| `IList<int>` | `Integer[]` |
| `ImmutableArray<int>` | `Integer[]` |
| `Dictionary<string, int>` | `Integer[Text]` |

`ImmutableArray` is currently misused and will change in the future.

### Constants

C#:
```cs
const int Constant = 5;
```
ManiaScript:
```
#Const C_Constant 5
```

### Settings

Settings are decorated using the `SettingAttribute`. They can be either constants or read-only fields, as you should not change them throughout the script.

Read-only fields are allowed to support types that are not allowed to be a C# constant, like an array for example.

Translation of setting names is attempted automatically by default. You can turn this off by setting `Translated = false` on the `SettingAttribute`.

C#:
```cs
[Setting]
const string AdminLogin = "bigbang1112";

[Setting(As = "Chat time")]
const int ChatTime = 50;
```
ManiaScript:
```
#Setting S_AdminLogin "bigbang1112"
#Setting S_ChatTime 50 as _("Chat time")
```

#### Change detection

There's a special built in feature on settings that you can use to detect when a setting has changed. This was made for a possiblity to avoid repeating patterns in the C# code, but it also generates netwrites that you can use to read in client manialinks. It is a bit clunky to implement though so just be aware of it.

You can turn this on by adding `SettingsChangeDetectorsAttribute` on your class that has the settings you wanna work with (may be changed to interface in the future).

This will generate two new labels `Settings` and `UpdateSettings` (currently not caring if you can call them or not), so you have to add these methods manually:

```cs
public virtual void Settings() { }
public virtual void UpdateSettings() { }
```

`Settings` will initialize the detectors, and `UpdateSettings` should be called in a loop, preferably in every loop.

`SettingAttribute` has 2 additional settings:
- `ReloadOnChange` - when a change is detected, set `Reload` to true. This requires you to have a field boolean `Reload` in the C# code for it to translate correctly.
- `CallOnChange` - name of the method to call when the change is detected. Best practice is to use `nameof()` on the method.

### Globals

Fields that are public and not decorated with `SettingAttribute` become globals. Globals are prefixed with `G_`.

Some types do support inline set (it will initialize in the `main()` entry point), but on complicated types, preferably avoid it. For library scripts, inline set of field is not supported at all.

C#:
```cs
public int PreviousTime = -1;
```
ManiaScript:
```
declare Integer G_PreviousTime;

main() {
  G_PreviousTime = -1
}
```

### Bindings

Binding means retrieving manialink element by ID in a validated and strongly-typed way. It can significantly reduce code for simple UI logic.

For example, this manialink has `LabelCountdown`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manialink version="3" xmlns="manialink" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="manialink https://raw.githubusercontent.com/reaby/manialink-xsd/main/manialink_v3_ns.xsd">
    <label pos="1 10" z-index="0" size="100 25" text="3" halign="center" valign="center2" textsize="25" textfont="RajdhaniMono" textprefix="$o$i" textemboss="1" id="LabelCountdown"/>
</manialink>
```

Instead of using `Page.GetFirstChild`, you declare it as a field with an attribute:

```cs
public class MyManialink : CTmMlScriptIngame, IContext
{
    [ManialinkControl]
    public required CMlLabel LabelCountdown;

    // ...
}
```

> Accessors are not considered, use anything C# allows.

If the control ID is not provided on the `ManialinkControlAttribute`, you need to name the field exactly like in the XML `id` attribute.

This will generate:

```
declare CMlLabel LabelCountdown; // Bound to "LabelCountdown"

main() {
  LabelCountdown = (Page.GetFirstChild("LabelCountdown") as CMlLabel);
}
```

The attribute by default also tells the translator to validate the XML `id` if it correctly matches. Sometimes though, you wanna build the manialink dynamically, so for that, you can set `IgnoreValidation` to `true`.

### Functions

Use methods to represent functions.

- You should use regular C# conventions for formatting.
- Private accessor will add the `Private_` prefix.
- Parameters are automatically PascalCased and prefixed with underscore.
- `static` keyword does not affect anything in ManiaScript. You can use it to unit test easier.
- `virtual` keyword will turn the method into a label.

C#:
```cs
static string TimeToTextWithMilli(int time)
{
    return $"{TextLib.TimeToText(time, true)}{MathLib.Abs(time % 10)}";
}
```
ManiaScript:
```
Text Private_TimeToTextWithMilli(Integer _Time) {
  return TextLib::TimeToText(_Time, True) ^ MathLib::Abs(_Time % 10);
}
```

### Event handling (optional feature)

Use C# event handlers to deal with ManiaScript events and only work with parameters that are actually used - this is probably the most powerful feature of ManiaScriptSharp.

Right now, the only place where you can declare subscribed events is in the class constructor, but this will change in the future.

For example, to open a map dialog by clicking on a button:

```cs
public class MyManialink : CTmMlScriptIngame, IContext
{
    [ManialinkControl] public required CMlQuad QuadMapName;

    public MyManialink()
    {
        QuadMapName.MouseClick += () =>
        {
            ShowCurChallengeCard();
        };
    }
}
```

Which will generate this monstrosity:

```
declare CMlQuad QuadMapName; // Bound to "QuadMapName"

main() {
  QuadMapName = (Page.GetFirstChild("QuadMapName") as CMlQuad);

  while (True) {
    yield;
    foreach (Event in PendingEvents) {
      switch (Event.Type) {
        case CMlScriptEvent::Type::MouseClick: {
          switch (Event.Control) {
            case QuadMapName: {
              // Start of anonymous function
              ShowCurChallengeCard();
              // End of anonymous function
            }
          }
        }
      }
    }
  }
}
```

Delegates, lambdas, and other sorts of method references should be supported.

Just referencing the method will call it instead of putting the contents directly into the event handling. This is recommended to not cause unexpected name collisions.

### Labels (`***` and `+++`)

The closest C# feature to labels is virtual methods.

`base` is unused. ManiaScript will always run the inherited mode first.

C# (1):
```cs
public class MyMode : CTmMode, IContext
{
    public virtual void OnMapIntroEnd()
    {
        UIManager.UIAll.UISequence = CUIConfig.EUISequence.Playing;
    }

    public void Main()
    {
        OnMapIntroEnd();
    }

    public void Loop()
    {
    }
}
```
C# (2):
```cs
public class MyNextMode : MyMode
{
    public override void OnMapIntroEnd()
    {
	Log("I do something");
    }
}
```
ManiaScript (1):
```
#RequireContext CTmMode

***OnMapIntroEnd***
***
UIManager.UIAll.UISequence = CUIConfig::EUISequence::Playing;
***

main() {
  +++OnMapIntroEnd+++
}
```
ManiaScript (2):
```
#Extends "Modes/TrackMania/MyMode.Script.txt"

***OnMapIntroEnd***
***
log("I do something");
***
```

Note that labels do keep the scope of the function it was called in, so there can be unexpected name collisions after the translation. This could be solved by generating random prefixes to fake the scope, but that wasn't implemented yet.

### Netwrites

TODO

### Netreads

TODO

### Locals

TODO

### Pattern matching

ManiaScriptSharp provides a powerful set of snippet generators for the pattern matching with the C#'s `is` keyword.

TODO examples

### Switch statement

TODO

### Manialink XML

TODO

### Accessors

It is not required to use `public` accessors for the generator to recognize the classes (except globals which are an exception). Use ones you need to better show what you mean to expose.

## Conclusion

This project does not replace ManiaScript, nor text editor extensions that support ManiaScript. This is just an alternative way to be more productive in ManiaScript by using a language that you prefer more, which some may not agree with, and that is understandable. For code generation and unit testing though, this may not be the worst project. Just note that unit testing is just a theory that wasn't yet implemented.
