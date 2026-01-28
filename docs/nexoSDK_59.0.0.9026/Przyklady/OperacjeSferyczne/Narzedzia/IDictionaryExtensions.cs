using System;
using System.Collections.Generic;

namespace OperacjeSferyczne.Narzedzia
{
    internal static class IDictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key, Func<TValue> valueCreator)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (!source.TryGetValue(key, out TValue value))
            {
                value = valueCreator();
                source[key] = value;
            }

            return value;
        }
    }
}
