namespace Sample;

public class Manialink : CTmMlScriptIngame, IContext
{    
    public const bool GaemplayUi = true;

    [ManialinkControl("background")]
    public CMlQuad Background { get; set; }

    [ManialinkControl("title")]
    public CMlLabel Title;

    public Manialink()
    {
        this.Http.RequestComplete += delegate(CHttpRequest request)
        {
            Log("Request complete: " + request.Url);
        };

        this.MouseClick += Manialink_MouseClick;

        MouseOver += (control, controlId) =>
        {
        };

        Title.MouseClick += () =>
        {
            Log("Title clicked");
        };
        
        Background.MouseClick += () =>
        {
            Log("Title clicked");
        };
    }

    private void Manialink_MouseClick(CMlControl control, string controlId)
    {
        throw new NotImplementedException();
    }

    protected override void OnMouseClick(CMlControl control, string controlId)
    {
        base.OnMouseClick(control, controlId);
    }

    protected override void OnEntrySubmit(CMlControl control, string controlId)
    {
        base.OnEntrySubmit(control, controlId);
    }

    protected override void OnRaceEvent(CTmRaceClientEvent e)
    {
        base.OnRaceEvent(e);
    }

    /// <summary>
    /// Tonic my fav drink
    /// </summary>
    public void Main()
    {
        
    }

    /// <summary>
    /// why do i document loop
    /// </summary>
    public void Loop()
    {

    }

    /*protected override void OnMouseClick(CMlControl control, string controlId)
    {
        if (control is CMlLabel label)
        {
            label.Value = "Clicked";
        }
    }*/
}