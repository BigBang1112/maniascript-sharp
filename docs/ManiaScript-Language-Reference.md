# ManiaScript Language Reference

ManiaScript is the scripting language used across ManiaPlanet-based games. It lets you create game modes, map editor plugins, server plugins, and build more interactive manialinks and user interfaces.

## Table of Contents

1. [Syntax basics](#syntax-basics)
2. [Simple Data Types](#simple-data-types)
3. [Variables](#variables)
4. [Comments](#comments)
5. [Operators](#operators)
6. [Control Flow](#control-flow)
7. [Functions](#functions)
8. [Advanced Types](#advanced-types)
9. [Directives](#directives)
10. [Labels](#labels)
11. [Timing Instructions](#timing-instructions)
12. [Conventions](#conventions)
13. [Script Contexts and Host Structure](#script-contexts-and-host-structure)
14. [Reference Card](#reference-card)

---

## Syntax basics

A script is text composed of instructions. Ordinary instructions are separated by semicolons, as in C/C++:

```
declare MyVar = 12;
MyVar += 1;
DoSomething(MyVar);
```

Curly braces delimit blocks. `if`, `else`, and loop bodies may also contain a single unbraced instruction:

```
if (Player == Null) return;
else if (Player.IsBot) Player.Score += 1;
```

Use braces for multi-instruction bodies and whenever it makes the scope clearer.

### Templates and Manialink Content

Some hosts use a composition/template layer in addition to ManiaScript, including labels and placeholders such as `{{{P}}}` in identifiers or embedded Manialink XML. Such placeholders are expanded before the resulting script runs; they are not general-purpose expression delimiters. Inside a triple-quoted `Text`, `{{{ Expression }}}` interpolates an expression (see [Text Literals](#text-literals)).

---

## Simple Data Types

| Type | Description |
|------|-------------|
| `Boolean` | Can be either `True` or `False` |
| `Integer` | Numbers such as `2`, `-5`, or `31337` |
| `Real` | Decimal numbers such as `-4.2` or `99.` (the trailing dot is required — `99` is an Integer) |
| `Text` | Any character sequence between double quotes: `"plop"`, `"gouzi"`, `"456.32"` |
| `Ident` | An engine object identifier; its empty value is `NullId` |
| `Vec2` / `Vec3` | Two- and three-component real vectors |
| `Int2` / `Int3` | Two- and three-component integer vectors |

`Void` is a function return type, not a variable type. Engine-provided class types (normally named with a `C` prefix, such as `CSmPlayer` or `CMlFrame`) refer to existing engine objects and use `Null` as their empty value.

### Literal Forms

```
declare Integer Count = 42;
declare Real Ratio = .5;
declare Real WholeReal = 99.;
declare Boolean Enabled = True;
declare Ident PlayerId = NullId;
declare Vec2 Position = <10., -4.5>;
declare Vec3 Color = <1., .5, 0.>;
declare Int2 Range = <0, 10>;
```

Enum values from the engine and libraries are qualified with `::`:

```
declare Direction = CBlock::CardinalDirections::North;
```

### Text Literals

Use ordinary double-quoted text for short, fixed values. Common escape sequences include `\n` for a newline, `\"` for a quote, and `\\` for a backslash:

```
declare Greeting = "Welcome";
declare Message = "First line\nSecond line";
declare Quoted = "The button is named \"Start\".";
```

#### Triple-Quoted Text

A triple-quoted literal is a `Text` value that begins and ends with three double quotes: `"""..."""`. It is still a `Text`, not a separate string, XML, or template type.

Between the delimiters, ordinary double quotes do not end the literal, backslashes are kept as text, and physical line breaks are preserved. This makes triple-quoted text suitable for content whose readability would suffer from escaping, such as markup and regular expressions. Whitespace is significant: indentation and newlines between the delimiters become part of the resulting value.

Triple-quoted text can include interpolation. The syntax `{{{ Expression }}}` evaluates `Expression` when the surrounding statement executes and appends its text representation.

For example, this creates one `Text` value from literal segments and two runtime expressions:

```
declare Text PlayerName = "Alice";
declare Integer FinishTime = 65321;
declare Summary = """Player: {{{PlayerName}}}
Time: {{{FinishTime / 1000.}}} seconds""";
```

The delimiters and interpolation marker are syntax, not content. A triple-quoted literal cannot contain an unbroken `"""` delimiter, and `{{{` begins interpolation. When either sequence must appear literally, compose it from smaller text values:

```
declare Quote = "\"";
declare ThreeQuotes = Quote ^ Quote ^ Quote;
declare OpenInterpolation = "{{" ^ "{";
```

For practical compatibility, keep each triple-quoted literal below roughly 50,000 source characters. This is a practical split point rather than a formally specified language limit. Larger Manialink documents can be divided into fragments and joined with `^`; no character is added at the join. For generated text, splitting at about 49,000 characters leaves a small margin:

```
declare PageXml =
    """<manialink>
    <label id="status" text="Loading" />
""" ^
    """    <label id="detail" text="Please wait" />
</manialink>""";
```

Use ordinary quoted text for short values and triple-quoted text when its literal-content rules are helpful.

**Multiline Manialink/XML:**

```
declare Manialink = """<manialink>
    <label id="status" text="Waiting for players" />
</manialink>""";
```

**Formatted status or log message:**

```
declare Status = """Player: {{{Player.Login}}}
Score: {{{Player.Score}}}""";
log(Status);
```

**Regular expression:**

```
declare TagPattern = """<\s*{{{TagName}}}\b[^>]*\/?>""";
```

The same pattern in an ordinary double-quoted string would require doubled backslashes, such as `"<\\s*...\\b...\\/?>"`.

#### Localized Text

`_()` marks a text literal for localization and is used in settings, commands, and UI text:

```
#Setting S_TimeLimit 600 as _("Time limit")
declare Caption = _("Waiting for players");
```

---

## Variables

### Variable Declaration

Variables must be declared by specifying a type or an initial value:

```
declare Integer MyVariable;
declare MyVariable = 42;
```

After declaration, the type cannot change. Variables are always initialized to a default value if not specified.

```
declare Planets = 9000;         // Planets is cast to Integer
Planets = "9000 planets";       // ERROR: type mismatch

declare Text ServerName;        // initial value is empty string
ServerName = "My testing server";
log(ServerName ^ " has currently " ^ Planets ^ "p.");
```

### Variable Scope

Scope is defined by matching curly brackets `{ }`. Variable visibility is limited to the enclosing scope following the declaration.

```
main() {
    declare Text Status = "Waiting";

    if (True) {
        declare Integer RetryCount = 0;
        RetryCount += 1;
        Status = "Retry " ^ RetryCount;
    }

    log(Status); // available here
    // RetryCount is no longer in scope here
}
```

### Global Variables

Defined outside all functions. Cannot be initialized with a value inline in the global scope (explicit type is required):

```
declare Text G_GlobalVariable;
```

Globals are available to functions declared in the same script:

```
declare Integer G_CompletedRounds;

Void RecordCompletedRound() {
    G_CompletedRounds += 1;
}
```

### Extension Variables (`declare for`)

Variables can be attached to existing objects using the `for` clause. This is also referred to as a "local" declaration when no extra keyword is specified:

```
declare Text SomeVar for LocalUser;
declare Integer SomeOtherVar for LocalUser = 42;
```

The initial value acts as a default when the object hasn't initialized the variable yet.

**Aliasing** is required when two objects of the same type share the same variable name:

```
declare Integer CustomScore for Players[0];
declare Integer CustomScore for Players[1];
```

An explicit `as` clause gives the variable two distinct names: the one before `as` is the
object-side storage name, the one after `as` is the alias the rest of the script references:

```
declare Integer SomeObject_Name as SomeAlias for Object;
SomeAlias = 42; // writes to SomeObject_Name on the object side
```

**`declare for` variants:**

| Form | Description |
|------|-------------|
| `declare Type Name for Object` | Local extension variable (no special behaviour) |
| `declare Type Name as Alias for Object` | Aliased — `Alias` is used in the script, `Name` is stored on the object |
| `declare metadata Type Name for Object` | Metadata variable (stored in map/replay metadata) |
| `declare persistent Type Name [for Object]` | Persistent variable; it may be script-scoped or attached to an object |
| `declare netwrite Type Name for Object` | Network-synchronized output variable (sender side) |
| `declare netread Type Name for Object` | Network-synchronized input variable (receiver side) |

`persistent` does **not** always require `for`; script-scoped declarations are used extensively:

```
declare persistent Boolean Persistent_MapRestarted = False;
declare persistent Boolean[Text] Persistent_ModuleVisibilities;
```

`metadata`, `netwrite`, and `netread` use a `for` target. As with ordinary declarations, the type can be inferred from an initializer when the initializer has an unambiguous type.

**Storage modifier examples:**

```
declare metadata Text AuthorNote for Map = "";
declare persistent Boolean HasSeenIntro = False;
declare netwrite Boolean Net_IsPaused for UI;
```

### Network Variables (`netwrite` / `netread`)

The game mode script (server) and manialink scripts (client) are two separate layers that don't communicate directly. To share data between them, use network-synchronized variables.

This works as an input/output system:
- **`netwrite`** — declares the variable on the *sending* side (registers a value to transmit)
- **`netread`** — declares the variable on the *receiving* side (reads the transmitted value)

**Sending from script to manialink (server → client):**

In the game mode script:
```
declare UI <=> UIManager.GetUI(Player);
declare netwrite Integer Net_MyVariable for UI;
Net_MyVariable = 42;
```

In the manialink script:
```
declare netread Integer Net_MyVariable for UI;
log(Net_MyVariable); // reads 42
```

**Addressing targets:**

| Target | Scope |
|--------|-------|
| `for UI` | Addressed to a specific player (retrieve with `UIManager.GetUI(Player)`) |
| `for Teams[0]` | Addressed to all players in team 0 |
| `for Teams[1]` | Addressed to all players in team 1 |

**Important rules:**

- You cannot name a `netread`/`netwrite` variable with the same name as an existing standard variable (name conflict).
- You cannot assign to a `netread` variable — it is read-only on the receiving side (the script will crash).

**Modifiers summary:**

- `persistent` — Variable persists when revisiting the script (like cookies). Note: limited storage; type cannot change.
- `metadata` — Variable is stored in map or replay metadata.
- `netread` / `netwrite` — Used for network-synchronized variables between script and manialink layers.

### Variable Assignment

```
declare Text SomeVar;
SomeVar = "foo";
SomeVar = "bar";
```

No implicit type conversion — types must match.

**Common declaration patterns:**

```
declare Integer RoundCount = 0;      // explicit type and initial value
declare IsWarmUp = True;             // type inferred as Boolean
declare Text[] PendingMessages = []; // empty list with an explicit element type
declare Integer[Text] ScoresByLogin = [];

ScoresByLogin["Alice"] = 42;
declare AliceScore = ScoresByLogin.get("Alice", 0);
```

---

## Comments

```
Var = 2 + 5; // This is a line comment

Var = 2 /* This is an inline comment */ + 5;

/*
   This is a
   multi-line comment
*/
```

---

## Operators

### Mathematical

| Operator | Description |
|----------|-------------|
| `+` | Add |
| `-` | Subtract |
| `*` | Multiply |
| `/` | Divide |
| `%` | Remainder |

Mixing `Real` and `Integer` produces a `Real`.

```
declare Integer Checkpoints = 3;
declare Real Bonus = 0.5;
declare Total = Checkpoints + Bonus; // Real: 3.5

Checkpoints *= 2; // 6
Checkpoints %= 4; // 2
```

### String Concatenation

Use `^` to join values. Non-`Text` values are automatically converted:

```
MyVar = "Hello " ^ "world!";
```

With triple-quote strings, embed variables or expressions using `{{{ }}}`:

```
MyVar = """Hello {{{NameOfThePlayer}}}, how are you? Five = {{{2+3}}}.""";
```

### Boolean

| Operator | Description |
|----------|-------------|
| `!` | NOT |
| `&&` | AND |
| `\|\|` | OR |

### Comparison

| Operator | Description |
|----------|-------------|
| `==` | Equals |
| `!=` | Not equals |
| `<` | Less than |
| `>` | Greater than |
| `<=` | Less or equal |
| `>=` | Greater or equal |

> Greater/lower comparisons do not work with `Boolean`.

```
declare Boolean HasEnoughPlayers = Players.count >= 2;
declare Boolean CanStart = HasEnoughPlayers && !GameIsPaused;

if (CanStart || IsLocalMode) {
    log("Starting race");
}
```

### Increment / Decrement

| Operator | Description |
|----------|-------------|
| `+=` | Add to current |
| `-=` | Subtract from current |
| `*=` | Multiply current |
| `/=` | Divide current |
| `%=` | Apply remainder to current |

### Members, Namespaces, and Class Casts

Use `.` for a member or property and `::` to qualify a library, class, enum, struct, or constant. Use `as` to narrow a class reference before accessing class-specific members:

```
declare Frame <=> (Control as CMlFrame);
if (Frame != Null) {
    declare Label <=> (Frame.GetFirstChild("title") as CMlLabel);
    if (Label != Null) Label.Value = "Ready";
}
```

Test class references against `Null` before dereferencing them. `NullId` is only for `Ident` values.

---

## Control Flow

### If / Else

```
if (List.count > 2) {
    doSomething(List);
} else {
    log("List has too few items");
}
```

```
if (List.count == 0) {
    log("No items");
} else if (List.count <= 2) {
    log("Too few items");
} else {
    doSomething(List);
}
```

### Switch

```
switch (Block.Direction) {
    case CBlock::CardinalDirections::North: {
        log("Facing north");
    }
    case CBlock::CardinalDirections::South: {
        log("Facing south");
    }
    default: {
        log("Facing east or west");
    }
}
```

A special form `switchtype` checks whether a class instance matches a certain type:

```
switchtype (Control) {
    case CMlEntry: log((Control as CMlEntry).Value);
    case CMlTextEdit: log((Control as CMlTextEdit).Value);
    default: log("not an input element");
}
```

### While

```
declare ItemCount = 10;
while (ItemCount > 0) {
    ItemCount -= 1;
}
```

### For

The range form takes all values from `FirstValue` to `LastValue` (inclusive). It accepts an optional step, including a negative step for reverse numeric traversal:

```
for (I, 2, 5) {
    log(I); // logs 2, 3, 4, 5
}

for (I, Players.count - 1, 0, -1) {
    log(Players[I].Login);
}
```

### Foreach

```
declare MyList = ["foo", "bar", "baz"];
foreach (Item in MyList) {
    log(Item);
}
```

With index/key:

```
declare MyArray = [1 => "foo", 3 => "bar", 8 => "baz"];
foreach (Index => Item in MyArray) {
    log(Index ^ ": " ^ Item);
}
```

`for` also supports collection iteration, including key/value forms.

```
for (Player in Players) {
    log(Player.Login);
}

```

The `reverse` forms below are available only in Trackmania (2020) script contexts:

```
for (Id => Player in reverse PlayersById) {
    log(Id ^ ": " ^ Player.Login);
}

foreach (Id => Player in PlayersById reverse) {
    log(Player.Login);
}
```

Do not use `reverse` in ManiaPlanet scripts.

Use `break;` to exit a loop early and `continue;` to skip to the next iteration.

```
declare Integer[] Scores = [0, 12, 0, 25];
foreach (Score in Scores) {
    if (Score == 0) continue; // ignore unfinished entries

    log("Score: " ^ Score);
    if (Score >= 25) break;   // first qualifying score is enough
}
```

---

## Functions

### Defining a Function

```
Integer Sum(Integer _A, Integer _B) {
    return _A + _B;
}
```

- Return type `Void` means no value is returned.
- `return;` exits early from a `Void` function; `return Expression;` returns a value from a typed function.
- A function must be defined before it is called.
- Functions may call themselves recursively, but circular calls between functions are not allowed.

### Calling a Function

```
declare Result = Sum(23, 19);
```

Use a `Void` function for an action. An early `return;` is useful for guard clauses:

```
Void SetLabel(CMlLabel _Label, Text _Value) {
    if (_Label == Null) return;

    _Label.Value = _Value;
}

SetLabel(TitleLabel, "Ready");
```

### Overloading (Polymorphism)

Multiple functions can share the same name if their argument types differ:

```
Integer Sum(Integer _A, Integer _B) { return _A + _B; }
Real Sum(Real _A, Real _B) { return _A + _B; }
```

For optional arguments, declare the more generic function first:

```
Void doSomething(CMlControl _Control, Boolean _Flag) { /* ... */ }
Void doSomething(CMlControl _Control) {
    doSomething(_Control, True);
}
```

### The `main()` Function

The entry point of every script. No return type, no arguments:

```
main() {
    declare Text Nothing;
    declare Sentence = Hello();
    log(Sentence);
}
```

If the entire script fits inside `main()`, the function header and braces can be omitted.

---

## Advanced Types

### Lists

```
declare Text[] MyList;
MyList = ["Alpha", "Beta", "Gamma", "Omega"];

log(MyList[0]); // Alpha
log(MyList[3]); // Omega
```

**List operations:**

```
declare Size = List.count;
declare SortedList = List.sort();
declare ReversedList = List.sortreverse();

List.add(Value);
List.addfirst(Value);
List.removekey(Index);
List.remove(Value);

declare Exists = List.existskey(Index);
declare Exists2 = List.exists(Value);
declare Index = List.keyof(Value);
declare ValueOrDefault = List.get(Index, DefaultValue);

List.clear();
```

`get(Key, DefaultValue)` safely looks up a value in both lists and keyed arrays. It returns the supplied fallback rather than indexing a missing key.

### Arrays

Arrays allow a custom key type:

```
declare Text[Integer] MyArray1 = [15 => "Quinze", 42 => "Quarante-deux", 100 => "Cent"];
declare Real[Text] MyArray2 = ["Pi" => 3.14, "Tau" => 6.28];

log(MyArray1[42]);     // Quarante-deux
log(MyArray2["Tau"]);  // 6.28
MyArray2["SquareRootOfTwo"] = 1.41; // Add new entry
```

Arrays can be nested:

```
declare Text[Text][] UsersData;
UsersData = [
    ["login" => "me", "name" => "still me"],
    ["login" => "you", "name" => "still you"]
];
log(UsersData[0]["login"]); // me
```

Collection suffixes can be composed: `Ident[][]` is a nested list, while `Ident[][Integer]` is an `Integer`-keyed array of identifier lists.

### Structs

```
#Struct MyStruct {
    Integer MyMember;
    Text MyTextMember;
}

main() {
    declare MyStruct MyVar;
    log(MyVar.MyMember); // 0

    MyVar.MyMember = 1;
    log(MyVar.MyMember); // 1

    declare MyStruct MyCopy = MyVar;
    MyVar.MyMember = MyVar.MyMember + 1;

    log(MyVar.MyMember);  // 2
    log(MyCopy.MyMember); // 1
}
```

Struct values can be built with named fields; omitted fields retain their default value. This is the predominant initialization style in the newer scripts:

```
declare MyStruct Value = MyStruct {
    MyMember = 1,
    MyTextMember = "ready"
};

declare MyStruct Empty = MyStruct {};
```

An included library's struct can be made available under a local name:

```
#Struct SomeLibrary::K_Result as K_Result
```

This aliases the imported struct type; it does not define a new struct.

### Vectors

```
declare Vec2 V2 = <1.0, 2.0>;
declare Vec3 V3 = <1.0, 2.0, 3.0>;
declare Int3 Color = <0, 255, 0>;
```

Vector components can be accessed by name (`V.X`, `V.Y`, `V.Z`) or by index (`V[0]`, `V[1]`, `V[2]`).

### Classes and Aliases

You cannot declare new classes or instantiate them directly. You declare pointers to existing class objects.

**Alias (`<=>`)** — the variable refers to the *expression*, not the *resolved value*:

```
declare BestPlayer <=> Players[0]; // BestPlayer always means "the player at index 0"
Players[1].Score += 1000;
log(BestPlayer.Login); // May now log a different player if scores changed
```

**Regular assignment (`=`)** — resolves the object by its `Id` at the time of assignment:

```
declare BestPlayer = Players[0]; // BestPlayer refers to Alice's Id
Players[1].Score += 1000;
log(BestPlayer.Login); // Still logs Alice
```

> When in doubt, use `=`.

Rebind an existing alias with `<=>`, including to `Null` when clearing a class reference:

```
CurrentLayer <=> UIManager.UILayerCreate();
// ...
CurrentLayer <=> Null;
```

---

## Directives

Directives appear at the top of a script and begin with `#`. They do **not** end with a semicolon.

| Directive | Description |
|-----------|-------------|
| `#RequireContext ContextType` | Declares the required host context |
| `#Const Name Value` | Declares an immutable constant |
| `#Const Namespace::Name as LocalName` | Imports a constant under a local name |
| `#Setting XXX YYYY` | Like `#Const`, but can be modified externally |
| `#Command Name (Type) as _("Description")` | Declares a host-exposed command, used by game-mode scripts |
| `#Include "XXX" as YYYY` | Loads a library or file and binds it to namespace `YYYY` |
| `#Extends "XXX"` | Extends a base script and its host-defined labels |
| `#Struct Name { }` | Declares a struct type |
| `#Struct Namespace::Name as LocalName` | Imports a struct type under a local name |

Common context types include `CManiaApp`, `CManiaAppPlayground`, `CManiaAppTitle`, `CManiaplanetPlugin`, `CMap`, `CSmMapType`, `CSmMode`, and `CTmMode`. The context controls which engine objects, events, labels, and API members are available.

**Constants and struct declaration:**

```
#Const C_MaxPlayers 16
#Const C_DefaultColor <1., 1., 1.>

#Struct K_PlayerResult {
    Text Login;
    Integer Score;
}
```

**Include example:**

```
#Include "Library.Script.txt" as MyLib1
MyLib1::Function1();
```

**Setting example:**

```
#Setting S_TimeLimit  600 as _("Time limit")   ///<  Time limit on a map
#Setting S_PointLimit 25  as _("Points limit") ///<  Points limit on a map
```

**Command example:**

```
#Command Command_SetPause (Boolean) as _("Pause the game")
```

It adds a command section in the "Manage server" menu of the pause menu.

---

## Labels

Labels are extension points supplied by a base or extended script. They are host-defined, not a universal list of language events.

- `+++MyLabel+++` — an additive hook; multiple extensions may contribute code.
- `---MyLabel---` — an override hook; only one implementation (the selected/latest extension) applies.

Write label markers without inner spaces. Label names range from general lifecycle hooks such as `MainInit`, `MainStart`, and `MainLoop` to host-specific names such as `Match_StartMap` and `CMPlugins_AfterYield`.

**Defining label code:**

```
***MyLabel***
***
// code to run at this label
***
```

Labels share the scope of the insertion point. It is good practice to wrap the insertion in braces to avoid variable leakage:

```
Void fn1() {
    declare V = 3;
    {+++MyLabel+++}
}
```

Templates can parameterize label names, for example `+++{{{HookName}}}+++`; this is template expansion, not a runtime label lookup.

---

## Timing Instructions

These instructions pause script execution. During a pause, display updates, game simulation, and event processing are all suspended.

| Instruction | Description |
|-------------|-------------|
| `yield;` | Pauses for the shortest possible time (1 frame) |
| `sleep(XXXX);` | Pauses for `XXXX` milliseconds |
| `wait(YYYYY);` | Pauses until the Boolean expression `YYYYY` is `True` |

**Equivalents:**

```
yield; // equivalent to sleep(0)

// sleep can be written as:
Start = Now;
while (Now < Start + XXXX) {
    yield;
}

// wait can be written as:
while (!YYYYY) {
    yield;
}
```

> **Note:** Using `sleep()` in event-driven scripts (e.g., Manialink scripts) will cause you to miss events that occur during the sleep. Use `wait` with a compound condition instead:
> ```
> Start = Now;
> wait(Now > Start + 1000 || PendingEvents.count >= 1);
> ```

### Log and Assertions

```
log("Something went wrong!");       // Prints to the debug window (Ctrl+~)
assert(MyVariable == 3);            // Halts the script if the condition is False
```

---

## Conventions

### Files

- **Line endings:** Preserve the project's existing convention.
- **Encoding:** UTF-8 without BOM
- **Naming:** PascalCase
- **Extension:** `.Script.txt`

### Naming

| Element | Convention |
|---------|-----------|
| Settings | Prefix `S_`, PascalCase |
| Constants | Prefix `C_`, PascalCase |
| Global variables | Prefix `G_`, PascalCase |
| `netwrite`/`netread` vars | Prefix `Net_` |
| Persistent variables | Prefix `Persistent_` or `P_` |
| Function arguments | Prefix `_`, start with uppercase |
| Private functions | Prefix `Private_` |

### Control Structure Formatting

- One space after the keyword.
- No space after `(` or before `)`.
- One space between `)` and `{`.
- Body indented once.
- Closing brace on its own line.

**Example (`LibFoo.Script.txt`):**

```
#Const C_ConstantVariable 456

declare Integer G_GlobalVariable;

Void Private_DoNothing() {
}

Text DoSomething(Integer _Id) {
    declare Text Result;
    if (_Id == 42) {
        Private_DoNothing();
        Result = "42";
    } else {
        Result = "";
    }

    foreach (Player in Players) {
        declare Text LibFoo_VariableForSomething for Player;
        LibFoo_VariableForSomething = "42";

        declare netwrite Integer Net_LibFoo_Variable for Player;
        Net_LibFoo_Variable = 42;
    }

    return Result;
}
```

---

## Script Contexts and Host Structure

ManiaScript supports several host families, including game modes, ManiaApps, Manialink/UI layers, plugins, map types, and libraries. `#RequireContext` selects the engine context; `#Extends` selects a base script and the labels it exposes. A valid lifecycle label in one family is not necessarily valid in another.

### Typical Mode Directives

```
#RequireContext CSmMode
#Const Version            "1.2"
#Const ScriptName         "Modes/ShootMania/Example.Script.txt"

#Include "Libs/Nadeo/Message.Script.txt" as Message
#Include "TextLib" as TextLib

#Extends "Modes/ShootMania/Base/ModeShootmania.Script.txt"
// A Trackmania mode instead uses `#RequireContext CTmMode` and:
// #Extends "Modes/Trackmania/Base/ModeTrackmania.Script.txt"
```

Use an included library through its namespace:

```
declare One = TextLib::ToInteger("1");
declare MessageVersion = Message::Version;
```

### Context-specific Lifecycle Labels

Mode bases commonly organize work around server, match, map, round, turn, and play-loop stages. But the exact label names are supplied by the selected base script:

| Host family | Typical label families |
|-------------|-------------------------|
| Game modes | `Match_InitServer`, `Match_StartMap`, `Match_PlayLoop`, `Match_EndRound`; older bases also expose unprefixed lifecycle labels |
| Matchmaking mode bases | `Lobby_...`, `Match_...`, and `MB_Private_...` hooks |
| ManiaApps and UI modules | `MainInit`, `MainStart`, `MainLoop`; app-specific labels such as `InitApp` and `AppLoop` |
| Plugins | `CMPlugins_PluginStart`, `CMPlugins_AfterYield`, and synchronization hooks |

Read the extended base script before implementing a label. In particular, do not assume a `Lobby_`/`Match_` pair, an `InitServer` label, or a server-to-round hierarchy is available in every context.

**Example mode extension:**

```
***Match_StartMap***
***
ModeStatusMessage = "Current map: " ^ Map.MapInfo.Name;
***
```

---

## Reference Card

### Primitive Types

| Type | Description | Default |
|------|-------------|---------|
| `Boolean` | `True` or `False` | `False` |
| `Integer` | Integer in range $[-2^{31}, 2^{31}-1]$ | `0` |
| `Real` | Floating-point number | `0.` |
| `Text` | String of characters | `""` |
| `Vec2` | 2D vector `<X, Y>` | `<0., 0.>` |
| `Vec3` | 3D vector `<X, Y, Z>` | `<0., 0., 0.>` |
| `Int2` | 2D integer vector `<X, Y>` | `<0, 0>` |
| `Int3` | 3D integer vector `<X, Y, Z>` | `<0, 0, 0>` |
| `Ident` | Object identifier | `NullId` |
| `Void` | No value (function return only) | — |

Class types (e.g., `CSmPlayer`, `CMlLabel`) are composite types whose names start with `C`. They cannot be instantiated directly; you can only declare pointers to existing objects.

### Constant Values

| Type | Example Values |
|------|----------------|
| `Boolean` | `True`, `False` |
| `Text` | `"XXX"`, `"""XXX"""` |
| `Integer` | `123789` |
| `Real` | `123789.` or `.12312` |
| `Vec2` | `<Real1, Real2>` |
| `Vec3` | `<Real1, Real2, Real3>` |
| `Int2` | `<Integer1, Integer2>` |
| `Int3` | `<Integer1, Integer2, Integer3>` |
| `Ident` | `NullId` |
| Class | `Null` |
| List | `[]`, `[Value1, Value2]` |
| Keyed array | `[Key1 => Value1, Key2 => Value2]` |
| Struct | `MyStruct { Member = Value }` |
