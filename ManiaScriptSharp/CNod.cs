namespace ManiaScriptSharp;

public class CNod
{
    private static int idCounter = 0;

    public Ident Id { get; }
    
    protected internal CNod()
    {
        Id = new(idCounter++);
    }
}
