namespace Sample;

public class Manialink : CMlScript, IContext
{
    [ManialinkControl("background")]
    public CMlQuad QuadBackground { get; set; }
    
    public Manialink()
    {
        Http.RequestComplete += delegate(CHttpRequest request)
        {
            Log("Request complete: " + request.Url);
        };
    }
    
    public void Main()
    {
        
    }

    public void Loop()
    {

    }

    protected override void OnMouseClick(CMlControl control, string controlId)
    {
        if (control is CMlLabel label)
        {
            label.Value = "Clicked";
        }
    }
}