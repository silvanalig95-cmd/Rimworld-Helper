using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Travellers who seek out the remote (docs/design/stage1-region.md, item 5): which reason of the land's brings a
    /// camp of a kind that seeks something (CampKindDef.seeks, World/Region/TravelLand.cs), and where it pitches for it.
    /// </summary>
    public static class CampSeek
    {
        /// <summary>The reason this kind gives at this home, from what the land has; null when it seeks nothing the land has.</summary>
        public static string Choose(CampKindDef kind, LandReasons land)
        {
            if (kind?.seeks == null || kind.seeks.Count == 0 || land == null) return null;
            return TravelMath.Pick(kind.seeks, land.holds, Rand.Value);
        }

        /// <summary>
        /// Where a camp pitches: near the ruins or the ore it came for where there's room (the camp finder's own rules),
        /// else where it would have anyway (<paramref name="usual"/>).
        /// </summary>
        public static IntVec3 SiteFor(string seek, LandReasons land, Map map, IntVec3 usual, bool shade)
        {
            if (!usual.IsValid || land == null || map == null || seek != TravelMath.Ruins && seek != TravelMath.Ore) return usual;
            try
            {
                var at = land.Look(seek);
                var target = at.HasThing ? at.Thing.Position : at.Cell;
                if (!target.IsValid || at.Map != map && !(at.HasThing && at.Thing.Map == map)) return usual;
                var c = CampLand.TryFindNear(map, target, 9f, 20f, shade);
                return c.IsValid ? c : usual;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("travellers: where to pitch", e);
                return usual;
            }
        }
    }

    public partial class Camp
    {
        /// <summary>The errand place that is what they came for (CampAsk effects: to "seek").</summary>
        public const string SeekTo = "seek";

        /// <summary>What brought them (a reason of the land's: TravelMath), or null.</summary>
        public string seeking;
        /// <summary>What they came for, in words, as it was when they came ("the old ruins north of the colony").</summary>
        public string seekWhat;
        /// <summary>Where it is: a thing on the map (a relic's shrine, a bookcase, the ore, a tree), a cell (the ruins), or a world object (a ruin in the region).</summary>
        public Thing seekThing;
        public IntVec3 seekCell = IntVec3.Invalid;
        public int seekObject = -1;
        /// <summary>Their own walk out to it done (the ruins, the ore), their request for it made (the relic, the books, the tree, the colony's story), a quarrel had.</summary>
        public bool seekWalked, seekAsked, quarrelled;
        /// <summary>How many pieces of news of far towns they have told (two at most: TravelMath.FarNewsPerCamp).</summary>
        public int farNewsTold;

        private void ExposeSeek()
        {
            if (Scribe.mode == LoadSaveMode.Saving && seekThing != null && seekThing.Destroyed) seekThing = null;
            Scribe_Values.Look(ref seeking, "seeking");
            Scribe_Values.Look(ref seekWhat, "seekWhat");
            Scribe_References.Look(ref seekThing, "seekThing");
            Scribe_Values.Look(ref seekCell, "seekCell", IntVec3.Invalid);
            Scribe_Values.Look(ref seekObject, "seekObject", -1);
            Scribe_Values.Look(ref seekWalked, "seekWalked");
            Scribe_Values.Look(ref seekAsked, "seekAsked");
            Scribe_Values.Look(ref quarrelled, "quarrelled");
            Scribe_Values.Look(ref farNewsTold, "farNewsTold");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !TravelMath.Known(seeking)) seeking = null;
        }

        /// <summary>A traveller's kind: one that seeks something of the land's (and tells news of far towns).</summary>
        public bool Travellers => kind?.seeks != null && kind.seeks.Count > 0;

        /// <summary>The camp has come: what brought it, and where that is.</summary>
        public void SeekStarted(CampSetup s)
        {
            seeking = s?.seek;
            if (seeking == null || s.land == null) return;
            seekWhat = s.land.What(seeking);
            var at = s.land.Look(seeking);
            if (at.HasThing && at.Thing.Map == map) seekThing = at.Thing;
            else if (at.HasWorldObject) seekObject = at.WorldObject.ID;
            else if (at.Cell.IsValid && at.Map == map) seekCell = at.Cell;
        }

        /// <summary>Where what they came for is, to look at: the thing while it stands, else its cell, else the ruin out in the world.</summary>
        public GlobalTargetInfo SeekTarget
        {
            get
            {
                if (seekThing != null && !seekThing.Destroyed && seekThing.SpawnedOrAnyParentSpawned) return new GlobalTargetInfo(seekThing.SpawnedParentOrMe);
                if (seekCell.IsValid && map != null) return new GlobalTargetInfo(seekCell, map);
                if (seekObject >= 0)
                {
                    var o = Find.WorldObjects?.AllWorldObjects.FirstOrDefault(x => x != null && x.ID == seekObject && !x.Destroyed);
                    if (o != null) return new GlobalTargetInfo(o);
                }
                return GlobalTargetInfo.Invalid;
            }
        }

        /// <summary>Why they came, in their own words where their kind has some ([seek:REASON]), else the general line; null when nothing of the land's brought them.</summary>
        public string SeekLine(Pawn x = null, Pawn a = null)
        {
            if (seeking.NullOrEmpty()) return null;
            var tags = new HashSet<string> { "seek:" + seeking };
            Flavor.AddEraTags(tags);
            CampWords.AddTags(tags, kind, faction, map);
            var own = kind?.seekTexts?.Where(l => HasTag(l, "seek:" + seeking)).ToList();
            string raw = own.NullOrEmpty() ? null : Flavor.Pick(own, tags);
            if (raw == null)
            {
                string key = "CC_Travel_Came_" + seeking;
                if (!key.CanTranslate()) return null;
                raw = TextVariants.Vary(key).Translate().RawText;
            }
            return Format(Texts.Fill(raw, ("SEEK", seekWhat ?? "")), x, a);
        }

        /// <summary>For the World page: what brought them.</summary>
        public string SeekPanelLine() => seeking.NullOrEmpty() || seekWhat.NullOrEmpty() ? null : "CC_Travel_Panel_Came".Translate(seekWhat.Named("SEEK")).Resolve();

        // ---------------- Every hour ----------------

        private void SeekHourly()
        {
            if (phase != CampPhase.Living || Hostile || map == null) return;
            try
            {
                int hour = GenLocalDate.HourOfDay(map);
                if (!seeking.NullOrEmpty())
                {
                    WalkToWhatTheyCameFor(hour);
                    AskForWhatTheyCameFor(hour);
                    Quarrel(hour);
                }
                if (Travellers) TellFarNews();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("travellers: the camp's hour", e);
            }
        }

        /// <summary>What they do at it, by what they came for: study the ruins, prospect the ore.</summary>
        private string WalkDoing => seeking == TravelMath.Ruins ? "study" : seeking == TravelMath.Ore ? "prospect" : null;

        /// <summary>
        /// Out on the land, the ruins or the ore they came for: the first working day they walk out to it themselves (it
        /// needs nobody's leave), and the colony hears of it when they get there.
        /// </summary>
        private void WalkToWhatTheyCameFor(int hour)
        {
            // A kind with work of its own on the land (the salvagers stripping the ruins) is out there already.
            if (seekWalked || WalkDoing == null || !kind.work.NullOrEmpty() || ErrandSet || DaysHere < 0.3f || hour < 8 || hour > 15) return;
            seekWalked = true;
            var lines = kind?.momentLines?.Where(l => HasTag(l, "seek:" + seeking)).Select(l => Flavor.Strip(l)).Where(l => !l.NullOrEmpty()).ToList();
            var walk = new DilemmaEffect_CampComes { to = SeekTo, count = 2, doing = WalkDoing, hours = new FloatRange(2f, 3f), when = new IntRange(8, 16), lines = lines };
            StartErrand(walk, null);
        }

        /// <summary>
        /// What lies in the colony itself (the relic, the books, the tree, the colony's story): they ask the colony's leave
        /// on their first day, by letter, when their kind has the request (an ask with needs "seek:REASON").
        /// </summary>
        private void AskForWhatTheyCameFor(int hour)
        {
            if (seekAsked || DaysHere < 0.3f || hour < 9 || hour > 18 || !ColonyCouncil.Tell.CanAsk) return;
            var ask = Requests.FirstOrDefault(a => a.needs == "seek:" + seeking && !a.askTexts.NullOrEmpty());
            if (ask == null)
            {
                seekAsked = true;
                return;
            }
            if (Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Camp c && c.campId == id && !c.resolved)) return;
            if (AskBlocked(ask) != null) return;
            seekAsked = true;
            Request(ask);
        }

        /// <summary>Why an ask about what they came for can't be made now (null: it can); null for any other ask.</summary>
        private string SeekAskBlocked(CampAsk a)
        {
            if (a?.needs == null || !a.needs.StartsWith("seek:")) return null;
            if (seeking != a.needs.Substring(5)) return "CC_Travel_Ask_NotFor".Translate().ToString();
            if (seeking == TravelMath.History) return null;
            return SeekTarget.IsValid && SeekTarget.IsMapTarget ? null : "CC_Travel_Ask_Gone".Translate().ToString();
        }

        /// <summary>Where an errand to what they came for goes: beside it, where they can stand.</summary>
        private IntVec3 SeekErrandCell(Pawn walker)
        {
            var at = SeekTarget;
            if (!at.IsValid || !at.IsMapTarget || at.Map != map) return IntVec3.Invalid;
            var c = at.HasThing ? at.Thing.Position : at.Cell;
            return c.IsValid ? PlaySpots.FreeCellNear(c, map, walker) : IntVec3.Invalid;
        }

        /// <summary>A walk out to what they came for, done: what comes of it.</summary>
        private void SeekErrandDone(string doing)
        {
            var who = errandPeople.FirstOrDefault();
            switch (doing)
            {
                case "study":
                case "prospect":
                case "relic":
                case "sing":
                    AddTension(-4f);
                    Note(Format(Texts.Fill(("CC_Travel_Done_" + doing).Translate().RawText, ("SEEK", seekWhat ?? "")), who, null), MessageTypeDefOf.NeutralEvent, Importance.Notable, who);
                    return;
                case "read":
                {
                    // Their notes in the margins of the colony's books: a little toward what the colony studies now.
                    AddTension(-4f);
                    var proj = Find.ResearchManager?.GetProject(null);
                    if (proj == null || proj.IsFinished)
                    {
                        Note(Format("CC_Travel_Done_read".Translate().RawText, who, null), MessageTypeDefOf.NeutralEvent, Importance.Notable, who);
                        return;
                    }
                    float left = proj.Cost - Find.ResearchManager.GetProgress(proj);
                    float amount = Mathf.Min(left * 0.1f, 300f, left - 1f);
                    if (amount > 1f) Find.ResearchManager.AddProgress(proj, amount, null);
                    string notes = Texts.Fill("CC_Travel_Done_readNotes".Translate().RawText, ("AMOUNT", Mathf.RoundToInt(amount).ToString()), ("PROJECT", proj.LabelCap.ToString()));
                    Note(Format(notes, who, null),
                        MessageTypeDefOf.PositiveEvent, Importance.Notable, who);
                    return;
                }
                case "listen":
                {
                    // together: the errand walked them to the colonist's side and kept them there while it lasted.
                    var teller = errandFor;
                    if (teller == null || teller.Dead) return;
                    AddTension(-4f);
                    teller.skills?.Learn(SkillDefOf.Social, 400f);
                    Warm(teller, 2);
                    string line = Format("CC_Travel_Done_listen".Translate().RawText, who, teller);
                    Note(line, MessageTypeDefOf.PositiveEvent, Importance.Notable, teller);
                    CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, line, teller));
                    return;
                }
            }
        }

        /// <summary>A book of theirs, carried to the colony's gate and set down there (CampCarry: made when it's taken up).</summary>
        public const string BookGift = "book";

        /// <summary>
        /// The travellers' good turns: "book", one of them carries a book of theirs to the colony's gate and leaves it there
        /// (once a stay; a textbook or a novel, never from a people of the tribal age, who keep their tales by heart). False
        /// when it can't come about today.
        /// </summary>
        private bool SeekHelp(string h)
        {
            if (h != BookGift || asksMade.Contains(BookGift) || faction == null || Ages.Of(faction) == WorldAge.Tribal) return false;
            if (carries.Any(c => c?.help == BookGift)) return false;
            var defs = new[] { "TextBook", "Novel" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
            if (defs.Count == 0) return false;
            var porter = Free.Where(p => !p.WorkTagIsDisabled(WorkTags.Hauling)).OrderBy(p => p == leader ? 1 : 0).FirstOrDefault();
            if (porter == null) return false;
            var gate = ErrandCell("gate", null, porter);
            if (!gate.IsValid) return false;
            asksMade.Add(BookGift);
            carries.Add(new CampCarry { by = porter, help = BookGift, def = Rand.Chance(0.6f) ? defs[0] : defs[defs.Count - 1], count = 1, to = gate, from = Now, until = Now + GenDate.TicksPerHour * 12, carrying = true });
            return true;
        }

        /// <summary>
        /// Prospectors who came for the ore may fall out over a claim: once a stay at most (TravelMath.QuarrelChance a
        /// stay), by day, two of their own come to blows at their camp (the game's own social fight). Nobody of the
        /// colony's is in it; the colony sees it and hears why.
        /// </summary>
        private void Quarrel(int hour)
        {
            if (quarrelled || seeking != TravelMath.Ore || hour < 9 || hour > 17 || MapFacts.ThreatOn(map)) return;
            if (!Rand.Chance(TravelMath.QuarrelHourChance(StayDays))) return;
            var fit = Here.Where(p => !p.Downed && !p.InMentalState && p.Awake() && !p.DevelopmentalStage.Juvenile() && p.interactions != null && !p.WorkTagIsDisabled(WorkTags.Violent)
                                      && !OnErrand(p) && p.Position.InHorDistOf(FireCell, 16f)).ToList();
            if (fit.Count < 2) return;
            var a = fit.RandomElement();
            // together: the two of them at their camp, within talking range of each other (Together.Can).
            var b = fit.Where(x => x != a && Together.Can(a, x)).RandomElementWithFallback();
            if (b == null) return;
            quarrelled = true;
            Speech.Say(a, TextVariants.Vary("CC_Travel_Quarrel_Say").Translate().Resolve(), b);
            a.interactions.StartSocialFight(b);
            if (a.MentalStateDef != MentalStateDefOf.SocialFighting) return;
            AddTension(3f);
            string text = "CC_Travel_Quarrel".Translate(a.Named("X"), b.Named("OTHER"), Label.Named("NAME"), (seekWhat ?? "").Named("SEEK")).Resolve();
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map, text, new LookTargets(new[] { a, b }), MessageTypeDefOf.NegativeEvent);
        }

        /// <summary>
        /// News of far towns: when a colonist sits with one of them at their fire (Together), now and then one of them tells
        /// what is happening in a town beyond the region (World/Region/FarNews.cs), two pieces a stay at most.
        /// </summary>
        private void TellFarNews()
        {
            if (!TravelLand.NewsOn || !TravelMath.MayTell(farNewsTold) || !Rand.Chance(TravelMath.FarNewsHourChance)) return;
            var here = Here.Where(m => m.Awake() && !m.Downed && !m.InMentalState && !m.DevelopmentalStage.Baby()).ToList();
            if (here.Count == 0) return;
            foreach (var c in IncidentWorker_Happening.Colonists(map).InRandomOrder())
            {
                if (c.Drafted || c.Downed || c.InMentalState || !c.Awake() || !c.Position.InHorDistOf(FireCell, 14f)) continue;
                var m = here.Where(x => x.Position.InHorDistOf(c.Position, 8f) && Together.Can(c, x)).OrderBy(x => x.Position.DistanceToSquared(c.Position)).FirstOrDefault();
                if (m == null) continue;
                if (FarNews.Tell(map, m, c, Label)) farNewsTold++;
                else farNewsTold = TravelMath.FarNewsPerCamp;
                return;
            }
        }
    }
}
