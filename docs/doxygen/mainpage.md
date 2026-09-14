# ManiaScriptSharp {#mainpage}

**Write ManiaScript in C#.**

ManiaScriptSharp is a Roslyn incremental source generator that translates C# into
ManiaScript `.Script.txt` files for Trackmania and ManiaPlanet — game modes, libraries,
manialinks, map editor plugins and server plugins.

## Libraries

| Library | Description |
|---------|-------------|
| `ManiaScriptSharp` | Runtime markers, attributes and helpers (`IContext`, `ILib`, `Local<T>`, `Persistent<T>`, …) |
| `ManiaScriptSharp.Generator` | The C# → ManiaScript source generator |
| `ManiaScriptSharp.ApiGenerator` | Parses Nadeo API inputs (doc.h, .Script.txt libs) and emits C# stubs |
| `ManiaScriptSharp.ManiaPlanet` | Generated API surface for ManiaPlanet (TM2/SM) |
| `ManiaScriptSharp.ManiaPlanet3` | Generated API surface for ManiaPlanet 3 |
| `ManiaScriptSharp.Trackmania` | Generated API surface for Trackmania |

The generated classes live under the `Generated/` folders of each API project — every
`doc.*.g.cs` class (e.g. `CPlayer`, `CSmPlayer`, `CMlLabel`) is part of the reference.

## Guides

- [Event translation](event-translation.md) — how `+=` subscriptions become the event loop
- [LINQ translation](linq-translation.md) — how LINQ chains desugar into `foreach` loops
- [ManiaScript language reference](ManiaScript-Language-Reference.md) — the target language in depth

## Getting started

See the [README](https://github.com/BigBang1112/maniascript-sharp) on GitHub for
installation (NuGet / project templates) and usage examples.
