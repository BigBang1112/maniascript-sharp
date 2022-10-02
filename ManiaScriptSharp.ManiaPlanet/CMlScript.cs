using System.Collections.Immutable;

namespace ManiaScriptSharp;

public partial class CMlScript
{
    public delegate void KeyPressEventHandler(int keyCode, string keyName, string charPressed);
    public delegate void MouseClickEventHandler(CMlControl control, string controlId);
    public delegate void MouseOverEventHandler(CMlControl control, string controlId);
    public delegate void MouseOutEventHandler(CMlControl control, string controlId);
    public delegate void EntrySubmitEventHandler(CMlControl control, string controlId);
    public delegate void MenuNavigationEventHandler(CMlScriptEvent.EMenuNavAction action);
    public delegate void PluginCustomEventEventHandler(string type, ImmutableArray<string> data);
}
