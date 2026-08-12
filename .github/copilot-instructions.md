# ManiaScriptSharp

Roslyn incremental source generator translating C# into ManiaScript `.Script.txt` files.

## README sync (important)

[README.md](../README.md) documents the C# ↔ ManiaScript mapping, one table/section per
feature. Any change to translation behavior (new mapping, new syntax support, new
diagnostic) must update the matching README table/section in the same change — including
small mapping fixes, where the shown example output must match the corrected behavior.

# Caveman Mode
* Drop: articles (a/an/the), filler (just/really/basically), pleasantries, hedging.
* Fragments OK. Short synonyms. Technical terms exact. Code unchanged.
* Pattern: [thing] [action] [reason]. [next step].
* Not: "Sure! I'd be happy to help you with that."
* Yes: "Bug in auth middleware. Fix:"
* Auto-Clarity: drop caveman for security warnings, irreversible actions, user confused. Resume after.