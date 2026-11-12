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
    /// pushed, GodWorld.Force), what is under way and each god's ways listed in the log (the hidden state, for testing
    /// only), and a few things set up for a test: a grudge on the selected colonist, a god's anger or kindness in its own
    /// shape, a fever's blessing, the Hunger's ear. What can't come still says why (no god of the kind, no relic, no holy
    /// place, only believed).
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
                                       + b.hungerTaken + " taken, today's kindness " + b.kindness + " / cruelty " + b.cruelty + ".\n");
            foreach (var d in b.doings)
                sb.Append("  ").Append(d.kind).Append(" #").Append(d.id).Append(" god ").Append(Gods.ById(d.god)?.name ?? "-").Append(" other ").Append(Gods.ById(d.other)?.name ?? "-")
                    .Append(" pawn ").Append(d.pawn?.LabelShort ?? "-").Append(" stage ").Append(d.stage).Append(" what ").Append(d.what ?? "-").Append(d.done ? " (done)" : "").AppendLine();
            foreach (var g in b.gifts)
                sb.Append("  gift #").Append(g.id).Append(' ').Append(g.kind).Append(' ').Append(g.what).Append(" from ").Append(Gods.ById(g.god)?.name ?? "?").Append(" to ")
                    .Append(g.pawn?.LabelShort ?? "?").Append(" price ").Append(g.price).Append(g.undone ? " (undone)" : "").AppendLine();
            foreach (var kv in b.lasts) sb.Append("  last ").Append(kv.Key).Append(": ").Append(((GodWorld.Now - kv.Value) / (float)GenDate.TicksPerDay).ToString("0.0")).AppendLine(" days ago");
            var map = Find.CurrentMap;
            foreach (var g in Gods.All.Take(60))
                sb.Append("  ways of ").Append(g.name).Append(GodWays.Own(g) != null ? " (its own)" : " (by domain)").Append(": standing here ")
                    .Append(map != null ? Gods.Favour(g, map).ToString() : "-").Append(", wrath ").Append(GodWays.Wrath(g)).Append(", grudge ").Append(GodWays.Grudge(g))
                    .Append(GodWays.Blind(g) ? " (blind)" : GodWays.Mirror(g) ? " (mirror)" : "").Append(", kindness ").Append(GodWays.Blessing(g)).Append(", side ").Append(GodWays.Side(g) ?? "-").Append(", site ").Append(GodWays.Site(g) ?? "-")
                    .Append(", gives ").Append(string.Join("/", Enum.GetValues(typeof(GodGiftKind)).Cast<GodGiftKind>().Where(k => k != GodGiftKind.None && GodWays.Gives(g, k))))
                    .Append(", classes ").Append(string.Join("/", GodWays.Classes(g))).Append(", paths ").Append(string.Join("/", GodWays.Paths(g)))
                    .Append(GodVoid.Quiet(b, g) ? " (quiet)" : "").AppendLine();
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

        [DebugAction("Rimfolk", "Gods in the world: a dream told (a gift)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGift() => Push("a dream and a gift (needs a god that favors the colony here; dev 'Gods: add favour here...')", GodWorldGifts.Offer);

        [DebugAction("Rimfolk", "Gods in the world: gifts received now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReceive()
        {
            var b = Ready();
            if (b == null) return;
            foreach (var d in b.Of("receive")) d.at -= GodWorldMath.GiftWaitTicks;
            Push("gifts received", x => GodWorldGifts.Hourly(x, GodWorld.Now));
        }

        [DebugAction("Rimfolk", "Gods in the world: a rival's dream for the newest gift", allowedGameStates = AllowedGameStates.PlayingOnMap)]
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
            Push("a rival's dream (needs a blessing on the body, a beast or a gifted trait, and a known rival of the giver)", x => GodWorldGifts.Rival(x, gift));
        }

        [DebugAction("Rimfolk", "Gods in the world: a fever broken tonight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBless() => Push("a fever broken (needs one of the colony ill, and a god whose kindness that is: Morel)", b => GodHealth.Blessings(b, Find.CurrentMap));

        [DebugAction("Rimfolk", "Gods in the world: a god's grudge on the selected colonist...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGrudge()
        {
            var p = Find.Selector.SingleSelectedThing as Pawn;
            if (p == null || !p.IsColonist)
            {
                Say("select one colonist first.");
                return;
            }
            var options = Gods.All.Where(g => GodWays.Grudge(g) != GodWrath.None).OrderBy(g => g.name)
                .Select(g => new FloatMenuOption(g.name + " (" + GodWays.Grudge(g) + ")", () =>
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

        [DebugAction("Rimfolk", "Gods in the world: a god's anger at this home (by standing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAnger() => Push("anger (needs a god displeased here)", b => GodHealth.Anger(b, Find.CurrentMap));

        [DebugAction("Rimfolk", "Gods in the world: a god's anger in its own shape now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWrathOf() => Pick(g => GodWays.Wraths(g).Count > 0, g => g.name + " (" + string.Join("/", GodWays.Wraths(g)) + ")",
            g => Push(g.name + "'s anger (the game must allow it: crops for blight, a machine to break, a storm for lightning, silver for a hoard)",
                b => GodWays.Wraths(g).InRandomOrder().Any(w => GodHealth.Wrath(g, Find.CurrentMap, w))));

        [DebugAction("Rimfolk", "Gods in the world: a god's kindness in its own shape now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugKindOf() => Pick(g => GodWays.Blessing(g) != GodBlessing.None && GodWays.Blessing(g) != GodBlessing.Fever, g => g.name + " (" + GodWays.Blessing(g) + ")",
            g => Push(g.name + "'s kindness (a broken machine or a battery to fill, a cold snap to end, open ground)", b => GodHealth.Bless(g, Find.CurrentMap, GodWays.Blessing(g))));

        /// <summary>A float menu of the gods that pass, each running its action.</summary>
        private static void Pick(Func<God, bool> which, Func<God, string> label, Action<God> act)
        {
            if (Ready() == null) return;
            var options = Gods.All.Where(which).OrderBy(g => g.name).Select(g => new FloatMenuOption(label(g), () => act(g))).ToList();
            if (options.Count == 0) Say("no god of that kind.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods in the world: the moon returns a kind day tonight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMirrorKind() => Push("a kindness returned (needs Selene known; a wounded colonist asleep, else something found)", b =>
        {
            if (GodHealth.Mirror(Find.CurrentMap, 4, 0) && GodsOnMapBook.Instance != null) GodsOnMapBook.Instance.lastReturn = GodWorld.Now;
        });

        [DebugAction("Rimfolk", "Gods in the world: the moon returns a cruel day tonight", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMirrorCruel() => Push("a cruelty returned (needs Selene known)", b =>
        {
            if (GodHealth.Mirror(Find.CurrentMap, 0, 4) && GodsOnMapBook.Instance != null) GodsOnMapBook.Instance.lastReturn = GodWorld.Now;
        });

        [DebugAction("Rimfolk", "Gods in the world: the Hunger hears", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHunger() => Push("the Hunger (needs someone dying, breaking or the colony starving)", b => GodVoid.Hourly(b, GodWorld.Now));

        [DebugAction("Rimfolk", "Gods in the world: the Hunger takes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTake()
        {
            var b = Ready();
            if (b == null) return;
            foreach (var d in b.Of("take")) d.at = GodWorld.Now;
            Push("the Hunger takes", x => GodVoid.Daily(x, GodWorld.Now));
        }

        [DebugAction("Rimfolk", "Gods in the world: a quarrel in the weather or on the ground", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugQuarrel() => Push("a quarrel (needs two known gods tied to each other on opposed sides: Hush and Smoulder, Hollin and Morel)", GodDrama.Quarrel);

        [DebugAction("Rimfolk", "Gods in the world: one hunted at the gate", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAsk() => Push("one hunted (needs a god near the colony with a known rival, each held by a people)", GodDrama.Ask);

        [DebugAction("Rimfolk", "Gods in the world: a myth at supper now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMyth()
        {
            var b = Ready();
            if (b == null) return;
            b.lasts.Remove("myth:" + Homes.KeyOf(Find.CurrentMap));
            Push("a myth (needs two of the colony eating together at supper time)", x => GodDrama.Hourly(x, GodWorld.Now));
        }
    }
}
