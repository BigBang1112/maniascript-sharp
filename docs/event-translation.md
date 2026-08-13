# Event Translation — C# → ManiaScript

ManiaScript has no native event system. Instead, scripts iterate over an event queue
(`foreach (Event in PendingEvents)`) and dispatch by switching on `Event.Type`.
ManiaScriptSharp provides two C#-side mechanisms that map to this pattern, plus a way to
keep full manual control over the loop.

---

## 1. Control-specific events (`[ManiaScriptExternalEvent]`)

Use this when you want to react to an event **on a specific Manialink control**.
Subscribe with `+=` in `Main()`.

### Method-group handler

```csharp
public class MyManialink : CTmMlScriptIngame, IContext
{
    [ManialinkControl] public required CMlQuad QuadMapName;

    public void Main()
    {
        QuadMapName.MouseClick += ShowCurChallengeCard;
    }

    private void ShowCurChallengeCard() { /* ... */ }
}
```

```maniascript
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
                            ShowCurChallengeCard();
                        }
                    }
                }
            }
        }
    }
}
```

### Lambda handler (inline body)

```csharp
public void Main()
{
    QuadMapName.MouseClick += () =>
    {
        Log("Map quad clicked");
    };
}
```

```maniascript
case QuadMapName: {
    log("Map quad clicked");
}
```

### Multiple controls for the same event type

When multiple controls subscribe to the same event type, they are merged into a single
`switch (Event.Control)` block.

```csharp
public void Main()
{
    QuadMapName.MouseClick   += OnMapClick;
    QuadGlobalBack.MouseClick += OnBack;
}
```

```maniascript
case CMlScriptEvent::Type::MouseClick: {
    switch (Event.Control) {
        case QuadMapName: {
            OnMapClick();
        }
        case QuadGlobalBack: {
            OnBack();
        }
    }
}
```

---

## 2. Class-level typed events (`[ManiaScriptEvent]`)

Use these when you want to handle an event type **regardless of which control triggered
it** — or when the event is not control-bound at all (e.g. keyboard input).

Each delegate type declared with `[ManiaScriptEvent]` in `CMlScript` has a corresponding
subscribable event property. Subscribe with `+=` in `Main()`.

Available events (from `CMlScript`):

| C# event            | Delegate signature                                                  | ManiaScript type         |
|---------------------|---------------------------------------------------------------------|--------------------------|
| `Pending`           | `PendingEventHandler(CMlScriptEvent e)`                             | *(general, any type)*    |
| `KeyPress`          | `KeyPressEventHandler(int keyCode, string keyName, string charPressed)` | `KeyPress`           |
| `MouseClick`        | `MouseClickEventHandler(CMlControl control, string controlId)`      | `MouseClick`             |
| `MouseOver`         | `MouseOverEventHandler(CMlControl control, string controlId)`       | `MouseOver`              |
| `MouseOut`          | `MouseOutEventHandler(CMlControl control, string controlId)`        | `MouseOut`               |
| `EntrySubmit`       | `EntrySubmitEventHandler(CMlControl control, string controlId)`     | `EntrySubmit`            |
| `MenuNavigation`    | `MenuNavigationEventHandler(CMlScriptEvent.EMenuNavAction action)`  | `MenuNavigation`         |
| `PluginCustomEvent` | `PluginCustomEventEventHandler(string type, string[] data)`         | `LayerCustomEvent`       |

### Typed handler — method group

```csharp
public class MyManialink : CTmMlScriptIngame, IContext
{
    public void Main()
    {
        MouseClick += OnAnyClick;
    }

    private void OnAnyClick(CMlControl control, string controlId)
    {
        Log(controlId);
    }
}
```

The generator maps each delegate parameter to the corresponding `Event.*` property using
the parameter name (PascalCase) or an explicit `[MemberName]` attribute:

```maniascript
case CMlScriptEvent::Type::MouseClick: {
    OnAnyClick(Event.Control, Event.ControlId);
}
```

### Typed handler — lambda

Lambda parameters are declared as ManiaScript locals bound to `Event.*` fields, then the
lambda body is inlined:

```csharp
public void Main()
{
    MouseClick += (control, controlId) =>
    {
        Log(controlId);
    };
}
```

```maniascript
case CMlScriptEvent::Type::MouseClick: {
    declare Control = Event.Control;
    declare ControlId = Event.ControlId;
    log(ControlId);
}
```

### Key-press handler

```csharp
public void Main()
{
    KeyPress += (keyCode, keyName, charPressed) =>
    {
        Log(keyName);
    };
}
```

```maniascript
case CMlScriptEvent::Type::KeyPress: {
    declare KeyCode = Event.KeyCode;
    declare KeyName = Event.KeyName;
    declare CharPressed = Event.CharPressed;
    log(KeyName);
}
```

### Menu-navigation handler (`[MemberName]`)

`MenuNavigationEventHandler` uses `[MemberName]` to map the `action` parameter to
`Event.MenuNavAction` instead of deriving it from the parameter name:

```csharp
public void Main()
{
    MenuNavigation += OnNav;
}

private void OnNav(CMlScriptEvent.EMenuNavAction action)
{
    if (action == CMlScriptEvent.EMenuNavAction.Select) { /* ... */ }
}
```

