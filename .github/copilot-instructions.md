# ManiaScriptSharp

Roslyn incremental source generator translating C# into ManiaScript `.Script.txt` files.

## README sync (important)

[README.md](../README.md) documents the C# ↔ ManiaScript mapping, one table/section per
feature. Any change to translation behavior (new mapping, new syntax support, new
diagnostic) must update the matching README table/section in the same change — including
small mapping fixes, where the shown example output must match the corrected behavior.
