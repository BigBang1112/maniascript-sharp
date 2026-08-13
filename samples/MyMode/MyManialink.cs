using System.Collections.Generic;
using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyMode;

/// <summary>
/// A second sample exercising labels, manialink bindings, events, structs,
/// patterns, vectors, lists and dictionaries — i.e. most of ManiaScriptSharp.md.
/// </summary>
public class MyManialink : CTmMlScriptIngame, IContext
{
    public required TextLib TL;

    // ----- Structs (translated to #Struct) -----
    public struct ScoreEntry
    {
        public int Value;
        public string Login;
    }

    public required MyLib Libo;

    // ----- Settings & consts -----
    [Setting(As = "Show debug HUD")]
    const bool ShowDebug = false;

    const int MaxItems = 8;

    // ----- Manialink-bound controls -----
    [ManialinkControl]
    public required CMlLabel LabelCountdown;

    [ManialinkControl("CustomId")]
    public required CMlQuad QuadMapName;

    [ManialinkControl(IgnoreValidation = true)]
    public required CMlFrame DynamicFrame;

    [ManialinkControl]
    public required CMlEntry EntryInput;

    // ----- Public field with initializer (moves into main()) -----
    public int PreviousTime = -1;

    // ----- Plain locals -----
    private readonly List<string> _logHistory = new();
    private readonly Dictionary<string, int> _counters = new();

    public virtual void OnMapIntroEnd()
    {
        Log("Intro ended");
    }

    public void Main()
    {
        Local<int>.For(LocalUser, out var myVar);
        myVar.Value = 5;

        OnMapIntroEnd();

        Libo.Nice();

        var scores = new Dictionary<string, int> { ["alpha"] = 10, ["beta"] = 20 };
        var names = new List<string>
        {
            "Alpha",
            "Beta",
            "Gamma",
            "Omega"
        };

        foreach (var n in names)
            Log($"Player: {n}");

        var doubled = Multiply(3, 4);

        // ── Event subscriptions ──────────────────────────────────────────────
        // The generator turns these += into a foreach/switch in the while loop.
        // The statements themselves are NOT emitted verbatim.

        // Fires for any click, regardless of which control was clicked.
        MouseClick += OnAnyClick;

        // Fires only when any entry field is submitted.
        EntrySubmit += OnEntrySubmitted;

        // Fires on keyboard navigation actions.
        MenuNavigation += (action) =>
        {
            if (action == CMlScriptEvent.EMenuNavAction.Cancel)
                Log("Cancelled");
        };

        // General fallback: receives every event as the raw ManiaScript Event object.
        Pending += LogAllEvents;

        QuadMapName.MouseClick += (control, controlId) => Log($"Clicked the quad {controlId}!");
    }

    public void Loop()
    {
        // Manual foreach — the generator injects the event-dispatch switch inside
        // the body, after any user statements, instead of auto-generating one.
        foreach (var e in PendingEvents)
        {
            // Custom pre-dispatch logic (runs before the generated switch).
            Log("event received");
        }

        Describe(QuadMapName);
    }

    private void OnAnyClick(CMlControl control, string controlId)
    {
        Log($"Clicked: {controlId}");

        if (controlId == "CustomId")
            QuadMapName.Hide();
    }

    private void OnEntrySubmitted(CMlControl control, string controlId)
    {
        Log($"Entry submitted: {controlId}");
        LabelCountdown.Value = EntryInput.Value;
    }

    private static void LogAllEvents(CMlScriptEvent e)
    {
        // Receives the whole ManiaScript Event object — useful for debugging.
        Log("raw event");
    }

    private static void Describe(CMlControl control)
    {
        if (control is CMlLabel label)
        {
            Log(label.Value);
        }
        else if (control is CMlQuad quad)
        {
            Log(quad.ImageUrl);
        }

        switch (control)
        {
            case CMlEntry entry:
                Log(entry.Value);
                break;
            case CMlTextEdit edit:
                Log(edit.Value);
                break;
            default:
                Log("other control");
                break;
        }
    }

    private static int Multiply(int a, int b) => a * b;
}
