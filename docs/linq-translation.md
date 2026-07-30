# LINQ → ManiaScript Translation Reference

ManiaScriptSharp translates C# LINQ chains on local variables into ManiaScript `foreach`
loops at compile time.  Because ManiaScript has no lazy evaluation, every LINQ chain must
ultimately produce a concrete value — either an array or a scalar.

> **Diagnostic MSS008** is emitted whenever a LINQ call cannot be translated.  The message
> describes what is needed (usually "assign to a local variable to enable LINQ desugaring").

---

## How it works

A LINQ chain is desugared **at the point of its terminal call**.  Intermediate (stage-only)
chains can be stored in local variables and composed later — the generator inlines them at
the materialisation site.

```csharp
// Registered as a pending chain — no code emitted here.
var evens = nums.Where(x => x % 2 == 0);

// Stages from "evens" are inlined; foreach is emitted here.
var result = evens.OrderBy(x => x).ToList();
```

Pending chains can be chained arbitrarily:

```csharp
var q1 = nums.Where(x => x > 0);       // pending
var q2 = q1.Where(x => x < 100);       // pending (inlines q1)
var result = q2.ToList();               // emits single foreach with combined guard
```

---

## Supported stages

Stages appear in the middle of a chain (between the source and the terminal).  Multiple
stages can be composed freely, subject to the ordering rules below.

### `Where(pred)` — filter

```csharp
var result = nums.Where(x => x > 0).ToList();
```
```maniascript
declare Integer[] Result;
foreach (X in Nums) {
    if (X > 0) {
        Result.add(X);
    }
}
```

### `Select(selector)` — project / transform

```csharp
var result = nums.Select(x => x * 2).ToList();
```
```maniascript
declare Integer[] Result;
foreach (X in Nums) {
    Result.add(X * 2);
}
```

When followed by `Where`, the projected value is declared as a local variable so that the
post-select guard can reference it:

```csharp
var result = nums.Select(x => x * 2).Where(y => y > 5).ToList();
```
```maniascript
declare Integer[] Result;
foreach (X in Nums) {
    declare Y = X * 2;
    if (Y > 5) {
        Result.add(Y);
    }
}
```

### `Where` + `Select` + `Where` — filter, project, filter again

```csharp
var result = nums.Where(x => x > 0).Select(x => x * 2).Where(y => y < 100).ToList();
```
```maniascript
declare Integer[] Result;
foreach (X in Nums) {
    if (X > 0) {
        declare Y = X * 2;
        if (Y < 100) {
            Result.add(Y);
        }
    }
}
```

### `Distinct()` — deduplicate

```csharp
var result = nums.Distinct().ToList();
```
```maniascript
declare Integer[] Result;
foreach (Item in Nums) {
    if (!Result.exists(Item)) {
        Result.add(Item);
    }
}
```

### `OrderBy(key)` / `ThenBy(key)` — sort ascending

```csharp
var result = nums.Where(x => x > 0).OrderBy(x => x).ToList();
```
```maniascript
declare Integer[] Result;
foreach (X in Nums) {
    if (X > 0) {
        Result.add(X);
    }
}
Result = Result.sort();
```

### `OrderByDescending(key)` / `ThenByDescending(key)` — sort descending

```csharp
var result = nums.OrderByDescending(x => x).ToList();
```
```maniascript
declare Integer[] Result;
foreach (X in Nums) {
    Result.add(X);
}
Result = Result.sortreverse();
```

> **Note:** ManiaScript `sort()` / `sortreverse()` sort by the element value itself, not by a
> key selector.  The key lambda is parsed and accepted syntactically but **ignored** — the
> sort is always on the element's natural ordering.  If you need a custom key, pre-project
> with `Select` into a sortable type first.

---

## Supported terminals

The terminal is the last call in the chain and determines what code is generated.

### `ToList()` / `ToArray()` — materialise to array

See stage examples above.  The result variable is declared as `ElementType[]`.

### `Count()` / `Count(pred)` — count matching elements

```csharp
var cnt = nums.Count(x => x > 0);
```
```maniascript
declare Integer Cnt = 0;
foreach (X in Nums) {
    if (X > 0) {
        Cnt += 1;
    }
}
```

Combine with a `Where` stage:

