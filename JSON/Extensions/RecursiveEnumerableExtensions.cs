using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanPosOverride.JSON.Extensions
{
    public static partial class RecursiveEnumerableExtensions
    {
        // Rewritten from the answer by Eric Lippert https://stackoverflow.com/users/88656/eric-lippert
        // to "Efficient graph traversal with LINQ - eliminating recursion" http://stackoverflow.com/questions/10253161/efficient-graph-traversal-with-linq-eliminating-recursion
        // to ensure items are returned in the order they are encountered.
        public static IEnumerable<T> Traverse<T>(
            T root,
            Func<T, IEnumerable<T>> children, bool includeSelf = true)
        {
            if (includeSelf)
                yield return root;
            var stack = new Stack<IEnumerator<T>>();
            try
            {
                stack.Push(children(root).GetEnumerator());
                while (stack.Count != 0)
                {
                    var enumerator = stack.Peek();
                    if (!enumerator.MoveNext())
                    {
                        stack.Pop();
                        enumerator.Dispose();
                    }
                    else
                    {
                        yield return enumerator.Current;
                        stack.Push(children(enumerator.Current).GetEnumerator());
                    }
                }
            }
            finally
            {
                foreach (var enumerator in stack)
                    enumerator.Dispose();
            }
        }
    }
}
