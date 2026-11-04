using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A hamlet's questions to the colony (docs/design/stage1-region.md, item 6), each through Tell.Ask: the settlers' ask
    /// (take them under protection and escort them, let them go alone, choose another place, refuse), the hamlet's ask for
    /// help in a hard season (send it, or refuse), its ask to swear fealty (accept, or not yet), and, if its people turn on
    /// the colony first, its families' question (come to the colony, or stay with their people). Asks that can wait come
    /// only when one of Rimfolk's questions may (Tell.CanAsk); the settlers' ask after the refugees' answer and the families'
    /// question come at once.
    /// </summary>
    public static class HamletAsk
    {
        public const string Settle = "settle", HelpAsk = "help", Oath = "oath", Hostile = "hostile";

        private static Letter Make(Map home, HamletHome h, string ask, string label, string text, LookTargets look, int at, int need = -1)
        {
            var def = HamletDefOf.Rimfolk_HamletAsk;
            if (def == null) return null;
            var letter = LetterMaker.MakeLetter(label, text, def, look ?? LookTargets.Invalid, h.people);
            if (letter is ChoiceLetter_Hamlet l)
            {
                l.home = Homes.KeyOf(home);
                l.ask = ask;
                l.at = at;
                l.need = need;
                l.town = h.town;
            }
            return letter;
        }

        private static LookTargets PlaceLook(HamletHome h)
        {
            var t = Hamlets.TileOf(h);
            return t.Valid ? new LookTargets(new GlobalTargetInfo(t)) : LookTargets.Invalid;
        }

        /// <summary>The settlers' ask, looking at the place they have in mind.</summary>
        internal static void Ask(Map home, HamletHome h)
        {
            try
            {
                var letter = Make(home, h, Settle, HamletWords.AskLabel(h), HamletWords.AskText(home, h), PlaceLook(h), h.askedAt);
                if (letter != null) Tell.Ask(letter, home);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: the settlers' ask", e);
            }
        }

        /// <summary>The ask again with the place the colony chose: its words and its look changed, and opened again.</summary>
        internal static void Retell(Map home, HamletHome h, Letter letter)
        {
            if (!(letter is ChoiceLetter c) || h == null) return;
            try
            {
                c.Text = HamletWords.AskText(home, h);
                c.lookTargets = PlaceLook(h);
                if (Find.LetterStack.LettersListForReading.Contains(c)) c.OpenLetter();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: the ask with a new place", e);
            }
        }

        /// <summary>A hard season: the hamlet asks for food, a doctor or tools.</summary>
        internal static void AskHelp(Map home, HamletHome h, HamletNeed need)
        {
            var town = Hamlets.TownOf(h);
            var letter = Make(home, h, HelpAsk, HamletWords.HelpLabel(h, need), HamletWords.HelpText(home, h, need), town != null ? new LookTargets(town) : null, h.helpAt, (int)need);
            if (letter != null) Tell.Ask(letter, home);
        }

        /// <summary>The hamlet asks to swear fealty to the colony.</summary>
        internal static void AskOath(Map home, HamletHome h)
        {
            var town = Hamlets.TownOf(h);
            var letter = Make(home, h, Oath, HamletWords.OathLabel(h), HamletWords.OathText(home, h), town != null ? new LookTargets(town) : null, h.oathAt);
            if (letter != null) Tell.Ask(letter, home);
        }

        /// <summary>Its people turned on the colony: may its families come to the colony, or do they stay with their people?</summary>
        internal static void AskHostile(Map home, HamletHome h)
        {
            var town = Hamlets.TownOf(h);
            var letter = Make(home, h, Hostile, HamletWords.HostileLabel(h), HamletWords.HostileText(home, h), town != null ? new LookTargets(town) : null, h.oathAt);
            if (letter != null) Tell.Ask(letter, home);
        }
    }

    /// <summary>
    /// The hamlet's questions as a letter (see <see cref="HamletAsk"/>). Once what it asked about has moved on (answered
    /// another way, the hamlet gone, its people no longer as they were), only closing is left.
    /// </summary>
    public class ChoiceLetter_Hamlet : ChoiceLetter
    {
        public int home = HomesMath.None;
        public string ask;
        /// <summary>When the ask went out (the hamlet's own record of it), to know it is still the same question.</summary>
        public int at = -1;
        public int need = -1;
        public int town = -1;

        private HamletHome Live(out Map map)
        {
            map = Homes.MapOf(home);
            var h = Hamlets.Peek(map);
            if (h == null) return null;
            switch (ask)
            {
                case HamletAsk.Settle:
                    return h.stage == HamletStage.Asked && h.askedAt == at ? h : null;
                case HamletAsk.HelpAsk:
                    return HamletMath.Stands(h.stage) && h.town == town && h.helpAt == at && h.people != null && !h.people.HostileTo(Faction.OfPlayer) ? h : null;
                case HamletAsk.Oath:
                    return h.stage == HamletStage.Standing && h.town == town && h.oathAt == at && h.people != null && !h.people.HostileTo(Faction.OfPlayer) ? h : null;
                case HamletAsk.Hostile:
                    return HamletMath.Stands(h.stage) && h.town == town && h.hostileAsked && h.people != null && h.people.HostileTo(Faction.OfPlayer) ? h : null;
                default:
                    return null;
            }
        }

        /// <summary>The question still waits for the colony's answer (This week's Needs you counts it).</summary>
        public bool Waiting => !ArchivedOnly && Live(out _) != null;

        private DiaOption Option(string key, Action<Map, HamletHome> act, string disabled = null, NamedArgument? arg = null)
        {
            var o = new DiaOption((arg.HasValue ? key.Translate(arg.Value) : key.Translate()).Resolve())
            {
                action = () =>
                {
                    var h = Live(out var map);
                    Find.LetterStack.RemoveLetter(this);
                    if (h != null) act(map, h);
                },
                resolveTree = true
            };
            if (disabled != null) o.Disable(disabled);
            return o;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                Map map = null;
                var h = ArchivedOnly ? null : Live(out map);
                if (h == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                switch (ask)
                {
                    case HamletAsk.Settle:
                        int adults = Hamlets.Adults(map);
                        yield return Option("CC_Hamlet_Ask_Escort", Hamlets.Escort,
                            adults < HamletMath.MinEscort ? "CC_Hamlet_Ask_TooFew".Translate(HamletMath.MinEscort.Named("COUNT")).Resolve() : null);
                        yield return Option("CC_Hamlet_Ask_Alone", (m, x) => Hamlets.GoAlone(m, x, false));
                        var choose = new DiaOption("CC_Hamlet_Ask_Place".Translate().Resolve())
                        {
                            action = () =>
                            {
                                var now = Live(out var m);
                                if (now != null) Hamlets.ChoosePlace(m, now, this);
                            },
                            resolveTree = true
                        };
                        yield return choose;
                        yield return Option("CC_Hamlet_Ask_Refuse", (m, x) => Hamlets.End(m, x, HamletEnd.Refused, "refused"));
                        break;
                    case HamletAsk.HelpAsk:
                        var n = (HamletNeed)Math.Max(0, need);
                        yield return Option("CC_Hamlet_Help_Send", (m, x) => Hamlets.Helped(m, x, n), Hamlets.CantHelp(map, n), HamletWords.HelpGoods(n).Named("GOODS"));
                        yield return Option("CC_Hamlet_Help_Refuse", (m, x) => Hamlets.Refused(m, x, n));
                        break;
                    case HamletAsk.Oath:
                        yield return Option("CC_Hamlet_Oath_Accept", Hamlets.Swear);
                        yield return Option("CC_Hamlet_Oath_NotYet", (m, x) => HamletWords.TellNotYet(m, x));
                        break;
                    case HamletAsk.Hostile:
                        yield return Option("CC_Hamlet_Hostile_Come", Hamlets.Join);
                        yield return Option("CC_Hamlet_Hostile_Stay", (m, x) => Hamlets.End(m, x, HamletEnd.Stayed, null));
                        break;
                    default:
                        yield return Option_Close;
                        yield break;
                }
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref ask, "ask");
            Scribe_Values.Look(ref at, "at", -1);
            Scribe_Values.Look(ref need, "need", -1);
            Scribe_Values.Look(ref town, "town", -1);
        }
    }

    /// <summary>
    /// The town-abandoned event's answer "Offer them a place for a hamlet" (Defs/WorldSituationDefs/Rimfolk_WS_RiseFall.xml):
    /// the family at the gate goes back for the others of their town, and the settlers' ask comes at once. Greyed out when
    /// settlers could not ask at this home now (a hamlet already, too few to protect one, an ask this year).
    /// </summary>
    public class WorldEffect_Hamlet : WorldEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            return Hamlets.CanAskHere(map, ctx?.faction, out var why) ? null : why;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx == null || !Allowed(ctx)) return;
            sheet.Add(ImpactKind.People, 1, "CC_Hamlet_Impact".Translate().Resolve(), "CC_Hamlet_ImpactBrief".Translate().Resolve(), "hamlet");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null || !Gate(ctx) || ctx.faction == null) return;
            try
            {
                var s = SituationEffectUtil.Of(ctx);
                int id = s?.Num("town") ?? 0;
                var town = id != 0 ? NotableBook.TownOf(id) : null;
                var trip = VisitorTracker.Instance?.WaitingFor(ctx);
                var atGate = trip?.people?.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map && p.RaceProps.Humanlike && p.Faction == ctx.faction).ToList();
                Hamlets.AskFromRefugees(map, ctx.faction, town, atGate);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: refugees offered a place", e);
            }
        }
    }

    [DefOf]
    public static class HamletDefOf
    {
        public static LetterDef Rimfolk_HamletAsk;

        static HamletDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HamletDefOf));
        }
    }
}
