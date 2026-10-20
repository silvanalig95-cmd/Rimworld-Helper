using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's column in the game's own Social tab, to the right of the game's card (Patches/Patch_SocialTab.cs puts it
    /// there): a colonist's bonds (Rimfolk's ties, their circle, the ones they lost), their groups and role, what people
    /// say about them, the memories and in-jokes they share, their last few Rimfolk talks (read from the game's own
    /// log, nothing new is saved) and the secrets the colony knows of. Each section folds away. Hovering a row of the
    /// game's list adds a line about the pair to the game's own tooltip (<see cref="PairNote"/>).
    /// <para>The model is built a couple of times a second, like the Story tab's; drawing only lays it out.</para>
    /// </summary>
    public static class SocialColumn
    {
        private static readonly Folds folds = new Folds();
        private static Vector2 scroll;
        private static float height = 300f;
        private static Pawn scrolledFor;

        private static Pawn cachedPawn;
        private static float cachedAt = -1f;
        private static List<PaneSection> cached;

        /// <summary>The Social tab's sections shown in the column, by the character page's keys.</summary>
        public static readonly string[] CharacterKeys = { "groups", "reputation", "talk.memories" };

        /// <summary>How many of their Rimfolk talks the column lists.</summary>
        public const int TalksShown = 5;

        /// <summary>Forget the last game's colonist (called when a game loads).</summary>
        public static void Reset()
        {
            cachedPawn = null;
            cached = null;
            scrolledFor = null;
            pairNotes.Clear();
        }

        // ---------------- When it shows ----------------

        /// <summary>
        /// The column can show at all: the setting is on, the deeper game hooks are on, both of the patches it needs (the
        /// tab's width and the game's card kept to its own width) are in place, the screen is wide enough, and nothing
        /// has kept the tab at its own width. When it can't, the Story tab shows the social parts again.
        /// </summary>
        public static bool Available
        {
            get
            {
                var s = ColonyCouncilMod.Settings;
                if (s == null || !s.socialTabColumn || !RimfolkPatches.On || Blocked || !Patched) return false;
                float baseWidth = SocialTabState.baseWidth > 0f ? SocialTabState.baseWidth : TabFitMath.SocialWidth;
                return TabFitMath.ColumnWidth(UI.screenWidth, baseWidth) > 0f;
            }
        }

        /// <summary>
        /// Something (another mod's own Social tab width, most likely) kept the tab narrow for a second while the column
        /// wanted room: the column stays off for the session and the tab keeps its width.
        /// </summary>
        public static bool Blocked;

        /// <summary>Frames in a row the column wanted room it didn't get.</summary>
        private static int framesWithout;

        /// <summary>Called by the tab's patch each frame it draws: whether the column wanted room, and got it.</summary>
        public static void Noted(bool wanted, bool shown)
        {
            if (!wanted || shown)
            {
                framesWithout = 0;
                return;
            }
            // The first frame or two after the tab widens are still drawn at the old width; a second of it is something else.
            if (++framesWithout < 90 || Blocked) return;
            Blocked = true;
            Log.Message("[Rimfolk] The game's Social tab is kept at its own width (by another mod?), so Rimfolk's column there is off for this session; the Story tab shows bonds, groups and secrets again.");
        }

        private static int patchedChecked = -1;
        private static bool patched;

        /// <summary>Both patches applied (worked out once the patches have been tried).</summary>
        public static bool Patched
        {
            get
            {
                int tried = RimfolkPatches.Applied.Count + RimfolkPatches.Failed.Count;
                if (tried != patchedChecked)
                {
                    patchedChecked = tried;
                    patched = RimfolkPatches.Applied.Contains(nameof(Patch_SocialTabColumn)) && RimfolkPatches.Applied.Contains(nameof(Patch_SocialCardWidth));
                }
                return patched;
            }
        }

        /// <summary>The colony's own people with a story: colonists, prisoners and slaves, alive.</summary>
        public static bool Eligible(Pawn p) =>
            p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike && (p.IsColonist || p.IsPrisonerOfColony || p.IsSlaveOfColony);

        /// <summary>The column shows for this colonist: it's available, they're the colony's own, and there's something to say.</summary>
        public static bool Wants(Pawn p)
        {
            if (!Available || !Eligible(p) || CouncilGameComponent.Instance == null) return false;
            var model = Model(p);
            return model != null && model.Count > 0;
        }

        // ---------------- The model ----------------

        private static List<PaneSection> Model(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || p == null) return null;
            if (cachedPawn != p || cached == null || Time.realtimeSinceStartup - cachedAt > 0.5f || Time.realtimeSinceStartup < cachedAt)
            {
                try
                {
                    cached = Build(comp, p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("social tab column", e);
                    cached = new List<PaneSection>();
                }
                cachedPawn = p;
                cachedAt = Time.realtimeSinceStartup;
            }
            return cached;
        }

        private static List<PaneSection> Build(CouncilGameComponent comp, Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            var list = new List<PaneSection>();

            // Bonds: Rimfolk's ties (each with its strength), their circle, and the ones they lost.
            if (s.socialWeb && comp.social != null)
            {
                var bonds = new PaneSection { key = "bonds", header = "CC_Story_Bonds".Translate(), icon = Icons.Bonds };
                var ties = comp.social.ties.Where(t => t != null && t.announced && t.Involves(p) && t.Other(p) != null && !t.Other(p).Dead)
                    .OrderBy(t => TieRank(t.kind)).ThenByDescending(t => t.strength).ToList();
                foreach (var t in ties) bonds.rows.Add(TieRow(t, p));
                if (!GroupLife.Active)
                {
                    var circle = comp.social.CliqueOf(p);
                    if (circle != null)
                    {
                        var row = bonds.Add("CC_Social_Describe_Circle".Translate(circle.name).Resolve(), UiKit.Gold, "CC_Social_CliqueTip".Translate());
                        if (row != null)
                        {
                            row.icon = Icons.Bonds;
                            row.accent = UiKit.Gold;
                        }
                    }
                }
                foreach (var l in comp.social.lost.Where(l => l != null && l.survivor == p).OrderByDescending(l => l.tick).Take(5))
                {
                    var row = bonds.Add("CC_Story_Lost".Translate(l.name, ("CC_Tie_" + l.kind).Translate()).Resolve(), UiKit.LossColor,
                        "CC_SocialCol_LostTip".Translate(l.name, PersonaMarks.DateLabel(l.tick, p)).Resolve());
                    if (row != null)
                    {
                        row.icon = Icons.Death;
                        row.accent = UiKit.WithAlpha(UiKit.LossColor, 0.8f);
                    }
                }
                if (ties.Count > 0) bonds.right = ties.Count.ToString();
                if (!bonds.Empty) list.Add(bonds);
            }

            // Their groups, what people say of them, and what they went through with others (the character page's own sections).
            if (Persona.On && Persona.Eligible(p))
            {
                AddCharacter(list, p, "groups", Icons.Bonds, null);
                AddCharacter(list, p, "reputation", Icons.Chronicle, null);
                AddCharacter(list, p, "talk.memories", Icons.Heart, "CC_SocialCol_Memories".Translate());
            }

            // Their last few Rimfolk talks, from the game's own log.
            var talks = Talks(p);
            if (talks.Count > 0)
            {
                var sec = new PaneSection { key = "talks", header = "CC_SocialCol_Talks".Translate(), icon = Icons.Council };
                sec.rows.AddRange(talks);
                list.Add(sec);
            }

            // Secrets: only what the colony knows.
            if (s.secrets && comp.secrets != null)
            {
                var sec = new PaneSection { key = "secrets", header = "CC_Social_Secrets".Translate(), icon = Icons.Secret };
                var own = comp.secrets.Of(p);
                if (own != null && own.revealed)
                    sec.Add(own.Truth, own.def != null && own.def.good ? UiKit.Good : new Color(0.9f, 0.6f, 0.5f), "CC_SocialCol_SecretOutTip".Translate());
                else if (own != null && own.knowers.Count > 0)
                    sec.Add("CC_Social_SecretKnown".Translate(p.LabelShort, TextUtil.JoinAnd(own.knowers.Where(k => k != null).Select(k => k.LabelShort).ToList())).Resolve(),
                        UiKit.Faint, "CC_SocialCol_ConfidedTip".Translate());
                foreach (var other in comp.secrets.secrets.Where(x => x?.owner != null && x.owner != p && !x.revealed && x.knowers.Contains(p)))
                    sec.Add("CC_Story_Keeps".Translate(other.owner.LabelShort).Resolve(), UiKit.Faint, "CC_SocialCol_KeepsTip".Translate());
                if (!sec.Empty) list.Add(sec);
            }
            return list;
        }

        private static void AddCharacter(List<PaneSection> list, Pawn p, string key, Texture2D icon, string header)
        {
            var cs = CharacterPage.Make(key, p);
            if (cs == null) return;
            list.Add(new PaneSection { key = key, header = header ?? cs.title, icon = icon, character = cs });
        }

        private static int TieRank(TieKind k) => k == TieKind.Friends ? 0 : k == TieKind.Mentor ? 1 : k == TieKind.Comrades ? 2 : 3;

        private static PaneRow TieRow(SocialTie t, Pawn p)
        {
            var other = t.Other(p);
            var color = UiKit.TieColor(t.kind);
            string text = other.LabelShort;
            string right = t.Label;
            if (t.kind == TieKind.Mentor && t.skill != null)
            {
                text = (t.a == p ? "CC_Social_Describe_Teaching" : "CC_Social_Describe_Learning").Translate(other.LabelShort, t.skill.label).Resolve().TrimEnd('.');
                right = Stage(t.strength);
            }
            var tie = t;
            return new PaneRow
            {
                text = text, face = other, accent = color, right = right, bar = Mathf.Clamp01(t.strength / 100f), select = other,
                tipFn = () => SocialPanel.Tip(tie), tipId = Gen.HashCombineInt(t.a?.thingIDNumber ?? 0, t.b?.thingIDNumber ?? 0) ^ 0x50C1A1
            };
        }

        private static string Stage(float strength) => (strength >= 70f ? "CC_Social_Deep" : strength >= 45f ? "CC_Social_Solid" : "CC_Social_Weak").Translate();

        // ---------------- Recent talks ----------------

        private static AccessTools.FieldRef<PlayLogEntry_Interaction, InteractionDef> intDefRef;
        private static bool intDefLooked;

        /// <summary>The interaction a log entry is, or null when that can't be read.</summary>
        private static InteractionDef DefOf(PlayLogEntry_Interaction e)
        {
            if (!intDefLooked)
            {
                intDefLooked = true;
                try
                {
                    intDefRef = AccessTools.FieldRefAccess<PlayLogEntry_Interaction, InteractionDef>("intDef");
                }
                catch (Exception e2)
                {
                    intDefRef = null;
                    CouncilLog.ErrorOnce("social tab column: the log's interactions", e2);
                }
            }
            return intDefRef == null ? null : intDefRef(e);
        }

        /// <summary>
        /// Their last few Rimfolk exchanges with another (talks, moments, gossip, social events), newest first, as the
        /// game's log words them. Nothing is kept for this: the game's log already is.
        /// </summary>
        private static List<PaneRow> Talks(Pawn p)
        {
            var rows = new List<PaneRow>();
            var entries = Find.PlayLog?.AllEntries;
            if (entries == null || ColonyCouncilMod.Pack == null) return rows;
            var found = new List<PlayLogEntry_Interaction>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (!(entries[i] is PlayLogEntry_Interaction e) || !e.Concerns(p)) continue;
                if (!(e is PlayLogEntry_CouncilEvent))
                {
                    var def = DefOf(e);
                    if (def == null || def.modContentPack != ColonyCouncilMod.Pack) continue;
                }
                found.Add(e);
            }
            foreach (var e in found.OrderByDescending(x => x.Tick).Take(TalksShown))
            {
                string text = e.ToGameStringFromPOV(p);
                if (text.NullOrEmpty()) continue;
                var other = e.GetConcerns().OfType<Pawn>().FirstOrDefault(x => x != p);
                rows.Add(new PaneRow
                {
                    text = text,
                    face = other,
                    accent = UiKit.Neutral,
                    right = TextUtil.DaysString(Math.Max(GenDate.TicksPerHour, e.Age)),
                    tip = e.GetTipString(),
                    font = GameFont.Small,
                    select = other != null && !other.Dead ? other : null
                });
            }
            return rows;
        }

        // ---------------- Drawing ----------------

        /// <summary>The column in <paramref name="rect"/> (the room right of the game's card, inside the tab).</summary>
        public static void Draw(Rect rect, Pawn p)
        {
            var model = Model(p);
            if (model == null) return;
            if (scrolledFor != p)
            {
                scroll = Vector2.zero;
                scrolledFor = p;
            }
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            var oldColor = GUI.color;
            try
            {
                Widgets.DrawBoxSolid(new Rect(rect.x - TabFitMath.ColumnGap / 2f, rect.y + 10f, 1f, Mathf.Max(0f, rect.height - 20f)), UiKit.DividerColor);
                var inner = new Rect(rect.x, rect.y + 10f, rect.width - 10f, rect.height - 20f);
                // The tab's close button sits in the top right corner, above the column.
                float top = UiKit.PageHeader(inner, Icons.Bonds, "CC_SocialCol_Title".Translate(), "CC_SocialCol_Sub".Translate(), null, null, 26f,
                    "CC_SocialCol_Tip".Translate());
                PawnPane.Draw(new Rect(inner.x, inner.y + top, inner.width, inner.height - top), model, ref scroll, ref height, folds,
                    "CC_SocialCol_Empty".Translate(p.LabelShort), p);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        // ---------------- A row of the game's list ----------------

        private static readonly Dictionary<long, (float at, string text)> pairNotes = new Dictionary<long, (float, string)>();

        /// <summary>Lines about the pair are added to the game's tooltip on its relation rows (with the column's setting).</summary>
        public static bool TipsWanted => (ColonyCouncilMod.Settings?.socialTabColumn ?? false) && RimfolkPatches.On;

        /// <summary>
        /// A short Rimfolk line about two people, for the game's tooltip on a row of its Social tab: their tie, a group
        /// they share, the memories they share, views they agree on or not. Kept a second per pair; null if there's
        /// nothing to say.
        /// </summary>
        public static string PairNote(Pawn a, Pawn b)
        {
            if (a == null || b == null || a == b || !Eligible(a)) return null;
            long key = PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber) * 2 + (a.thingIDNumber < b.thingIDNumber ? 0 : 1);
            float now = Time.realtimeSinceStartup;
            if (pairNotes.TryGetValue(key, out var kept) && now - kept.at < 1f && now >= kept.at) return kept.text;
            string text;
            try
            {
                text = BuildPairNote(a, b);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("social tab: a pair's line", e);
                text = null;
            }
            if (pairNotes.Count > 300) pairNotes.Clear();
            pairNotes[key] = (now, text);
            return text;
        }

        private static string BuildPairNote(Pawn a, Pawn b)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return null;
            var parts = new List<string>();
            if (ColonyCouncilMod.Settings.socialWeb && comp.social != null)
                foreach (var t in comp.social.ties.Where(t => t != null && t.announced && t.Between(a, b)).OrderBy(t => TieRank(t.kind)))
                {
                    if (t.kind == TieKind.Mentor && t.skill != null)
                        parts.Add((t.a == a ? "CC_Social_Describe_Teaching" : "CC_Social_Describe_Learning").Translate(b.LabelShort, t.skill.label).Resolve());
                    else parts.Add("CC_SocialCol_PairTie".Translate(t.Label.CapitalizeFirst(), Stage(t.strength)).Resolve());
                }
            var group = Groups.Shared(a, b);
            if (group != null) parts.Add("CC_Social_TieGroup".Translate(group.Name).Resolve());
            if (Persona.On)
            {
                int memories = SharedMemories.Of(a, b).Count;
                if (memories == 1) parts.Add("CC_SocialCol_PairMemory".Translate().Resolve());
                else if (memories > 1) parts.Add("CC_SocialCol_PairMemories".Translate(memories).Resolve());
                var (agree, odds) = Views(a, b);
                if (agree.Count > 0) parts.Add("CC_SocialCol_PairAgree".Translate(TextUtil.JoinAnd(agree)).Resolve());
                if (odds.Count > 0) parts.Add("CC_SocialCol_PairOdds".Translate(TextUtil.JoinAnd(odds)).Resolve());
            }
            if (parts.Count == 0) return null;
            return ("CC_SocialCol_PairHeader".Translate().Resolve() + " " + string.Join(" ", parts)).Colorize(UiKit.HeaderColor);
        }

        /// <summary>The topics both hold a view on: the ones they agree about, and the ones they're at odds over (two of each at most).</summary>
        private static (List<string> agree, List<string> odds) Views(Pawn a, Pawn b)
        {
            var agree = new List<string>();
            var odds = new List<string>();
            var ra = Persona.Of(a);
            var rb = Persona.Of(b);
            if (ra == null || rb == null || ra.stances.Count == 0 || rb.stances.Count == 0) return (agree, odds);
            var both = new List<(string key, float va, float vb)>();
            foreach (var kv in ra.stances)
            {
                if (Mathf.Abs(kv.Value) < TalkMath.SideAt || !rb.stances.TryGetValue(kv.Key, out float vb) || Mathf.Abs(vb) < TalkMath.SideAt) continue;
                both.Add((kv.Key, kv.Value, vb));
            }
            foreach (var (key, va, vb) in both.OrderByDescending(x => Mathf.Abs(x.va) + Mathf.Abs(x.vb)).ThenBy(x => x.key))
            {
                var list = Mathf.Sign(va) == Mathf.Sign(vb) ? agree : odds;
                if (list.Count >= 2) continue;
                string about = TopicAbout(key);
                if (!about.NullOrEmpty() && !list.Contains(about)) list.Add(about);
            }
            return (agree, odds);
        }

        /// <summary>A stance's topic in a few words ("the walls", "Mira, the newcomer"), or null.</summary>
        private static string TopicAbout(string key)
        {
            int colon = key.IndexOf(':');
            var topic = DefRefs.Named<TopicDef>(colon >= 0 ? key.Substring(0, colon) : key);
            if (topic == null) return null;
            TalkSubject subject = null;
            if (colon >= 0)
            {
                int id = int.TryParse(key.Substring(colon + 1), out int x) ? x : -1;
                var who = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive.FirstOrDefault(o => o.thingIDNumber == id);
                if (who == null) return null;
                subject = TalkSubject.Of(who);
            }
            return Talk.About(topic, subject);
        }
    }
}