```csharp
var cnt = nums.Where(x => x > 5).Count();
```
```maniascript
declare Integer Cnt = 0;
foreach (X in Nums) {
    if (X > 5) {
        Cnt += 1;
    }
}
```

### `Any(pred)` — true if any element matches

```csharp
var any = nums.Any(x => x > 0);
```
```maniascript
declare Boolean Any = False;
foreach (X in Nums) {
    if (X > 0) {
        Any = True;
        break;
    }
}
```

### `All(pred)` — true if all elements match

```csharp
var all = nums.All(x => x > 0);
```
```maniascript
declare Boolean All = True;
foreach (X in Nums) {
    if (!(X > 0)) {
        All = False;
        break;
    }
}
```

### `Sum(selector)` — accumulate

```csharp
var total = nums.Sum(x => x * 2);
```
```maniascript
declare Integer Total = 0;
foreach (X in Nums) {
    Total += X * 2;
}
```

For `Real` sums the initial value is `0.`.

### `First(pred)` / `FirstOrDefault(pred)` — first matching element

```csharp
var first = nums.First(x => x > 5);
```
```maniascript
declare Integer First;
foreach (X in Nums) {
    if (X > 5) {
        First = X;
        break;
    }
}
```

> No bounds checking is emitted.  If no element matches, `First` retains its default value.
> Use `FirstOrDefault` in C# to signal intent (translation is identical).

### `Last(pred)` / `LastOrDefault(pred)` — last matching element

```csharp
var last = nums.Last(x => x > 5);
```
```maniascript
declare Integer Last;
foreach (X in Nums) {
    if (X > 5) {
        Last = X;   // keeps overwriting — last match wins
    }
}
```

---

## `Index()` — pairing with position

`Index()` (`System.Linq.Enumerable.Index`) is only recognized when the result is
immediately destructured in a `foreach`. Unlike a ManiaScript array's native key (`Key =>
Val`), which is only sequential for plain ordered arrays and not for associative/
dictionary-style ones, `.Index()` guarantees a 0-based sequential position on *any* list —
so it can't reuse that sugar. It desugars into a counter declared before the loop and
incremented once per iteration instead:

```csharp
foreach (var (index, item) in myArray.Index())
{
    Log($"{index}: {item}");
}
```
```maniascript
declare Integer Index = 0;
foreach (Item in MyArray) {
    log(Index ^ ": " ^ Item);
    Index += 1;
}
```

Using `.Index()` anywhere else (as a mid-chain stage, non-destructured loop variable, or
materialised with `.ToList()`) has no ManiaScript equivalent (no tuple/array-of-pairs type)
and emits **MSS008**.

---

## Lazy / pending chains

A chain **without a terminal** is registered as a *pending* chain.  No code is emitted at
the declaration site.  The variable can later be used as the source of another chain that
does have a terminal — the stages are inlined at that point.

```csharp
var positives = nums.Where(x => x > 0);   // pending — no ManiaScript emitted
var sorted    = positives.OrderBy(x => x).ToList();   // emits here, inlining positives
var count     = positives.Count(x => x < 100);        // emits again, independently
```

Using a pending chain variable anywhere other than as the source of another LINQ chain
emits **MSS008**.

---

## Naming conventions

| C# | ManiaScript |
|---|---|
| Local variable `result` | `Result` (PascalCase) |
| Lambda param `x` (loop variable) | `X` |
| Projected variable (post-Select Where param) | param name PascalCased; suffixed `Sel` if it would collide with the loop var |

---

## Partially unsupported patterns

The following patterns either have no direct ManiaScript equivalent or are not yet
implemented.  Unimplemented ones emit **MSS008**.

### `GroupBy` — grouping

ManiaScript has no nested arrays, so `GroupBy` cannot produce an `IGrouping<K,V>[]`
equivalent.  Two patterns are supported depending on what is done with the groups.

**Aggregate per group** (count, sum, etc.) — use a dictionary terminal:

```csharp
var countPerGroup = nums.GroupBy(x => x % 3)
                        .ToDictionary(g => g.Key, g => g.Count());
