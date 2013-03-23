using System;
using System.Collections.Concurrent;

namespace WcfClientProxyGenerator.Util
{
    internal static class DictionaryExtensions
    {
        public static TValue GetOrAddSafe<TKey, TValue>(
            this ConcurrentDictionary<TKey, Lazy<TValue>> dictionary,
            TKey key,
            Func<TKey, TValue> valueFactory)
            where TValue : class
        {
            Lazy<TValue> lazy = dictionary.GetOrAdd(key, new Lazy<TValue>(() => valueFactory(key)));
            return lazy.Value;
        }
    }
}
