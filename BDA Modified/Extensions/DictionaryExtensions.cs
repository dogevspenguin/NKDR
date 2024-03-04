using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BDArmory.Extensions
{
    /// <summary>
    /// Extensions to Dictionaries.
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Equivalent of GetValueOrDefault for dictionaries, but with an option to specify the default value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="S"></typeparam>
        /// <param name="dict">The dictionary.</param>
        /// <param name="key">The key to look for.</param>
        /// <param name="def">The default to return if not the default for the type S.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static S GetValueOrDefault<T, S>(this Dictionary<T, S> dict, T key, S def = default) => dict.TryGetValue(key, out var value) ? value : def;
    }
}