using System;
using System.Collections.Concurrent;

namespace WcfClientProxyGenerator.Util
{
    static class DictionaryExtensions
    {
        public static TValue GetOrAddSafe<TKey, TValue>(
            this ConcurrentDictionary<TKey, Lazy<TValue>> dictionary,
            TKey key,
            Func<TValue> valueFactory)
            where TValue : class
        {
            Lazy<TValue> lazy = dictionary.GetOrAdd(key, new Lazy<TValue>(valueFactory));
            return lazy.Value;
        }
    }
}
