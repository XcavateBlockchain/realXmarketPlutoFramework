using System.Runtime.CompilerServices;

namespace UniqueryPlus
{
    public class RecursionHelper
    {
        public static async IAsyncEnumerable<T> ToIAsyncEnumerableAsync<K, T>(
            Func<K?, CancellationToken, Task<RecursiveReturn<K?, T>>> getter,
            uint limit,
            [EnumeratorCancellation] CancellationToken token = default
        )
        {
            K? lastId = default;

            while (true)
            {
                RecursiveReturn<K?, T> recursiveReturn;
                try
                {
                    recursiveReturn = await getter.Invoke(lastId, token);
                }
                catch
                {
                    break;
                }

                foreach (var item in recursiveReturn.Items)
                {
                    yield return item;
                }

                if (recursiveReturn.Items.Count() != limit)
                {
                    break;
                }


                lastId = recursiveReturn.LastKey;
            }
        }
    }
}
