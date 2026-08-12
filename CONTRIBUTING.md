# Contributing to ManiaScriptSharp

This covers building the repository from source. For using ManiaScriptSharp in your own
project via NuGet, see the [README](README.md#installation) instead.

## Windows: enable long paths

`ManiaScriptSharp.Trackmania` (and the ManiaPlanet API projects) set `EmitCompilerGeneratedFiles=true`
with `CompilerGeneratedFilesOutputPath=Generated`. The API generator's hint names mirror deep
`Scripts/` folder namespaces joined by dots (e.g.
`Script.Libs.Nadeo.Trackmania.Modes.X.UIModules.Y_Client.g.cs`), so full paths under
`src/ManiaScriptSharp.Trackmania/Generated/...` can exceed Windows' 260-character `MAX_PATH`.
Git then silently fails to check these out (they show up as locally "deleted" in `git status`).

Before cloning or building, run once per clone:

```powershell
git config core.longpaths true
```

If files are already missing from a prior checkout, restore them afterwards with `git checkout -- .`.

## Building the solution

```powershell
dotnet build ManiaScriptSharp.slnx
```

## Running the sample

```powershell
dotnet build samples/MyMode/MyMode.csproj
```

After building (or just opening `samples/MyMode/MyGamemode.cs` in Visual Studio / VS Code with the
C# extension), look at:

```
samples/MyMode/ManiaScript/MyGamemode.Script.txt
```

Edit the C# file, save, and the `.Script.txt` is rewritten automatically. See
[IDE setup](README.md#ide-setup) in the README to get live regeneration on every keystroke.

The sample references the generator and libraries via `ProjectReference` rather than NuGet
packages, so changes made locally are picked up immediately. Its `.csproj` also imports
`build/ManiaScriptSharp.Generator.props` explicitly — that file is imported automatically when
the generator is consumed as a NuGet package instead.

## Testing the project template locally

```powershell
dotnet new install ./templates/ManiaScriptSharp.Templates/content/ManiaScriptSharp.Gamemode
dotnet new msharp-gamemode -n Test --Api Trackmania -o /tmp/Test
dotnet new uninstall ./templates/ManiaScriptSharp.Templates/content/ManiaScriptSharp.Gamemode
```

Installing `content/ManiaScriptSharp.Gamemode` directly (instead of the packed `.nupkg`) picks up
template edits immediately, without a pack/publish round-trip.

## Running tests

```powershell
dotnet test ManiaScriptSharp.slnx
```

## Project structure

- `src/ManiaScriptSharp` — attributes, markers and runtime helper stubs (`IContext`, `ILib`, ...).
- `src/ManiaScriptSharp.Generator` — the Roslyn incremental source generator (C# → ManiaScript).
- `src/ManiaScriptSharp.ApiGenerator` — turns Nadeo's `doc.h` headers and `.Script.txt` libraries into a C# API surface.
- `src/ManiaScriptSharp.ManiaPlanet`, `ManiaScriptSharp.ManiaPlanet3`, `ManiaScriptSharp.Trackmania` — generated API surfaces per game.
- `templates/ManiaScriptSharp.Templates` — `dotnet new` template pack (`msharp-gamemode`, `--Api ManiaPlanet|ManiaPlanet3|Trackmania`) for scaffolding a new consumer project.
- `samples/MyMode` — a sample game mode exercising the generator end to end.
- `tests/` — unit tests for the generator and API generator.
