using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods in the letters (docs/design/gods.md, stage 2, brainstorm item 17):
    /// <list type="bullet">
    /// <item>Their letters tell of their god by what is true at their town (<see cref="Shape"/>, from Letters/LetterFormShapes.cs):
    /// a sign of its favour when the town thrives, of its anger when it struggles, sickens or mourns, a myth of it and the god
    /// it is tied to, the war they fight as their gods' own quarrel. The sermons, stories and chronicles have lines for each
    /// (tagged god:miracle, god:curse, god:myth, god:holywar; {SIGNGOD}, {MYTHGOD}, {MYTHOTHER}, {ENEMYGOD}).</item>
    /// <item>The colony's letter slights their god (<see cref="ShapeOurs"/>): written by someone whose own god is their god's
    /// rival, or by a scoffer, now and then; the words are in the letter, and when they read it their answer says they took
    /// offence (<see cref="Answered"/>, from Letters/LetterForm.cs): their goodwill and their god's favour fall.</item>
    /// <item>A friend's invitation to their god's festival (<see cref="Daily"/>): a gift for the festival carried out to the
    /// gate for their messenger, or the colony's regrets.</item>
    /// </list>
    /// At the letters' own rates: nothing here writes a letter that wasn't being written, but the invitation.
    /// </summary>
    public static class GodLetters
    {
        // ================= Their letter =================

        /// <summary>What their letter tells of their god: tags and values for its sermon, story or chronicle.</summary>
        public static void Shape(LetterSpec s, Pawn writer, Faction f)
        {
            if (s == null || !Gods.On || f == null || f.IsPlayer) return;
            try
            {
                var g = Gods.SwearBy(writer) ?? Gods.Of(f).FirstOrDefault(x => x.kind != GodKind.Dead);
                if (g == null) return;
                if (Gods.ColonyGods(Homes.Seat).Contains(g)) s.tags.Add("god:shared");
                bool good = s.tags.Contains("town:flourishing") || s.tags.Contains("town:thriving") || s.tags.Contains("town:situation:good");
                bool bad = s.tags.Contains("town:struggling") || s.tags.Contains("town:ill") || s.tags.Contains("town:loss") || s.tags.Contains("town:situation:bad");
                God enemyGod = null;
                Faction enemy = null;
                if (s.tags.Contains("their:war") && s.values.TryGetValue("WAR", out var war))
                {
                    enemy = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x != null && x.Name == war);
                    if (enemy != null) enemyGod = Gods.Of(enemy).FirstOrDefault(x => Gods.Rivals(g).Contains(x));
                }
                var tie = Gods.TiesOf(g).Select(t => (t, other: Gods.ById(t.Other(g.id)))).FirstOrDefault(x => x.other != null && x.other.kind != GodKind.Dead);
                string tells = GodWorldMath.LetterTells(good, bad, enemyGod != null, tie.other != null, Rand.Value);
                if (tells == null) return;
                s.tags.Add("god:" + tells);
                s.values["SIGNGOD"] = g.name;
                s.values["SIGNGODEPITHET"] = g.epithet ?? "";
                switch (tells)
                {
                    case "holywar":
                        s.values["ENEMYGOD"] = enemyGod.name;
                        s.values["ENEMYPEOPLE"] = enemy.Name;
                        Gods.Learn(enemyGod, "letter");
                        break;
                    case "myth":
                        s.values["MYTHGOD"] = g.name;
                        s.values["MYTHOTHER"] = tie.other.name;
                        s.tags.Add("myth:" + tie.t.kind.ToString().ToLowerInvariant());
                        s.tags.Add("myth:" + GodWorldMath.MythStage(TieActs(g, tie.other)));
                        Gods.Learn(tie.other, "letter");
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: their letter's god", e);
            }
        }

        /// <summary>The acts the colony remembers of two gods' tie (how far their myth has come).</summary>
        public static int TieActs(God a, God b) =>
            a == null || b == null ? 0 : Gods.ActsOf(a).Count(x => x.kind == GodActKind.Tie && x.other == b.id) + Gods.ActsOf(b).Count(x => x.kind == GodActKind.Tie && x.other == a.id);

        // ================= The colony's letter that slights their god =================

        /// <summary>
        /// The colony's letter, as its writer finishes it: now and then a writer whose own god is their god's rival, or one who
        /// scoffs at gods, lets a slighting word in. It is in the letter (a paragraph after the heart), the narrator says it
        /// won't sit well, and the slight waits for their reading.
        /// </summary>
        public static void ShapeOurs(LetterSpec s, NeighbourLetter l, Notable n, Pawn writer)
        {
            if (s == null || l == null || n?.faction == null || writer == null || !Gods.On || s.war) return;
            var book = GodWorld.Book;
            if (book == null) return;
            try
            {
                var theirs = Gods.Of(n.faction).FirstOrDefault(x => x.kind != GodKind.Dead && Gods.Known(x));
                if (theirs == null) return;
                var mine = Gods.SwearBy(writer);
                bool rival = mine != null && Gods.Rivals(theirs).Contains(mine);
                bool scoffer = mine == null || ModsConfig.IdeologyActive && writer.ideo != null && writer.ideo.Certainty < 0.25f;
                if (!GodWorldMath.Slights(rival, scoffer, s.purpose == "condolences", Rand.Value)) return;
                string key = (rival ? "CC_GW_Slight_Rival_" : "CC_GW_Slight_Scoff_") + Rand.RangeInclusive(0, 2);
                s.more.Add(key.Translate(theirs.name.Named("THEIRGOD"), (mine?.name ?? "").Named("OURGOD"), (theirs.epithet ?? "").Named("THEIREPITHET")).Resolve());
                s.outcomes.Add("CC_GW_Slight_Note".Translate(writer.LabelShort.Named("WRITER"), theirs.name.Named("GOD"), (n.townName ?? n.faction.Name).Named("TOWN")).Resolve());
                book.Add(new GodDoing
                {
                    kind = "slight", god = theirs.id, other = mine?.id ?? -1, faction = n.faction, who = writer, count = l.id, home = Homes.KeyOf(GodWorld.HomeOr(writer.MapHeld)),
                    what = writer.LabelShort, until = GodWorld.Now + 60 * GodWorldMath.DayTicks
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a slight in the colony's letter", e);
            }
        }

        /// <summary>
        /// Their answer to a letter that slighted their god: they took offence (the narrator says so under it), their
        /// goodwill falls (never into war), their god is displeased (a scoffer's words count more with a proud god), and the
        /// colony's writer is remembered by that god.
        /// </summary>
        public static void Answered(LetterSpec s, NeighbourLetter l, Notable n)
        {
            var book = GodWorld.Book;
            if (book == null || s == null || l == null) return;
            try
            {
                var d = book.Of("slight").FirstOrDefault(x => x.count == l.id);
                if (d == null) return;
                d.done = true;
                var g = Gods.ById(d.god);
                var f = d.faction ?? n?.faction;
                if (g == null || f == null) return;
                s.outcomes.Add("CC_GW_Slight_Taken".Translate(f.Name.Named("PEOPLE"), g.name.Named("GOD"), (d.what ?? "?").Named("WRITER")).Resolve());
                StoryChains.Goodwill(f, GodWorldMath.SlightGoodwill);
                FactionMemory.Note(f, "slighted", g.name);
                var home = d.Map ?? Homes.Seat;
                Gods.AddFavour(g, home, GodWorldMath.SlightFavour, "CC_GW_Slight_Why".Translate((d.what ?? "?").Named("WRITER")).Resolve(), FavourSource.Deed, GodOffence.Scoffing);
                // The god remembers who wrote it: a storm god's lightning finds that roof, a wild god's beasts that trail.
                if (d.who != null && !d.who.Dead) GodHealth.Grudge(g, d.who, home, "slight");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: a slight read", e);
            }
        }

        // ================= A festival's invitation =================

        /// <summary>
        /// Now and then a friend invites the colony to their god's festival: a people at peace whose god the colony knows, and
        /// who think well enough of it. Their letter asks for nothing but the colony's word; a gift for the festival goes out
        /// to the gate, where their messenger takes it up the road.
        /// </summary>
        public static void Daily(GodWorldBook b, int now)
        {
            if (!GodWorld.CanAsk || !b.Gap("invite", GodWorldMath.InviteGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.InviteDays, GodWorld.Factor()))) return;
            var home = Homes.Seat;
            if (home == null) return;
            var f = Find.FactionManager.AllFactionsListForReading.Where(x => Arrivals.Usable(x) && x.HasGoodwill && x.PlayerGoodwill >= 10)
                .Where(x => Gods.Of(x).Any(g => Gods.Known(g) && g.kind != GodKind.Dead)).RandomElementWithFallback();
            if (f == null) return;
            var god = Gods.Of(f).First(g => Gods.Known(g) && g.kind != GodKind.Dead);
            int silver = GodWorldMath.Gift(GodWorld.Wealth(home), GodWorldMath.FestivalSilver);
            string town = LivingEvents.TownOf(f, home) ?? f.Name;
            string text = GodWorld.T("CC_GW_Invite_Text", god, f.Name.Named("PEOPLE"), town.Named("TOWN"), GodWays.Feast(god).Named("FEAST"),
                silver.Named("COUNT"));
            if (Gods.ColonyGods(home).Contains(god)) text += "\n\n" + GodWorld.T("CC_GW_Invite_Ours", god);
            else if (Gods.Patron(home) is God patron && Gods.Rivals(god).Contains(patron)) text += "\n\n" + GodWorld.T("CC_GW_Invite_Rival", god, patron.name.Named("PATRON"));
            if (GodAsks.Ask(GodAsks.Invite, home, god, GodWorld.T("CC_GW_Invite_Label", god), text, null, null, f, null, silver, null, -1, 4f) != null) b.Came("invite");
        }

        public static IEnumerable<GodAnswer> InviteAnswers(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var f = l.people;
            var map = l.HomeMap;
            if (g == null || f == null || map == null || !Arrivals.Usable(f)) yield break;
            int have = PlayCarry.Have(map, "silver", ThingDefOf.Silver);
            yield return new GodAnswer("CC_GW_Invite_Gift".Translate(l.count.Named("COUNT")).Resolve(), x => SendGift(x),
                have < l.count ? "CC_Dilemma_NotEnough".Translate(l.count, ThingDefOf.Silver.label).ToString() : null);
            yield return new GodAnswer("CC_GW_Invite_Regrets".Translate().Resolve(), x => Regrets(x));
        }

        private static void SendGift(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var f = l.people;
            var map = l.HomeMap;
            if (g == null || f == null || map == null) return;
            var ctx = new DilemmaContext { map = map, faction = f };
            // apart: one of the colony carries the festival's gift out to the gate, where their messenger takes it up the road.
            new DilemmaEffect_CarryOut { what = "silver", count = l.count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = "CC_GW_Invite_GiftLabel".Translate().Resolve() }
                .Apply(ctx);
            StoryChains.Goodwill(f, GodWorldMath.FestivalGoodwill);
            FactionMemory.Note(f, "gaveGift", GodWays.Feast(g));
            Gods.AddFavour(g, map, GodWorldMath.FestivalFavour, GodWorld.T("CC_GW_Invite_Why", g, f.Name.Named("PEOPLE")), FavourSource.Offering);
            GodWorld.Remark(map, IntVec3.Invalid, "festival", g);
        }

        private static void Regrets(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var f = l.people;
            if (f == null) return;
            // A proud people's god notices the empty place at its table; the rest only note it.
            if (g != null && (g.nature == GodNature.Proud || g.nature == GodNature.Jealous)) StoryChains.Goodwill(f, GodWorldMath.FestivalRegrets);
        }
    }
}
