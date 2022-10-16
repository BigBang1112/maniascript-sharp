namespace ManiaScriptSharp.Generator;

public static class EnumerableExtensions
{
    public static IEnumerable<T> Flatten<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> selector)
    {
        var stack = new Stack<IEnumerator<T>>();
        stack.Push(source.GetEnumerator());
        
        while (stack.Count > 0)
        {
            var enumerator = stack.Peek();
            
            if (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                
                yield return current;
                
                var children = selector(current);

                if (children is not null)
                {
                    stack.Push(children.GetEnumerator());
                }
            }
            else
            {
                enumerator.Dispose();
                stack.Pop();
            }
        }
    }
}