```maniascript
case CMlScriptEvent::Type::MenuNavigation: {
    OnNav(Event.MenuNavAction);
}
```

---

## 3. General event handler (`Pending`)

Subscribe to `Pending` when you want a single method that receives **every event** as the
raw ManiaScript `Event` object — no type switch is generated around it.

```csharp
public void Main()
{
    Pending += LogAllEvents;
}

private void LogAllEvents(CMlScriptEvent e)
{
    Log("event received");
}
```

```maniascript
foreach (Event in PendingEvents) {
    LogAllEvents(Event);
    // ... any typed subscriptions follow in switch(Event.Type)
}
```

The general handler executes **before** the `switch (Event.Type)` block inside the same
foreach, so it always runs once per event.

---

## 4. Combining control-specific and class-level subscriptions

All subscriptions — external and class-level — are merged into a **single** foreach per
event list. Handlers execute in this order for each event:

1. General handlers (`Pending`)
2. `switch (Event.Type)` block
   - Class-level typed handlers (no control switch)
   - Control-specific `switch (Event.Control)` block

```csharp
public void Main()
{
    Pending          += LogAll;          // general
    MouseClick       += OnAnyClick;      // class-level typed
    QuadMapName.MouseClick += ShowCard;  // control-specific
}
```

```maniascript
foreach (Event in PendingEvents) {
    LogAll(Event);
    switch (Event.Type) {
        case CMlScriptEvent::Type::MouseClick: {
            OnAnyClick(Event.Control, Event.ControlId);
            switch (Event.Control) {
                case QuadMapName: {
                    ShowCard();
                }
            }
        }
    }
}
```

---

## 5. Manual foreach injection

If you want full control over the loop body — for example to process events before **and**
after your own logic — write the `foreach` yourself in `Loop()`. The generator detects it
and **injects** the event-dispatching switch into the body instead of auto-generating a
separate foreach.

```csharp
public class MyManialink : CTmMlScriptIngame, IContext
{
    [ManialinkControl] public required CMlQuad QuadMapName;

    public MyManialink()
    {
        QuadMapName.MouseClick += ShowCard;
    }

    public void Loop()
    {
        // Custom pre-event logic
        Log("tick");

        // The generator injects the switch here.
        foreach (var e in PendingEvents)
        {
            // Optionally add your own per-event logic.
            // The generated switch is appended after this body.
        }

        // Custom post-event logic
        Log("done");
    }
}
```

```maniascript
while (True) {
    yield;
    log("tick");
    foreach (Event in PendingEvents) {
        switch (Event.Type) {
            case CMlScriptEvent::Type::MouseClick: {
                switch (Event.Control) {
                    case QuadMapName: {
                        ShowCard();
                    }
                }
            }
        }
    }
    log("done");
}
```

> **Note**: The loop variable (`e` in C#) is always translated to `Event` in the generated
> ManiaScript so that the injected switch can reference `Event.Type`, `Event.Control`, etc.
> If you reference `e` in the foreach body, it will also be translated to `Event`.

---

## 6. How events are declared

### `[ManiaScriptExternalEvent]` — on C# `event` properties in script-API types

```csharp
// CMlControl.cs
[ManiaScriptExternalEvent(
    eventContext: nameof(CMlScript),
    eventList:   nameof(CMlScript.PendingEvents),
    eventType:   nameof(CMlScriptEvent.Type.MouseClick),
    identifier:  nameof(CMlScriptEvent.Control))]
public event CMlScript.MouseClickEventHandler? MouseClick;
```

- **`eventContext`** — the script class that owns the event list (must be in the inheritance chain)
- **`eventList`** — the ManiaScript event queue property name
- **`eventType`** — the enum member name on `CMlScriptEvent::Type` that identifies this event
- **`identifier`** — the `Event.*` property used in `switch (Event.{identifier})`

### `[ManiaScriptEvent]` — on delegate type declarations

```csharp
// CMlScript.cs
[ManiaScriptEvent(nameof(PendingEvents))]
public delegate void MouseClickEventHandler(CMlControl control, string controlId);

// The subscribable event:
public event MouseClickEventHandler? MouseClick;
```

- The constructor argument names the event list.
- Delegate parameter names are PascalCase'd to derive the `Event.*` property names.
- Override with `[MemberName("ActualName")]` when the C# name diverges from the
  ManiaScript field name (e.g. `[MemberName(nameof(CMlScriptEvent.MenuNavAction))]`).

---

## 7. Known limitations

| Scenario | Status |
|----------|--------|
| Method-group handlers for external events | ✅ Supported |
| Lambda handlers for external events | ✅ Supported (body inlined) |
| Method-group handlers for typed class-level events | ✅ Supported (args auto-mapped) |
| Lambda handlers for typed class-level events | ✅ Supported (params declared as locals) |
| General (`Pending`) handlers | ✅ Supported |
| Manual foreach injection in `Loop()` | ✅ Supported |
| Multiple event lists (e.g. XmlRpc.PendingEvents) | ⚠️ Grouped per list; custom event-list injection matches `PendingEvents` only |
| `foreach (var e in PendingEvents)` with e used in body | ✅ `e` is remapped to `Event` |
| Subscriptions outside `Main()` | ❌ Not supported; use `Main()` |
