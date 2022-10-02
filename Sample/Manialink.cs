namespace Sample;

public class Manialink : CMlScript, IContext
{
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
