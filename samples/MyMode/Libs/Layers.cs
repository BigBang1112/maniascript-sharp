using ManiaScriptSharp;
using System.Collections.Generic;

namespace MyMode.Libs;

public class Layers : ILib<CManiaApp>
{
    private readonly Dictionary<string, Ident> LayersByName = new();

    public required CManiaApp Context { get; init; }

    public bool Exists(string layerName)
    {
        return LayersByName.ContainsKey(layerName);
    }

    public CUILayer? Get(string layerName)
    {
        if (!Exists(layerName))
            return null;

        return Context.UILayers[LayersByName[layerName]];
    }

    public CUILayer? Get(Ident layerId)
    {
        if (!LayersByName.ContainsValue(layerId))
            return null;

        return Context.UILayers[layerId];
    }

    public string GetName(Ident layerId)
    {
        foreach (var pair in LayersByName)
        {
            if (pair.Value == layerId)
                return pair.Key;
        }

        return string.Empty;
    }

    public string GetName(CUILayer layer)
    {
        if (layer is null)
            return string.Empty;

        return GetName(layer.Id);
    }

    public void Destroy(string layerName)
    {
        if (!Exists(layerName))
            return;

        Context.UILayerDestroy(Context.UILayers[LayersByName[layerName]]);
        LayersByName.Remove(layerName);
    }

    public void DestroyAll()
    {
        foreach (var layerName in LayersByName.Keys)
        {
            Destroy(layerName);
        }
    }

    public void Create(string layerName, string layerManialink = "", bool isVisible = true, bool showEvent = false, CUILayer.EUILayerType layerType = CUILayer.EUILayerType.Normal)
    {
        if (Exists(layerName))
            Destroy(layerName);

        var newLayer = Context.UILayerCreate();

        newLayer.ManialinkPage = layerManialink;

        if (showEvent)
            Context.LayerCustomEvent(newLayer, "Show", []);

        newLayer.IsVisible = isVisible;
        newLayer.Type = layerType;

        LayersByName[layerName] = newLayer.Id;
    }

    public void Update(string layerName, string layerManialink)
    {
        var layer = Get(layerName);

        if (layer is null)
            return;

        layer.ManialinkPage = layerManialink;
    }

    public void SetType(string layerName, CUILayer.EUILayerType layerType)
    {
        var layer = Get(layerName);

        if (layer is null)
            return;

        layer.Type = layerType;
    }

    public void SetAnimationIn(string layerName, CUILayer.EUILayerAnimation animation)
    {
        var layer = Get(layerName);

        if (layer is null)
            return;

        layer.InAnimation = animation;
    }

    public void SetAnimationOut(string layerName, CUILayer.EUILayerAnimation animation)
    {
        var layer = Get(layerName);

        if (layer is null)
            return;

        layer.OutAnimation = animation;
    }

    public void SetAnimationInOut(string layerName, CUILayer.EUILayerAnimation animation)
    {
        var layer = Get(layerName);

        if (layer is null)
            return;

        layer.InOutAnimation = animation;
    }

    public CMlPage? Page(string layerName)
    {
        var layer = Get(layerName);

        if (layer is null)
            return null;

        return layer.LocalPage;
    }

    public void Show(string layerName, bool showEvent = false)
    {
        var layer = Get(layerName);

        if (layer is null)
            return;

        if (showEvent)
            Context.LayerCustomEvent(layer, "Show", []);

        layer.IsVisible = true;
    }

    public void Hide(string layerName, bool hideEvent = false)
    {
        var layer = Get(layerName);

        if (layer is null)
            return;

        if (hideEvent)
            Context.LayerCustomEvent(layer, "Hide", []);
        else
            layer.IsVisible = false;
    }

    public void ShowOnly(string layerName, bool showEvent = false, bool hideEvent = false)
    {
        foreach (var layer in LayersByName.Keys)
        {
            Hide(layer, hideEvent);
        }

        Show(layerName, showEvent);
    }

    public void SendEvent(string layerName, string type, string[] data)
    {
        Context.LayerCustomEvent(Get(layerName), type, data);
    }

    public void SendEvent(string layerName, string type, string data)
    {
        Context.LayerCustomEvent(Get(layerName), type, [data]);
    }

    public void SendEvent(string layerName, string type, int data)
    {
        Context.LayerCustomEvent(Get(layerName), type, [data.ToString()]);
    }

    public void SendEvent(string layerName, string type, double data)
    {
        Context.LayerCustomEvent(Get(layerName), type, [data.ToString()]);
    }

    public void SendEvent(string layerName, string type, Ident data)
    {
        Context.LayerCustomEvent(Get(layerName), type, [data.ToString()]);
    }

    public void SendEvent(string layerName, string type, Vec2 data)
    {
        Context.LayerCustomEvent(Get(layerName), type, [data.ToString()]);
    }

    public void SendEvent(string layerName, string type, Vec3 data)
    {
        Context.LayerCustomEvent(Get(layerName), type, [data.ToString()]);
    }

    public void SendEvent(string layerName, string type, Int3 data)
    {
        Context.LayerCustomEvent(Get(layerName), type, [data.ToString()]);
    }

    public void SendEvent(string layerName, string type)
    {
        Context.LayerCustomEvent(Get(layerName), type, []);
    }

    public void Event(CManiaAppEvent @event)
    {
        if (@event.Type != CManiaAppEvent.EType.LayerCustomEvent)
            return;

        if (!LayersByName.ContainsValue(@event.CustomEventLayer.Id))
            return;

        if (@event.CustomEventType == "Hide_Response")
        {
            @event.CustomEventLayer.IsVisible = false;
        }
    }
}
