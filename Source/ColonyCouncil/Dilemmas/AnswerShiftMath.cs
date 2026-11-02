using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// What an answer changes for a while and puts back after (Dilemmas/AnswerShifts.cs), written as short lines so they
    /// keep in a save: an hour on a colonist's timetable ("h:13=Anything>Sleep") or their place in a kind of work
    /// ("w:Cooking=1>0"); the timetable plans the answers ask for (the heat wave's hours, a night at the bench, a late
    /// start after a night under the stars); and which of two colonists' work needs a new limb more. Pure (no game types),
    /// for the tests.
    /// </summary>
    public static class AnswerShiftMath
    {
        public const string Work = "Work", Anything = "Anything", Sleep = "Sleep", Joy = "Joy";

        /// <summary>The plans: the heat wave's hours, a midday rest, a night at work, a late start.</summary>
        public const string Heat = "heat", Siesta = "siesta", Night = "night", Late = "late";

        public static readonly string[] Plans = { Heat, Siesta, Night, Late };

        public struct Change
        {
            /// <summary>A kind of work (its place in the work tab) rather than an hour.</summary>
            public bool work;
            /// <summary>The hour (0 to 23), for an hour.</summary>
            public int hour;
            /// <summary>The work type's name, for work.</summary>
            public string key;
            /// <summary>What it was, and what the answer set it to.</summary>
            public string was;
            public string set;
        }

        public static Change Hour(int hour, string was, string set) => new Change { hour = hour, was = was, set = set };

        public static Change WorkPlace(string work, int was, int set) => new Change { work = true, key = work, was = was.ToString(), set = set.ToString() };

        /// <summary>"h:13=Anything>Sleep;w:Cooking=1>0".</summary>
        public static string Write(IEnumerable<Change> changes)
        {
            if (changes == null) return "";
            return string.Join(";", changes.Select(c => (c.work ? "w:" + c.key : "h:" + c.hour) + "=" + c.was + ">" + c.set));
        }

        /// <summary>The changes written by <see cref="Write"/>; anything it can't read is left out.</summary>
        public static List<Change> Read(string s)
        {
            var list = new List<Change>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var part in s.Split(';'))
            {
                int eq = part.IndexOf('='), gt = part.IndexOf('>');
                if (part.Length < 3 || part[1] != ':' || eq < 3 || gt < eq + 1) continue;
                string key = part.Substring(2, eq - 2), was = part.Substring(eq + 1, gt - eq - 1), set = part.Substring(gt + 1);
                if (was.Length == 0 || set.Length == 0) continue;
                if (part[0] == 'h' && int.TryParse(key, out int h) && h >= 0 && h < 24) list.Add(Hour(h, was, set));
                else if (part[0] == 'w' && key.Length > 0 && int.TryParse(was, out _) && int.TryParse(set, out _)) list.Add(new Change { work = true, key = key, was = was, set = set });
            }
            return list;
        }

        /// <summary>The hours from <paramref name="from"/> to <paramref name="to"/>, both included, round midnight when it comes first.</summary>
        public static IEnumerable<int> Span(int from, int to)
        {
            for (int h = from; ; h = (h + 1) % 24)
            {
                yield return h;
                if (h == to) yield break;
            }
        }

        private static bool Waking(string a) => a == Work || a == Anything;

        /// <summary>
        /// What a plan does to one colonist's day, from what each hour is now (<paramref name="now"/>: "Work", "Anything",
        /// "Sleep", "Joy" or another assignment's name): the heat wave's hours sleep through the midday heat (11 to 16)
        /// and work the night instead (21 to 2); a midday rest makes 12 to 15 recreation; a night at work turns tonight's
        /// sleep (22 to 5) to work and sleeps the morning after (6 to 10); a late start sleeps the morning (6 to 8). Only
        /// work, anything and sleep hours change, never someone's meditation or recreation.
        /// </summary>
        public static List<Change> Plan(string plan, Func<int, string> now)
        {
            var list = new List<Change>();
            if (now == null) return list;
            void Turn(int from, int to, Func<string, bool> which, string set)
            {
                foreach (int h in Span(from, to))
                {
                    string a = now(h);
                    if (a != null && a != set && which(a)) list.Add(Hour(h, a, set));
                }
            }
            switch (plan)
            {
                case Heat:
                    Turn(11, 16, Waking, Sleep);
                    Turn(21, 2, a => a == Sleep, Work);
                    break;
                case Siesta:
                    Turn(12, 15, Waking, Joy);
                    break;
                case Night:
                    Turn(22, 5, a => a == Sleep, Work);
                    Turn(6, 10, Waking, Sleep);
                    break;
                case Late:
                    Turn(6, 8, Waking, Sleep);
                    break;
            }
            return list;
        }

        /// <summary>Hours from <paramref name="hourNow"/> (0 to 24, with fractions) to the next <paramref name="hour"/> o'clock that is at least <paramref name="minHours"/> away.</summary>
        public static float HoursUntil(float hourNow, int hour, float minHours)
        {
            float d = hour - hourNow;
            while (d < minHours) d += 24f;
            return d;
        }

        /// <summary>
        /// How long a plan lasts, in hours, unless its condition ends sooner: the heat wave's hours and a midday rest until
        /// the heat wave ends (at most <see cref="MaxDays"/> days); a night at work until 11 o'clock the morning after (the
        /// next 11 o'clock at least 9 hours away, so a night already begun counts); a late start until 9 the next morning.
        /// </summary>
        public static float Hours(string plan, float hourNow)
        {
            switch (plan)
            {
                case Night: return HoursUntil(hourNow, 11, 9f);
                case Late: return HoursUntil(hourNow, 9, 6f);
                default: return MaxDays * 24f;
            }
        }

        /// <summary>A shift tied to a condition (a heat wave) ends with it, or after this many days at most.</summary>
        public const float MaxDays = 15f;

        /// <summary>How long a hand-over of work lasts, in days.</summary>
        public const float HandOverDays = 3f;

        // ---------------- A new limb ----------------

        /// <summary>What a body part is for: legs (walking, hauling, the hunt), arms and hands (crafting, doctoring, building), eyes (aim, close work), anything else.</summary>
        public static string PartKind(string partDefName)
        {
            string n = (partDefName ?? "").ToLowerInvariant();
            if (n.Contains("leg") || n.Contains("foot") || n.Contains("knee") || n.Contains("toe")) return "leg";
            if (n.Contains("arm") || n.Contains("hand") || n.Contains("shoulder") || n.Contains("finger") || n.Contains("clavicle") || n.Contains("humerus")) return "arm";
            if (n.Contains("eye")) return "eye";
            return "any";
        }

        /// <summary>The skills whose work needs such a part most: a hunter's or a herder's legs, a crafter's or a doctor's hands, a shooter's or a doctor's eyes.</summary>
        public static string[] SkillsFor(string kind)
        {
            switch (kind)
            {
                case "leg": return new[] { "Shooting", "Animals", "Plants", "Mining", "Construction" };
                case "arm": return new[] { "Crafting", "Medicine", "Construction", "Cooking", "Artistic" };
                case "eye": return new[] { "Shooting", "Medicine", "Crafting", "Intellectual", "Artistic" };
                default: return new[] { "Construction", "Crafting", "Medicine", "Plants", "Shooting", "Cooking" };
            }
        }

        /// <summary>How much someone's work needs the part: their best level in its skills, and which skill that is (null with none).</summary>
        public static int Need(IDictionary<string, int> levels, string kind, out string skill)
        {
            skill = null;
            int best = -1;
            if (levels == null) return best;
            foreach (var s in SkillsFor(kind))
                if (levels.TryGetValue(s, out int l) && l > best)
                {
                    best = l;
                    skill = s;
                }
            return best;
        }

        /// <summary>The first of two whose work needs the part more: true for the first, false for the second (a tie goes to the first).</summary>
        public static bool FirstNeedsMore(IDictionary<string, int> first, IDictionary<string, int> second, string kind) =>
            Need(first, kind, out _) >= Need(second, kind, out _);
    }
}
