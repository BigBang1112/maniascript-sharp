using System.Collections.Immutable;

namespace ManiaScriptSharp;

public partial class CXmlRpc
{
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PendingEventHandler(CXmlRpcEvent e);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void CallbackEventHandler([ActualName("Param1")] string method,
        [ActualName("Param2")] string parameter);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void CallbackArrayEventHandler([ActualName("ParamArray1")] string method,
        [ActualName("ParamArray2")] ImmutableArray<string> parameters);
}