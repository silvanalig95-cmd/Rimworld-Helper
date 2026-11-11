using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What shapes a god takes on the map (Defs/GodDefs/Rimfolk_GodsOnMap_Shapes.xml; the player, 11 November 2026: "not
    /// every god necessarily has to have a sacred beast, each god should be individual in what it looks like and whether it
    /// has any form at all"): whether it can come as a stranger at the gate, whether it walks as a beast and as which, and
    /// whether it answers in the weather, and how (some gods have no body and show only in the sky). Who comes, and as
    /// what, is read from here, never decided by the code: a god named in <see cref="gods"/> (by its name, its patron's
    /// defName, its look's defName, or the defName of a hand-made god's own def) takes that shape; any other god its
    /// domain's (<see cref="domains"/>), else the default.
    /// </summary>
    public class GodShapeDef : Def
    {
        /// <summary>The particular gods this shape is for: a god's name, its patron's defName, its look's defName, or its own def's.</summary>
        public List<string> gods = new List<string>();
        /// <summary>The domains this is every god's shape for (the generic shape by domain), when no shape names the god itself.</summary>
        public List<GodDomain> domains = new List<GodDomain>();
        /// <summary>The default: the shape of a god with nothing else to go by.</summary>
        public bool isDefault;

        /// <summary>It can come as a lone traveller asking for shelter (a god in disguise).</summary>
        public bool stranger = true;
        /// <summary>It walks as a beast, and which (ThingDef names, the player's mods' first; empty: its look's own beasts).</summary>
        public bool beast = true;
        public List<string> beasts = new List<string>();
        /// <summary>It answers in the weather, with these voices (Rain, Thunder, Eclipse, Aurora; empty: its domain's).</summary>
        public bool weather = true;
        public List<WeatherVoice> voices = new List<WeatherVoice>();
        /// <summary>It has no body at all: it shows only in the sky, and answers there more readily and more often.</summary>
        public bool bodiless;
        /// <summary>A tag its own lines carry (a stranger's hints, what is said of it), so a god written by hand can have words of its own.</summary>
        public string lineTag;

        /// <summary>The signs it sends before it comes or acts (GodOmens; empty: the five any god may send).</summary>
        public List<OmenKind> omens = new List<OmenKind>();
        /// <summary>Very rarely it rises from the land for an hour, with no body of its own (GodRising).</summary>
        public bool rises;
        /// <summary>
        /// Its true form when the colony wakes it (GodWaking; a PawnKindDef's defName, read by name, so a mod's creature
        /// can be it when that mod is loaded), and the look it takes then (a GodLookDef's defName).
        /// </summary>
        public string trueForm, trueLook;

        // ---- Its beast, when it walks as one ----

        /// <summary>The look its beast takes (a GodLookDef's defName), else the god's own look.</summary>
        public string look;
        /// <summary>Others of its kind that follow it at a distance, like pilgrims (a PawnKindDef's defName), and how many.</summary>
        public string kin;
        public IntRange kinCount = new IntRange(0, 0);
        /// <summary>It leads someone who follows it to a find, a cure or a vein; it takes food from someone's hand.</summary>
        public bool leads = true;
        public bool feeds = true;
        /// <summary>Someone may sit near it while it grazes, and come away changed.</summary>
        public bool sitNear;
        /// <summary>Harming it or its kin is the worst thing a colony can do: every god turns colder, every people on the map hears of it.</summary>
        public bool harmIsWorst;
        /// <summary>Let pass unharmed, it leaves a strange peace on those who saw it, for a season.</summary>
        public bool peace;
        /// <summary>At night it returns what was done by day (Selene): a kindness as something found, a cruelty as a nightmare (GodReturn).</summary>
        public bool returns;
        /// <summary>How many days its beast stays (Mog: a season), else the usual few.</summary>
        public FloatRange stayDays = new FloatRange(0f, 0f);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!isDefault && gods.NullOrEmpty() && domains.NullOrEmpty()) yield return "a god's shape is for some gods, some domains, or the default";
            if (bodiless && (stranger || beast)) yield return "a bodiless god comes neither as a stranger nor as a beast";
            if (!beast && (sitNear || peace || !kin.NullOrEmpty() || !look.NullOrEmpty())) yield return "only a god that walks as a beast has a look for it, kin or someone sitting near it";
            if (kinCount.min < 0 || kinCount.max < kinCount.min) yield return "kinCount out of order";
            if (stayDays.min < 0f || stayDays.max < stayDays.min) yield return "stayDays out of order";
            if (voices != null && voices.Contains(WeatherVoice.None)) yield return "None isn't a weather voice";
        }
    }

    /// <summary>A god's shape as it stands now (GodShapes.Of): by its short name (Hollin, Smoulder, Bellwether, Morel, Ennu, Pip, Cirrus, Hush, Undertow), else by domain.</summary>
    public static class GodShapes
    {
        /// <summary>For the hand-made pantheon to come: a god's shape by its own def, ahead of these defs (null: by these defs).</summary>
        public static Func<God, GodShapeDef> Override;

        private static List<GodShapeDef> All => DefDatabase<GodShapeDef>.AllDefsListForReading;

        private static readonly GodShapeDef Open = new GodShapeDef { defName = "Rimfolk_GodShape_Open", isDefault = true };

        /// <summary>The shape of a god: its own, else its domain's, else the default (everything open).</summary>
        public static GodShapeDef Of(God g)
        {
            if (g == null) return Open;
            try
            {
                var own = Override?.Invoke(g);
                if (own != null) return own;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's own shape", e);
            }
            var all = All;
            foreach (var s in all)
                if (s.gods != null && s.gods.Count > 0 && (s.gods.Contains(g.name) || g.patron != null && s.gods.Contains(g.patron) || g.look != null && s.gods.Contains(g.look)))
                    return s;
            foreach (var s in all)
                if (s.domains != null && s.domains.Contains(g.domain))
                    return s;
            return all.FirstOrDefault(s => s.isDefault) ?? Open;
        }

        public static bool Stranger(God g) => g != null && Of(g).stranger;

        public static bool Beast(God g) => g != null && Of(g).beast;

        public static bool Weather(God g) => g != null && Of(g).weather;

        public static bool Bodiless(God g) => g != null && Of(g).bodiless;

        /// <summary>The weather voices it uses, or null for its domain's.</summary>
        public static IList<WeatherVoice> Voices(God g)
        {
            var v = Of(g).voices;
            return v.NullOrEmpty() ? null : v;
        }

        /// <summary>The beasts its shape names that are in this game (ThingDefs), or null for its look's own.</summary>
        public static List<ThingDef> Beasts(God g)
        {
            var names = Of(g).beasts;
            if (names.NullOrEmpty()) return null;
            return names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d?.race != null).ToList();
        }

        /// <summary>Its own lines' tag, or null.</summary>
        public static string LineTag(God g) => g == null ? null : Of(g).lineTag;

        /// <summary>Its own signs, or null for the five any god may send.</summary>
        public static IList<OmenKind> Omens(God g)
        {
            var o = Of(g).omens;
            return o.NullOrEmpty() ? null : o;
        }

        public static bool Rises(God g) => g != null && Of(g).rises;

        /// <summary>The look its beast takes when its shape names one in this game, else null (the god's own look).</summary>
        public static GodLookDef Look(God g) => g == null ? null : GodLooks.Named(Of(g).look);

        /// <summary>The kind its kin are, when its shape names one in this game, else null.</summary>
        public static PawnKindDef Kin(God g) => g == null || Of(g).kin.NullOrEmpty() ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(Of(g).kin);

        public static IntRange KinCount(God g) => g == null ? new IntRange(0, 0) : Of(g).kinCount;

        public static bool Leads(God g) => g != null && Of(g).leads;

        public static bool Feeds(God g) => g != null && Of(g).feeds;

        public static bool SitNear(God g) => g != null && Of(g).sitNear;

        public static bool HarmIsWorst(God g) => g != null && Of(g).harmIsWorst;

        public static bool Peace(God g) => g != null && Of(g).peace;

        /// <summary>Its true form, when its shape names one that is in this game (its mod loaded), else null: a kind by its name, else the kind of a race by that name.</summary>
        public static PawnKindDef TrueForm(God g)
        {
            string n = g == null ? null : Of(g).trueForm;
            if (n.NullOrEmpty()) return null;
            return DefDatabase<PawnKindDef>.GetNamedSilentFail(n) ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race?.defName == n);
        }

        /// <summary>How many days its beast stays: its shape's, else <paramref name="usual"/>.</summary>
        public static float StayDays(God g, float usual)
        {
            var r = g == null ? new FloatRange(0f, 0f) : Of(g).stayDays;
            return r.max > 0f ? r.RandomInRange : usual;
        }

        public static GodLookDef TrueLook(God g) => g == null ? null : GodLooks.Named(Of(g).trueLook);
    }

    /// <summary>
    /// The gods on the map in words: a god the colony knows by its name, one it doesn't as what it is ("a goddess of the
    /// harvest"), and what the colony's people saw of it, in their own words, for the colony's book.
    /// </summary>
    public static class GodsOnMapWords
    {
        /// <summary>{GOD}, {GODEPITHET} and {GODOF} for the god: its name where the colony knows it or it shows itself (<paramref name="named"/>), else what it is.</summary>
        public static NamedArgument[] Args(God g, bool named)
        {
            if (g == null) return new NamedArgument[0];
            if (named || g.known) return Gods.Args(g);
            string what = Gods.AGodOf(g);
            return new[] { what.Named(GodsMath.God), "".Named(GodsMath.Epithet), Gods.GodOf(g).Named(GodsMath.Of) };
        }

        /// <summary>
        /// Remembers what someone of the colony saw, heard or felt, in the world's voice and their own words, for the
        /// colony's book (Gods.Remember, which fills in who else could see it): "Tamsin saw frost by the wall in lines like
        /// writing", never "the god wrote a word". <paramref name="key"/> takes {WHO} (the witness by name, else someone of
        /// the home) and {HOME}, with <paramref name="more"/>. The words, for the chronicle too; null when nothing was kept.
        /// </summary>
        public static string Saw(God god, Map map, IntVec3 cell, GodActKind kind, Pawn witness, string key, params NamedArgument[] more)
        {
            if (god == null || map == null || key.NullOrEmpty()) return null;
            string home = Homes.Name(map);
            string who = witness != null ? witness.LabelShort : "CC_GodM_SomeoneAt".Translate(home.Named("HOME")).Resolve();
            var args = new List<NamedArgument> { who.Named("WHO"), home.Named("HOME") };
            if (more != null) args.AddRange(more);
            string text = key.Translate(args.ToArray()).Resolve().CapitalizeFirst();
            Gods.Remember(new GodAct { god = god.id, home = Gods.HomeKey(map), tick = Find.TickManager.TicksGame, kind = kind, cell = cell, text = text });
            return text;
        }

        /// <summary>
        /// The name a card or a message may give the god (the player: "in events etc the god can be named, if we know it's
        /// them that caused something"): the colony's name for it when the colony knows it was this god (it showed itself
        /// before their eyes: <paramref name="sure"/>; or the colony knows the god and the gods walk plainly), else null,
        /// and the words stay unnamed.
        /// MERGE: a local stand-in for the core's Gods.CardName(god, home); swap it in at merge.
        /// </summary>
        public static string CardName(God g, Map home, bool sure)
        {
            if (g == null || Gods.Name(g).NullOrEmpty() || Gods.Realness == GodRealness.Believed) return null;
            return sure || Gods.Known(g) && Gods.Realness == GodRealness.Walking ? Gods.Name(g) : null;
        }

        /// <summary>A card's title: by the god's name where the colony knows it was this god (<see cref="CardName"/>), else what was seen.</summary>
        public static string Title(God g, Map home, bool sure, string unnamed, string named, params NamedArgument[] args)
        {
            string name = CardName(g, home, sure);
            var all = new List<NamedArgument>(args ?? new NamedArgument[0]);
            if (name == null) return unnamed.Translate(all.ToArray()).Resolve().CapitalizeFirst();
            all.Add(name.Named("GOD"));
            return named.Translate(all.ToArray()).Resolve().CapitalizeFirst();
        }

        /// <summary>The nearest of the colony awake who could see <paramref name="at"/>, or null.</summary>
        public static Pawn Seer(Map map, IntVec3 at, float range = 30f)
        {
            if (map == null || !at.IsValid) return null;
            return map.mapPawns.FreeColonistsSpawned.Where(p => p.Awake() && !p.Downed && People.HasInnerLife(p) && p.Position.InHorDistOf(at, range)
                                                                && GenSight.LineOfSight(p.Position, at, map, true))
                .OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault();
        }
    }
}
