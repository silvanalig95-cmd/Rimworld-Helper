using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How groups are put into words and colours: roles, ways, habits, dates, names.</summary>
    public static class GroupText
    {
        public static string RoleKey(GroupRole r) => r.ToString().ToLowerInvariant();

        /// <summary>"the joker"; "one of them" for no role.</summary>
        public static string Role(GroupRole r) => ("CC_GroupRole_" + r).Translate().ToString();

        /// <summary>What a role means, for tooltips (the anchor's in words of its own: a group is never named after them).</summary>
        public static string RoleTip(GroupRole r) => (r == GroupRole.Anchor ? "CC_GroupRoleTip_AnchorCalls" : "CC_GroupRoleTip_" + r).Translate().ToString();

        public static Color RoleColor(GroupRole r)
        {
            switch (r)
            {
                case GroupRole.Anchor: return UiKit.Gold;
                case GroupRole.Joker: return new Color(0.98f, 0.8f, 0.45f);
                case GroupRole.Peacemaker: return new Color(0.6f, 0.86f, 0.75f);
                case GroupRole.Firebrand: return new Color(0.95f, 0.55f, 0.42f);
                case GroupRole.Worrier: return new Color(0.72f, 0.72f, 0.92f);
                case GroupRole.Quiet: return new Color(0.7f, 0.75f, 0.8f);
                case GroupRole.Newcomer: return UiKit.Info;
                default: return UiKit.Faint;
            }
        }

        private static readonly Color[] Palette =
        {
            new Color(1f, 0.82f, 0.45f), new Color(0.55f, 0.8f, 0.95f), new Color(0.7f, 0.9f, 0.55f), new Color(0.95f, 0.6f, 0.75f),
            new Color(0.8f, 0.68f, 0.98f), new Color(0.98f, 0.66f, 0.42f), new Color(0.5f, 0.88f, 0.82f), new Color(0.9f, 0.9f, 0.6f)
        };

        /// <summary>A group's own colour, the same for as long as it lasts.</summary>
        public static Color ColorOf(SocialGroup g) => g == null ? UiKit.Faint : Palette[Sched.Mod(g.id * 5 + 3, Palette.Length)];

        public static string Day(int tick) => "CC_DayN".Translate(tick / GenDate.TicksPerDay + 1).ToString();

        /// <summary>A date the way the game writes one ("5th of Aprimay, 5501"), at home.</summary>
        public static string Date(int tick)
        {
            try
            {
                var map = Find.AnyPlayerHomeMap;
                var loc = map != null ? Find.WorldGrid.LongLatOf(map.Tile) : Vector2.zero;
                int abs = tick + (Find.TickManager.TicksAbs - Find.TickManager.TicksGame);
                return GenDate.DateFullStringAt(abs, loc);
            }
            catch
            {
                return Day(tick);
            }
        }

        public static string Cohesion(float c) =>
            (c >= 75f ? "CC_Group_Cohesion_Tight" : c >= GroupMath.CloseAt ? "CC_Group_Cohesion_Close" : c >= GroupMath.SplitBelow ? "CC_Group_Cohesion_Loose" : "CC_Group_Cohesion_Fraying").Translate().ToString();

        public static string Rivalry(float r) => (r >= 65f ? "CC_Group_Rivalry_Bad" : r >= GroupMath.RivalryShows ? "CC_Group_Rivalry_Some" : "CC_Group_Rivalry_Little").Translate().ToString();

        /// <summary>"the hunters and the night shift".</summary>
        public static string Names(IEnumerable<SocialGroup> groups) => TextUtil.JoinAnd(groups.Select(g => g.Name).ToList());

        public static string Members(SocialGroup g) => TextUtil.JoinAnd(g.Pawns.Select(p => p.LabelShort).ToList());

        /// <summary>
        /// The names a kind may give a new group, in the colony's era. A friends kind's are the old ones after its anchor
        /// (for older saves only): none.
        /// </summary>
        public static List<string> NameOptions(GroupKindDef kind) => Fitting(kind?.binds == GroupBinds.Friends ? null : kind?.names);

        /// <summary>The names a share may give a group, in the colony's era.</summary>
        public static List<string> NameOptions(GroupShareDef share) => Fitting(share?.names);

        private static List<string> Fitting(List<string> names)
        {
            var list = new List<string>();
            if (names == null) return list;
            bool pods = ArrivedByPods();
            foreach (var raw in names)
            {
                if (raw.NullOrEmpty() || !Flavor.TechFits(raw)) continue;
                string text = Flavor.Strip(raw, out var tags);
                if (tags != null && tags.Contains("pods") && !pods) continue;
                if (tags != null && tags.Contains("!pods") && pods) continue;
                // Never after one of them (the old names of a friends kind are for finding older saves).
                if (text.Contains(GroupNaming.Someone)) continue;
                list.Add(text);
            }
            return list;
        }

        /// <summary>
        /// A name for one of the older circles (with "Personalities and social life" off: friends round the best-connected
        /// of them, worked out each day), never after one of them: one of the friendship names no other circle has, the
        /// same for the same <paramref name="seed"/> (their leader's), else the first with a number.
        /// </summary>
        public static string CircleName(int seed, ICollection<string> taken)
        {
            var options = new List<string>();
            foreach (var s in GroupShareDef.All)
                if (s.idea == ShareIdea.Friends)
                    options.AddRange(NameOptions(s));
            string name = GroupNaming.Pick(options, taken, seed);
            if (name != null) return name;
            string first = options.FirstOrDefault() ?? "CC_Social_Cliques".Translate().ToString().ToLowerInvariant();
            for (int n = 2; n < 50; n++)
            {
                string s = first + " (" + n + ")";
                if (taken == null || !taken.Contains(s)) return s;
            }
            return first;
        }

        /// <summary>
        /// How names after one person were made in older saves, as patterns (<see cref="GroupNaming.AfterSomeone"/>): a
        /// friends kind's ("{ANCHOR}'s lot"), the older circles' ("Stace's friends"), and any kind's once its own names were
        /// all taken ("Hector's hunters", with <paramref name="kind"/>'s label; every kind's when it's gone).
        /// </summary>
        public static List<string> AfterSomeonePatterns(GroupKindDef kind)
        {
            var list = new List<string>();
            foreach (var k in GroupKindDef.All)
                if (k.binds == GroupBinds.Friends && k.names != null)
                    list.AddRange(k.names.Select(n => Flavor.Strip(n, out _)));
            list.Add(GroupNaming.AsPattern("CC_Clique_Name".Translate().RawText, "PAWN"));
            string nameOf = "CC_Group_NameOf".Translate().RawText;
            foreach (var k in kind != null ? new List<GroupKindDef> { kind } : GroupKindDef.All)
                if (!k.label.NullOrEmpty())
                    list.Add(GroupNaming.AsPattern(nameOf, "ANCHOR", k.label));
            return list;
        }

        /// <summary>Every name a kind or a share gives (era tags off): nobody's, whatever they look like.</summary>
        public static List<string> GivenNames()
        {
            var list = new List<string>();
            foreach (var k in GroupKindDef.All)
                if (k.binds != GroupBinds.Friends && k.names != null)
                    list.AddRange(k.names.Select(n => Flavor.Strip(n, out _)));
            foreach (var s in GroupShareDef.All)
                if (s.names != null)
                    list.AddRange(s.names.Select(n => Flavor.Strip(n, out _)));
            return list;
        }

        private static bool? pods;
        private static Scenario podsFor;

        /// <summary>The colony's first people came down in drop pods (a ship's crew, as in Crashlanded).</summary>
        public static bool ArrivedByPods()
        {
            try
            {
                var scen = Find.Scenario;
                if (scen == null) return false;
                if (podsFor == scen && pods != null) return pods.Value;
                podsFor = scen;
                pods = false;
                foreach (var part in scen.AllParts)
                {
                    if (!(part is ScenPart_PlayerPawnsArriveMethod)) continue;
                    var field = HarmonyLib.AccessTools.Field(typeof(ScenPart_PlayerPawnsArriveMethod), "method");
                    if (field != null && field.GetValue(part) is PlayerPawnsArriveMethod m) pods = m == PlayerPawnsArriveMethod.DropPods;
                }
                return pods.Value;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Every sentence starts with a capital ("the hunters are..." at the start of one, from a group's name).</summary>
        public static string Sentences(string text)
        {
            if (text.NullOrEmpty()) return text;
            var c = text.ToCharArray();
            bool start = true;
            for (int i = 0; i < c.Length; i++)
            {
                if (start && char.IsLetter(c[i]))
                {
                    c[i] = char.ToUpperInvariant(c[i]);
                    start = false;
                }
                else if (c[i] == '.' || c[i] == '!' || c[i] == '?') start = i + 1 < c.Length && c[i + 1] == ' ';
                else if (!char.IsWhiteSpace(c[i]) && c[i] != '"' && c[i] != '\'') start = false;
            }
            return new string(c);
        }

        /// <summary>
        /// A journal line from a pool in the colonist's voice, with a group's name in it ({GROUP}): written with every
        /// sentence capitalised, since a group's name ("the hunters") may begin one.
        /// </summary>
        public static void Journal(Pawn p, string pool, params NamedArgument[] args)
        {
            if (p == null || p.Dead || !(ColonyCouncilMod.Settings?.journals ?? false)) return;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            try
            {
                string line = Flavor.FromPool(pool, p);
                if (line.NullOrEmpty()) return;
                var all = new List<NamedArgument> { p.Named("PAWN") };
                if (args != null) all.AddRange(args);
                comp.journals.WriteText(p, Sentences(line.Formatted(all.ToArray()).Resolve()));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("group journal " + pool, e);
            }
        }

        /// <summary>The group's ways, in words ("loud and merry, and hard workers").</summary>
        public static string NormWords(SocialGroup g) => TextUtil.JoinAnd(g.NormDefs.Select(n => n.label).ToList());

        /// <summary>A shared view as the card says it ("for: the council's decrees").</summary>
        public static string View(string view)
        {
            int bar = view?.LastIndexOf('|') ?? -1;
            if (bar <= 0) return view;
            string topic = TopicLabel(view.Substring(0, bar));
            bool for_ = view.Substring(bar + 1) != "-1";
            return (for_ ? "CC_Group_ViewFor" : "CC_Group_ViewAgainst").Translate(topic).ToString();
        }

        private static readonly Dictionary<string, string> topicLabels = new Dictionary<string, string>();
        private static Type topicType;
        private static bool topicLooked;

        /// <summary>
        /// A topic's label: the label of the def that names it (a TopicDef, if the talk part of the mod has one by that
        /// name), else the key made readable.
        /// </summary>
        public static string TopicLabel(string key)
        {
            if (key.NullOrEmpty()) return "?";
            if (topicLabels.TryGetValue(key, out var label)) return label;
            label = null;
            try
            {
                if (!topicLooked)
                {
                    topicLooked = true;
                    topicType = GenTypes.GetTypeInAnyAssembly("ColonyCouncil.TopicDef");
                }
                if (topicType != null && GenDefDatabase.GetDefSilentFail(topicType, key, false) is Def d) label = d.label;
            }
            catch
            {
                label = null;
            }
            if (label.NullOrEmpty())
            {
                string k = key;
                int us = k.LastIndexOf('_');
                if (us >= 0 && us < k.Length - 1) k = k.Substring(us + 1);
                label = GenText.SplitCamelCase(k).ToLowerInvariant();
            }
            if (topicLabels.Count < 500) topicLabels[key] = label;
            return label;
        }
    }
}
