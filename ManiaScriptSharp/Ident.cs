namespace ManiaScriptSharp;

public class Ident
{
    internal int Id { get; }

    protected internal Ident(int id)
    {
        Id = id;
    }

    public override int GetHashCode()
    {
        return EqualityComparer<Type>.Default.GetHashCode() * -1521134295 + EqualityComparer<int>.Default.GetHashCode(Id);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Ident);
    }

    public virtual bool Equals(Ident? other)
    {
        return (object)this == other || other is not null && EqualityComparer<int>.Default.Equals(Id, other.Id);
    }

    public static bool operator ==(Ident left, Ident right)
    {
        return (object)left == right || (left is not null && left.Equals(right));
    }

    public static bool operator !=(Ident left, Ident right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"#{Id}";
    }
}
