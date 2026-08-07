# ManiaScript Language Reference

## Table of Contents

1. [Syntax Basics](#syntax-basics)
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
13. [Game Mode Script Structure](#game-mode-script-structure)
14. [Reference Card](#reference-card)

---

## Syntax Basics

A script is a text composed of lines (instructions). Instructions are separated by semicolons, as in C/C++:

```
declare MyVar = 12;
MyVar += 1;
DoSomething(MyVar);
```

> **Beware:** Case is important, always!

---

## Simple Data Types

| Type | Description |
|------|-------------|
| `Boolean` | Can be either `True` or `False` |
| `Integer` | Numbers such as `2`, `-5`, or `31337` |
| `Real` | Decimal numbers such as `-4.2` or `99.` (the trailing dot is required — `99` is an Integer) |
| `Text` | Any character sequence between double quotes: `"plop"`, `"gouzi"`, `"456.32"` |

**Protips:**

- Inside a `Text`, the usual escape sequences such as `"\n"` or `"\\"` are supported.
- You can declare a `Text` value between triple double quotes. No escaping is needed, and it can span multiple lines:
  ```
  """plop="452.12.22" toto"""
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

### Global Variables

Defined outside all functions. Cannot be initialized with a value inline in the global scope (explicit type is required):

```
declare Text G_GlobalVariable;
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
declare Integer CustomScore for Players[0] as CustomScore1;
declare Integer CustomScore for Players[1] as CustomScore2;
```

**`declare for` variants:**

| Form | Description |
|------|-------------|
| `declare Type Name for Object` | Local extension variable (no special behaviour) |
| `declare metadata Type Name for Object` | Metadata variable (stored in map/replay metadata) |
| `declare persistent Type Name for Object` | Persistent variable (stored on profile) |
| `declare netwrite Type Name for Object` | Network-synchronized output variable (sender side) |
| `declare netread Type Name for Object` | Network-synchronized input variable (receiver side) |

> **Important:** The modifiers `metadata`, `persistent`, `netwrite`, and `netread` **always** require a `for` clause — they cannot be declared without a target object. A plain `declare netwrite Integer X;` without `for` is invalid.

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

### Increment / Decrement

| Operator | Description |
|----------|-------------|
| `+=` | Add to current |
| `-=` | Subtract from current |
| `*=` | Multiply current |
| `/=` | Divide current |

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

The loop variable takes all values from `FirstValue` to `LastValue` (inclusive):

```
for (I, 2, 5) {
    log(I); // logs 2, 3, 4, 5
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

Use `break;` to exit a loop early and `continue;` to skip to the next iteration.

---

## Functions

### Defining a Function

```
Integer Sum(Integer _A, Integer _B) {
    return _A + _B;
}
```

- Return type `Void` means no value is returned.
- A function must be defined before it is called.
- Functions may call themselves recursively, but circular calls between functions are not allowed.

### Calling a Function

```
declare Result = Sum(23, 19);
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

List.clear();
```

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

---

## Directives

Directives appear at the top of a script and begin with `#`. They do **not** end with a semicolon.

| Directive | Description |
|-----------|-------------|
| `#RequireContext XXX` | Declares the required script context (e.g., GameMode, EditorPlugin) |
| `#Const XXX YYYY` | Declares a constant `XXX` with value `YYYY` (immutable) |
| `#Setting XXX YYYY` | Like `#Const`, but can be modified externally |
| `#Include "XXX" as YYYY` | Loads a library or file and binds it to namespace `YYYY` |
| `#Extends "XXX"` | Extends a base script (used for game modes) |
| `#Struct Name { }` | Declares a struct type |

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

---

## Labels

Labels are extension points in a script.

- `+++ MyLabel +++` — Can be extended multiple times.
- `--- MyLabel ---` — Only the latest extension applies.

**Defining label code:**

```
*** MyLabel ***
***
// code to run at this label
***
```

Labels share the scope of the insertion point. It is good practice to wrap the insertion in braces to avoid variable leakage:

```
Void fn1() {
    declare V = 3;
    {+++ MyLabel +++}
}
```

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

- **Line endings:** LF only
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

## Game Mode Script Structure

### Common Directives

```
#Const CompatibleMapTypes "ObstacleArena,TimeAttackArena"
#Const Version            "1.2"
#Const ScriptName         "Modes/ShootMania/Obstacle.Script.txt"

#Include "Libs/Nadeo/Message.Script.txt" as Message
#Include "TextLib" as TextLib

#Extends "Modes/ShootMania/Base/ModeShootmania.Script.txt"
// or
#Extends "Modes/Trackmania/Base/ModeTrackmania.Script.txt"
```

Using a library:

```
declare One = TextLib::ToInteger("1");
declare MessageVersion = Message::Version;
```

### Script Execution Hierarchy

```
Server → Match → Map → Round → Turn → PlayLoop
```

### Available Labels

| Label | Triggered |
|-------|-----------|
| `Yield` | Every tick |
| `InitServer` / `StartServer` / `EndServer` | Server lifecycle |
| `InitMatch` / `StartMatch` / `EndMatch` | Match lifecycle |
| `InitMap` / `StartMap` / `EndMap` | Map lifecycle |
| `InitRound` / `StartRound` / `EndRound` | Round lifecycle |
| `InitTurn` / `StartTurn` / `EndTurn` | Turn lifecycle |
| `PlayLoop` | Every frame during play |
| `Settings` | When settings are loaded |
| `LoadLibraries` / `LogVersions` | Initialization |
| `Rules` | Rules display |

> Labels exist in two versions: `Lobby_` and `Match_`. Use `Match_` when not using matchmaking.

**Example:**

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
| `Int3` | `<Integer1, Integer2, Integer3>` |
| `Ident` | `NullId` |
| Class | `Null` |
