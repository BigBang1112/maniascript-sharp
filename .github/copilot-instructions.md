# ManiaScriptSharp

Roslyn incremental source generator translating C# into ManiaScript `.Script.txt` files.

## Commands

- Before checkout/build on Windows, run `git config core.longpaths true`; API generated-file paths exceed `MAX_PATH`.

## Architecture

- `src/ManiaScriptSharp`: translatable runtime markers, attributes, and interfaces.
- `src/ManiaScriptSharp.Generator`: C# to ManiaScript translation. Begin behavior changes in `ManiaScriptGenerator`, `Emission/`, `TypeMapper`, `Naming/`, or `Diagnostics` according to ownership.
- `src/ManiaScriptSharp.ApiGenerator`: parses Nadeo API inputs and emits C# stubs; separate from script translation.
- `src/ManiaScriptSharp.{ManiaPlanet,ManiaPlanet3,Trackmania}`: target-specific generated API surfaces. Do not hand-edit generated `.g.cs` files; change generator/input or hand-authored partials instead.
- `tests/ManiaScriptSharp.Generator.Tests`: Roslyn-backed emitter tests. Add focused `[Fact]` output/diagnostic coverage beside owning emitter test.

## README sync (important)

[README.md](../README.md) documents the C# ↔ ManiaScript mapping, one table/section per
feature. Any change to translation behavior (new mapping, new syntax support, new
diagnostic) must update the matching README table/section in the same change — including
small mapping fixes, where the shown example output must match the corrected behavior.

For event, LINQ, and ManiaScript language details, link to existing docs instead of duplicating them: [event translation](../docs/event-translation.md), [LINQ translation](../docs/linq-translation.md), and [language reference](../docs/ManiaScript-Language-Reference.md).

# Caveman Mode
* Drop: articles (a/an/the), filler (just/really/basically), pleasantries, hedging.
* Fragments OK. Short synonyms. Technical terms exact. Code unchanged.
* Pattern: [thing] [action] [reason]. [next step].
* Not: "Sure! I'd be happy to help you with that."
* Yes: "Bug in auth middleware. Fix:"
* Auto-Clarity: drop caveman for security warnings, irreversible actions, user confused. Resume after.
