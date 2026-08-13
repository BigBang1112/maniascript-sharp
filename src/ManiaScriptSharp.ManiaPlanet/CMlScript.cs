namespace ManiaScriptSharp;

public partial class CMlScript
{
    // Each delegate describes the signature and the event list it belongs to.
    // Subscribable events derived from these delegates are declared below.

    /// <summary>Receives every pending event as the raw ManiaScript Event object.</summary>
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PendingEventHandler(CMlScriptEvent e);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void KeyPressEventHandler(int keyCode, string keyName, string charPressed);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void MouseClickEventHandler(CMlControl control, string controlId);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void MouseOverEventHandler(CMlControl control, string controlId);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void MouseOutEventHandler(CMlControl control, string controlId);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void EntrySubmitEventHandler(CMlControl control, string controlId);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void MenuNavigationEventHandler(
        [MemberName(nameof(CMlScriptEvent.MenuNavAction))] CMlScriptEvent.EMenuNavAction action);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PluginCustomEventEventHandler(
        [MemberName(nameof(CMlScriptEvent.CustomEventType))] string type,
        [MemberName(nameof(CMlScriptEvent.CustomEventData))] string[] data);

    // Subscribe to these with += in the constructor or Main().
    // The generator consumes the subscription and emits a foreach/switch in the
    // while-loop; the statements are NOT emitted verbatim into ManiaScript.

    /// <summary>Fires for every pending event (passes the whole Event object).</summary>
    public event PendingEventHandler? Pending;

    /// <summary>Fires for every KeyPress event regardless of which key was pressed.</summary>
    public event KeyPressEventHandler? KeyPress;

    /// <summary>Fires for every MouseClick event regardless of which control was clicked.</summary>
    public event MouseClickEventHandler? MouseClick;

    /// <summary>Fires for every MouseOver event regardless of which control triggered it.</summary>
    public event MouseOverEventHandler? MouseOver;

    /// <summary>Fires for every MouseOut event regardless of which control triggered it.</summary>
    public event MouseOutEventHandler? MouseOut;

    /// <summary>Fires when any entry field is submitted.</summary>
    public event EntrySubmitEventHandler? EntrySubmit;

    /// <summary>Fires on any menu-navigation action (Up/Down/Select/Cancel …).</summary>
    public event MenuNavigationEventHandler? MenuNavigation;

    /// <summary>Fires on any plugin custom event.</summary>
    public event PluginCustomEventEventHandler? PluginCustomEvent;
}
