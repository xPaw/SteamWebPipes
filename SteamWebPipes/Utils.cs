using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SteamWebPipes
{
    internal static class Utils
    {
        // Adapted from http://stackoverflow.com/a/13503860/139147
        public static IEnumerable<TResult> FullOuterJoin<TLeft, TRight, TKey, TResult>(
            /*            this*/ IEnumerable<TLeft> left,
            IEnumerable<TRight> right,
            Func<TLeft, TKey> leftKeySelector,
            Func<TRight, TKey> rightKeySelector,
            Func<TLeft, TRight, TKey, TResult> resultSelector,
            TLeft defaultLeft = default,
            TRight defaultRight = default)
        {
            var leftLookup = left.ToLookup(leftKeySelector);
            var rightLookup = right.ToLookup(rightKeySelector);

            var leftKeys = leftLookup.Select(l => l.Key);
            var rightKeys = rightLookup.Select(r => r.Key);

            var keySet = new HashSet<TKey>(leftKeys.Union(rightKeys));

            return from key in keySet
                from leftValue in leftLookup[key].DefaultIfEmpty(defaultLeft)
                from rightValue in rightLookup[key].DefaultIfEmpty(defaultRight)
                select resultSelector(leftValue, rightValue, key);
        }
    }

    internal class EmptyGrouping<TKey, TValue> : IGrouping<TKey, TValue>
    {
        public TKey Key { get; set; }

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
        {
            return Enumerable.Empty<TValue>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Enumerable.Empty<TValue>().GetEnumerator();
        }
    }
}
