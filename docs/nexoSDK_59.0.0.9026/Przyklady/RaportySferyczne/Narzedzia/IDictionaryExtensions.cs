using System.Collections.Generic;

namespace RaportySferyczne
{
    internal static class IDictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
                value = defaultValue;

            return value;
        }
    }
}
