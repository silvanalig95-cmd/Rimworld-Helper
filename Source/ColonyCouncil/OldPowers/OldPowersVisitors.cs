using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The old powers' visitors (OldPowers.cs): those who walk in from the road on their own business, each on the game's
    /// own visit (Arrivals.Visit) to a place in the colony, and walk on when it is done.
    /// <list type="bullet">
    /// <item>The seer of the season: in a season's last two days a seer (of A RimWorld of Magic's arcane fold where it is in
    /// the world, a chronomancer by the mod; else of a people of seers, or any at peace) comes to the colony's middle, casts
    /// in the age's way, someone sits to hear it, and the seer says what the next season holds, every word true: how warm it
    /// will be, whether the fields will grow, whether the stores will last, and blades where a fight is coming.</item>
    /// <item>The wandering enchanter: in a season's first days (never in winter), an enchanter (A RimWorld of Magic's
    /// Enchanter class where the mod has it) offers to work one of the colony's pieces up a grade for silver; asked, the one
    /// who carries it walks over, the two stand an hour over it, and it comes away one grade better.</item>
    /// <item>The pilgrims passing: three to five pilgrims (of a people of pilgrims, likelier) walk in at spring's or autumn's
    /// start or under a sign in the sky, stop at the colony's graves, its oldest tree or its middle, pray there in turn,
    /// leave a small token of their people's age from the player's whole pool, and walk on.</item>
    /// </list>
    /// </summary>
    public static class OldPowersVisitors
    {
        private static int Now => OldPowers.Now;

        private static readonly Color SeerColor = new Color(0.72f, 0.62f, 1f), Gold = new Color(1f, 0.86f, 0.5f), Holy = new Color(1f, 0.95f, 0.78f);

        // ================= The seer of the season =================

        /// <summary>The seer stays this many hours on the visit.</summary>
        private const float SeerHours = 9f;

        public static GodPaceMath.Trigger SeerCall(Map map)
        {
            var why = OldPowersMath.SeerTrigger(OldPowers.DayOfSeason(map), OldPowersMath.HardAhead(map.mapTemperature.OutdoorTemp, NextMean(map)));
            return why != GodPaceMath.Trigger.None && SeerPeople() != null ? why : GodPaceMath.Trigger.None;
        }

        /// <summary>The mean temperature of the season after this one at the home (°C).</summary>
        internal static float NextMean(Map map)
        {
            var ts = OldPowersMath.NextSeasonTwelfths((int)GenLocalDate.Twelfth(map));
            return ts.Average(t => GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, (Twelfth)t));
        }

        private static FactionDef ArcaneFold => DefDatabase<FactionDef>.GetNamedSilentFail("Seers");

        /// <summary>A people at peace a seer comes from: A RimWorld of Magic's arcane fold much likelier, then a people of seers.</summary>
        private static Faction SeerPeople()
        {
            var fold = ArcaneFold;
            return Arrivals.Friendly(null, f => (fold != null && f.def == fold ? 6f : 1f) * Peoples.Preference(PeoplesMath.Seers, f));
        }

        public static bool Seer(OldPowersBook b, Map map, bool strong)
        {
            var f = SeerPeople();
            var kind = Arrivals.PersonKind(f);
            if (kind == null) return false;
            var spot = OldPowers.OpenNear(map, OwnHappenings.Middle(map), 8);
            if (!spot.IsValid) return false;
            var seer = Arrivals.Make(kind, f);
            if (seer == null) return false;
            // A RimWorld of Magic's chronomancer, who reads time (its class and its piece), where the mod and its settings allow.
            if (PeoplesMagic.Loaded && PeopleGifts.GiveClass(seer, "Chronomancer")) PeoplesMagic.Dress(seer, OldPowers.Age);
            if (Arrivals.Visit(new List<Pawn> { seer }, map, f, spot, SeerHours) == null)
            {
                Arrivals.Discard(seer);
                return false;
            }
            var p = OldPowers.Play(b, OldPower.Seer, map, SeerHours + 1f, 250);
            p.a = seer;
            p.faction = f;
            p.cell = spot;
            p.stage = 1;
            p.strong = strong;
            p.form = (f.def == ArcaneFold ? "fold" : "road") + OldPowers.Age;
            b.Came(OldPower.Seer, Now, p.form, f.loadID.ToString());
            return true;
        }

        internal static void SeerStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var seer = p.a;
            var map = p.map;
            if (seer == null || seer.Dead || !seer.Spawned || seer.Map != map || seer.Downed)
            {
                OldPowers.End(b);
                return;
            }
            switch (p.stage)
            {
                case 1:
                {
                    // Walking in: once at the place (or after a few hours), the seer sits down to cast.
                    if (!seer.Position.InHorDistOf(p.cell, 4f) && now < p.until - GenDate.TicksPerHour * 6)
                    {
                        OldPowers.Next(b, p, 250);
                        return;
                    }
                    p.stage = 2;
                    OwnHappenings.Stand(seer, p.cell, 3000);
                    Cast(map, seer);
                    OldPowers.Say(seer, "seer,cast");
                    var listener = OldPowers.FreeNear(map, seer.Position);
                    if (listener != null)
                    {
                        var at = OwnHappenings.Beside(map, seer.Position, listener);
                        if (at.IsValid) OwnHappenings.GoTo(listener, at, 1500);
                        p.b = listener;
                    }
                    OldPowers.Next(b, p, 300);
                    return;
                }
                case 2:
                {
                    // The listener comes and sits (or the seer reads to whoever is near); the words are worked out once.
                    var l = p.b;
                    bool there = l != null && l.Spawned && l.Position.InHorDistOf(seer.Position, 3f);
                    if (!there && l != null && l.Spawned && !l.Dead && p.count++ < 8)
                    {
                        OldPowers.Next(b, p, 150);
                        return;
                    }
                    if (l != null && l.Spawned) OwnHappenings.Stand(l, seer.Position, 2400);
                    var words = OldPowersMath.SeerWords(NextMean(map), OwnHappenings.FoodDays(map), OldPowers.FightAhead(map, 15f));
                    p.note = string.Join(",", words);
                    p.count = 0;
                    p.stage = 3;
                    OldPowers.Next(b, p, 200);
                    return;
                }
                case 3:
                {
                    // One word at a time, over the seer's head, then the card of all of it.
                    var words = Words(p.note);
                    if (p.count < words.Count)
                    {
                        Cast(map, seer);
                        OldPowers.Say(seer, "seer,word:" + words[p.count].ToString().ToLowerInvariant());
                        p.count++;
                        OldPowers.Next(b, p, 420);
                        return;
                    }
                    TellSeer(map, seer, p.b, words, p.faction);
                    if (p.b != null && p.b.Spawned)
                    {
                        OldPowers.Memory(p.b, "Rimfolk_Thought_OldP_Seer");
                        OldPowers.Say(p.b, "seer,heard");
                    }
                    OldPowers.End(b);
                    return;
                }
            }
        }

        private static List<OldPowersMath.SeerWord> Words(string note) =>
            (note ?? "").Split(',').Select(s => System.Enum.TryParse(s, out OldPowersMath.SeerWord w) ? (OldPowersMath.SeerWord?)w : null).Where(w => w.HasValue).Select(w => w.Value).ToList();

        /// <summary>The seer's casting: A RimWorld of Magic's casting circle and its gong, else the game's psychic ripple.</summary>
        private static void Cast(Map map, Pawn seer)
        {
            OldPowers.Motes(map, seer.DrawPos, SeerColor, 4, 0.6f, "Mote_Casting", "Mote_ArcaneCircle", "PsycastAreaEffect");
            OldPowers.Sound(map, seer.Position, "TM_Gong", "MeditationGainPsyfocus", "PsychicSootheGlobal");
        }

        /// <summary>The card: who came, how they cast by the colony's age, who heard it, and each word with its plain fact.</summary>
        internal static void TellSeer(Map map, Pawn seer, Pawn listener, List<OldPowersMath.SeerWord> words, Faction f)
        {
            float next = NextMean(map);
            var lines = words.Select(w => "- " + OldPowers.T("Seer_Word_" + w, next.ToStringTemperature("F0").Named("TEMP"))).ToList();
            string of = f != null && f.def == ArcaneFold ? OldPowers.T("Seer_OfFold") : f != null ? OldPowers.T("Seer_Of", f.Name.Named("PEOPLE")) : OldPowers.T("Seer_OfNone");
            string text = OldPowers.T("Seer_Text", seer.LabelShort.Named("SEER"), of.Named("OF"), OldPowers.Aged("Seer_Cast").Named("CAST"),
                (listener != null ? OldPowers.T("Seer_Listener", listener.LabelShort.Named("LISTENER")) : OldPowers.T("Seer_NoListener")).Named("WHO"),
                string.Join("\n", lines).Named("WORDS"));
            OldPowers.Card(map, "Seer", text, new LookTargets(seer), LetterDefOf.NeutralEvent);
        }

        // ================= The wandering enchanter =================

        private const float EnchanterHours = 10f;

        public static GodPaceMath.Trigger EnchanterCall(Map map)
        {
            if (Arrivals.Friendly() == null) return GodPaceMath.Trigger.None;
            var (who, piece) = Piece(map);
            int q = Quality(piece);
            bool canPay = piece != null && VarietyUtil.InStores(map, ThingDefOf.Silver) >= OldPowersMath.EnchantPrice(q);
            bool caster = map.mapPawns.FreeColonistsSpawned.Any(c => OldPowersNight.CasterOf(c) is OldPowersMath.Caster.Mage or OldPowersMath.Caster.Psycaster);
            return OldPowersMath.EnchanterTrigger(OldPowers.DayOfSeason(map), OldPowers.Quarter(map) == StoryWaysMath.Winter, caster, who != null, canPay);
        }

        private static int Quality(Thing t) => t?.TryGetComp<CompQuality>() is CompQuality c ? (int)c.Quality : -1;

        /// <summary>
        /// The piece the enchanter would work: the finest weapon carried by one of the colony that can still go up (a grade up
        /// to masterwork), else the finest piece worn; with its bearer.
        /// </summary>
        internal static (Pawn who, Thing piece) Piece(Map map)
        {
            Pawn who = null;
            Thing best = null;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours))
            {
                var w = p.equipment?.Primary;
                if (w != null && OldPowersMath.Enchantable(Quality(w)) && (best == null || w.MarketValue > best.MarketValue || !(best is ThingWithComps bw && bw.def.IsWeapon)))
                {
                    best = w;
                    who = p;
                }
            }
            if (best != null) return (who, best);
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Ours))
                foreach (var a in p.apparel?.WornApparel ?? new List<Apparel>())
                    if (OldPowersMath.Enchantable(Quality(a)) && (best == null || a.MarketValue > best.MarketValue))
                    {
                        best = a;
                        who = p;
                    }
            return (who, best);
        }

        public static bool Enchanter(OldPowersBook b, Map map)
        {
            var (who, piece) = Piece(map);
            if (who == null || piece == null) return false;
            var f = Arrivals.Friendly();
            var kind = Arrivals.PersonKind(f);
            if (kind == null) return false;
            var spot = OldPowers.OpenNear(map, OwnHappenings.Middle(map), 8);
            if (!spot.IsValid) return false;
            var ench = Arrivals.Make(kind, f);
            if (ench == null) return false;
            if (PeoplesMagic.Loaded && PeopleGifts.GiveClass(ench, "Enchanter")) PeoplesMagic.Dress(ench, OldPowers.Age);
            if (Arrivals.Visit(new List<Pawn> { ench }, map, f, spot, EnchanterHours) == null)
            {
                Arrivals.Discard(ench);
                return false;
            }
            var p = OldPowers.Play(b, OldPower.Enchanter, map, EnchanterHours + 1f, 250);
            p.a = ench;
            p.b = who;
            p.thing = piece;
            p.faction = f;
            p.cell = spot;
            p.stage = 1;
            p.value = OldPowersMath.EnchantPrice(Quality(piece));
            p.form = "age" + OldPowers.Age;
            b.Came(OldPower.Enchanter, Now, p.form, f.loadID.ToString());
            return true;
        }

        /// <summary>The enchanter waits for the colony's answer.</summary>
        public static bool EnchanterWaiting => OldPowersBook.Instance?.play is OldPowerPlay p && p.kind == OldPower.Enchanter && p.stage == 2;

        private static OldPowerPlay Waiting => EnchanterWaiting ? OldPowersBook.Instance.play : null;

        private static NamedArgument[] Args(OldPowerPlay p)
        {
            int q = Quality(p.thing);
            string now = q >= 0 ? ((QualityCategory)q).GetLabel() : "";
            string next = q >= 0 && q < 6 ? ((QualityCategory)(q + 1)).GetLabel() : "";
            return new[]
            {
                (p.a?.LabelShort ?? "?").Named("ENCHANTER"), (p.b?.LabelShort ?? "?").Named("BEARER"), (p.thing?.LabelNoCount ?? "?").Named("THING"),
                p.value.ToString().Named("PRICE"), now.Named("QUALITY"), next.Named("NEXT"), OldPowers.Aged("Enchanter_Who").Named("WHO")
            };
        }

        public static string YesLabel()
        {
            var p = Waiting;
            return p == null ? "CC_OldP_Enchanter_No".Translate().Resolve() : OldPowers.T("Enchanter_Yes", Args(p));
        }

        /// <summary>Why the work can't be had now (the silver gone, the piece no longer theirs), or null.</summary>
        public static string WhyNot()
        {
            var p = Waiting;
            if (p == null) return null;
            if (!Still(p)) return OldPowers.T("Enchanter_Gone", Args(p));
            if (VarietyUtil.InStores(p.map, ThingDefOf.Silver) < p.value) return OldPowers.T("Enchanter_NoSilver", Args(p));
            return null;
        }

        /// <summary>The piece is still carried or worn by its bearer, at home, and can still go up.</summary>
        private static bool Still(OldPowerPlay p)
        {
            var who = p.b;
            if (who == null || who.Dead || !who.Spawned || who.Map != p.map || p.thing == null || p.thing.Destroyed) return false;
            bool held = who.equipment?.Primary == p.thing || (who.apparel?.WornApparel?.Contains(p.thing as Apparel) ?? false);
            return held && OldPowersMath.Enchantable(Quality(p.thing));
        }

        internal static void EnchanterStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var ench = p.a;
            var map = p.map;
            if (ench == null || ench.Dead || !ench.Spawned || ench.Map != map || ench.Downed)
            {
                OldPowers.End(b);
                return;
            }
            switch (p.stage)
            {
                case 1:
                {
                    if (!ench.Position.InHorDistOf(p.cell, 4f) && now < p.until - GenDate.TicksPerHour * 7)
                    {
                        OldPowers.Next(b, p, 250);
                        return;
                    }
                    // A question that can wait: when the letters' gap allows it, while the visit lasts.
                    if (!Tell.CanAsk)
                    {
                        OldPowers.Next(b, p, 1250);
                        return;
                    }
                    if (!Still(p))
                    {
                        Leave(b, p, "enchanter,leave");
                        return;
                    }
                    OwnHappenings.Stand(ench, p.cell, 1200);
                    OldPowers.Motes(map, ench.DrawPos, Gold, 5, 0.6f, "Mote_Enchanting", "Mote_GlowingRune", "PsycastAreaEffect");
                    OldPowers.Say(ench, "enchanter,arrive");
                    var args = Args(p);
                    string text = OldPowers.T("Enchanter_Text", args) + "\n\n" + OldPowers.T("Enchanter_Ask", args);
                    var letter = LetterMaker.MakeLetter(OldPowers.T("Enchanter_Label", args), text, DefDatabase<LetterDef>.GetNamedSilentFail("Rimfolk_OldPowersLetter"),
                        new LookTargets(new[] { ench, p.b }.Where(x => x != null && x.Spawned))) as ChoiceLetter_OldPowers;
                    if (letter == null)
                    {
                        Leave(b, p, "enchanter,leave");
                        return;
                    }
                    letter.StartTimeout(Mathf.RoundToInt(OldPowersMath.AnswerHours * GenDate.TicksPerHour));
                    p.stage = 2;
                    Tell.Ask(letter, map);
                    OldPowers.Next(b, p, 2500);
                    return;
                }
                case 2:
                    // Waiting for the answer (the letter's own clock sends them on).
                    OldPowers.Next(b, p, 2500);
                    return;
                case 3:
                {
                    // The bearer walks over with the piece.
                    var who = p.b;
                    if (!Still(p))
                    {
                        Leave(b, p, "enchanter,leave");
                        return;
                    }
                    if (!who.Position.InHorDistOf(ench.Position, 2.5f) && p.count++ < 12)
                    {
                        if (who.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(who, OwnHappenings.Beside(map, ench.Position, who), 1200);
                        OldPowers.Next(b, p, 200);
                        return;
                    }
                    p.stage = 4;
                    p.count = 0;
                    OwnHappenings.Stand(ench, who.Position, OldPowersMath.EnchantTicks + 300);
                    OwnHappenings.Stand(who, ench.Position, OldPowersMath.EnchantTicks + 300);
                    OldPowers.Say(ench, "enchanter,work");
                    OldPowers.Next(b, p, 300);
                    return;
                }
                case 4:
                {
                    // An hour of work over it, the light running along it, then it comes away a grade better.
                    if (p.count * 300 < OldPowersMath.EnchantTicks)
                    {
                        p.count++;
                        var at = (ench.DrawPos + (p.b?.DrawPos ?? ench.DrawPos)) / 2f;
                        OldPowers.Motes(map, at, Gold, 3, 0.4f, "Mote_Enchanting", "Mote_GlowingRune", "PsycastPsychicEffect");
                        if (p.count % 3 == 0) OldPowers.Sound(map, ench.Position, "ItemEnchanted", "MeditationGainPsyfocus", "PsychicSootheGlobal");
                        OldPowers.Next(b, p, 300);
                        return;
                    }
                    var args = Args(p);
                    if (!Still(p) || !VarietyUtil.TakeFromStores(map, ThingDefOf.Silver, p.value))
                    {
                        Leave(b, p, "enchanter,nosilver");
                        return;
                    }
                    var comp = p.thing.TryGetComp<CompQuality>();
                    var up = (QualityCategory)Mathf.Min((int)comp.Quality + 1, (int)QualityCategory.Legendary);
                    comp.SetQuality(up, ArtGenerationContext.Outsider);
                    OldPowers.Sound(map, ench.Position, "ItemEnchanted", "PsychicSootheGlobal");
                    OldPowers.Motes(map, p.b.DrawPos, Gold, 8, 0.5f, "Mote_GlowingRune", "Mote_Enchanting", "PsycastAreaEffect");
                    OldPowers.Say(p.b, "enchanter,done");
                    OldPowers.Card(map, "Enchanter", OldPowers.T("Enchanter_Done", args), new LookTargets(p.b), LetterDefOf.PositiveEvent);
                    Leave(b, p, null);
                    return;
                }
            }
        }

        /// <summary>The colony's answer: the bearer walks over with the piece, or the enchanter walks on.</summary>
        public static void Answer(bool yes)
        {
            var b = OldPowersBook.Instance;
            var p = Waiting;
            if (b == null || p == null) return;
            if (!yes || !Still(p) || VarietyUtil.InStores(p.map, ThingDefOf.Silver) < p.value)
            {
                Leave(b, p, "enchanter,leave");
                return;
            }
            p.stage = 3;
            p.count = 0;
            if (p.b.Spawned) OwnHappenings.GoTo(p.b, OwnHappenings.Beside(p.map, p.a.Position, p.b), 1500);
            OldPowers.Next(b, p, 120);
        }

        private static void Leave(OldPowersBook b, OldPowerPlay p, string moment)
        {
            if (moment != null && p.a != null && p.a.Spawned) OldPowers.Say(p.a, moment);
            if (p.a != null && p.a.Spawned) Arrivals.SendAway(new[] { p.a });
            OldPowers.End(b);
        }

        /// <summary>The visit ran out: the enchanter walks on.</summary>
        internal static void EnchanterOver(OldPowerPlay p)
        {
            if (p.a != null && p.a.Spawned && !p.a.Dead) Arrivals.SendAway(new[] { p.a });
        }

        // ================= The pilgrims passing =================

        public static GodPaceMath.Trigger PilgrimsCall(Map map)
        {
            var why = OldPowersMath.PilgrimsTrigger(OldPowers.DayOfSeason(map), OldPowers.Quarter(map), OldPowers.AnyCondition(map, "Eclipse", "Aurora"));
            return why != GodPaceMath.Trigger.None && Arrivals.Friendly() != null && Place(map, out _).IsValid ? why : GodPaceMath.Trigger.None;
        }

        /// <summary>Where they stop to pray: the colony's oldest grave, its oldest tree near the middle, else the middle itself.</summary>
        internal static IntVec3 Place(Map map, out string what)
        {
            var grave = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => g.Spawned && g.HasCorpse).OrderBy(g => g.thingIDNumber).FirstOrDefault();
            if (grave != null)
            {
                what = "Graves";
                return grave.Position;
            }
            var mid = OwnHappenings.Middle(map);
            var tree = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(t => t.Spawned && t.def.plant.IsTree && t.Growth > 0.9f && t.Position.InHorDistOf(mid, 40f) && !t.Position.Fogged(map))
                .OrderByDescending(t => t.def.plant.harvestYield).ThenBy(t => t.thingIDNumber).FirstOrDefault();
            if (tree != null)
            {
                what = "Tree";
                return tree.Position;
            }
            what = "Middle";
            return mid;
        }

        public static bool Pilgrims(OldPowersBook b, Map map, bool strong)
        {
            var f = Arrivals.Friendly(null, x => Peoples.Preference(PeoplesMath.Pilgrims, x));
            var kind = Arrivals.PersonKind(f);
            var place = Place(map, out string what);
            if (kind == null || !place.IsValid) return false;
            var spot = OldPowers.OpenNear(map, place, 4);
            if (!spot.IsValid) return false;
            int n = OldPowersMath.PilgrimCount(Rand.Value);
            var group = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var x = Arrivals.Make(kind, f);
                if (x != null) group.Add(x);
            }
            if (group.Count == 0 || Arrivals.Visit(group, map, f, spot, OldPowersMath.PilgrimHours) == null)
            {
                foreach (var x in group) Arrivals.Discard(x);
                return false;
            }
            var p = OldPowers.Play(b, OldPower.Pilgrims, map, OldPowersMath.PilgrimHours + 1f, 250);
            p.group = group;
            p.faction = f;
            p.cell = place;
            p.cell2 = spot;
            p.stage = 1;
            p.strong = strong;
            p.form = what;
            b.Came(OldPower.Pilgrims, Now, what, f.loadID.ToString());
            return true;
        }

        internal static void PilgrimsStep(OldPowersBook b, OldPowerPlay p, int now)
        {
            var map = p.map;
            var here = p.group.Where(x => x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed).ToList();
            if (here.Count == 0)
            {
                OldPowers.End(b);
                return;
            }
            switch (p.stage)
            {
                case 1:
                {
                    if (!here.Any(x => x.Position.InHorDistOf(p.cell, 7f)) && now < p.until - GenDate.TicksPerHour * 5)
                    {
                        OldPowers.Next(b, p, 250);
                        return;
                    }
                    p.stage = 2;
                    p.count = 0;
                    string place = OldPowers.T("Pilgrims_Place_" + p.form);
                    string text = OldPowers.T("Pilgrims_Text", here.Count.ToString().Named("COUNT"), (p.faction?.Name ?? "?").Named("PEOPLE"), place.Named("PLACE"),
                        (p.strong ? OldPowers.T("Pilgrims_Sign") : OldPowers.T("Pilgrims_Road")).Named("WHY"));
                    OldPowers.Card(map, "Pilgrims", text, new LookTargets(here), LetterDefOf.NeutralEvent);
                    OldPowers.Next(b, p, 120);
                    return;
                }
                case 2:
                {
                    // They pray in turn, facing the place; whoever of the colony sees it is the gladder.
                    if (p.count < 3)
                    {
                        foreach (var x in here) OwnHappenings.Stand(x, p.cell, 900);
                        var speaker = here[p.count % here.Count];
                        OldPowers.Say(speaker, "pilgrims,pray");
                        OldPowers.Motes(map, p.cell.ToVector3Shifted(), Holy, 5, 0.8f, "Mote_Holy", "Mote_Twinkle", "Lightshaft");
                        if (p.count == 0) OldPowers.Sound(map, p.cell, "TM_Speech_Ideoligious_Male", "RitualConclusion_Positive", "PsychicSootheGlobal");
                        var seer = OldPowers.Witness(map, p.cell, 20f);
                        if (seer != null)
                        {
                            OldPowers.Memory(seer, "Rimfolk_Thought_OldP_Pilgrims");
                            if (p.count == 1) OldPowers.Say(seer, "pilgrims,seen");
                        }
                        p.count++;
                        OldPowers.Next(b, p, 900);
                        return;
                    }
                    // A small token of their people's age at the place, from the player's whole pool, and they walk on.
                    var def = ThingRoles.From(ThingRole.Carving, p.faction, map, ThingRoles.Seed("oldpowers-pilgrims", p.faction, map));
                    var at = OwnHappenings.Beside(map, p.cell, here[0]);
                    if (def != null && at.IsValid)
                    {
                        var token = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                        token.stackCount = 1;
                        token.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
                        if (GenPlace.TryPlaceThing(token, at, map, ThingPlaceMode.Near, out var placed))
                        {
                            OldPowers.Say(here[0], "pilgrims,token");
                            OldPowers.Minor(map, OldPowers.T("Pilgrims_Token", (placed?.LabelNoCount ?? def.label).Named("TOKEN"), OldPowers.T("Pilgrims_Place_" + p.form).Named("PLACE")),
                                new LookTargets(placed));
                        }
                    }
                    Arrivals.SendAway(here);
                    OldPowers.End(b);
                    return;
                }
            }
        }
    }
}