```
```maniascript
declare Integer[Integer] CountPerGroup;
foreach (X in Nums) {
    declare Key = X % 3;
    if (!CountPerGroup.existskey(Key)) CountPerGroup[Key] = 0;
    CountPerGroup[Key] += 1;
}
```

**Iterate all elements per group** — two-pass loop (collect distinct keys first, then
filter per key):

```csharp
var groups = nums.GroupBy(x => x % 3);
foreach (var g in groups)
    foreach (var item in g)
        DoWork(g.Key, item);
```
```maniascript
declare Integer[] Keys;
foreach (X in Nums) {
    declare Key = X % 3;
    if (!Keys.exists(Key)) Keys.add(Key);
}
foreach (Key in Keys) {
    foreach (X in Nums) {
        if (X % 3 == Key) {
            DoWork(Key, X);
        }
    }
}
```

> The two-pass approach is O(n·k) where k is the number of distinct keys.  The key
> expression is re-evaluated on every inner iteration; if it is expensive, pre-project
> the keys with `Select` first.

Not yet implemented — emits **MSS008**.

### `Join` / `GroupJoin` — relational join

```csharp
var joined = nums.Join(strs, n => n, s => s.Length, (n, s) => s);
```

Nested `foreach` with equality guard.  Quadratic complexity.
Not yet implemented — emits **MSS008**.

### `Zip` — pairwise combination

```csharp
var pairs = nums.Zip(strs, (n, s) => n + ": " + s).ToList();
```
```maniascript
declare Text[] Pairs;
declare ZipCount = Nums.count;
if (Strs.count < ZipCount) ZipCount = Strs.count;
for (ZipI, 0, ZipCount - 1) {
    Pairs.add(Nums[ZipI] ^ ": " ^ Strs[ZipI]);
}
```

The result length is clamped to the shorter of the two inputs, matching C# `Zip` semantics.
ManiaScript `for (I, start, end)` is inclusive on both ends, so the upper bound is `count - 1`.
Requires a materialising terminal (`.ToList()`).

### `SelectMany` — flat-map

Collection selector only (one-argument form):

```csharp
var flat = nested.SelectMany(x => x.Items);
```
```maniascript
declare ItemType[] Flat;
foreach (X in Nested) {
    foreach (Item in X.Items) {
        Flat.add(Item);
    }
}
```

Collection selector + result selector (two-argument form):

```csharp
var flat = nested.SelectMany(x => x.Items, (x, item) => item.Name);
```
```maniascript
declare Text[] Flat;
foreach (X in Nested) {
    foreach (Item in X.Items) {
        Flat.add(Item.Name);
    }
}
```

Preceding `Where` stages on the outer sequence compose normally:

```csharp
var flat = nested.Where(x => x.Active).SelectMany(x => x.Items);
```
```maniascript
declare ItemType[] Flat;
foreach (X in Nested) {
    if (X.Active) {
        foreach (Item in X.Items) {
            Flat.add(Item);
        }
    }
}
```

### `Aggregate` — general fold

Seeded form (initial value + accumulator lambda):

```csharp
var product = nums.Aggregate(1, (acc, x) => acc * x);
```
```maniascript
declare Integer Product = 1;
foreach (X in Nums) {
    Product = Product * X;
}
```

No-seed form (first element used as seed):

```csharp
var product = nums.Aggregate((acc, x) => acc + x);
```
```maniascript
assert(Nums.count > 0);
declare Integer Product = Nums[0];
for (AggI, 1, Nums.count - 1) {
    Product = Product + Nums[AggI];
}
```

> **Note:** No-seed form uses a `for` loop so the first element is consumed as the seed and
> subsequent elements are folded in.  `assert(count > 0)` is emitted to match the
> `InvalidOperationException` C# throws on an empty sequence.

### `Skip(n)` / `Take(n)` on a lazy chain

A `foreach` with a skip counter is emitted:

```csharp
var page = nums.Skip(10).Take(5).ToList();
```
```maniascript
declare Integer[] Page;
declare Integer PageSkipI = 0;
foreach (Item in Nums) {
    if (PageSkipI < 10) { PageSkipI += 1; } else {
        Page.add(Item);
        if (Page.count >= 5) break;
    }
}
```

With upstream `Where` stages the guard wraps the counter logic:

```csharp
var page = nums.Where(x => x > 0).Skip(10).Take(5).ToList();
```
```maniascript
declare Integer[] Page;
declare Integer PageSkipI = 0;
foreach (X in Nums) {
    if (X > 0) {
        if (PageSkipI < 10) { PageSkipI += 1; } else {
            Page.add(X);
            if (Page.count >= 5) break;
        }
    }
}
```

### `Min` / `Max`

No-selector form:

```csharp
var min = nums.Min();
```
```maniascript
assert(Nums.count > 0);
declare Integer Min = Nums[0];
for (MinMaxI, 1, Nums.count - 1) {
    if (Nums[MinMaxI] < Min) Min = Nums[MinMaxI];
}
```

With a selector lambda:

```csharp
var max = nums.Max(x => x * 2);
```
```maniascript
assert(Nums.count > 0);
declare Integer Max = Nums[0] * 2;
for (MinMaxI, 1, Nums.count - 1) {
    declare Val = Nums[MinMaxI] * 2;
    if (Val > Max) Max = Val;
}
```

> `assert(count > 0)` is emitted to match the `InvalidOperationException` C# throws on
> an empty sequence.  The selector expression is evaluated once for the seed (index 0)
> and once per subsequent element.

### `Single` / `SingleOrDefault`

`Single(pred)` asserts exactly one match exists:

```csharp
var item = nums.Single(x => x > 5);
```
```maniascript
declare Integer Item;
declare Boolean ItemFound = False;
foreach (X in Nums) {
    if (X > 5) {
        assert(!ItemFound);
        Item = X;
        ItemFound = True;
    }
}
assert(ItemFound);
```

`SingleOrDefault(pred)` omits the final assert (returns the default value if nothing matched):

```csharp
var item = nums.SingleOrDefault(x => x > 5);
```
```maniascript
declare Integer Item;
declare Boolean ItemFound = False;
foreach (X in Nums) {
    if (X > 5) {
        assert(!ItemFound);
        Item = X;
        ItemFound = True;
    }
}
```

### `Contains` on a lazy chain

`Contains(value)` desugars as `Any(x => x == value)`:

```csharp
var q = nums.Where(x => x > 0);
bool has = q.Contains(42);
```
```maniascript
declare Boolean Has = False;
foreach (X in Nums) {
    if (X > 0) {
        if (X == 42) {
            Has = True;
            break;
        }
    }
}
```

Direct `List<T>.Contains` (no upstream stages) maps to `.exists()` directly without desugaring:

```csharp
bool has = nums.Contains(42);   // List<T>.Contains — not a LINQ chain
```
```maniascript
declare Has = Nums.exists(42);
```

### `ToDictionary`

Two-argument form (key selector + value selector):

```csharp
var map = items.ToDictionary(x => x.Id, x => x.Name);
```
```maniascript
declare Text[Integer] Map;
foreach (X in Items) {
    Map[X.Id] = X.Name;
}
```

One-argument form (key selector only — element becomes the value):

```csharp
var map = items.ToDictionary(x => x.Id);
```
```maniascript
declare ItemType[Integer] Map;
foreach (X in Items) {
    Map[X.Id] = X;
}
```

The ManiaScript type is `TValue[TKey]`.  `TKey` is inferred from the key-selector return
type; `TValue` is inferred from the value-selector return type (two-arg form) or the
source element type (one-arg form).

Preceding `Where` / `Select` stages compose in the same way as `ToList`:

```csharp
var map = items.Where(x => x.Active).ToDictionary(x => x.Id, x => x.Score);
```
```maniascript
declare Integer[Integer] Map;
foreach (X in Items) {
    if (X.Active) {
        Map[X.Id] = X.Score;
    }
}
```

> **Note:** `assert(!Map.existskey(key))` is emitted before each insertion to match C#'s
> `ArgumentException` on duplicate keys.

### Block-body lambdas

```csharp
var result = nums.Where(x => { var v = x * 2; return v > 5; }).ToList();
```

Only expression-body lambdas (`x => expr`) are supported.  Block-body lambdas emit MSS008.

### Query syntax (`from … where … select`)

```csharp
var result = (from x in nums where x > 0 select x * 2).ToList();
```

C# query syntax desugars to method calls before reaching the generator, so it **does**
work as long as it maps to supported stages.  The generator sees method calls regardless.
