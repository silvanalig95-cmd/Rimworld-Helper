using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' hands in the game's events (docs/gods-pantheon-brainstorm.md, section C; docs/design/gods.md, "the
    /// contract"), as people's words only. When one of the events in the hands' table happens at a home
    /// (GodHandsMath.ByDef), within the hour a colonist who believes such things says whose hand they take it for, over
    /// their head, in one sentence; unless the colony knows the hand, someone near answers as a scoffer. They may be wrong: a
    /// believer names the god they know of and take it for (GodLoreMath.Attribute), or "something" when they know of none.
    /// The colony's book writes it down as they saw and said it, with who was there; the gods' hidden history keeps the act;
    /// a neighbour may write of it later (Gods/GodTales.cs). The game's own letter is never touched, and nothing tells the
    /// player what a god meant. Rare: a god keeps quiet <see cref="GodHandsMath.QuietDays"/> days after, and no god's hand
    /// is remarked on twice in <see cref="GodHandsMath.AnyQuietDays"/> at a home.
    /// </summary>
    public static class GodHands
    {
        private sealed class Pending
        {
            public Map map;
            public string defName, worker, label;
            public int tick;
        }

        /// <summary>Events waiting for the hour's look (not saved: a load drops them, as people's attention moves on).</summary>
        private static readonly List<Pending> pending = new List<Pending>();

        private const int MaxPending = 6;

        /// <summary>An event happened (the postfix): noted for the hour, if it is one whose hand people see, at a home.</summary>
        internal static void Happened(IncidentWorker w, IncidentParms parms)
        {
            if (!Gods.On || w?.def == null || !(parms?.target is Map map) || !Homes.IsHome(map)) return;
            string worker = w.GetType().Name;
            if (!GodHandsMath.ByDef.ContainsKey(w.def.defName) && !GodHandsMath.ByWorker.ContainsKey(worker)) return;
            if (pending.Count >= MaxPending) pending.RemoveAt(0);
            pending.Add(new Pending { map = map, defName = w.def.defName, worker = worker, label = w.def.label ?? w.def.defName, tick = Find.TickManager.TicksGame });
        }

        /// <summary>The region's hour: each event noted since is remarked on, or let pass.</summary>
        public static void Hourly(int now)
        {
            if (pending.Count == 0) return;
            var list = pending.ToList();
            pending.Clear();
            foreach (var p in list)
            {
                try
                {
                    if (p.map == null || !Homes.IsHome(p.map) || now - p.tick > GenDate.TicksPerDay) continue;
                    Remark(p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a god's hand", e);
                }
            }
        }

        private static void Remark(Pending p, int now)
        {
            var lore = GodLore.Instance;
            if (lore == null) return;
            var hollin = Gods.ByKey("Hollin");
            bool rootsDispleased = hollin != null && Gods.FavourValue(hollin, p.map) < GodsMath.UnknownFrom;
            var hand = GodHandsMath.Of(p.defName, p.worker, rootsDispleased);
            var god = Gods.ByKey(hand.god);
            if (!hand.Valid || god == null) return;
            // Selene returns at night what was done by day: her hand is seen only then.
            if (GodHandsMath.NightOnly(hand.god) && !GodHandsMath.Night(GenLocalDate.HourOfDay(p.map))) return;
            int home = Homes.KeyOf(p.map);
            int lastGod = lore.lastHand.TryGetValue(hand.god, out int lg) ? lg : -1;
            int lastAny = lore.lastHandAt.TryGetValue(home, out int la) ? la : -1;
            if (!GodHandsMath.Remarked(lastGod, lastAny, now, hand.chance, Rand.Value, GenDate.TicksPerDay)) return;
            Show(p.map, god, hand.kind, p.label, now, Gods.ByKey(hand.with), p.defName);
        }

        /// <summary>
        /// A god's hand remarked on at a home (the hands, and the dev action): a believer's line, a scoffer's answer unless the
        /// colony knows the hand, the colony's book, the gods' hidden history, and a note for a neighbour's letter. <paramref name="with"/>:
        /// a second god met in it (the eclipse: Selene stepping between Helios and the world), so a believer who takes it
        /// for the first says a line of their meeting. False when nobody there would say it.
        /// </summary>
        public static bool Show(Map map, God god, HandKind kind, string label, int now, God with = null, string sign = null)
        {
            var lore = GodLore.Instance;
            if (lore == null || map == null || god == null) return false;
            var people = map.mapPawns.FreeColonistsSpawned.Where(Able).ToList();
            var believer = people.Where(x => GodLore.Credence(x) >= GodLoreMath.BelieverFrom).OrderByDescending(x => GodLore.Credence(x) + Rand.Range(0f, 0.3f)).FirstOrDefault();
            if (believer == null) return false;
            // Whose hand they take it for: one they know of, rightly or not, or something they can't name.
            var knownOf = lore.KnownOf();
            foreach (var g in Gods.ColonyGods(map)) knownOf.Add(g.Key);
            // Known for the god's own (the colony saw it act plainly, or learned this sign as its own): named plainly, and nobody
            // doubts it. Else whoever the believer takes it for.
            bool known = Gods.CardName(god, map, sign) != null;
            var taken = known ? god : Gods.ByKey(GodLoreMath.Attribute(god.Key, knownOf, Rand.Value));
            bool meeting = with != null && (taken == god || taken == with);
            // The god they take it for drawn near (GodNear): their words say it was felt plainly.
            bool near = taken != null && GodNear.Near(taken, map);
            string line = Line(taken?.def?.hands, kind, near, "CC_GodHand_Unnamed_", UnnamedLines, taken, believer, meeting);
            if (line.NullOrEmpty()) return false;
            // apart: a believer's word on something everyone on the land can see, said to nobody in particular.
            Speech.Say(believer, line);
            var said = new List<(Pawn, string, bool)> { (believer, line, false) };
            Pawn scoffer = null;
            if (!known)
            {
                scoffer = people.Where(x => x != believer && Together.Can(believer, x) && GodLore.Credence(x) < GodLoreMath.ScofferBelow).OrderBy(GodLore.Credence).FirstOrDefault();
                string answer = scoffer == null ? null : Line(taken?.def?.scoffs, kind, near, "CC_GodHand_UnnamedScoff_", UnnamedScoffs, taken, scoffer, false);
                if (!answer.NullOrEmpty() && Speech.Say(scoffer, answer, believer)) said.Add((scoffer, answer, true));
                else scoffer = null;
            }
            var witnesses = new List<Pawn> { believer };
            if (scoffer != null) witnesses.Add(scoffer);
            witnesses.AddRange(people.Where(x => !witnesses.Contains(x) && x.Position.DistanceTo(believer.Position) <= 12f).Take(3));
            GodLore.Saw(witnesses, taken, label, said, "SawHand");
            int home = Homes.KeyOf(map);
            lore.lastHand[god.Key] = now;
            lore.lastHandAt[home] = now;
            lore.hands.Add(new LoreHand { god = god.id, tick = now, home = home, what = label });
            while (lore.hands.Count > 8) lore.hands.RemoveAt(0);
            Gods.Remember(new GodAct
            {
                god = god.id, home = home, tick = now, kind = kind == HandKind.Gift ? GodActKind.Blessing : kind == HandKind.Wrath ? GodActKind.Wrath : GodActKind.Omen,
                text = "CC_GodHand_Act".Translate(label.Named("EVENT")).Resolve(), witnesses = witnesses.Select(x => x.LabelShort).ToList(), booked = true
            });
            if (with != null)
                Gods.Remember(new GodAct
                {
                    god = with.id, other = god.id, home = home, tick = now, kind = GodActKind.Omen, text = "CC_GodHand_Act".Translate(label.Named("EVENT")).Resolve(),
                    witnesses = witnesses.Select(x => x.LabelShort).ToList(), booked = true
                });
            return true;
        }

        public const int UnnamedLines = 6, UnnamedScoffs = 4;

        private static bool Able(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake()
                                            && p.RaceProps?.Humanlike == true && !p.DevelopmentalStage.Baby() && People.HasInnerLife(p);

        /// <summary>A line for the moment: one of the god's (tagged for it first), else one of the keyed ones for something unnamed, filled with the speaker's name for it.</summary>
        private static string Line(List<string> lines, HandKind kind, bool near, string unnamedKey, int unnamedCount, God taken, Pawn speaker, bool meeting)
        {
            string raw;
            var fit = taken != null ? GodHandsMath.Fitting(lines, kind, near, meeting) : new List<string>();
            if (fit.Count > 0) raw = fit.RandomElement();
            else raw = (unnamedKey + Rand.Range(0, unnamedCount)).Translate();
            if (raw.NullOrEmpty()) return null;
            string name = taken != null ? GodLore.NameOf(taken, speaker) : "";
            return raw.Formatted(name.Named(GodsMath.God), (taken?.epithet ?? "").Named(GodsMath.Epithet), Gods.GodOf(taken).Named(GodsMath.Of)).Resolve().CapitalizeFirst();
        }
    }

    /// <summary>
    /// The gods' hands: after one of the game's events has happened, it is noted for the hour's look (Gods/GodHands.cs).
    /// The event and its letter are untouched. Off with the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class Patch_GodHands_TryExecute
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                GodHands.Happened(__instance, parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: noting an event", e);
            }
        }
    }
}
