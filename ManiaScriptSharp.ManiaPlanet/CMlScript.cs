﻿using System.Collections.Immutable;

namespace ManiaScriptSharp;

public partial class CMlScript
{
    public delegate void PendingEventHandler(CMlScriptEvent e);
    public delegate void KeyPressEventHandler(int keyCode, string keyName, string charPressed);
    public delegate void MouseClickEventHandler(CMlControl control, string controlId);
    public delegate void MouseOverEventHandler(CMlControl control, string controlId);
    public delegate void MouseOutEventHandler(CMlControl control, string controlId);
    public delegate void EntrySubmitEventHandler(CMlControl control, string controlId);
    public delegate void MenuNavigationEventHandler(CMlScriptEvent.EMenuNavAction action);
    public delegate void PluginCustomEventEventHandler(string type, ImmutableArray<string> data);
    
    [ManiaScriptEventList(nameof(PendingEventHandler))]
    public IList<CMlScriptEvent> PendingEvents { get; }
    
    protected virtual void OnPendingEvent(CMlScriptEvent e)
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
