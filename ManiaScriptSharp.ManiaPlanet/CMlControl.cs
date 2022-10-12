namespace ManiaScriptSharp;

public partial class CMlControl
{
    [ManiaScriptExternalEvent(
        eventClass: nameof(CMlScriptEvent),
        eventKind: nameof(CMlScriptEvent.Type.MouseClick),
        identifier: nameof(CMlScriptEvent.Control))]
    public event Action? MouseClick;
    
    [ManiaScriptExternalEvent(
        eventClass: nameof(CMlScriptEvent),
        eventKind: nameof(CMlScriptEvent.Type.MouseOver),
        identifier: nameof(CMlScriptEvent.Control))]
    public event Action? MouseOver;
    
    [ManiaScriptExternalEvent(
        eventClass: nameof(CMlScriptEvent),
        eventKind: nameof(CMlScriptEvent.Type.MouseOut),
        identifier: nameof(CMlScriptEvent.Control))]
    public event Action? MouseOut;
}