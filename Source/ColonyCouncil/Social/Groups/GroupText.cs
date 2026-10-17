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

        /// <summary>What a role means, for tooltips.</summary>
        public static string RoleTip(GroupRole r) => ("CC_GroupRoleTip_" + r).Translate().ToString();

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

        /// <summary>A date the way the game writes one ("5 Aprimay 5501"), at home.</summary>
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
            (c >= 75f ? "CC_Group_Cohesion_Tight" : c >= 50f ? "CC_Group_Cohesion_Close" : c >= GroupMath.SplitBelow ? "CC_Group_Cohesion_Loose" : "CC_Group_Cohesion_Fraying").Translate().ToString();

        public static string Rivalry(float r) => (r >= 65f ? "CC_Group_Rivalry_Bad" : r >= GroupMath.RivalryShows ? "CC_Group_Rivalry_Some" : "CC_Group_Rivalry_Little").Translate().ToString();

        /// <summary>"the hunters and the night shift".</summary>
        public static string Names(IEnumerable<SocialGroup> groups) => TextUtil.JoinAnd(groups.Select(g => g.Name).ToList());

        public static string Members(SocialGroup g) => TextUtil.JoinAnd(g.Pawns.Select(p => p.LabelShort).ToList());

        /// <summary>The names a kind may give a new group, in the colony's era, with the one it gathers round put in.</summary>
        public static List<string> NameOptions(GroupKindDef kind, Pawn anchor)
        {
            var list = new List<string>();
            if (kind?.names == null) return list;
            bool pods = ArrivedByPods();
            foreach (var raw in kind.names)
            {
                if (raw.NullOrEmpty() || !Flavor.TechFits(raw)) continue;
                string text = Flavor.Strip(raw, out var tags);
                if (tags != null && tags.Contains("pods") && !pods) continue;
                if (tags != null && tags.Contains("!pods") && pods) continue;
                if (text.Contains("{ANCHOR}"))
                {
                    if (anchor == null) continue;
                    text = text.Replace("{ANCHOR}", anchor.LabelShort);
                }
                list.Add(text);
            }
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
