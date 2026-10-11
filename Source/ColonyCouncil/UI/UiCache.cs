using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColonyCouncil
{
    /// <summary>
    /// Values that are slow to work out (wish progress, ambition goals), remembered for about a second so the
    /// council window doesn't redo them on every frame.
    /// </summary>
    public static class UiCache
    {
        private static readonly Dictionary<(object, int), (float time, object value)> values = new Dictionary<(object, int), (float, object)>();

        public static T Get<T>(object owner, int part, Func<T> make, float seconds = 1f)
        {
            float now = Time.realtimeSinceStartup;
            var key = (owner, part);
            if (values.TryGetValue(key, out var e) && now - e.time < seconds && (e.value is T || e.value == null)) return (T)e.value;
            if (values.Count > 600) values.Clear();
            T v = make();
            values[key] = (now, v);
            return v;
        }
    }
}
