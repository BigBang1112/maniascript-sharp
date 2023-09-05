namespace ManiaScriptSharp;

public static class DictionaryExtensions
{
    public static TKey KeyOf<TKey, TValue>(this Dictionary<TKey, TValue> dict, TValue val) where TKey : notnull
    {
        foreach (var pair in dict)
        {
            if (Equals(pair.Value, val))
            {
                return pair.Key;
            }
        }

        throw new Exception("Value not found in dictionary");
    }
}
