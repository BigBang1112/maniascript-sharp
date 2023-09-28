using System.Collections.Immutable;

namespace ManiaScriptSharp;

public partial class CMapEditorPlugin
{
    /// <summary>
    /// Any pending event handler.
    /// </summary>
    /// <param name="e">Event.</param>
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PendingEventHandler(CMapEditorPluginEvent e);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void StartTestEventHandler();
}
