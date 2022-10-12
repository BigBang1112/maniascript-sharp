using System.Collections.Immutable;

namespace ManiaScriptSharp;

public partial class CMlScript
{
    /// <summary>
    /// Any pending event handler.
    /// </summary>
    /// <param name="e">Event.</param>
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PendingEventHandler(CMlScriptEvent e);
    
    /// <summary>
    /// Handles key presses.
    /// </summary>
    /// <param name="keyCode"></param>
    /// <param name="keyName"></param>
    /// <param name="charPressed"></param>
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
        [ActualName(nameof(CMlScriptEvent.MenuNavAction))] CMlScriptEvent.EMenuNavAction action);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PluginCustomEventEventHandler(
        [ActualName(nameof(CMlScriptEvent.CustomEventType))] string type,
        [ActualName(nameof(CMlScriptEvent.CustomEventData))] ImmutableArray<string> data);
    
    [ManiaScriptEventList(nameof(PendingEventHandler))]
    public IList<CMlScriptEvent> PendingEvents { get; }
    
    protected virtual partial void OnPendingEvent(CMlScriptEvent e)
    {
        PendingEvent?.Invoke(e);

        switch (e.TypeE)
        {
            case CMlScriptEvent.Type.KeyPress:
                OnKeyPress(e.KeyCode, e.KeyName, e.CharPressed);
                break;
            case CMlScriptEvent.Type.MouseClick:
                OnMouseClick(e.Control, e.ControlId);
                break;
            case CMlScriptEvent.Type.MouseOver:
                OnMouseOver(e.Control, e.ControlId);
                break;
            case CMlScriptEvent.Type.MouseOut:
                OnMouseOut(e.Control, e.ControlId);
                break;
            case CMlScriptEvent.Type.EntrySubmit:
                OnEntrySubmit(e.Control, e.ControlId);
                break;
            case CMlScriptEvent.Type.MenuNavigation:
                OnMenuNavigation(e.MenuNavAction);
                break;
            case CMlScriptEvent.Type.PluginCustomEvent:
                OnPluginCustomEvent(e.CustomEventType, e.CustomEventData.ToImmutableArray());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e.TypeE), "Invalid event type");
        }
    }
}
