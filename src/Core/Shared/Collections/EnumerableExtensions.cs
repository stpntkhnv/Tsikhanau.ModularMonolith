namespace Tsikhanau.Monolith.Shared.Collections;

public static class EnumerableExtensions
{
    extension<T>(IEnumerable<T> items)
    {
        public void ForEach(Action<T> action)
        {
            foreach (var item in items)
            {
                action(item);
            }
        }

        [Obsolete("ForEach does not await async actions. Use foreach with await.", error: true)]
        public void ForEach(Func<T, Task> action) => throw new NotSupportedException();
    }
}
