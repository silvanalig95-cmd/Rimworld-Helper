using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The book read at the fire (OwnHappening.FaithBookRead, D10): on a quiet evening, with new entries in the colony's book
    /// of the gods since it was last read, the keeper takes it to the fire (or a table) and reads the newest chapter aloud, a
    /// verse at a time over their head, to whoever sits; a spacer keeper reads from a slate. Those listening hold the god a
    /// little more (PawnGods.Told). One whose own words are read may say it wasn't like that, and the book sets their telling
    /// down beside it (GodLore.Disputed); a scoffer gets up and walks off. The keeper may say how the god seems to them toward
    /// the colony, in the book's words only and maybe wrong (GodTemper).
    /// </summary>
    public class FaithBookRead : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithBookRead;

        /// <summary>The keeper free at this home, the newest chapter's god and name, the fire, those to sit and listen, and how many entries are new since the last reading.</summary>
        public static (Pawn keeper, God g, string page, Building fire, List<Pawn> listeners) Reading(OwnHappeningsBook b, Map map, out int fresh)
        {
            fresh = 0;
            var lore = GodLore.Instance;
            var keeper = GodLore.Keeper(out _);
            if (lore == null || keeper == null || keeper.Map != map || !OwnBetween.FreeNow(keeper) || OwnBetween.Doubts(keeper)) return (null, null, null, null, null);
            int last = b.Last(OwnHappening.FaithBookRead);
            var newest = lore.entries.Where(e => e != null && e.tick > last && !e.page.NullOrEmpty()).ToList();
            fresh = newest.Count;
            var e1 = newest.LastOrDefault(e => Gods.ById(lore.Named(e.page)?.god ?? -1) != null);
            var g = e1 == null ? null : Gods.ById(lore.Named(e1.page)?.god ?? -1);
            var fire = OwnRevel.Fire(map, keeper.Position);
            if (g == null || fire == null) return (null, null, null, null, null);
            var listeners = OwnBetween.People(map).Where(p => p != keeper && OwnBetween.FreeNow(p) && p.Position.InHorDistOf(fire.Position, 40f))
                .OrderBy(p => p.Position.DistanceToSquared(fire.Position)).Take(OwnHappeningsMath.ReadListeners).ToList();
            return (keeper, g, e1.page, fire, listeners);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.ReadFrom || hour >= OwnHappeningsMath.ReadTo) return;
            var (keeper, g, page, fire, listeners) = Reading(b, map, out int fresh);
            if (keeper == null) return;
            var why = OwnHappeningsMath.ReadTrigger(fresh, listeners.Count);
            if (!OwnHappenings.Calls(b, H, why, g, map, now)) return;
            Begin(b, g, map, keeper, page, fire, listeners);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn keeper, string page, Building fire, List<Pawn> listeners)
        {
            var s = OwnBetween.Begin(b, OwnHappening.FaithBookRead, g, map, fire.Position, keeper, listeners.ElementAtOrDefault(0), 3f);
            s.c = listeners.ElementAtOrDefault(1);
            s.thing = fire;
            s.word = page;
            s.stage = 1;
            OwnBetween.Send(keeper, map, fire.Position, 2f, 2);
            foreach (var p in listeners) OwnBetween.Send(p, map, fire.Position, 2f, 3);
        }

        private static IEnumerable<Pawn> Listeners(BetweenScene s) => new[] { s.b, s.c }.Where(p => p != null && OwnBetween.Still(p, s.map) && p.Position.InHorDistOf(s.at, 8f));

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var keeper = s.a;
            if (!OwnBetween.Still(keeper, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                if (OwnBetween.There(keeper, s.at, 3.5f))
                {
                    // A verse at a time, over the keeper's head, in the old voice.
                    if (s.num == 0) OwnHappenings.Stand(keeper, s.at, 2400);
                    // apart: read aloud to the fire, to whoever sits there.
                    OwnBetween.Say(keeper, H, "read", g);
                    s.num++;
                    if (s.num >= OwnHappeningsMath.ReadVerses) s.stage = 2;
                    OwnBetween.Wait(s, 500);
                    return;
                }
                if (keeper.CurJobDef != JobDefOf.Goto) OwnBetween.Send(keeper, map, s.at, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            var heard = Listeners(s).ToList();
            foreach (var p in heard) PawnGods.Told(p, keeper, g);
            var lore = GodLore.Instance;
            var read = lore?.entries.Where(e => e != null && e.page == s.word).Reverse().Take(6).ToList() ?? new List<LoreEntry>();
            // One whose own words were read says it wasn't like that; the keeper writes their telling beside it.
            var own = heard.FirstOrDefault(p => read.Any(e => e.who != null && e.who.Contains(p.LabelShort)) && Together.Can(p, keeper));
            var scoffer = heard.FirstOrDefault(p => p != own && OwnBetween.Doubts(p));
            if (own != null)
            {
                string line = OwnBetween.Say(own, H, "notso", g, keeper);
                OwnBetween.Say(keeper, H, "write", g, own);
                OwnBetween.Doubted(own, g, line);
            }
            if (scoffer != null)
            {
                OwnBetween.Say(scoffer, H, "leave", g);
                var away = CellFinder.RandomClosewalkCellNear(OwnHappenings.Middle(map), map, 12);
                if (away.IsValid) OwnHappenings.GoTo(scoffer, away, GenDate.TicksPerHour);
            }
            // How the god seems toward the colony as the keeper reads it, in the book's words, when it is said in one sentence.
            string temper = GodTemper.Line(g, s.word);
            if (!temper.NullOrEmpty() && temper.Length <= 90 && GodWorld.FirstSentence(temper) == temper) Speech.Say(keeper, temper);
            var holder = heard.FirstOrDefault(p => p != scoffer && OwnBetween.Holds(p, g) && Together.Can(p, keeper));
            if (holder != null) OwnBetween.End(H, BetweenOutcome.Closer, holder, keeper);
            string place = s.thing?.def.label ?? "CC_GodB_FaithBookRead_Fire".Translate().Resolve();
            OwnBetween.Card(H, g, map, s.at, false, OwnHappenings.Age >= 3 ? "TextSlate" : null, keeper.Named("A"), place.Named("PLACE"), heard.Count.Named("COUNT"));
            OwnBetween.Book(H, g, map, s.at, GodActKind.Act, keeper);
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (keeper, g, page, fire, listeners) = Reading(b, map, out _);
            if (keeper == null) return "the keeper of the colony's book (free, not a scoffer), an entry under a god's name, and a fire or a table are needed.";
            Begin(b, g, map, keeper, page, fire, listeners);
            return keeper.LabelShort + " reads the newest of the book at the " + fire.def.label + ".";
        }
    }

    /// <summary>
    /// A sign at the wedding (OwnHappening.FaithWeddingSign, D11): during a wedding (the game's own ceremony), something the
    /// guests take for a sign: rain begins on the couple out of doors, a light near them goes out, or the god's own small
    /// sign where they stand. Two guests say what they take it for, one a blessing and one a warning, each holding their own
    /// god the harder; the couple write it down. The book keeps it as the first guest said it.
    /// </summary>
    public class FaithWeddingSign : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithWeddingSign;

        /// <summary>A wedding under way at this home not yet answered, two of its guests (the first holding a god), and that god.</summary>
        public static (LordJob_Joinable_MarriageCeremony job, Lord lord, Pawn first, Pawn second, God g) Wedding(OwnHappeningsBook b, Map map)
        {
            foreach (var l in map.lordManager.lords)
            {
                if (!(l?.LordJob is LordJob_Joinable_MarriageCeremony j) || j.firstPawn == null || j.secondPawn == null) continue;
                if (b.between.answered.Contains("wedding|" + l.loadID)) continue;
                var guests = l.ownedPawns.Where(p => p != j.firstPawn && p != j.secondPawn && OwnBetween.Able(p) && OwnBetween.Grown(p)).ToList();
                var first = guests.FirstOrDefault(p => OwnBetween.HeldBy(p) != null && OwnHappenings.Acts(OwnBetween.HeldBy(p), map));
                var second = first == null ? null : guests.Where(p => p != first).OrderBy(p => p.Position.DistanceToSquared(first.Position)).FirstOrDefault();
                if (second != null) return (j, l, first, second, OwnBetween.HeldBy(first));
            }
            return (null, null, null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            var (job, lord, first, second, g) = Wedding(b, map);
            if (job == null) return;
            b.between.answered.Add("wedding|" + lord.loadID);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.WeddingChance)) return;
            Begin(b, g, map, job, first, second);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, LordJob_Joinable_MarriageCeremony job, Pawn first, Pawn second)
        {
            var c1 = job.firstPawn;
            var c2 = job.secondPawn;
            var spot = new IntVec3((c1.Position.x + c2.Position.x) / 2, 0, (c1.Position.z + c2.Position.z) / 2);
            if (!spot.InBounds(map)) spot = c1.Position;
            var rain = GodHealth.Rainy(map);
            var light = FaithBetween.LightNear(map, spot, 8f);
            var fuel = light?.TryGetComp<CompRefuelable>();
            if (fuel == null || !fuel.HasFuel) light = null;
            var signs = OwnHappeningsMath.WeddingSigns(!spot.Roofed(map), OwnHappenings.Raining(map), rain != null && (map.mapTemperature?.OutdoorTemp ?? 0f) > 2f, light != null);
            var sign = signs.RandomElement();
            try
            {
                if (sign == WeddingSign.Rain) map.weatherManager.TransitionTo(rain);
                else if (sign == WeddingSign.LightOut) fuel.ConsumeFuel(fuel.Fuel);
                else OwnBetween.Show(g, map, spot);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods between them: a sign at the wedding", e);
                return;
            }
            var s = OwnBetween.Begin(b, OwnHappening.FaithWeddingSign, g, map, spot, first, second, 2f);
            s.c = c1;
            s.word = sign.ToString();
            s.stage = 1;
            string what = sign == WeddingSign.LightOut ? light.def.label : "";
            OwnBetween.Card(OwnHappening.FaithWeddingSign, g, map, spot, false, sign == WeddingSign.Rain ? null : sign == WeddingSign.LightOut ? "TextLight" : "TextSign", c1.Named("A"), c2.Named("B"),
                what.Named("WHAT"));
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var first = s.a;
            var second = s.b;
            if (!OwnBetween.Still(first, map) || !OwnBetween.Still(second, map))
            {
                s.stage = -1;
                return;
            }
            if (!Together.Can(first, second))
            {
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            var sign = Enum.TryParse(s.word, out WeddingSign w) ? w : WeddingSign.GodSign;
            bool firstBlesses = OwnHappeningsMath.FirstBlesses(sign, Rand.Value);
            var bless = firstBlesses ? first : second;
            var warn = firstBlesses ? second : first;
            if (sign == WeddingSign.Rain)
            {
                OwnBetween.Say(bless, H, "blessrain", g, warn);
                OwnBetween.Say(warn, H, "warnrain", g, bless);
            }
            else if (sign == WeddingSign.LightOut)
            {
                OwnBetween.Say(bless, H, "blesslight", g, warn);
                OwnBetween.Say(warn, H, "warnlight", g, bless);
            }
            else
            {
                OwnBetween.Say(bless, H, "blesssign", g, warn);
                OwnBetween.Say(warn, H, "warnsign", g, bless);
            }
            // Each holds their own god the harder for it.
            foreach (var p in new[] { bless, warn })
            {
                var own = OwnBetween.HeldBy(p) ?? g;
                PawnGods.Move(p, own, BeliefEvent.SawOmen);
            }
            // The couple write it down.
            var c1 = s.c;
            var c2 = c1?.relations?.DirectRelations.FirstOrDefault(r => r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Fiance)?.otherPawn;
            foreach (var c in new[] { c1, c2 })
                if (c != null && !c.Dead) OwnHappenings.Journal(c, "CC_GodB_FaithWeddingSign_Journal", bless.LabelShort.Named("BLESS"), warn.LabelShort.Named("WARN"));
            OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, first, (c1?.LabelShort ?? "?").Named("OTHER"));
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (job, lord, first, second, g) = Wedding(b, map);
            if (job == null) return "a wedding under way at this home, with two grown guests (one holding a god), is needed.";
            b.between.answered.Add("wedding|" + lord.loadID);
            Begin(b, g, map, job, first, second);
            return "a sign at the wedding; " + first.LabelShort + " and " + second.LabelShort + " say what they take it for.";
        }
    }

    /// <summary>
    /// A word over the raider (OwnHappening.FaithRaiderWord, D12): after a raid by a people who hold gods (C14's raiders cry
    /// them), one of the colony who holds one of that people's gods walks to a fallen raider, kneels, says the god's words
    /// over them and asks that they be buried. One who lost a friend in the fight, else the marshal, walks over and says
    /// what they think: a nod and they stand there together (closer), or a quarrel. The council's question of the enemy dead
    /// stays its own.
    /// </summary>
    public class FaithRaiderWord : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithRaiderWord;

        /// <summary>A raider fallen lately at this home whose people hold a god one of the colony holds, the one who holds it, that god, and its key.</summary>
        public static (Corpse body, Pawn speaker, God g, string key) Fallen(OwnHappeningsBook b, Map map)
        {
            var people = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p)).ToList();
            if (people.Count == 0) return (null, null, null, null);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!(t is Corpse c) || !c.Spawned || c.Age > OwnHappeningsMath.RaiderDays * GenDate.TicksPerDay) continue;
                var dead = c.InnerPawn;
                var f = dead?.Faction;
                if (dead?.RaceProps?.Humanlike != true || f == null || f.IsPlayer || !f.HostileTo(Faction.OfPlayer)) continue;
                string key = "raider|" + c.thingIDNumber;
                if (b.between.answered.Contains(key)) continue;
                var gods = Gods.Of(f);
                foreach (var p in people)
                {
                    var g = OwnBetween.HeldBy(p);
                    if (g != null && gods.Contains(g) && OwnBetween.Holds(p, g) && p.CanReach(c, PathEndMode.Touch, Danger.Some)) return (c, p, g, key);
                }
            }
            return (null, null, null, null);
        }

        /// <summary>One of the colony fell in the fight lately whom this one was a friend of.</summary>
        private static bool LostFriend(Pawn p, Map map)
        {
            var dead = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Select(c => c.InnerPawn)
                .Concat(map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Select(g => g.Corpse?.InnerPawn));
            return dead.Any(d => d != null && d.Faction == Faction.OfPlayer && d.RaceProps?.Humanlike == true && d.Corpse != null
                                 && d.Corpse.Age <= OwnHappeningsMath.RaiderDays * GenDate.TicksPerDay && OwnBetween.Opinion(p, d) >= OwnHappeningsMath.LostFriendFrom);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 6 || hour >= 20) return;
            var (body, speaker, g, key) = Fallen(b, map);
            if (body == null || !OwnHappenings.Acts(g, map)) return;
            b.between.answered.Add(key);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.RaiderChance)) return;
            Begin(b, g, map, body, speaker);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Corpse body, Pawn speaker)
        {
            var s = OwnBetween.Begin(b, OwnHappening.FaithRaiderWord, g, map, body.Position, speaker, null, 3f);
            s.thing = body;
            s.word = body.InnerPawn?.LabelShort ?? "";
            s.stage = 1;
            OwnBetween.Send(speaker, map, body.Position, 2f, 1);
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var speaker = s.a;
            if (!OwnBetween.Still(speaker, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                if (s.thing == null || !s.thing.Spawned)
                {
                    s.stage = -1;
                    return;
                }
                if (speaker.CurJobDef != JobDefOf.Goto && speaker.Position.InHorDistOf(s.thing.Position, 2.5f))
                {
                    OwnHappenings.Stand(speaker, s.thing.Position, 1500);
                    // apart: the words said over the body, to nobody in particular.
                    OwnBetween.Say(speaker, H, "words", g);
                    OwnBetween.Say(speaker, H, "bury", g);
                    var people = (s.thing as Corpse)?.InnerPawn?.Faction?.Name ?? "";
                    OwnBetween.Card(H, g, map, s.thing.Position, false, null, speaker.Named("A"), s.word.Named("DEAD"), people.Named("PEOPLE"));
                    OwnBetween.Book(H, g, map, s.thing.Position, GodActKind.Act, speaker, s.word.Named("OTHER"));
                    // One who lost a friend in the fight, else the marshal, walks over.
                    var others = OwnBetween.People(map).Where(p => p != speaker && OwnBetween.Grown(p) && OwnBetween.FreeNow(p)).ToList();
                    var answer = others.FirstOrDefault(p => LostFriend(p, map));
                    s.num = answer != null ? 1 : 0;
                    if (answer == null)
                    {
                        var marshal = Interplay.Officer("Council_Office_Marshal", map);
                        answer = marshal != null && others.Contains(marshal) ? marshal : null;
                    }
                    if (answer == null)
                    {
                        s.stage = -1;
                        return;
                    }
                    s.b = answer;
                    OwnBetween.Send(answer, map, speaker.Position, 2f, 2);
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 2;
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (speaker.CurJobDef != JobDefOf.Goto) OwnBetween.Send(speaker, map, s.thing.Position, 1f, 1);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            var other = s.b;
            if (!OwnBetween.Still(other, map))
            {
                s.stage = -1;
                return;
            }
            if (other.Position.InHorDistOf(speaker.Position, 4f) && Together.Can(other, speaker))
            {
                var o = OwnHappeningsMath.OverRaider(OwnBetween.Opinion(other, speaker), s.num == 1);
                OwnBetween.Face(other, speaker, 900);
                OwnBetween.Say(other, H, o == BetweenOutcome.Closer ? "nod" : "angry", g, speaker);
                OwnBetween.End(H, o, speaker, other, g, OwnBetween.HeldBy(other), s.word.Named("DEAD"));
                s.stage = -1;
                return;
            }
            if (other.CurJobDef != JobDefOf.Goto) OwnBetween.Send(other, map, speaker.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 200);
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (body, speaker, g, _) = Fallen(b, map);
            if (body == null) return "a raider fallen lately here whose people hold a god one of the colony holds is needed.";
            Begin(b, g, map, body, speaker);
            return speaker.LabelShort + " goes to say a word over " + body.InnerPawn?.LabelShort + ".";
        }
    }

    /// <summary>
    /// Suspected of a bargain (OwnHappening.FaithBargain, D14): one of the colony has had a run of luck others notice (the
    /// beasts shy from them, the Hunger's bargain paid them, the great cat's chosen in her lucky season), and someone who
    /// thinks little of them says to their face that they made a bargain with the dark. Most deny it; one who did may own it.
    /// A friend stands up for them (a quarrel with the accuser), and believers of the gods that turn from such bargains keep
    /// away from them a while. Owned, the chaplain sits a vigil with them at a god's place. The accusation can fall on one
    /// who made no bargain at all, and nothing says which. The dark is never named: not on the card, not in their words.
    /// </summary>
    public class FaithBargain : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithBargain;

        /// <summary>The run of luck others could notice on someone.</summary>
        public static LuckOf Luck(OwnHappeningsBook b, Pawn p)
        {
            if (p == null) return LuckOf.None;
            var world = GodWorld.Book;
            if (world != null && world.doings.Any(d => d != null && d.kind == "take" && d.pawn == p)) return LuckOf.Bargained;
            if (b.undertow != null && b.undertow.shunned == p && b.undertow.shunUntil > OwnBetween.Now) return LuckOf.Shunned;
            if (SacredAnimal.Lucky(p)) return LuckOf.Lucky;
            return LuckOf.None;
        }

        /// <summary>One with a run of luck, one who thinks little of them and holds a god to say it, the luck, and the accuser's god.</summary>
        public static (Pawn accused, Pawn accuser, LuckOf luck, God g) Suspect(OwnHappeningsBook b, Map map)
        {
            var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
            foreach (var l in people)
            {
                var luck = Luck(b, l);
                if (luck == LuckOf.None) continue;
                var a = people.Where(p => p != l && OwnBetween.FreeNow(p) && OwnBetween.HeldBy(p) != null && !OwnBetween.Doubts(p) && OwnBetween.Opinion(p, l) < OwnHappeningsMath.AccuserBelow
                                         && p.Position.InHorDistOf(l.Position, 30f)).OrderBy(p => OwnBetween.Opinion(p, l)).FirstOrDefault();
                if (a != null) return (l, a, luck, OwnBetween.HeldBy(a));
            }
            return (null, null, LuckOf.None, null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 9 || hour >= 20) return;
            var (l, a, luck, g) = Suspect(b, map);
            if (l == null || !OwnHappenings.Calls(b, H, OwnHappeningsMath.BargainTrigger(luck), g, map, now)) return;
            Begin(b, g, map, l, a, luck);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn l, Pawn a, LuckOf luck)
        {
            var s = OwnBetween.Begin(b, OwnHappening.FaithBargain, g, map, l.Position, a, l, 3f);
            s.num = (int)luck;
            s.stage = 1;
            OwnBetween.Send(a, map, l.Position, 1f, 2);
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var accuser = s.a;
            var accused = s.b;
            if (!OwnBetween.Still(accuser, map) || !OwnBetween.Still(accused, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                if (accuser.Position.InHorDistOf(accused.Position, 4f) && Together.Can(accuser, accused))
                {
                    OwnBetween.Face(accuser, accused, 900);
                    OwnBetween.Say(accuser, H, "accuse", g, accused);
                    bool owns = OwnHappeningsMath.Owns((LuckOf)s.num, Rand.Value);
                    OwnBetween.Say(accused, H, owns ? "own" : "deny", g, accuser);
                    // The card never names the dark, nor any god (no title of a god's name).
                    OwnBetween.Card(H, null, map, accused.Position, false, owns ? "TextOwned" : null, accuser.Named("A"), accused.Named("B"));
                    var hunger = GodVoid.Hunger;
                    OwnBetween.Book(H, hunger ?? g, map, accused.Position, GodActKind.Omen, accuser, accused.LabelShort.Named("OTHER"));
                    // A friend stands up for them.
                    var friend = OwnBetween.People(map).FirstOrDefault(p => p != accuser && p != accused && OwnBetween.Grown(p) && OwnBetween.Opinion(p, accused) >= OwnHappeningsMath.DefenderFrom
                                                                           && Together.Can(p, accuser));
                    if (friend != null)
                    {
                        OwnBetween.Say(friend, H, "defend", OwnBetween.HeldBy(friend), accuser);
                        OwnBetween.End(H, BetweenOutcome.Quarrel, friend, accuser, OwnBetween.HeldBy(friend), g);
                    }
                    else OwnBetween.End(H, BetweenOutcome.Quarrel, accused, accuser);
                    // Believers of the gods that turn from such bargains, there to hear it, keep away from them a while.
                    var shuns = hunger == null ? new List<God>() : GodWays.Shuns(hunger);
                    foreach (var p in OwnBetween.People(map).Where(p => p != accuser && p != accused && p != friend && OwnBetween.Grown(p) && shuns.Any(x => OwnBetween.Holds(p, x))
                                                                       && p.Position.InHorDistOf(accused.Position, OwnHappeningsMath.ShunWithin) && Together.Can(p, accused)).Take(2))
                        OwnBetween.End(H, BetweenOutcome.Wary, p, accused);
                    // Owned: the chaplain sits a vigil with them at a god's place.
                    var priest = owns ? Worship.Priest(map) : null;
                    if (priest == null || priest == accused || priest == accuser || !OwnBetween.FreeNow(priest))
                    {
                        s.stage = -1;
                        return;
                    }
                    s.c = priest;
                    OwnBetween.Send(priest, map, accused.Position, 2f, 2);
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 3;
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (accuser.CurJobDef != JobDefOf.Goto) OwnBetween.Send(accuser, map, accused.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 150);
                return;
            }
            var chaplain = s.c;
            if (!OwnBetween.Still(chaplain, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 2)
            {
                if (chaplain.Position.InHorDistOf(accused.Position, 4f) && Together.Can(chaplain, accused))
                {
                    OwnBetween.Face(chaplain, accused, 300);
                    OwnBetween.Say(chaplain, H, "vigil", OwnBetween.HeldBy(chaplain), accused);
                    var at = FaithBetween.PlaceOf(chaplain, OwnBetween.HeldBy(chaplain), out _);
                    if (at.IsValid && OwnBetween.Send(chaplain, map, at, 2f, 2) && OwnBetween.Send(accused, map, at, 2f, 2))
                    {
                        s.at = at;
                        s.stage = 3;
                    }
                    else
                    {
                        OwnBetween.Face(chaplain, accused, 2500);
                        OwnBetween.End(H, BetweenOutcome.Kind, accused, chaplain);
                        s.stage = -1;
                    }
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (chaplain.CurJobDef != JobDefOf.Goto) OwnBetween.Send(chaplain, map, accused.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            // The vigil at the place, the two side by side.
            if (OwnBetween.There(chaplain, s.at, 4f) && OwnBetween.There(accused, s.at, 4f) && Together.Can(chaplain, accused))
            {
                OwnHappenings.Stand(chaplain, s.at, 2500);
                OwnHappenings.Stand(accused, s.at, 2500);
                OwnBetween.End(H, BetweenOutcome.Kind, accused, chaplain);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 200);
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (l, a, luck, g) = Suspect(b, map);
            if (l == null)
            {
                var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
                a = people.FirstOrDefault(p => OwnBetween.HeldBy(p) != null && !OwnBetween.Doubts(p));
                l = people.FirstOrDefault(p => p != a);
                g = OwnBetween.HeldBy(a);
                luck = Luck(b, l);
            }
            if (l == null || a == null || g == null) return "two grown of the colony, the accuser holding a god, are needed.";
            Begin(b, g, map, l, a, luck);
            return a.LabelShort + " goes to accuse " + l.LabelShort + " of a bargain (" + luck + ").";
        }
    }

    /// <summary>
    /// Lightning on the shrine (OwnHappening.FaithLightning, D15): in a thunderstorm, at most once a year at a home, when the
    /// colony has kept one god's dedicated place busy and someone at home holds that god's rival, lightning strikes the
    /// place, or the tree beside it where the place is under a roof: the game's own strike, scorch and fire, never aimed at
    /// a person (nobody near the cell) and through the shared harm budget (HarmKinds.GodLightning). The card says what was
    /// struck. In the morning one who holds the rival says whose they take it for (a quarrel with the place's believer, who
    /// says it was the storm, or a test); two of the place's own rebuild it together where it burned down (its blueprint
    /// laid where it stood, closer).
    /// </summary>
    public class FaithLightning : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithLightning;

        /// <summary>Nobody near a cell.</summary>
        private static bool Clear(Map map, IntVec3 c) => c.InBounds(map) && !map.mapPawns.AllPawnsSpawned.Any(x => x.Position.InHorDistOf(c, GodWorldMath.LightningClear));

        /// <summary>A busy dedicated place at this home whose god's rival someone holds, its god, the cell to strike (the place out under the sky, else a tree beside it), and what is there.</summary>
        public static (Thing place, God g, IntVec3 cell, Thing struck) Busy(Map map, bool storm, int now)
        {
            if (!storm) return (null, null, IntVec3.Invalid, null);
            var people = OwnBetween.People(map);
            foreach (var r in Shrines.Of(map, null).InRandomOrder())
            {
                var g = Gods.ById(r.god);
                int tended = r.tended >= 0 ? r.tended : r.since;
                bool busy = tended >= 0 && now - tended <= OwnHappeningsMath.BusyDays * GenDate.TicksPerDay;
                var why = OwnHappeningsMath.LightningTrigger(true, busy, g != null && people.Any(p => FaithBetween.HoldsRival(p, g)));
                if (why == GodPaceMath.Trigger.None) continue;
                var t = r.thing;
                Thing struck = null;
                if (!t.Position.Roofed(map)) struck = t;
                else struck = GenRadial.RadialDistinctThingsAround(t.Position, map, 4f, true).OfType<Plant>().FirstOrDefault(p => p.def.plant?.IsTree == true && !p.Position.Roofed(map));
                if (struck == null || !Clear(map, struck.Position)) continue;
                return (t, g, struck.Position, struck);
            }
            return (null, null, IntVec3.Invalid, null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            var (place, g, cell, struck) = Busy(map, StormSense.Thunder(map), now);
            if (place == null || !OwnHappenings.Acts(g, map)) return;
            // The strike's own chance through the shared harm budget, then the gods' pace for it.
            if (OwnHappenings.forced != H && !Harm.Roll(HarmKinds.GodLightning, OwnHappeningsMath.LightningChance)) return;
            if (!OwnBetween.Follows(b, H, g, map, 1f)) return;
            Strike(b, g, map, place, cell, struck);
        }

        private static void Strike(OwnHappeningsBook b, God g, Map map, Thing place, IntVec3 cell, Thing struck)
        {
            try
            {
                map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, cell));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods between them: lightning on the place", e);
                return;
            }
            var s = OwnBetween.Begin(b, OwnHappening.FaithLightning, g, map, place.Position, null, null, 30f);
            s.thing = place;
            s.word = struck.def.label;
            s.stage = 1;
            bool tree = struck != place;
            OwnBetween.Card(OwnHappening.FaithLightning, g, map, cell, true, tree ? "TextTree" : null, struck.def.label.Named("WHAT"), place.def.label.Named("PLACE"));
            int wait = OwnBetween.TicksToHour(map, 7);
            s.until = OwnBetween.Now + wait + GenDate.TicksPerHour * 8;
            OwnBetween.Wait(s, wait);
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            if (s.stage == 1)
            {
                // The morning: one who holds the rival, and two of the place's own, walk out to it.
                var people = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p)).ToList();
                var own = people.Where(p => OwnBetween.Holds(p, g)).OrderByDescending(p => PawnGods.HoldOf(p, g)).Take(2).ToList();
                if (own.Count == 0)
                {
                    if (now > s.until) s.stage = -1;
                    OwnBetween.Wait(s, 1000);
                    return;
                }
                s.a = own[0];
                s.b = own.ElementAtOrDefault(1);
                s.c = people.Where(p => FaithBetween.HoldsRival(p, g)).OrderBy(p => p.Position.DistanceToSquared(s.at)).FirstOrDefault();
                foreach (var p in new[] { s.a, s.b, s.c }.Where(p => p != null)) OwnBetween.Send(p, map, s.at, 2f, 3);
                s.num = 0;
                s.stage = 2;
                s.until = now + GenDate.TicksPerHour * 4;
                OwnBetween.Wait(s, 250);
                return;
            }
            var first = s.a;
            if (!OwnBetween.Still(first, map))
            {
                s.stage = -1;
                return;
            }
            if (!first.Position.InHorDistOf(s.at, 5f))
            {
                if (first.CurJobDef != JobDefOf.Goto) OwnBetween.Send(first, map, s.at, 1f, 3);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            var rival = s.c;
            var second = s.b;
            if ((s.num & 1) == 0)
            {
                // The place's believer says what they take it for.
                s.num |= 1;
                OwnHappenings.Stand(first, s.at, 1200);
                OwnBetween.Say(first, H, Rand.Bool ? "storm" : "test", g);
            }
            if ((s.num & 2) == 0 && rival != null && OwnBetween.Still(rival, map) && rival.Position.InHorDistOf(first.Position, 5f) && Together.Can(rival, first))
            {
                s.num |= 2;
                OwnBetween.Say(rival, H, "rival", OwnBetween.HeldBy(rival), first);
                OwnBetween.End(H, BetweenOutcome.Quarrel, first, rival, g, OwnBetween.HeldBy(rival), s.word.Named("WHAT"));
            }
            if ((s.num & 4) == 0 && second != null && OwnBetween.Still(second, map) && second.Position.InHorDistOf(first.Position, 5f) && Together.Can(second, first))
            {
                s.num |= 4;
                var o = OwnHappeningsMath.Rebuild(OwnBetween.StanceOf(first, g), OwnBetween.StanceOf(second, g));
                OwnBetween.Say(second, H, "rebuild", g, first);
                OwnBetween.End(H, o, first, second, g, g, s.word.Named("WHAT"));
                if (o == BetweenOutcome.Closer) Rebuild(map, s.thing, s.at);
            }
            bool done = (rival == null || (s.num & 2) != 0 || !OwnBetween.Still(rival, map)) && (second == null || (s.num & 4) != 0 || !OwnBetween.Still(second, map));
            if (done || now > s.until)
            {
                OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, first, s.word.Named("WHAT"));
                s.stage = -1;
                return;
            }
            foreach (var p in new[] { rival, second }.Where(p => p != null && OwnBetween.FreeNow(p) && p.CurJobDef != JobDefOf.Goto && !p.Position.InHorDistOf(s.at, 5f)))
                OwnBetween.Send(p, map, s.at, 1f, 3);
            OwnBetween.Wait(s, 200);
        }

        /// <summary>The place burned down: its blueprint laid where it stood, as it was (the colony's people build it again).</summary>
        private static void Rebuild(Map map, Thing place, IntVec3 at)
        {
            if (place == null || !place.Destroyed || place.def?.building == null || place.def.blueprintDef == null) return;
            try
            {
                if (GenConstruct.CanPlaceBlueprintAt(place.def, at, place.Rotation, map, false, null, null, place.Stuff).Accepted)
                    GenConstruct.PlaceBlueprintForBuild(place.def, at, map, place.Rotation, Faction.OfPlayer, place.Stuff);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods between them: the place laid out again", e);
            }
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (place, g, cell, struck) = Busy(map, true, OwnBetween.Now);
            if (place == null)
            {
                var r = Shrines.Of(map, null).FirstOrDefault(x => !x.thing.Position.Roofed(map) && Clear(map, x.thing.Position));
                if (r == null) return "a dedicated place out under the sky (or with a tree beside it), nobody near it, is needed.";
                place = r.thing;
                g = Gods.ById(r.god);
                cell = place.Position;
                struck = place;
            }
            if (g == null) return "the place's god isn't in this pantheon.";
            Strike(b, g, map, place, cell, struck);
            return "lightning strikes the " + struck.def.label + "; its people look at it in the morning.";
        }
    }
}
