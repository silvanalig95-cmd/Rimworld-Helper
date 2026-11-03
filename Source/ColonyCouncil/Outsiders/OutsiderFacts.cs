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
    /// What is true right now for a talk between a colonist and someone from outside the colony, read from the game each
    /// time two are about to talk (OutsiderTalk.cs): the hour, the season and the weather; who the outsider is and their
    /// people (their era, the Empire's titles, their leader); their town and its people by name (a new head, a death, one
    /// ill, letters exchanged), the land and the cold back home, places and work near it, what they're short of; their
    /// people's wars and peace (the game's own relations, whichever mod set them, and battles seen on the colony's land),
    /// the world's events running or just over, the conditions over the land; the colony's own news (a raid and by whom,
    /// a death, a birth, a wedding, a newcomer, a decree, the speaker elected, a camp, the harvest, the stores); what their
    /// people remember the colony for; and what passed between the two before (their earlier talks, picked up by
    /// callbacks with what became of it since). Every fact is a tag for the pieces' conditions, most with a value for a
    /// placeholder and a heat for how current it is. Nothing is claimed that isn't so: a fact the game can't give is
    /// simply left out, and a piece that needs it isn't said.
    /// </summary>
    public static class OutsiderFacts
    {
        private static int Now => Find.TickManager.TicksGame;

        private static float Days(int tick) => (Now - tick) / (float)GenDate.TicksPerDay;

        /// <summary>How current something <paramref name="days"/> old is: <paramref name="top"/> when new, fading over <paramref name="span"/> days to a floor.</summary>
        private static float Fresh(float days, float span, float top) => Mathf.Max(0.4f, top * (1f - Mathf.Clamp01(days / Mathf.Max(0.1f, span))));

        public static TalkFacts For(Pawn c, Pawn x, TalkTone tone)
        {
            var f = new TalkFacts();
            if (c == null || x == null) return f;
            f.Tag("tone:" + tone.ToString().ToLowerInvariant());
            f.Value("COLONIST", c.LabelShort);
            f.Value("OUTSIDER", x.LabelShort);
            var people = Outsiders.PeopleOf(x);
            var town = Outsiders.TownOf(x);
            Safe("moment", () => Moment(f, c, x));
            Safe("people", () => People(f, c, x, people));
            Safe("persons", () => Persons(f, c, x));
            Safe("pair", () => Pair(f, c, x));
            Safe("town", () => Town(f, x, people, town));
            Safe("wars", () => Wars(f, people));
            Safe("situations", () => Situations(f, people));
            Safe("conditions", () => Conditions(f, c.MapHeld));
            Safe("colony", () => Colony(f, c, people));
            Safe("deeds", () => Deeds(f, c, people));
            Safe("marks", () => Marks(f, c, x, people, town));
            return f;
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider facts: " + what, e);
            }
        }

        // ---------------- The moment ----------------

        private static readonly HashSet<string> MomentTags = new HashSet<string>
        {
            "spring", "summer", "fall", "winter", "morning", "day", "evening", "night", "rain", "snow", "fog", "storm", "clear", "hot", "cold",
            "tribal", "medieval", "industrial", "spacer", "lowtech", "poor", "rich", "small", "large"
        };

        private static void Moment(TalkFacts f, Pawn c, Pawn x)
        {
            foreach (var t in Flavor.TagsFor(c))
            {
                if (MomentTags.Contains(t)) f.Tag(t, t == "storm" ? 1f : t == "snow" || t == "hot" || t == "cold" ? 0.5f : 0f);
                else if (t.StartsWith("trait:", StringComparison.Ordinal) || t.StartsWith("skill:", StringComparison.Ordinal) || t == "old" || t == "teen" || t == "hurt"
                         || t == "grieving" || t == "lowmood" || t == "highmood")
                    f.usTags.Add(t);
            }
            var map = c.MapHeld;
            if (map != null)
            {
                f.Value("SEASON", GenLocalDate.Season(map).Label());
                var w = map.weatherManager?.curWeather;
                if (w != null && !w.label.NullOrEmpty()) f.Value("WEATHER", w.label);
            }
            // The outsider's own: their nature, their age, their hurts.
            var traits = x.story?.traits?.allTraits;
            if (traits != null)
                foreach (var t in traits)
                    if (t?.def != null)
                        f.themTags.Add(Flavor.DefTag("trait:", t.def));
            float age = Flavor.HumanAge(x);
            if (age >= 60f) f.themTags.Add("old");
            else if (age < 20f) f.themTags.Add("teen");
            if ((x.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.85f) f.themTags.Add("hurt");
        }

        // ---------------- Their people ----------------

        private static void People(TalkFacts f, Pawn c, Pawn x, Faction p)
        {
            if (p != null)
            {
                f.Value("PEOPLE", p.Name);
                int tech = (int)p.def.techLevel;
                f.Tag(tech <= NotableMath.Neolithic ? "their:tribal" : tech == NotableMath.Medieval ? "their:medieval" : tech == NotableMath.Industrial ? "their:industrial" : "their:spacer");
                f.Tag(tech <= NotableMath.Medieval ? "their:lowtech" : "their:hightech");
                if (p.def.HasRoyalTitles) f.Tag("their:empire");
                var leader = p.leader;
                if (leader != null && !leader.Dead && leader != x)
                {
                    f.Tag("their:leader");
                    f.Value("LEADER", leader.LabelShort, "leader:" + p.loadID + ":" + leader.thingIDNumber);
                    if (!p.LeaderTitle.NullOrEmpty()) f.Value("LEADERTITLE", p.LeaderTitle);
                }
                if (leader == x) f.Tag("them:leader", 1f);
            }
            var title = x.royalty?.MostSeniorTitle;
            if (title != null)
            {
                f.Tag("their:noble", 1f);
                f.Value("THEIRTITLE", title.Label);
            }
            var ours = c.royalty?.MostSeniorTitle;
            if (ours != null)
            {
                f.Tag("us:noble");
                f.Value("OURTITLE", ours.Label);
            }
            if (ModsConfig.IdeologyActive && c.Ideo != null && x.Ideo != null)
            {
                if (c.Ideo == x.Ideo) f.Tag("samefaith");
                else f.Tag("otherfaith");
                f.Value("THEIRFAITH", x.Ideo.name);
                f.Value("OURFAITH", c.Ideo.name);
            }
            // What they are here as.
            var n = Outsiders.NotableOf(x);
            if (Strangers.IsHospitalityGuest(x)) f.Tag("role:guest", 1f);
            else if (x.IsQuestLodger()) f.Tag("role:lodger", 1f);
            else if (x.TraderKind != null) f.Tag("role:trader", 1f);
            else f.Tag("role:visitor");
            if (n != null && n.role != NotableRole.Acquaintance)
            {
                f.Tag("role:notable", 1f);
                f.Tag("role:" + n.role.ToString().ToLowerInvariant(), 1f);
                f.Value("THEIRROLE", NotableBook.TitleOf(n));
            }
        }

        // ---------------- The two of them ----------------

        private static void Persons(TalkFacts f, Pawn c, Pawn x)
        {
            if (x.DevelopmentalStage.Juvenile()) f.Tag("them:young");
            if (c.DevelopmentalStage.Juvenile()) f.Tag("us:young");
            if (Flavor.HumanAge(x) >= 60f) f.Tag("them:old");
            if (Flavor.HumanAge(c) >= 60f) f.Tag("us:old");
            if ((x.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.85f) f.Tag("them:hurt", 1.5f);
            if ((c.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.85f) f.Tag("us:hurt", 1f);
            if (x.story?.traits?.HasTrait(TraitDefOf.Kind) ?? false) f.Tag("them:kind");
            if (x.story?.traits?.HasTrait(TraitDefOf.Abrasive) ?? false) f.Tag("them:abrasive");
            Best(f, x, "them", "THEIRSKILL");
            Best(f, c, "us", "OURSKILL");
            Family(f, x, "them", "THEIRSPOUSE", "THEIRCHILD");
            Family(f, c, "us", "OURSPOUSE", "OURCHILD");
            // Family of theirs in the colony.
            foreach (var o in PawnsFinder.AllMaps_FreeColonists)
            {
                if (o == null || o == x || o.Dead || x.relations == null) continue;
                var rel = x.GetRelations(o).FirstOrDefault(r => r != null && (r.familyByBloodRelation || r == PawnRelationDefOf.Spouse));
                if (rel == null) continue;
                f.Tag("kin", 2f);
                if (o == c) f.Tag("kin:you", 2.5f);
                f.Value("KIN", o.LabelShort);
                f.Value("KINWORD", rel.GetGenderSpecificLabel(o));
                break;
            }
            int days = (c.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999) / GenDate.TicksPerDay;
            if (days < 15) f.Tag("us:newcomer", 1f);
        }

        private static void Best(TalkFacts f, Pawn p, string who, string key)
        {
            var skills = p.skills?.skills;
            if (skills == null) return;
            SkillRecord best = null;
            foreach (var s in skills)
                if (s != null && !s.TotallyDisabled && s.Level >= 8 && (best == null || s.Level > best.Level))
                    best = s;
            if (best == null) return;
            f.Tag(who + ":best:" + best.def.defName);
            f.Value(key, best.def.label);
        }

        private static void Family(TalkFacts f, Pawn p, string who, string spouseKey, string childKey)
        {
            var rel = p.relations;
            if (rel == null) return;
            var spouse = rel.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
            if (spouse != null)
            {
                f.Tag(spouse.Dead ? who + ":widowed" : who + ":married");
                if (!spouse.Dead) f.Value(spouseKey, spouse.LabelShort);
                // Here with them (on the same map), not waiting at home.
                if (!spouse.Dead && spouse.Spawned && spouse.Map == p.MapHeld) f.Tag(who + ":spousehere");
            }
            var child = rel.Children?.Where(k => k != null && !k.Dead).OrderBy(k => k.ageTracker?.AgeBiologicalYearsFloat ?? 99f).FirstOrDefault();
            if (child != null)
            {
                f.Tag(who + ":parent");
                if (child.DevelopmentalStage.Juvenile()) f.Tag(who + ":smallchild");
                if (child.Spawned && child.Map == p.MapHeld) f.Tag(who + ":childhere");
                f.Value(childKey, child.LabelShort);
            }
        }

        // ---------------- What passed between them ----------------

        private static void Pair(TalkFacts f, Pawn c, Pawn x)
        {
            var pair = OutsiderBook.Instance?.Pair(c, x, false);
            int now = Now;
            int ofColonist = Outsiders.OfColonist(x, c), ofColony = Outsiders.OfColony(x);
            if (ofColonist >= 25) f.Tag("likesyou");
            else if (ofColonist <= -10) f.Tag("coolyou");
            if (ofColony >= 30) f.Tag("likesus");
            else if (ofColony <= -10) f.Tag("wary");
            if (pair == null || pair.talks == 0 && pair.lastTick < 0)
            {
                f.Tag("first", 1.5f);
                return;
            }
            f.Tag("met");
            if (pair.lastTick >= 0 && OutsiderMath.NewVisit(pair.lastTick, now)) f.Tag("returning", 2f);
            if (pair.fam >= 30f) f.Tag("known");
            if (pair.fam >= 60f) f.Tag("close");
            if (pair.friends) f.Tag("friends", 1f);
            if (pair.court == 1) f.Tag("sweethearts", 1.5f);
            if (pair.court == 2) f.Tag("lovers", 1.5f);
            if (pair.talks >= 3) f.Tag("talks:3");
            if (pair.talks >= 8) f.Tag("talks:8");
            void Did(OutsiderAct a, string tag)
            {
                if (pair.Last(a) >= 0) f.Tag("did:" + tag, Days(pair.Last(a)) < 3f ? 1f : 0f);
            }
            Did(OutsiderAct.Gift, "gift");
            Did(OutsiderAct.Meal, "meal");
            Did(OutsiderAct.Drink, "meal");
            Did(OutsiderAct.Teach, "teach");
            Did(OutsiderAct.Spar, "spar");
            Did(OutsiderAct.Duel, "duel");
            Did(OutsiderAct.Provoke, "provoke");
            Did(OutsiderAct.Faith, "faith");
            Did(OutsiderAct.Court, "court");
            Did(OutsiderAct.Letter, "letter");
            Did(OutsiderAct.Bring, "bring");
            if (pair.topics.Count > 0) f.Tag("last:" + pair.topics[pair.topics.Count - 1]);
        }

        // ---------------- Their town ----------------

        private static void Town(TalkFacts f, Pawn x, Faction p, Settlement town)
        {
            var map = HappeningTracker.HomeMap() ?? x.MapHeld;
            if (town != null)
            {
                f.Tag("town");
                f.Value("TOWN", town.Name);
                if (map != null)
                {
                    float tiles = LivingEvents.Distance(map.Tile, town.Tile);
                    float days = NotableMath.TravelDays(tiles, (int)(p?.def?.techLevel ?? TechLevel.Industrial));
                    if (days <= 1.2f) f.Tag("town:near");
                    else if (days >= 4f) f.Tag("town:far");
                }
                Land(f, town.Tile);
                Notables(f, x, p, town);
                Places(f, town, p);
            }
            if (p == null) return;
            // What their people are short of, from a hard time that falls on them.
            foreach (var s in WorldSituations.Instance?.Running ?? Enumerable.Empty<WorldSituation>())
            {
                if (s?.def == null || !s.def.bad || s.ctx?.faction != p) continue;
                string kind = Kind(s.def.defName);
                if (kind == "hunger") f.Tag("town:short:food", 1.5f);
                else if (kind == "sickness") f.Tag("town:short:medicine", 1.5f);
                else if (kind == "war") f.Tag("town:short:hands", 1.5f);
                else if (kind == "cold") f.Tag("town:short:fuel", 1.5f);
            }
            // Their people coming to the colony, as the storyteller has it.
            var q = Find.Storyteller?.incidentQueue;
            if (q != null)
                foreach (var o in q)
                {
                    if (!(o is QueuedIncident qi) || qi.FiringIncident?.parms?.faction != p || qi.FiringIncident.def == null) continue;
                    string n = qi.FiringIncident.def.defName;
                    if (n.Contains("Trader") || n.Contains("Caravan")) f.Tag("town:caravan", 1.5f);
                    else if (n.Contains("Visitor") || n.Contains("Traveler")) f.Tag("town:visitors", 1.5f);
                }
        }

        private static void Land(TalkFacts f, PlanetTile tile)
        {
            var t = Find.WorldGrid[tile];
            var biome = t?.PrimaryBiome;
            if (biome != null)
            {
                f.Value("HOMELAND", biome.label);
                string b = biome.defName.ToLowerInvariant();
                if (b.Contains("desert")) f.Tag("home:desert");
                else if (b.Contains("tundra") || b.Contains("ice")) f.Tag("home:frozen");
                else if (b.Contains("jungle") || b.Contains("tropical")) f.Tag("home:jungle");
                else if (b.Contains("swamp") || b.Contains("marsh")) f.Tag("home:swamp");
                else if (b.Contains("forest") || b.Contains("taiga")) f.Tag("home:forest");
                else if (b.Contains("steppe") || b.Contains("grass")) f.Tag("home:plains");
            }
            if (t != null && (t.hilliness == Hilliness.Mountainous || t.hilliness == Hilliness.Impassable)) f.Tag("home:mountains");
            else if (t != null && t.hilliness == Hilliness.LargeHills) f.Tag("home:hills");
            try
            {
                if (Find.World.CoastDirectionAt(tile).IsValid) f.Tag("home:coast");
            }
            catch
            {
                // A world without coasts (another layer): nothing to say of the sea.
            }
            float temp = Find.World.tileTemperatures.GetOutdoorTemp(tile);
            if (temp <= -5f) f.Tag("home:cold", 0.5f);
            else if (temp >= 32f) f.Tag("home:hot", 0.5f);
            else f.Tag("home:mild");
        }

        private static void Notables(TalkFacts f, Pawn x, Faction p, Settlement town)
        {
            var book = NotableBook.Instance;
            if (book == null || !NotableBook.On) return;
            var mine = book.notables.Where(n => n != null && n.town == town.ID && n.Alive && n.pawn != x).ToList();
            var head = mine.FirstOrDefault(n => n.role == NotableRole.Head);
            if (head != null)
            {
                f.Tag("town:head");
                f.Value("HEAD", head.Name, "head:" + town.ID + ":" + head.pawn.thingIDNumber);
                f.Value("HEADTITLE", NotableBook.TitleOf(head));
                if (head.since > 0 && Days(head.since) <= 30f) f.Tag("town:newhead", Fresh(Days(head.since), 30f, 2.5f));
            }
            var other = mine.Where(n => n.role == NotableRole.Heir || n.role == NotableRole.Envoy || n.role == NotableRole.Trader).OrderBy(n => (int)n.role).FirstOrDefault();
            if (other != null)
            {
                f.Tag("town:notable");
                f.Value("NOTABLE", other.Name, "notable:" + other.id);
                f.Value("NOTABLETITLE", NotableBook.TitleOf(other));
            }
            // A notable ill or badly hurt now.
            foreach (var n in mine)
            {
                var hs = n.pawn.health?.hediffSet;
                if (hs == null) continue;
                bool sick = hs.hediffs.Any(h => h?.def != null && h.Visible && (h.def.makesSickThought || h.def.lethalSeverity > 0f && h.def.isBad && !(h is Hediff_Injury)));
                bool hurt = (n.pawn.health.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.6f;
                if (!sick && !hurt) continue;
                f.Tag("town:ill", 2f);
                f.Value("ILL", n.Name, "ill:" + n.id);
                f.Value("ILLTITLE", NotableBook.TitleOf(n));
                break;
            }
            // A death among their people's notables.
            var loss = book.losses.Where(l => l != null && l.faction == p && (l.town == town.ID || l.town < 0) && Days(l.tick) <= 30f).OrderByDescending(l => l.tick).FirstOrDefault();
            if (loss != null)
            {
                f.Tag("town:loss", Fresh(Days(loss.tick), 30f, 2.5f));
                f.Value("LOST", loss.name);
                f.Value("LOSTTITLE", loss.role);
                if (head != null && head.since > 0 && head.since >= loss.tick - GenDate.TicksPerDay)
                {
                    f.Tag("town:headdied", Fresh(Days(loss.tick), 30f, 3f));
                    f.Value("OLDHEAD", loss.name);
                }
            }
            // Letters between the colony and someone of their town.
            Notable wrote = null;
            int wroteTick = -1;
            foreach (var n in mine)
                foreach (LetterPurpose purpose in Enum.GetValues(typeof(LetterPurpose)))
                {
                    int t = n.Last(purpose);
                    if (t > wroteTick)
                    {
                        wroteTick = t;
                        wrote = n;
                    }
                }
            var open = book.letters.FirstOrDefault(l => l != null && l.Open && mine.Any(n => n.id == l.notable));
            if (open != null)
            {
                f.Tag("town:letter", 1.5f);
                f.Value("WROTE", open.toName ?? "?");
                f.Value("WRITER", open.WriterName);
            }
            else if (wrote != null && wroteTick >= 0 && Days(wroteTick) <= 20f)
            {
                f.Tag("town:wrote", Fresh(Days(wroteTick), 20f, 1.5f));
                f.Value("WROTE", wrote.Name);
            }
        }

        private static void Places(TalkFacts f, Settlement town, Faction p)
        {
            var site = Find.WorldObjects.Sites.Where(s => s != null && s.Tile.Layer == town.Tile.Layer && Find.WorldGrid.ApproxDistanceInTiles(s.Tile, town.Tile) <= 15f)
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, town.Tile)).FirstOrDefault();
            if (site != null && !site.Label.NullOrEmpty())
            {
                f.Tag("town:site", 1f);
                f.Value("SITE", Find.ActiveLanguageWorker.WithDefiniteArticle(site.Label), "site:" + site.ID);
            }
            if (p == null) return;
            foreach (var q in Find.QuestManager.QuestsListForReading)
            {
                if (q == null || q.hidden || q.State != QuestState.NotYetAccepted || q.name.NullOrEmpty() || !q.InvolvedFactions.Contains(p)) continue;
                f.Tag("town:quest", 1f);
                f.Value("QUEST", q.name);
                break;
            }
        }

        // ---------------- Wars and peace ----------------

        /// <summary>A people whose wars count: another people of the world, seen, standing, of people (not one at war with everyone).</summary>
        public static bool Counts(Faction g) =>
            g != null && !g.IsPlayer && !g.Hidden && !g.defeated && !g.temporary && g.def != null && g.def.humanlikeFaction && !g.def.permanentEnemy;

        private static void Wars(TalkFacts f, Faction p)
        {
            var book = OutsiderBook.Instance;
            if (p == null) return;
            int now = Now;
            // Their war: the newest of theirs still on (the game's own relations), with how it has gone since it began.
            WarSeen theirs = null;
            Faction foe = null;
            foreach (var g in Find.FactionManager.AllFactionsListForReading)
            {
                if (!Counts(g) || g == p || !p.HostileTo(g)) continue;
                var w = book?.wars.FirstOrDefault(x => x.end < 0 && x.Of(p) && x.Of(g));
                if (foe == null || w != null && (theirs == null || w.start > theirs.start))
                {
                    foe = g;
                    theirs = w;
                }
            }
            if (foe != null)
            {
                bool fresh = theirs != null && theirs.start >= 0 && Days(theirs.start) <= 20f;
                f.Tag("their:war", fresh ? Fresh(Days(theirs.start), 20f, 2.5f) : 1f);
                f.Value("WAR", foe.Name, "war:" + foe.loadID);
                if (fresh) f.Tag("war:new", Fresh(Days(theirs.start), 20f, 2.5f));
                else if (theirs == null || theirs.start < 0 || Days(theirs.start) >= 60f) f.Tag("war:long");
                if (theirs != null)
                {
                    int ours = Towns(p), them = Towns(foe);
                    int oursThen = theirs.a == p ? theirs.townsA : theirs.townsB, themThen = theirs.a == p ? theirs.townsB : theirs.townsA;
                    if (ours < oursThen) f.Tag("war:losing", 2f);
                    else if (them < themThen) f.Tag("war:winning", 2f);
                    if (theirs.fought >= 0 && Days(theirs.fought) <= 15f) f.Tag("war:fought", Fresh(Days(theirs.fought), 15f, 3f));
                }
                if (foe.HostileTo(Faction.OfPlayer)) f.Tag("war:ourfoe");
                else if (foe.HasGoodwill && foe.PlayerGoodwill >= 25) f.Tag("war:ourfriend", 1f);
            }
            if (book == null) return;
            // A war of theirs that ended lately.
            var peace = book.wars.Where(w => w.end >= 0 && w.Of(p) && Days(w.end) <= 30f && Counts(w.Other(p))).OrderByDescending(w => w.end).FirstOrDefault();
            if (peace != null)
            {
                f.Tag("their:peace", Fresh(Days(peace.end), 30f, 2.5f));
                f.Value("PEACEWITH", peace.Other(p).Name);
            }
            // A war between two other peoples, new this season.
            var other = book.wars.Where(w => w.end < 0 && !w.Of(p) && w.start >= 0 && Days(w.start) <= 30f && Counts(w.a) && Counts(w.b)).OrderByDescending(w => w.start).FirstOrDefault();
            if (other != null)
            {
                f.Tag("world:war", Fresh(Days(other.start), 30f, 1.5f));
                f.Value("WARA", other.a.Name);
                f.Value("WARB", other.b.Name);
            }
            // Two peoples' fighters met on the colony's land.
            var battle = book.wars.Where(w => w.fought >= 0 && Days(w.fought) <= 10f && w.a != null && w.b != null).OrderByDescending(w => w.fought).FirstOrDefault();
            if (battle != null)
            {
                f.Tag("battle:here", Fresh(Days(battle.fought), 10f, 2.5f));
                f.Value("BATTLEA", battle.a.Name);
                f.Value("BATTLEB", battle.b.Name);
                if (battle.Of(p)) f.Tag("battle:theirs", 2.5f);
            }
        }

        /// <summary>How many towns a people has standing now.</summary>
        public static int Towns(Faction g)
        {
            int n = 0;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s.Faction == g && !s.Destroyed)
                    n++;
            return n;
        }

        // ---------------- The world's events ----------------

        /// <summary>
        /// A world event's kind, from its name: sickness, hunger, cold, war, trade, faith, politics, disaster, empire,
        /// beasts, or null.
        /// </summary>
        public static string Kind(string defName)
        {
            string n = defName ?? "";
            foreach (var (kind, words) in Kinds)
                foreach (var w in words)
                    if (n.Contains(w))
                        return kind;
            return null;
        }

        /// <summary>The kinds, in the order they're tried, and the words in a world event's name that make one.</summary>
        private static readonly (string kind, string[] words)[] Kinds =
        {
            ("sickness", new[] { "Plague", "Murrain", "Fever", "Cough", "Sick", "Pox" }),
            ("hunger", new[] { "Famine", "Hunger", "Locust", "Drought", "Blight" }),
            ("cold", new[] { "HardWinter", "IceRoad", "Cold", "Freeze" }),
            ("empire", new[] { "Imperial", "Levy", "Lords" }),
            ("war", new[] { "War", "Feud", "Mercenar", "Bandit", "Claimant", "Deserter", "Raid" }),
            ("trade", new[] { "Trade", "Fair", "Market", "Rush", "Road", "Toll", "Embargo", "Scarce", "Lanes", "Waystation", "Crossroads" }),
            ("faith", new[] { "Pilgrim", "Holy", "Prophet", "Blessed" }),
            ("politics", new[] { "Succession", "Votes", "Peace", "Moot", "Assize", "NewTown" }),
            ("disaster", new[] { "Flood", "Fire", "Comet", "ShipDown" }),
            ("beasts", new[] { "Herd", "Beast", "Migration", "Drovers" })
        };

        private static string SituationName(string name, string label)
        {
            if (!name.NullOrEmpty()) return name;
            if (label.NullOrEmpty()) return null;
            string l = label.ToLowerInvariant();
            return l.StartsWith("the ") || l.StartsWith("a ") || l.StartsWith("an ") ? label : "the " + label;
        }

        private static void Situations(TalkFacts f, Faction p)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return;
            WorldSituation best = null;
            int bestRank = -1;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || s.ctx == null) continue;
                int rank = s.ctx.faction == p && p != null ? 3 : s.ctx.enemy == p && p != null ? 2 : s.ctx.faction == null ? 1 : 0;
                if (rank <= 0) continue;
                if (rank > bestRank || rank == bestRank && best != null && s.startTick > best.startTick)
                {
                    best = s;
                    bestRank = rank;
                }
            }
            if (best != null)
            {
                float days = best.DaysIn;
                float h = bestRank >= 2 ? Fresh(days, 12f, 3f) : Fresh(days, 12f, 1.8f);
                f.Tag("situation", h);
                f.Value("SITUATION", SituationName(best.ctx.name, best.def.label), "ws:" + best.id);
                if (!best.ctx.placeName.NullOrEmpty()) f.Value("SITUATIONPLACE", best.ctx.placeName);
                if (bestRank >= 2) f.Tag("situation:theirs", h);
                f.Tag(best.def.bad ? "situation:bad" : "situation:good");
                if (days <= 3f) f.Tag("situation:new", h);
                else if (days >= 10f) f.Tag("situation:long");
                string kind = Kind(best.def.defName);
                if (kind != null) f.Tag("situation:" + kind, h);
                f.Tag("ws:" + best.def.defName, h);
                if (best.help > 0) f.Tag("situation:helped", 1.5f);
            }
            // One just over, theirs or the region's.
            var ended = ws.history.Where(r => r != null && r.endTick > 0 && Days(r.endTick) <= 15f && (r.faction == null || r.faction == p)).OrderByDescending(r => r.endTick).FirstOrDefault();
            if (ended != null)
            {
                float h = Fresh(Days(ended.endTick), 15f, ended.faction == p ? 2.5f : 1.5f);
                f.Tag("situation:ended", h);
                f.Value("ENDED", SituationName(ended.name, DefDatabase<WorldSituationDef>.GetNamedSilentFail(ended.defName ?? "")?.label ?? ended.label));
                string kind = Kind(ended.defName);
                if (kind != null) f.Tag("ended:" + kind, h);
                if (ended.faction == p && p != null) f.Tag("ended:theirs", h);
                if (ended.helped) f.Tag("ended:helped", 2f);
            }
        }

        private static void Conditions(TalkFacts f, Map map)
        {
            GameCondition best = null;
            foreach (var c in (map?.gameConditionManager?.ActiveConditions ?? new List<GameCondition>()).Concat(Find.World?.gameConditionManager?.ActiveConditions ?? new List<GameCondition>()))
                if (c?.def != null && TalkHooks.IsDisaster(c.def) && (best == null || c.startTick > best.startTick))
                    best = c;
            if (best == null) return;
            f.Tag("condition", 2f);
            f.Tag("condition:" + best.def.defName, 2f);
            f.Value("CONDITION", Find.ActiveLanguageWorker.WithDefiniteArticle(best.Label));
        }

        // ---------------- The colony ----------------

        private static void Colony(TalkFacts f, Pawn c, Faction p)
        {
            var map = c.MapHeld;
            var home = map?.Parent as Settlement;
            f.Value("COLONY", home != null && home.HasName ? home.Name : Faction.OfPlayer?.Name);
            int days = Now / GenDate.TicksPerDay;
            if (days < 30) f.Tag("colony:young");
            else if (days >= 180) f.Tag("colony:old");
            var scene = TalkScene.Of(map);
            var tr = TalkTracker.Instance;
            if (scene != null)
            {
                if (scene.foodDays < 4f) f.Tag("colony:hungry", 1.5f);
                else if (scene.foodDays >= 30f) f.Tag("colony:plenty");
                if (scene.children.Count > 0) f.Tag("colony:children");
                if (scene.tameAnimals >= 5) f.Tag("colony:animals");
                if (scene.prisoners.Count > 0) f.Tag("colony:prisoners");
                if (scene.expecting) f.Tag("colony:expecting", 1f);
                if (scene.bestCook != null) f.Value("COOK", scene.bestCook.LabelShort);
                if (scene.bestArtist != null) f.Value("ARTIST", scene.bestArtist.LabelShort);
                if (scene.leader != null)
                {
                    f.Value("OURLEADER", scene.leader.LabelShort);
                    if (scene.leader == c) f.Tag("us:leader");
                }
                if (!scene.disaster.NullOrEmpty())
                {
                    f.Tag("colony:disaster", 2f);
                    f.Value("DISASTER", scene.disaster);
                }
                if (scene.camp != null && !scene.campName.NullOrEmpty())
                {
                    f.Tag("colony:camp", 1.5f);
                    f.Value("CAMP", scene.campName);
                }
                if (!scene.traderPeople.NullOrEmpty() && scene.traderPeople != p?.Name)
                {
                    f.Tag("colony:traders", 1f);
                    f.Value("TRADERS", scene.traderPeople);
                }
                var nc = scene.newcomers.FirstOrDefault(n => n.pawn != c && n.days < 10f);
                if (nc.pawn != null)
                {
                    f.Tag("colony:newcomer", Fresh(nc.days, 10f, 2f));
                    f.Value("NEWCOMER", nc.pawn.LabelShort);
                }
                if (scene.calendarDay != null && !scene.calendarDay.label.NullOrEmpty())
                {
                    f.Tag("colony:day", 1.5f);
                    f.Tag(scene.calendarWhen == 0 ? "colony:daytoday" : scene.calendarWhen > 0 ? "colony:daysoon" : "colony:daypast", 1.5f);
                    f.Value("DAY", scene.calendarDay.label);
                }
            }
            if (tr != null)
            {
                var raid = tr.Latest("Rimfolk_Topic_Raid", 6f);
                if (raid != null)
                {
                    float d = Days(raid.tick);
                    f.Tag("colony:raid", Fresh(d, 6f, 3f));
                    var parts = (raid.tag ?? "").Split(',');
                    if (parts.Contains("fallen")) f.Tag("colony:raid:fallen", Fresh(d, 6f, 3f));
                    foreach (var k in new[] { "mechs", "insects", "animals", "tribal", "people" })
                        if (parts.Contains(k))
                            f.Tag("colony:raid:" + k);
                    // Who came, as their people's memory of the colony has it.
                    var by = FactionMemory.Instance?.deeds.Where(x => x != null && x.kind == "raided" && x.faction != null && Days(x.tick) <= 7f).OrderByDescending(x => x.tick).FirstOrDefault();
                    if (by != null)
                    {
                        f.Tag("colony:raidby", Fresh(d, 6f, 3f));
                        f.Value("RAIDERS", by.faction.Name);
                        if (by.faction == p) f.Tag("colony:raid:them", 3f);
                        else if (p != null && by.faction.HostileTo(p)) f.Tag("colony:raid:theirfoe", 2.5f);
                    }
                }
                Note(f, tr.Latest("Rimfolk_Topic_Dead", 12f), "colony:death", "DEAD", 12f, 2.5f);
                var dead = tr.Latest("Rimfolk_Topic_Dead", 12f);
                if (dead != null && dead.tag == "battle") f.Tag("colony:death:battle");
                var born = tr.Recent("Rimfolk_Topic_Children", 12f).FirstOrDefault(n => n.tag == "birth");
                Note(f, born, "colony:birth", "BORN", 12f, 2.5f);
                var wed = tr.Recent("Rimfolk_Topic_Love", 12f).FirstOrDefault(n => n.tag == "wedding");
                Note(f, wed, "colony:wedding", "WEDDING", 12f, 2.5f);
                var decree = tr.Recent("Rimfolk_Topic_Council", 7f).FirstOrDefault(n => n.tag == "decree");
                Note(f, decree, "colony:decree", "DECREE", 7f, 1.5f);
                var harvest = tr.Recent("Rimfolk_Topic_Food", 7f).FirstOrDefault(n => n.tag == "harvest");
                if (harvest != null) f.Tag("colony:harvest", Fresh(Days(harvest.tick), 7f, 2f));
                Note(f, tr.Latest("Rimfolk_Topic_Tech", 7f), "colony:research", "RESEARCH", 7f, 1.2f);
                Note(f, tr.Latest("Rimfolk_Topic_Project", 15f), "colony:project", "PROJECT", 15f, 1.2f);
            }
            var offices = CouncilGameComponent.Instance?.offices;
            var speaker = offices?.Speaker;
            if (speaker != null)
            {
                f.Value("SPEAKER", speaker.LabelShort);
                if (Now - offices.lastElectionTick <= GenDate.TicksPerDay * 10) f.Tag("colony:election", Fresh(Days(offices.lastElectionTick), 10f, 2f));
                if (speaker == c) f.Tag("us:speaker");
            }
        }

        private static void Note(TalkFacts f, TalkNote n, string tag, string key, float span, float top)
        {
            if (n == null) return;
            f.Tag(tag, Fresh(Days(n.tick), span, top));
            if (!n.subject.NullOrEmpty()) f.Value(key, FightNames.Spoken(n.subject));
        }

        // ---------------- What their people remember ----------------

        private static void Deeds(TalkFacts f, Pawn c, Faction p)
        {
            var mem = FactionMemory.Instance;
            if (p == null || mem == null || !FactionMemory.On || !FactionMemory.Remembers(p)) return;
            int now = Now;
            var mine = mem.Of(p).Where(d => d != null && d.StrengthAt(now) >= 2f).OrderByDescending(d => d.StrengthAt(now)).ToList();
            FactionDeed good = null, bad = null, owed = null;
            foreach (var d in mine)
            {
                float h = Days(d.tick) <= 15f ? 2f : 1f;
                f.Tag("deed:" + d.kind, h);
                if (d.side == DeedSide.Favor && good == null) good = d;
                else if (d.side == DeedSide.Wrong && bad == null) bad = d;
                else if (d.side == DeedSide.TheirFavor && !d.settled && owed == null) owed = d;
            }
            if (good != null)
            {
                f.Tag("deed:good", 1f);
                // The one {DEEDNAME} and {DEEDBY} come from, by its kind: a piece naming them asks for deedbest:KIND.
                f.Tag("deedbest:" + good.kind, 1f);
                if (!good.name.NullOrEmpty()) f.Value("DEEDNAME", good.name);
                if (good.by != null && KinMemory.Ours(good.by)) f.Value("DEEDBY", good.by.LabelShort);
            }
            if (bad != null)
            {
                f.Tag("deed:bad", 1f);
                f.Tag("wrongbest:" + bad.kind, 1f);
                if (!bad.name.NullOrEmpty()) f.Value("WRONGNAME", bad.name);
            }
            if (owed != null) f.Tag("deed:owed", 1f);
            var friend = KinMemory.Friend(p, out var deed);
            if (friend != null)
            {
                f.Tag("their:friend", 1f);
                f.Value("FRIEND", friend.LabelShort);
            }
            var byYou = KinMemory.DeedBy(p, c);
            if (byYou != null)
            {
                f.Tag("byyou", 2f);
                f.Tag("byyou:" + KinMemory.KindTag(byYou), 2f);
                if (!byYou.name.NullOrEmpty()) f.Value("YOURDEEDNAME", byYou.name);
            }
        }

        // ---------------- Earlier talks, and what became of them ----------------

        private static void Marks(TalkFacts f, Pawn c, Pawn x, Faction p, Settlement town)
        {
            var pair = OutsiderBook.Instance?.Pair(c, x, false);
            if (pair == null || pair.marks.Count == 0) return;
            int now = Now;
            foreach (var s in pair.marks)
            {
                var m = OutsiderTalkMath.DecodeMark(s);
                if (m == null || now - m.tick < GenDate.TicksPerHour * 12) continue;
                var mf = new MarkFacts { key = m.key, subject = m.subject, fact = m.fact };
                mf.tags.Add("talked:" + m.key);
                mf.heat["talked:" + m.key] = 1f;
                f.tags.Add("talked:" + m.key);
                string ago = OutsiderTalkMath.AgoTag(m.tick, now);
                if (ago != null) mf.tags.Add(ago);
                if (!m.subject.NullOrEmpty()) mf.values["THEN"] = m.subject;
                Since(mf, m, p, town);
                f.marks.Add(mf);
            }
        }

        /// <summary>What became of the fact a mark remembers: still so (then:still), over (then:over), the one asked about dead (then:dead).</summary>
        private static void Since(MarkFacts mf, TalkMark m, Faction p, Settlement town)
        {
            string fact = m.fact;
            if (fact.NullOrEmpty()) return;
            var parts = fact.Split(':');
            void Then(string tag, float heat)
            {
                mf.tags.Add(tag);
                mf.heat[tag] = heat;
            }
            switch (parts[0])
            {
                case "ws":
                {
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    bool on = WorldSituations.Instance?.Running.Any(s => s.id == id) ?? false;
                    if (on) Then("then:still", 1.5f);
                    else Then("then:over", 3f);
                    break;
                }
                case "war":
                {
                    var g = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => parts.Length > 1 && x.loadID.ToString() == parts[1]);
                    if (g == null || p == null) return;
                    if (g.defeated) Then("then:over", 3f);
                    else if (p.HostileTo(g)) Then("then:still", 1.5f);
                    else Then("then:over", 3f);
                    break;
                }
                case "ill":
                {
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    var n = NotableBook.Instance?.Get(id);
                    if (n == null || !n.Alive)
                    {
                        if (n != null || NotableBook.Instance?.losses.Any(l => l.name == m.subject) == true) Then("then:dead", 3f);
                        return;
                    }
                    var hs = n.pawn.health?.hediffSet;
                    bool sick = hs != null && hs.hediffs.Any(h => h?.def != null && h.Visible && (h.def.makesSickThought || h.def.lethalSeverity > 0f && h.def.isBad && !(h is Hediff_Injury)));
                    if (sick || (n.pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.6f) Then("then:still", 1.5f);
                    else Then("then:over", 3f);
                    break;
                }
                case "head":
                case "leader":
                {
                    // head:townId:pawnId, leader:factionId:pawnId: the same one leads still, or someone new does.
                    int pid = parts.Length > 2 && int.TryParse(parts[2], out int i) ? i : -1;
                    Pawn now = null;
                    if (parts[0] == "leader") now = p?.leader;
                    else if (town != null) now = NotableBook.Instance?.notables.FirstOrDefault(n => n.town == town.ID && n.role == NotableRole.Head && n.Alive)?.pawn;
                    if (now == null) return;
                    if (now.thingIDNumber == pid) Then("then:still", 1f);
                    else
                    {
                        Then("then:changed", 3f);
                        mf.values["NOW"] = now.LabelShort;
                    }
                    break;
                }
                case "site":
                {
                    int id = parts.Length > 1 && int.TryParse(parts[1], out int i) ? i : -1;
                    bool there = Find.WorldObjects.Sites.Any(s => s.ID == id);
                    Then(there ? "then:still" : "then:over", there ? 1f : 2.5f);
                    break;
                }
            }
        }
    }
}
