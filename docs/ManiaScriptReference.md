# ManiaScript Language Reference

ManiaScript is the scripting language used across ManiaPlanet-based games. It lets you create game modes, map editor plugins, server plugins, and build more interactive manialinks and user interfaces.

## Table of Contents

1. [Syntax basics](#syntax-basics)
2. [Simple data types](#simple-data-types)
3. [Variables](#variables)
4. [Comments](#comments)
5. [Operators](#operators)
6. [Control flow](#control-flow)
7. [Functions](#functions)
8. [Advanced types](#advanced-types)
9. [Directives](#directives)
10. [Labels](#labels)
11. [Timing instructions](#timing-instructions)
12. [Conventions](#conventions)
13. [Script contexts and host structure](#script-contexts-and-host-structure)
14. [Reference card](#reference-card)

---

## Syntax basics

Instructions are separated by semicolons, as in C/C++:

```
declare MyVar = 12;
MyVar += 1;
DoSomething(MyVar);
```

Curly braces create blocks. `if`, `else`, and loops can have a single instruction without braces:

```
if (Player == Null) return;
else if (Player.IsBot) Player.Score += 1;
```

Use braces for multi-instruction bodies and whenever it makes the scope clearer.

---

## Simple data types

| Type | Description |
|------|-------------|
| `Boolean` | `True` or `False` |
| `Integer` | `2`, `-5`, or `31337` |
| `Real` | `-4.2` or `99.` (the trailing dot is required, `99` is an Integer) |
| `Text` | `"plop"`, `"gouzi"`, `"456.32"` |
| `Ident` | An object identifier, its empty value is `NullId` |
| `Vec2` / `Vec3` | Two- and three-component real vectors |
| `Int2` / `Int3` | Two- and three-component integer vectors |

`Void` is a function return type, not a variable type. Engine-provided class types (normally named with a `C` prefix, such as `CSmPlayer` or `CMlFrame`) refer to existing objects and use `Null` as their empty value.

### Literal forms

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

If the enum's containing class is already your current context, you can leave the class name
out:

```
declare Direction = ::CardinalDirections::North;
```

### Text literals

Use ordinary double-quoted text for short, fixed values. The usual escapes work: `\n` for a
newline, `\"` for a quote, and `\\` for a backslash:

```
declare Greeting = "Welcome";
declare Message = "First line\nSecond line";
declare Quoted = "The button is named \"Start\".";
```

#### Multiline strings

A multiline string is still just a `Text`. It starts and ends with three double quotes:
`"""..."""`. It is not a separate XML or template type.

Inside it, indentation and newlines are preserved, regular quotes do not end the string, backslashes stay as they are, and line
breaks are preserved. This is handy for Manialink markup and regular expressions where escaping
would otherwise make the text difficult to read.

You can interpolate values with `{{{ Expression }}}`. The expression runs when the
statement runs, and its text form is inserted into the string.

For example, this creates one `Text` value from literal segments and two runtime expressions:

```
declare Text PlayerName = "Alice";
declare Integer FinishTime = 65321;
declare Summary = """Player: {{{PlayerName}}}
Time: {{{FinishTime / 1000.}}} seconds""";
```

The delimiters and interpolation marker are syntax, not content. You cannot put an unbroken
`"""` inside a multiline string, and `{{{` always starts an interpolation.

Each multiline string is limited to 65535 bytes. The limit is measured in UTF-8 bytes, not characters, so non-ASCII text can reach the limit in fewer characters. Larger Manialink documents can be divided into fragments and joined with `^`; no character is added at the join:

```
declare PageXml =
    """<manialink>
    <label id="status" text="Loading" />
""" ^
    """    <label id="detail" text="Please wait" />
</manialink>""";
```

In short: use ordinary text for small values, and use multiline strings when their literal
rules make the content easier to maintain.

**Multiline manialink:**

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

#### Localized text

Wrap text in `_()` for localization. It can be used for settings,
commands, and general text:

```
#Setting S_TimeLimit 600 as _("Time limit")
declare Caption = _("Waiting for players");
```

---

## Variables

### Variable declaration

Variables must be declared by specifying a type or an initial value:

```
declare Integer MyVariable;
declare MyVariable = 42;
```

Once declared, the type stays fixed. If you omit the value, ManiaScript gives the variable its
normal default value.

```
declare Planets = 9000;         // Planets is cast to Integer
Planets = "9000 planets";       // ERROR: type mismatch

declare Text ServerName;        // initial value is empty string
ServerName = "My testing server";
log(ServerName ^ " has currently " ^ Planets ^ "p.");
```

### Variable scope

Curly braces define scope. A variable is available from its declaration until the matching closing
brace:

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

### Variable Assignment

```
declare Text SomeVar;
SomeVar = "foo";
SomeVar = "bar";
```

There is no implicit conversion here: the types must match.

**Common declaration patterns:**

```
declare Integer RoundCount = 0;      // explicit type and initial value
declare IsWarmUp = True;             // type inferred as Boolean
declare Text[] PendingMessages = []; // empty list with an explicit element type
declare Integer[Text] ScoresByLogin = [];

ScoresByLogin["Alice"] = 42;
declare AliceScore = ScoresByLogin.get("Alice", 0);
```

### Global variables

Globals live outside functions. They require declaring their type explicitly and you cannot initialize them inline:

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

### Extension variables (`declare for`)

Variables can be attached to existing objects using the `for` clause. This is also referred to as a "local" declaration when no extra keyword is specified:

```
declare Text SomeVar for LocalUser;
declare Integer SomeOtherVar for LocalUser = 42;
```

The initial value is the type's default when the object hasn't initialized the variable yet.

The name before `for` is stored on the target object. Add `as` when your script needs a
different local name for that value. This is especially useful when several objects use the
same object-side name:

```
declare Integer CustomScore as PlayerOneScore for Players[0];
declare Integer CustomScore as PlayerTwoScore for Players[1];

PlayerOneScore = 42; // writes to CustomScore on Players[0]
PlayerTwoScore = 17; // writes to CustomScore on Players[1]
```

**`declare for` variants:**

| Form | Description |
|------|-------------|
| `declare Type Name for Object` | Local extension variable (no special behaviour) |
| `declare metadata Type Name for Object` | Metadata variable (stored in map/replay metadata) |
| `declare persistent Type Name [for Object]` | Persistent variable (it may be script-scoped or attached to an object) |
| `declare netwrite Type Name for Object` | Network-synchronized output variable (sender side) |
| `declare netread Type Name for Object` | Network-synchronized input variable (receiver side) |

`persistent` does **not** always need `for`. Script-scoped persistent values are common:

```
declare persistent Boolean Persistent_MapRestarted = False;
declare persistent Boolean[Text] Persistent_ModuleVisibilities;
```

`metadata`, `netwrite`, and `netread` need a `for` target. Just like a normal declaration, the
type can be inferred when the initializer makes it obvious.

**Storage modifier examples:**

```
declare metadata Text AuthorNote for Map = "";
declare persistent Boolean Persistent_HasSeenIntro = False;
declare netwrite Boolean Net_IsPaused for UI;
```

### Network variables (`netwrite` / `netread`)

The game mode (server) and manialinks (client) are separate layers. They do not share variables
directly, so use network variables to pass data between them.

Think of it as a small input/output system:
- **`netwrite`**: declares the variable on the *sending* side (registers a value to transmit)
- **`netread`**: declares the variable on the *receiving* side (reads the transmitted value)

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

**Important:**

You cannot assign to a `netread` variable, as it is read-only on the receiving side (the script will crash).

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

Mixing `Real` and `Integer` gives you a `Real`.

```
declare Integer Checkpoints = 3;
declare Real Bonus = 0.5;
declare Total = Checkpoints + Bonus; // Real: 3.5

Checkpoints *= 2; // 6
Checkpoints %= 4; // 2
```

### String concatenation

Use `^` to join values. Everything gets converted to `Text`:

```
MyVar = "Hello " ^ "world!";
```

With multiline strings, embed variables or expressions using `{{{ }}}`:

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

### Increment or decrement

| Operator | Description |
|----------|-------------|
| `+=` | Add to current |
| `-=` | Subtract from current |
| `*=` | Multiply current |
| `/=` | Divide current |
| `%=` | Apply remainder to current |

### Members, namespaces, and class casts

Use `.` for a member or property. Use `::` for a library, class, enum, struct, or constant.
When you need a more specific class, narrow the reference with `as` first:

```
declare Frame <=> (Control as CMlFrame);
if (Frame != Null) {
    declare Label <=> (Frame.GetFirstChild("title") as CMlLabel);
    if (Label != Null) Label.Value = "Ready";
}
```

It's good to check a class reference against `Null` before using it. `NullId` is only for `Ident`
values.

---

## Control flow

### If / Else

```
if (List.count > 2) {
    DoSomething(List);
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
    DoSomething(List);
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

`switchtype` is the special form for checking which class type an object currently is:

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

The numeric form includes both ends of the range. Give it an optional step, including a
negative one when you want to go backwards:

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

`for` can also iterate collections, including key/value pairs.

```
for (Player in Players) {
    log(Player.Login);
}

```

The `reverse` forms below only exist in Trackmania (2020) script contexts:

```
for (Id => Player in reverse PlayersById) {
    log(Id ^ ": " ^ Player.Login);
}

foreach (Id => Player in PlayersById reverse) {
    log(Player.Login);
}
```

Don't use `reverse` in ManiaPlanet scripts.

Use `break;` when you are done with the loop, and `continue;` to skip straight to its next
iteration.

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

### Defining a function

```
Integer Sum(Integer _A, Integer _B) {
    return _A + _B;
}
```

- `Void` means the function returns nothing.
- `return;` leaves a `Void` function early; `return Expression;` returns a value from a typed one.
- Define a function before you call it.
- Self-recursion works, but a circular chain between separate functions does not.

### Calling a function

```
declare Result = Sum(23, 19);
```

Use a `Void` function for an action. An early `return;` is great for simple guard clauses:

```
Void SetLabel(CMlLabel _Label, Text _Value) {
    if (_Label == Null) return;

    _Label.Value = _Value;
}

SetLabel(TitleLabel, "Ready");
```

### Overloading (polymorphism)

You can reuse a function name when its argument types differ:

```
Integer Sum(Integer _A, Integer _B) { return _A + _B; }
Real Sum(Real _A, Real _B) { return _A + _B; }
```

For optional arguments, put the more generic function first:

```
Void DoSomething(CMlControl _Control, Boolean _Flag) { /* ... */ }
Void DoSomething(CMlControl _Control) {
    DoSomething(_Control, True);
}
```

### The `main()` entry point

Top-level instructions are the script's implicit entry point. Use an explicit `main()` block
only when the script also defines at least one function:

```
Text GetGreeting() {
    return "Hello";
}

main() {
    declare Text Greeting = GetGreeting();
    log(Greeting);
}
```

If the script has no functions, leave out `main()` and write its instructions at the top level.

---

## Advanced types

### Lists

```
declare Text[] MyList;
MyList = ["Alpha", "Beta", "Gamma", "Omega"];

log(MyList[0]); // Alpha
log(MyList[3]); // Omega
```

**List operations:**

`count` is a property, so it has no parentheses. Sorting, mutation, lookup, and slice operations
work on lists and, where their key/value behaviour applies, keyed arrays. JSON operations work on
serializable values.

| Operation | Description |
|-----------|-------------|
| `.count` | Number of elements |
| `.sort()` | Returns the collection sorted by value |
| `.sortreverse()` | Returns the collection sorted by value in reverse order |
| `.sortkey()` | Returns a keyed array sorted by key |
| `.sortkeyreverse()` | Returns a keyed array sorted by key in reverse order |
| `.add(Value)` | Adds a value at the end |
| `.addfirst(Value)` | Adds a value at the beginning |
| `.remove(Value)` | Removes a value |
| `.removekey(Key)` | Removes the element at a list index or array key |
| `.exists(Value)` | Tests whether a value exists |
| `.existskey(Key)` | Tests whether a list index or array key exists |
| `.keyof(Value)` | Gets the key of a value |
| `.get(Key)` | Gets a value. The script errors if `Key` is missing. |
| `.get(Key, DefaultValue)` | Gets a value, or returns `DefaultValue` when the key is missing. |
| `.clear()` | Removes every element |
| `.containsonly(Values)` | Tests whether every value is also in `Values` |
| `.containsoneof(Values)` | Tests whether at least one value is also in `Values` |
| `.slice(Start)` / `.slice(Start, Length)` | Returns part of a collection |
| `.tojson()` | Serializes a value to JSON `Text` |
| `.fromjson(JsonText)` | Loads a value from JSON `Text`, returns whether **entire JSON** was successfully parsed |

### Associative arrays

Associative arrays are keyed collections, so you can use a custom key type:

```
declare Text[Integer] MyArray1 = [15 => "Quinze", 42 => "Quarante-deux", 100 => "Cent"];
declare Real[Text] MyArray2 = ["Pi" => 3.14, "Tau" => 6.28];

log(MyArray1[42]);     // Quarante-deux
log(MyArray2["Tau"]);  // 6.28
MyArray2["SquareRootOfTwo"] = 1.41; // Add new entry
```

You can nest them too:

```
declare Text[Text][] UsersData;
UsersData = [
    ["login" => "me", "name" => "still me"],
    ["login" => "you", "name" => "still you"]
];
log(UsersData[0]["login"]); // me
```

You can combine collection suffixes, for example, `Ident[][]` is a nested list, while `Ident[][Integer]`
is an `Integer`-keyed array of identifier lists.

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

Build structs with named fields when you only want to set some values. Anything you leave out
keeps its default value. You can initialize them like this:

```
declare MyStruct Value = MyStruct {
    MyMember = 1,
    MyTextMember = "ready"
};

declare MyStruct Empty = MyStruct {};
```

Import a struct from a library under a local name like this:

```
#Struct SomeLibrary::K_Result as K_Result
```

This only aliases the imported type. It does not create a new struct.

### Vectors

```
declare Vec2 V2 = <1.0, 2.0>;
declare Vec3 V3 = <1.0, 2.0, 3.0>;
declare Int3 Color = <0, 255, 0>;
```

Use either names (`V.X`, `V.Y`, `V.Z`) or indexes (`V[0]`, `V[1]`, `V[2]`) for vector
components.

### Classes and aliases

**You cannot define a new class or instantiate one directly.** A class variable is a pointer to an
object the engine already owns.

An **alias** (`<=>`) follows the *expression*, not the resolved value:

```
declare BestPlayer <=> Players[0]; // BestPlayer always means "the player at index 0"
Players[1].Score += 1000;
log(BestPlayer.Login); // May now log a different player if scores changed
```

A regular assignment (`=`) resolves the object by its `Id` at assignment time:

```
declare BestPlayer = Players[0]; // BestPlayer refers to Alice's Id
Players[1].Score += 1000;
log(BestPlayer.Login); // Still logs Alice
```

Rebind an existing alias with `<=>`. That includes assigning `Null` when you want to clear a
class reference:

```
CurrentLayer <=> UIManager.UILayerCreate();
// ...
CurrentLayer <=> Null;
```

---

## Directives

Put directives at the top of the script. They start with `#` and never end with a semicolon.

| Directive | Description |
|-----------|-------------|
| `#RequireContext ContextType` | Declares the required host context |
| `#Const C_Name Value` | Declares an immutable constant |
| `#Const Namespace::C_Name as C_LocalName` | Imports a constant under a local name |
| `#Setting S_Name Value` | Like `#Const`, but can be modified externally |
| `#Command Name (Type) as _("Description")` | Declares a host-exposed command, used by game-mode scripts |
| `#Include "XXX" as YYYY` | Loads a library or file and binds it to namespace `YYYY` |
| `#Extends "XXX"` | Extends a base script and its host-defined labels |
| `#Struct Name { }` | Declares a struct type |
| `#Struct Namespace::Name as LocalName` | Imports a struct type under a local name |

Common contexts:
- `CMode` (`CTmMode` / `CSmMode`)
- `CMapType` (`CTmMapType` / `CSmMapType`)
- `CMapEditorPlugin`
- `CManiaAppTitle`
- `CManiaAppStation`
- `CManiaAppBrowser`
- `CServerPlugin`

Your context decides which engine objects, events, labels, and API members you can use.

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

Use the literal `"<hidden>"` label when a setting should stay out of the normal settings UI.
It is a special host value, not localized text, so don't wrap it in `_()`:

```
#Setting S_InternalOption 1 as "<hidden>"
```

**Command example:**

```
#Command Command_SetPause (Boolean) as _("Pause the game")
```

This adds a command section to the "Manage server" menu.

---

## Labels

Labels are named insertion points supplied by a base script. An extension selects that base with
`#Extends` and supplies code for the labels it exposes. They are not functions: when the script
is assembled, the extension's code is "inserted" at the label marker in the base script.

The base script chooses how a label is resolved:

- `+++AfterStart+++` is additive. Code from every applicable extension is inserted at this point.
- `---AfterStart---` is an override. Only one extension implementation is selected and inserted.

For example, a base script can leave an additive label between two statements:

```
Void Private_Start() {
    log("Before start");
    {+++AfterStart+++}
    log("After start");
}
```

An extension of that script supplies the code for `AfterStart`:

```
#Extends "Base.Script.txt"

***AfterStart***
***
declare Text Message = "The extension has started";
log(Message);
***
```

The assembled script runs the extension's `log` between the two base-script statements. The braces
are important: `Message` exists only inside that block and cannot conflict with variables later in
`Private_Start`.

---

## Timing instructions

These instructions pause the whole script. While it is paused, display updates, simulation, and
event processing wait too.

| Instruction | Description |
|-------------|-------------|
| `yield;` | Pauses for the shortest possible time (1 frame) |
| `sleep(XXXX);` | Pauses for `XXXX` milliseconds |
| `wait(YYYYY);` | Pauses until the Boolean expression `YYYYY` is `True` |

> **Important:** `sleep()` in an event-driven script makes you miss events
> that arrive during the sleep. Prefer `wait` with a compound condition:
> ```
> Start = Now;
> wait(Now > Start + 1000 || PendingEvents.count >= 1);
> ```

### Log and assert

```
log("Something went wrong!");       // Prints to the debug window (Ctrl+G)
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

### Control structure formatting

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

### Typical mode directives

```
#RequireContext CSmMode
#Const Version            "1.2"
#Const ScriptName         "Modes/ShootMania/Example.Script.txt"
```

Provide `Version` and `ScriptName` to show the script's version with the `/version` command.
