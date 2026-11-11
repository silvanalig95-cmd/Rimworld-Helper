using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods in the world ("Rimfolk", "Gods in the world: ..."). Each part made to come now (its chance and gap
    /// pushed, GodWorld.Force), what is under way listed, and a few things set up for a test: a grudge on the selected
    /// colonist, a fever's blessing, a gift turned, the Hunger's ear. What can't come still says why (no god of the kind,
    /// no relic, no holy place, only believed).
    /// </summary>
    public static class GodWorldDebug
    {
        private static void Say(string text) => Messages.Message("Gods in the world: " + text, MessageTypeDefOf.SilentInput, false);

        private static GodWorldBook Ready()
        {
            if (!Gods.On) Say("the gods are off (the setting \"Gods\").");
            else if (GodWorld.Book == null) Say("nothing kept in this game.");
            else return GodWorld.Book;
            return null;
        }

        /// <summary>Runs a part with every chance and gap pushed; says whether anything new came of it.</summary>
        private static void Push(string what, Action<GodWorldBook> a)
        {
            var b = Ready();
            if (b == null) return;
            int before = b.doings.Count + b.gifts.Count + Find.LetterStack.LettersListForReading.Count + b.lasts.Count;
            int beforeLast = b.lasts.Values.Sum();
            GodWorld.Force = true;
            try
            {
                a(b);
            }
            catch (Exception e)
            {
                Log.Error("[Rimfolk] gods in the world, dev: " + e);
            }
            finally
            {
                GodWorld.Force = false;
            }
            int after = b.doings.Count + b.gifts.Count + Find.LetterStack.LettersListForReading.Count + b.lasts.Count;
            Say(after != before || b.lasts.Values.Sum() != beforeLast ? what + ": done." : what + ": nothing could come (see the log for what is known).");
        }

        [DebugAction("Rimfolk", "Gods in the world: show what is under way (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShow()
        {
            var b = Ready();
            if (b == null) return;
            var sb = new StringBuilder("[Rimfolk] Gods in the world: " + b.doings.Count + " under way, " + b.gifts.Count + " gifts, Hunger " + b.hungerBargains + " bargains / "
                                       + b.hungerTaken + " taken, Sleeper's door " + b.sleeperStep + ".\n");
            foreach (var d in b.doings)
                sb.Append("  ").Append(d.kind).Append(" #").Append(d.id).Append(" god ").Append(Gods.ById(d.god)?.name ?? "-").Append(" other ").Append(Gods.ById(d.other)?.name ?? "-")
                    .Append(" pawn ").Append(d.pawn?.LabelShort ?? "-").Append(" stage ").Append(d.stage).Append(" what ").Append(d.what ?? "-").Append(d.done ? " (done)" : "").AppendLine();
            foreach (var g in b.gifts)
                sb.Append("  gift #").Append(g.id).Append(' ').Append(g.kind).Append(' ').Append(g.what).Append(" from ").Append(Gods.ById(g.god)?.name ?? "?").Append(" to ")
                    .Append(g.pawn?.LabelShort ?? "?").Append(g.cursed ? " (cursed)" : "").Append(g.undone ? " (undone)" : "").AppendLine();
            foreach (var kv in b.lasts) sb.Append("  last ").Append(kv.Key).Append(": ").Append(((GodWorld.Now - kv.Value) / (float)GenDate.TicksPerDay).ToString("0.0")).AppendLine(" days ago");
            foreach (var g in Gods.All.Take(60))
                sb.Append("  ways of ").Append(g.name).Append(": wrath ").Append(GodWays.Wrath(g)).Append(", site ").Append(GodWays.Site(g) ?? "-").Append(", weather ").Append(GodWays.Weather(g) ?? "-")
                    .Append(", classes ").Append(string.Join("/", GodWays.Classes(g))).AppendLine();
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Gods in the world: a festival's invitation", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugInvite() => Push("an invitation", b => GodLetters.Daily(b, GodWorld.Now));

        [DebugAction("Rimfolk", "Gods in the world: a holy war", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHolyWar() => Push("a holy war", GodPeoples.HolyWar);

        [DebugAction("Rimfolk", "Gods in the world: a temple's envoy for a relic", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnvoy() => Push("a temple's envoy (needs a relic of a god a friendly people holds)", GodPeoples.Envoy);

        [DebugAction("Rimfolk", "Gods in the world: shared gods' goodwill now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugShared() => Push("shared gods", b => GodPeoples.Shared());

        [DebugAction("Rimfolk", "Gods in the world: pilgrims at a holy place", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPilgrims() => Push("pilgrims", GodCamps.PilgrimsCome);

        [DebugAction("Rimfolk", "Gods in the world: a hermit seer", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSeer() => Push("a seer", GodCamps.SeerComes);

        [DebugAction("Rimfolk", "Gods in the world: the next raid led by a champion", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugChampion()
        {
            var b = Ready();
            if (b == null) return;
            // The raids on the map now are looked at again, every chance pushed.
            foreach (var lord in Find.CurrentMap.lordManager.lords) b.raids.Remove(lord.loadID);
            Push("a champion (needs a raid on the map by a people with a war god)", x => GodCamps.Hourly(x, GodWorld.Now));
        }

        [DebugAction("Rimfolk", "Gods in the world: a god offers a gift", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGift() => Push("a gift (needs a god that favours the colony; dev 'Gods: add favour here...')", GodGifts.Offer);

        [DebugAction("Rimfolk", "Gods in the world: gifts received now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReceive()
        {
            var b = Ready();
            if (b == null) return;
            foreach (var d in b.Of("receive")) d.at -= GodWorldMath.GiftWaitTicks;
            Push("gifts received", x => GodGifts.Hourly(x, GodWorld.Now));
        }

        [DebugAction("Rimfolk", "Gods in the world: turn a trickster's or any gift", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTrick()
        {
            var b = Ready();
            var gift = b?.gifts.LastOrDefault(x => x.Holds && x.kind == GodGiftKind.Hand);
            if (gift == null)
            {
                Say("no blessing on a body held now.");
                return;
            }
            var g = Gods.ById(gift.god);
            var was = g?.kind ?? GodKind.Ordinary;
            if (g != null) g.kind = GodKind.Trickster;
            gift.turnedAt = GodWorld.Now - 30 * GenDate.TicksPerDay;
            try
            {
                Push("the trickster's turn", x => GodGifts.Trickster(gift));
            }
            finally
            {
                if (g != null) g.kind = was;
            }
        }

        [DebugAction("Rimfolk", "Gods in the world: a rival comes for the newest gift", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRival()
        {
            var b = Ready();
            var gift = b?.gifts.LastOrDefault(x => x.Holds);
            if (gift == null)
            {
                Say("no gift held now.");
                return;
            }
            gift.undoTried = false;
            gift.since = GodWorld.Now - (GodWorldMath.UndoAfterDays + 1) * GenDate.TicksPerDay;
            Push("a rival's avatar (needs a known rival whose people are at peace)", x => GodGifts.Rival(x, gift));
        }

        [DebugAction("Rimfolk", "Gods in the world: a fever broken tonight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBless() => Push("a blessing (needs one of the colony ill, and a god of healing it knows)", b => GodHealth.Blessings(b, Find.CurrentMap));

        [DebugAction("Rimfolk", "Gods in the world: a god's grudge on the selected colonist...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGrudge()
        {
            var p = Find.Selector.SingleSelectedThing as Pawn;
            if (p == null || !p.IsColonist)
            {
                Say("select one colonist first.");
                return;
            }
            var options = Gods.All.Where(g => GodWays.Wrath(g) == GodWrath.Lightning || GodWays.Wrath(g) == GodWrath.Beasts).OrderBy(g => g.name)
                .Select(g => new FloatMenuOption(g.name + " (" + GodWays.Wrath(g) + ")", () =>
                {
                    GodHealth.Grudge(g, p, p.Map, "dev");
                    var d = GodWorld.Book?.Of("grudge").LastOrDefault(x => x.pawn == p);
                    if (d != null) d.at -= GodWorldMath.BeastsAfterDays * GenDate.TicksPerDay;
                    Say(g.name + " holds a grudge against " + p.LabelShort + ": the beasts the next day they are out, the lightning in the next thunderstorm.");
                })).ToList();
            if (options.Count == 0) Say("no god whose anger is lightning or beasts.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods in the world: the beasts come now (for a grudge held)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBeasts() => Push("beasts (needs a grudge of beasts, the colonist out under the sky)", b => GodHealth.Daily(b, GodWorld.Now));

        [DebugAction("Rimfolk", "Gods in the world: a god's anger at this home", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAnger() => Push("anger (needs a god displeased here whose anger is blight, an eclipse or a storm)", b => GodHealth.Anger(b, Find.CurrentMap));

        [DebugAction("Rimfolk", "Gods in the world: the Hunger hears", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHunger() => Push("the Hunger (needs someone dying, breaking or the colony starving; Felt or Walking)", b => GodVoid.Hourly(b, GodWorld.Now));

        [DebugAction("Rimfolk", "Gods in the world: the Hunger takes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTake()
        {
            var b = Ready();
            if (b == null) return;
            foreach (var d in b.Of("take")) d.at = GodWorld.Now;
            Push("the Hunger takes", x => GodVoid.Daily(x, GodWorld.Now));
        }

        [DebugAction("Rimfolk", "Gods in the world: a quarrel in the weather", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugQuarrel() => Push("a quarrel (needs a known storm god with a known rival of the fields, the waters or the hearth)", GodDrama.Quarrel);

        [DebugAction("Rimfolk", "Gods in the world: a god's ask", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAsk() => Push("a god's ask (needs a god near the colony with a known rival)", GodDrama.Ask);

        [DebugAction("Rimfolk", "Gods in the world: a myth at supper now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMyth()
        {
            var b = Ready();
            if (b == null) return;
            b.lasts.Remove("myth:" + Homes.KeyOf(Find.CurrentMap));
            Push("a myth (needs two of the colony eating together at supper time)", x => GodDrama.Hourly(x, GodWorld.Now));
        }

        [DebugAction("Rimfolk", "Gods in the world: an archotech's terms", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTerms() => Push("an archotech's terms (needs a known archotech)", GodOutliers.Terms);

        [DebugAction("Rimfolk", "Gods in the world: the dead god's mark", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDeadMark()
        {
            var b = Ready();
            b?.told.RemoveAll(t => t.StartsWith("deadmark:", StringComparison.Ordinal));
            Push("the dead god's mark (needs an ancient wall or wreck with one of the colony near it)", GodOutliers.DeadMark);
        }

        [DebugAction("Rimfolk", "Gods in the world: the fading god", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFading() => Push("the fading god", GodOutliers.Fading);

        [DebugAction("Rimfolk", "Gods in the world: the Sleeper's dream", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSleeper() => Push("the Sleeper (needs the Sleeper known: the Sunken Vault in the world)", GodOutliers.Sleeper);

        [DebugAction("Rimfolk", "Gods in the world: a newborn's next sign", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNewborn()
        {
            var g = Gods.All.FirstOrDefault(x => x.kind == GodKind.Newborn);
            if (g == null)
            {
                Say("no newborn god (dev 'Gods: a newborn of the colony's faith...').");
                return;
            }
            g.devotion = Math.Min(GodsMath.NewbornGrown - 1f, g.devotion + GodsMath.NewbornGrown / 4f);
            Push("a newborn's sign", GodOutliers.Newborn);
        }
    }
}
