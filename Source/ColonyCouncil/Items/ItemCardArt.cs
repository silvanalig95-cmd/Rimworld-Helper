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
    /// The item card's reading of a work of art (Rimfolk's look, stage 3, point 5, step 2): anything with the game's art comp,
    /// held, minified or placed. Its beauty and worth by the game's stats (for a kind, in its stuff at each quality), the tale
    /// it shows (the art comp's title, artist and description, the tale's own name and date), and where it stands. A placed
    /// piece is judged in the room its own cell belongs to: the room's beauty and impressiveness with it are the game's own
    /// (Room.GetStat), without it the game's own sums (ItemCardMath.Without: its beauty over the room's beauty divisor and its
    /// worth taken out, space and cleanliness kept) applied as a difference to the game's number; the verdict is the game's
    /// impressiveness stage. Outdoors (or not placed) it lifts no room, and the card names the colony's room it would lift
    /// most. The rules that care: a noble's throne room or bedroom it stands in, a faith's precepts and memes on art, the
    /// artistic meditation focus. Read when the card opens and every ten seconds while it stays open.
    /// </summary>
    public static partial class ItemCardLive
    {
        /// <summary>A work of art's own numbers, read once a kind.</summary>
        private sealed class ArtPart
        {
            public bool smallArt, meditation;
        }

        private static readonly AccessTools.FieldRef<TaleReference, Tale> TaleOf = TaleRef();
        private static SimpleCurve beautyCurve;
        private static bool beautyCurveRead;

        private static AccessTools.FieldRef<TaleReference, Tale> TaleRef()
        {
            try
            {
                return AccessTools.FieldRefAccess<TaleReference, Tale>("tale");
            }
            catch
            {
                return null;
            }
        }

        private static MeditationFocusDef Artistic => DefDatabase<MeditationFocusDef>.GetNamedSilentFail("Artistic");

        private static void ReadArt(DefPart p)
        {
            var d = p.def;
            var ap = p.art = new ArtPart();
            ap.smallArt = ThingRoles.Fits(ThingRole.SmallArt, d);
            var focus = d.GetCompProperties<CompProperties_MeditationFocus>()?.focusTypes;
            var artistic = Artistic;
            ap.meditation = focus != null && artistic != null && focus.Contains(artistic);
        }

        /// <summary>The game's room beauty divisor for a room of so many cells (its own curve, read once; Rimfolk's copy of it when it can't be read).</summary>
        private static float BeautyDivisor(int cells)
        {
            if (!beautyCurveRead)
            {
                beautyCurveRead = true;
                try
                {
                    beautyCurve = AccessTools.Field(typeof(RoomStatWorker_Beauty), "CellCountCurve")?.GetValue(null) as SimpleCurve;
                }
                catch
                {
                    beautyCurve = null;
                }
            }
            float v = beautyCurve != null ? beautyCurve.Evaluate(cells) : ItemCardMath.BeautyDivisor(cells);
            return v > 0f ? v : ItemCardMath.BeautyDivisor(cells);
        }

        private static float StatOf(StatDef stat, ThingDef d, ThingDef stuff, QualityCategory q)
        {
            try
            {
                return stat.Worker.GetValue(StatRequest.For(d, stuff ?? (d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null), q));
            }
            catch
            {
                return 0f;
            }
        }

        // ---------------------------------------------------------------- the live parts

        private static void LiveArt(ItemCardModel m, DefPart p)
        {
            var a = m.facts.ArtPiece ??= new ArtFacts();
            var ap = p.art ?? new ArtPart();
            var t = m.inner;
            a.SmallArt = ap.smallArt;
            a.MeditationFocus = ap.meditation;
            if (t != null && t.TryGetQuality(out var q))
            {
                a.Quality = q.GetLabel();
                a.QualityIndex = (int)q;
            }
            else
            {
                a.Quality = "";
                a.QualityIndex = -1;
            }
            try
            {
                a.Beauty = t != null ? t.GetStatValue(StatDefOf.Beauty) : StatOf(StatDefOf.Beauty, m.def, m.stuff, QualityCategory.Normal);
                a.Worth = t != null ? t.MarketValue : StatOf(StatDefOf.MarketValue, m.def, m.stuff, QualityCategory.Normal);
                a.Flammability = t != null ? t.GetStatValue(StatDefOf.Flammability) : StatOf(StatDefOf.Flammability, m.def, m.stuff, QualityCategory.Normal);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading a work of art", e);
            }
            a.Placed = t != null && t.Spawned && t == m.thing;
            if (!m.storyRead)
            {
                m.storyRead = true;
                Story(m, a);
                ByQuality(m);
            }
        }

        /// <summary>What kind of art: "Art, excellent, of marble".</summary>
        private static string ArtKindWords(ItemCardModel m, DefPart p)
        {
            var a = m.facts.ArtPiece;
            var parts = new List<string> { W("CC_ItemCard_Kind_Art") };
            if (a != null && a.Quality.Length > 0) parts.Add(a.Quality);
            if (m.stuff != null) parts.Add(W("CC_ItemCard_OfStuff", (m.stuff.label ?? m.stuff.defName).Named("STUFF")));
            return string.Join(", ", parts);
        }

        /// <summary>The tale it shows: its title, its artist, the tale's own name and date, and the game's description of the image (read once a card).</summary>
        private static void Story(ItemCardModel m, ArtFacts a)
        {
            m.story.Clear();
            m.storyText = null;
            a.Story = false;
            a.Title = a.Artist = a.Tale = "";
            var comp = m.inner?.TryGetComp<CompArt>();
            if (comp == null) return;
            try
            {
                if (!comp.Active) return;
                a.Title = comp.Title ?? "";
                a.Artist = comp.AuthorName.Resolve() ?? "";
                var ref0 = comp.TaleRef;
                var tale = ref0 != null && !ref0.IsNullTale && TaleOf != null ? TaleOf(ref0) : null;
                if (a.Title.Length > 0) m.story.Add((W("CC_ItemCard_Row_Title"), a.Title));
                if (a.Artist.Length > 0) m.story.Add((W("CC_ItemCard_Row_Artist"), a.Artist));
                if (tale != null)
                {
                    a.Story = true;
                    a.Tale = (tale.ShortSummary ?? tale.def?.label ?? "").UncapitalizeFirst();
                    string when = "";
                    try
                    {
                        var map = m.map ?? Find.CurrentMap;
                        when = GenDate.DateFullStringAt(tale.date, map != null ? Find.WorldGrid.LongLatOf(map.Tile) : Vector2.zero);
                    }
                    catch
                    {
                        // No date to give.
                    }
                    var who = tale.DominantPawn;
                    string ev = tale.ShortSummary ?? tale.def?.LabelCap ?? "";
                    if (who != null && ev.IndexOf(who.LabelShort, StringComparison.Ordinal) < 0) ev += " (" + who.LabelShort + ")";
                    if (when.Length > 0) ev += ", " + when;
                    m.story.Add((W("CC_ItemCard_Row_Tale"), ev));
                }
                else m.story.Add((W("CC_ItemCard_Row_Tale"), W("CC_ItemCard_NoTale")));
                m.storyText = comp.GenerateImageDescription().Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card reading a work of art's tale", e);
            }
        }

        /// <summary>Its beauty and worth at each quality in its stuff, by the game's own stats (once a card).</summary>
        private static void ByQuality(ItemCardModel m)
        {
            m.byQuality = null;
            if (!m.def.HasComp(typeof(CompQuality))) return;
            var parts = new List<string>();
            foreach (var q in new[] { QualityCategory.Poor, QualityCategory.Normal, QualityCategory.Good, QualityCategory.Excellent, QualityCategory.Masterwork, QualityCategory.Legendary })
            {
                float b = StatOf(StatDefOf.Beauty, m.def, m.stuff, q);
                parts.Add(q.GetLabel() + " " + Mathf.RoundToInt(b));
            }
            m.byQuality = string.Join(", ", parts);
        }

        // ---------------------------------------------------------------- the colony

        private static void ColonyArt(ItemCardModel m, DefPart p)
        {
            var a = m.facts.ArtPiece ??= new ArtFacts();
            var map = m.map;
            a.Indoors = a.RoomMatters = false;
            a.Room = a.Role = a.StageWith = a.StageWithout = a.BestRoom = a.BestStageNow = a.BestStageWith = a.Noble = a.NobleRoom = a.Faiths = "";
            a.BeautyWith = a.BeautyWithout = a.ImpressWith = a.ImpressWithout = a.BestNow = a.BestWith = 0f;
            a.StageIndexWith = a.StageIndexWithout = a.BestIndexNow = a.BestIndexWith = a.NobleNeeds = a.Meditators = 0;
            m.rooms.Clear();
            m.rules.Clear();
            if (map == null) return;
            var stat = RoomStatDefOf.Impressiveness;
            Room room = null;
            if (a.Placed)
            {
                try
                {
                    room = m.inner.GetRoom();
                }
                catch
                {
                    room = null;
                }
            }
            if (room != null && Indoors(room))
            {
                a.Indoors = true;
                a.Room = RoomWord(room);
                a.Role = room.Role?.defName ?? "";
                a.RoomMatters = ItemCardMath.RoleMatters(a.Role);
                var with = Numbers(room);
                var without = ItemCardMath.Without(with, a.Beauty, a.Worth, BeautyDivisor(with.Cells));
                a.BeautyWith = with.Beauty;
                a.BeautyWithout = without.Beauty;
                a.ImpressWith = with.Impressiveness;
                a.ImpressWithout = without.Impressiveness;
                a.StageIndexWith = stat.GetScoreStageIndex(with.Impressiveness);
                a.StageIndexWithout = stat.GetScoreStageIndex(without.Impressiveness);
                a.StageWith = stat.GetScoreStage(with.Impressiveness)?.label ?? "";
                a.StageWithout = stat.GetScoreStage(without.Impressiveness)?.label ?? "";
            }
            else BestRooms(m, a, map);
            var people = map.mapPawns.FreeColonistsSpawned.Where(x => x.RaceProps.Humanlike).ToList();
            if (room != null && a.Indoors) Nobles(m, a, room, people);
            Faiths(m, a, people);
            if (a.MeditationFocus)
            {
                var artistic = Artistic;
                foreach (var x in people)
                {
                    try
                    {
                        if (x.HasPsylink && artistic != null && MeditationFocusTypeAvailabilityCache.PawnCanUse(x, artistic)) a.Meditators++;
                    }
                    catch
                    {
                        // Read as not.
                    }
                }
                if (a.Meditators > 0) m.rules.Add(W("CC_ItemCard_RuleMeditation", a.Meditators.Named("N")));
            }
        }

        private static bool Indoors(Room r) => r != null && !r.PsychologicallyOutdoors && r.ProperRoom && !r.IsDoorway;

        private static string RoomWord(Room r)
        {
            try
            {
                string label = r.Role?.PostProcessedLabel(r) ?? r.Role?.label;
                return label.NullOrEmpty() ? W("CC_ItemCard_RoomPlain") : label;
            }
            catch
            {
                return W("CC_ItemCard_RoomPlain");
            }
        }

        private static RoomNumbers Numbers(Room r) => new RoomNumbers
        {
            Wealth = r.GetStat(RoomStatDefOf.Wealth), Beauty = r.GetStat(RoomStatDefOf.Beauty), Space = r.GetStat(RoomStatDefOf.Space),
            Cleanliness = r.GetStat(RoomStatDefOf.Cleanliness), Impressiveness = r.GetStat(RoomStatDefOf.Impressiveness), Cells = r.CellCount
        };

        /// <summary>Not placed or outdoors: the colony's rooms whose impressiveness gives a mood, each with the piece added by the game's sums; the one it lifts most first.</summary>
        private static void BestRooms(ItemCardModel m, ArtFacts a, Map map)
        {
            var stat = RoomStatDefOf.Impressiveness;
            var found = new List<(Room room, RoomNumbers now, RoomNumbers with, int from, int to)>();
            int looked = 0;
            foreach (var r in map.regionGrid.AllRooms)
            {
                if (looked >= 80) break;
                try
                {
                    if (!Indoors(r) || r.CellCount > 2000 || !ItemCardMath.RoleMatters(r.Role?.defName)) continue;
                    looked++;
                    var now = Numbers(r);
                    var with = ItemCardMath.With(now, a.Beauty, a.Worth, BeautyDivisor(now.Cells));
                    found.Add((r, now, with, stat.GetScoreStageIndex(now.Impressiveness), stat.GetScoreStageIndex(with.Impressiveness)));
                }
                catch
                {
                    // A room that can't be read: left out.
                }
            }
            var order = found.OrderByDescending(x => x.to - x.from).ThenByDescending(x => x.with.Impressiveness - x.now.Impressiveness).Take(3).ToList();
            if (order.Count == 0) return;
            var best = order[0];
            a.BestRoom = RoomWord(best.room);
            a.BestNow = best.now.Impressiveness;
            a.BestWith = best.with.Impressiveness;
            a.BestIndexNow = best.from;
            a.BestIndexWith = best.to;
            a.BestStageNow = stat.GetScoreStage(best.now.Impressiveness)?.label ?? "";
            a.BestStageWith = stat.GetScoreStage(best.with.Impressiveness)?.label ?? "";
            foreach (var x in order)
            {
                var room = x.room;
                string from = stat.GetScoreStage(x.now.Impressiveness)?.label ?? "", to = stat.GetScoreStage(x.with.Impressiveness)?.label ?? "";
                m.rooms.Add(new ItemGoLine
                {
                    text = x.to > x.from
                        ? W("CC_ItemCard_RoomWouldRise", RoomWord(room).CapitalizeFirst().Named("ROOM"), from.Named("FROM"), to.Named("TO"), Mathf.RoundToInt(x.with.Impressiveness - x.now.Impressiveness).Named("GAIN"))
                        : W("CC_ItemCard_RoomWouldStay", RoomWord(room).CapitalizeFirst().Named("ROOM"), from.Named("FROM"), Mathf.RoundToInt(x.with.Impressiveness - x.now.Impressiveness).Named("GAIN")),
                    tip = W("CC_ItemCard_GoRoom_Tip"), go = () => GoToRoom(room), tone = x.to > x.from ? 1 : 0
                });
            }
        }

        private static void GoToRoom(Room r)
        {
            try
            {
                var cells = r?.Cells?.ToList();
                if (cells == null || cells.Count == 0) return;
                Pages.LeaveForMap();
                CameraJumper.TryJump(cells[cells.Count / 2], r.Map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the item card going to a room", e);
            }
        }

        /// <summary>A noble's throne room or bedroom the piece stands in, with the impressiveness their title asks for (Royalty's room requirements).</summary>
        private static void Nobles(ItemCardModel m, ArtFacts a, Room room, List<Pawn> people)
        {
            if (!ModsConfig.RoyaltyActive) return;
            foreach (var x in people)
            {
                try
                {
                    if (x.royalty == null) continue;
                    var throne = x.royalty.HighestTitleWithThroneRoomRequirements();
                    if (throne != null && x.ownership?.AssignedThrone != null && x.ownership.AssignedThrone.GetRoom() == room)
                        Noble(m, a, x, W("CC_ItemCard_ThroneRoom"), throne.def.throneRoomRequirements);
                    var bed = x.royalty.HighestTitleWithBedroomRequirements();
                    if (bed != null && x.ownership?.OwnedRoom == room)
                        Noble(m, a, x, W("CC_ItemCard_Bedroom"), bed.def.GetBedroomRequirements(x));
                }
                catch
                {
                    // A title that can't be read: left out.
                }
            }
        }

        private static void Noble(ItemCardModel m, ArtFacts a, Pawn x, string roomWord, IEnumerable<RoomRequirement> reqs)
        {
            if (reqs == null) return;
            foreach (var req in reqs)
            {
                if (!(req is RoomRequirement_Impressiveness imp)) continue;
                if (a.NobleNeeds < imp.impressiveness)
                {
                    a.Noble = x.LabelShort;
                    a.NobleRoom = roomWord;
                    a.NobleNeeds = imp.impressiveness;
                }
                bool with = a.ImpressWith >= imp.impressiveness, without = a.ImpressWithout >= imp.impressiveness;
                m.rules.Add(W(with && !without ? "CC_ItemCard_RuleNobleNeedsIt" : with ? "CC_ItemCard_RuleNobleMet" : "CC_ItemCard_RuleNobleShort", x.LabelShort.Named("WHO"), roomWord.Named("ROOM"),
                    imp.impressiveness.Named("NEED"), Mathf.RoundToInt(a.ImpressWith).Named("WITH"), Mathf.RoundToInt(a.ImpressWithout).Named("WITHOUT")));
            }
        }

        /// <summary>Each faith at home with precepts or memes about art or beauty (by their names' words, the game's and the mods').</summary>
        private static void Faiths(ItemCardModel m, ArtFacts a, List<Pawn> people)
        {
            if (!ModsConfig.IdeologyActive) return;
            var seen = new HashSet<Ideo>();
            var faiths = new List<string>();
            foreach (var x in people)
            {
                Ideo ideo;
                try
                {
                    ideo = x.Ideo;
                }
                catch
                {
                    continue;
                }
                if (ideo == null || !seen.Add(ideo)) continue;
                var views = new List<string>();
                try
                {
                    foreach (var meme in ideo.memes ?? new List<MemeDef>())
                        if (meme != null && ItemCardMath.ArtRelated(meme.defName)) views.Add(meme.LabelCap);
                    foreach (var pr in ideo.PreceptsListForReading)
                        if (pr?.def != null && (ItemCardMath.ArtRelated(pr.def.defName) || ItemCardMath.ArtRelated(pr.def.issue?.defName))) views.Add(pr.LabelCap);
                }
                catch
                {
                    // A faith that can't be read: left out.
                }
                if (views.Count == 0) continue;
                faiths.Add(ideo.name);
                m.rules.Add(W("CC_ItemCard_RuleFaith", ideo.name.Named("FAITH"), string.Join(", ", views.Distinct()).Named("VIEWS")));
            }
            if (faiths.Count > 0) a.Faiths = W("CC_ItemCard_FaithsCare", string.Join(", ", faiths).Named("FAITHS"));
        }

        // ---------------------------------------------------------------- the lines

        private static void RowsArt(ItemCardModel m, DefPart p)
        {
            var f = m.facts;
            var a = f.ArtPiece;
            m.art.Clear();
            m.room.Clear();
            m.keepSell = "";
            if (a == null) return;
            m.art.Add((W("CC_ItemCard_Row_Beauty"), Mathf.RoundToInt(a.Beauty) + (a.Quality.Length > 0 ? " · " + W("CC_ItemCard_AtQuality", a.Quality.Named("QUALITY")) : "")));
            if (!m.byQuality.NullOrEmpty()) m.art.Add((W("CC_ItemCard_Row_ByQuality"), m.byQuality));
            m.art.Add((W("CC_ItemCard_Row_Value"), a.Worth.ToStringMoney()));
            if (m.stuff != null) m.art.Add((W("CC_ItemCard_Row_Material"), m.stuff.LabelCap));
            if (a.Flammability > 0.01f) m.art.Add((W("CC_ItemCard_Row_Flammability"), a.Flammability.ToStringPercent()));
            if (!f.HasColony) return;

            // Where it stands and what it does there.
            if (a.Placed && a.Indoors)
            {
                m.room.Add((W("CC_ItemCard_Row_Room"), a.Room.CapitalizeFirst() + " · " + W(a.RoomMatters ? "CC_ItemCard_RoomDoes_" + a.Role : "CC_ItemCard_RoomDoes_Other")));
                m.room.Add((W("CC_ItemCard_Row_RoomBeauty"), W("CC_ItemCard_WithWithout", a.BeautyWith.ToString("0.#").Named("WITH"), a.BeautyWithout.ToString("0.#").Named("WITHOUT"))));
                m.room.Add((W("CC_ItemCard_Row_Impressive"), W("CC_ItemCard_ImpressWithWithout", Mathf.RoundToInt(a.ImpressWith).Named("WITH"), a.StageWith.Named("STAGEWITH"),
                    Mathf.RoundToInt(a.ImpressWithout).Named("WITHOUT"), a.StageWithout.Named("STAGEWITHOUT"))));
                m.keepSell = a.StageIndexWithout < a.StageIndexWith
                    ? W("CC_ItemCard_KeepIt", a.Worth.ToStringMoney().Named("WORTH"), a.Room.Named("ROOM"), a.StageWith.Named("WITH"), a.StageWithout.Named("WITHOUT"))
                    : ItemCardMath.BetterWithout(a)
                        ? W("CC_ItemCard_SellBetter", a.Worth.ToStringMoney().Named("WORTH"), a.Room.Named("ROOM"), Mathf.RoundToInt(a.ImpressWithout - a.ImpressWith).Named("GAIN"))
                        : W("CC_ItemCard_SellFree", a.Worth.ToStringMoney().Named("WORTH"), a.Room.Named("ROOM"), a.StageWith.Named("WITH"), Mathf.RoundToInt(a.ImpressWith - a.ImpressWithout).Named("DROP"));
            }
            else
            {
                m.room.Add((W("CC_ItemCard_Row_Room"), W(a.Placed ? "CC_ItemCard_Outdoors" : "CC_ItemCard_NotPlaced")));
                m.keepSell = a.BestRoom.Length > 0 && a.BestIndexWith > a.BestIndexNow
                    ? W("CC_ItemCard_PlaceIt", a.BestRoom.Named("ROOM"), a.BestStageNow.Named("FROM"), a.BestStageWith.Named("TO"), a.Worth.ToStringMoney().Named("WORTH"))
                    : W("CC_ItemCard_SellIt", a.Worth.ToStringMoney().Named("WORTH"));
            }
        }

    }
}
