using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The providers of their town: the town, the land around it, its notables, the places and work near it.
    public static partial class Facts
    {
        // ---------------- Their town ----------------

        private static void Town(FactSet f, Pawn x, Faction p, Settlement town)
        {
            // From the colony they are at (their own colony's distance), else the seat (ManyColoniesMath.OutsiderHome).
            var map = ColonyState.Several && Homes.IsHome(x.MapHeld) ? x.MapHeld : HappeningTracker.HomeMap() ?? x.MapHeld;
            if (town != null)
            {
                f.Tag("town");
                f.Value("TOWN", town.Name);
                f.Thing("TOWN", Refs.Of(town).ToString());
                if (map != null)
                {
                    float tiles = LivingEvents.Distance(map.Tile, town.Tile);
                    float days = NotableMath.TravelDays(tiles, (int)(p?.def?.techLevel ?? TechLevel.Industrial));
                    if (days <= 1.2f) f.Tag("town:near");
                    else if (days >= 4f) f.Tag("town:far");
                }
            }
            if (p == null) return;
            // What their people are short of, from a hard time that falls on them.
            foreach (var s in WorldSituations.Instance?.Running ?? Enumerable.Empty<WorldSituation>())
            {
                if (s?.def == null || !s.def.bad || s.ctx?.faction != p) continue;
                string kind = Kind(s.def.defName);
                // A sickness in the herds (the murrain) leaves them short of milk and meat, not of medicine.
                bool herds = s.def.defName == "Council_WS_Murrain";
                if (kind == "hunger" || herds) f.Tag("town:short:food", 1.5f);
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

        private static void Land(FactSet f, PlanetTile tile)
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

        private static void Notables(FactSet f, Pawn x, Faction p, Settlement town)
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

        private static void Places(FactSet f, Settlement town, Faction p)
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
            // Work for or with their people the colony took on and is still about.
            foreach (var q in Find.QuestManager.QuestsListForReading)
            {
                if (q == null || q.hidden || q.State != QuestState.Ongoing || q.name.NullOrEmpty() || !q.InvolvedFactions.Contains(p)) continue;
                f.Tag("quest:ongoing", 1.2f);
                f.Value("QUESTNOW", q.name, "quest:" + q.id);
                break;
            }
        }
    }
}
