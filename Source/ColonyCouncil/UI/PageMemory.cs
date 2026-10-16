using System;
using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Where the council window opens (the "Council window opens on" setting).</summary>
    public enum CouncilOpensOn
    {
        /// <summary>Colony → This week, whatever page was open last.</summary>
        ThisWeek,
        /// <summary>The page last looked at, also after a restart.</summary>
        LastPage
    }

    /// <summary>
    /// The filters and sort orders the council's pages remember (the roster's sort, the chronicle's filter, the people
    /// table's columns...), kept with the mod settings so they last between sessions. A page reads its value once and
    /// writes it when the player changes it; the settings file is written only when the council window closes and
    /// something changed (<see cref="Flush"/>). Keys are "page.what", values plain text (an enum's name, a number).
    /// </summary>
    public static class PageMemory
    {
        private static Dictionary<string, string> values;
        private static bool dirty;

        private static Dictionary<string, string> Values
        {
            get
            {
                if (values != null) return values;
                values = new Dictionary<string, string>();
                var saved = ColonyCouncilMod.Settings?.councilPageState;
                if (saved != null)
                    foreach (var line in saved)
                    {
                        int eq = line?.IndexOf('=') ?? -1;
                        if (eq > 0) values[line.Substring(0, eq)] = line.Substring(eq + 1);
                    }
                return values;
            }
        }

        public static string Get(string key, string fallback = null) => key != null && Values.TryGetValue(key, out var v) ? v : fallback;

        public static T GetEnum<T>(string key, T fallback) where T : struct =>
            Enum.TryParse(Get(key), out T v) && Enum.IsDefined(typeof(T), v) ? v : fallback;

        public static bool GetBool(string key, bool fallback) => bool.TryParse(Get(key), out bool v) ? v : fallback;

        public static void Set(string key, string value)
        {
            if (key == null) return;
            if (value == null)
            {
                if (Values.Remove(key)) dirty = true;
                return;
            }
            if (Values.TryGetValue(key, out var had) && had == value) return;
            Values[key] = value;
            dirty = true;
        }

        public static void SetEnum<T>(string key, T value) where T : struct => Set(key, value.ToString());

        public static void SetBool(string key, bool value) => Set(key, value ? "true" : "false");

        /// <summary>Puts what changed into the settings; true when the settings file needs writing.</summary>
        public static bool Flush()
        {
            if (!dirty || values == null) return false;
            var s = ColonyCouncilMod.Settings;
            if (s == null) return false;
            dirty = false;
            var list = new List<string>(values.Count);
            foreach (var kv in values)
                if (!kv.Key.NullOrEmpty() && kv.Value != null && kv.Key.IndexOf('=') < 0)
                    list.Add(kv.Key + "=" + kv.Value);
            list.Sort(StringComparer.Ordinal);
            s.councilPageState = list;
            return true;
        }
    }
}
