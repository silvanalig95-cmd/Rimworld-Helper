using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The facts service (DESIGN.md, "Facts"): what is true for a scene, read from the game when a text is about to be
    /// written, so a line is written once and reacts to the same world wherever it is said. A text system makes a
    /// <see cref="FactScene"/> (who speaks, to whom, at which colony, their town and people, the tone, the providers it
    /// asks) and gets a <see cref="FactSet"/>: tags for its lines' conditions, values for their placeholders with the
    /// facts behind them, how current each tag is, earlier mentions' marks, and the world's news near the place.
    /// <para>
    /// The providers, each in its own file by subject: the moment, their people, the two of them, what passed between
    /// them (FactsWho.cs); their town, its land, notables and places (FactsPlace.cs); wars, the world's events and
    /// conditions (FactsWorld.cs); the colony, its hospital and what their people remember (FactsColony.cs); earlier
    /// mentions (FactsMarks.cs); the ages, the ties between the two sides and what the history holds of their town and
    /// of them (FactsLedger.cs); the world's news near them (FactsNews.cs). A fact the game can't give is left out, and a
    /// line that needs it isn't said. Providers only read: no dice, nothing changed. Each runs on its own, so one that
    /// fails leaves the others' facts.
    /// </para>
    /// </summary>
    public static partial class Facts
    {
        private static int Now => Find.TickManager.TicksGame;

        private static float Days(int tick) => (Now - tick) / (float)GenDate.TicksPerDay;

        /// <summary>How current something <paramref name="days"/> old is: <paramref name="top"/> when new, fading over <paramref name="span"/> days to a floor.</summary>
        private static float Fresh(float days, float span, float top) => FactsMath.Fresh(days, span, top);

        /// <summary>Both sides of the scene are there (the providers that read the two of them).</summary>
        private static bool Both(FactScene s) => s.speaker != null && s.listener != null;

        /// <summary>The providers by name, each reading what its scene has.</summary>
        private static readonly Dictionary<string, Action<FactScene, FactSet>> Providers = new Dictionary<string, Action<FactScene, FactSet>>
        {
            [FactsMath.Moment] = (s, f) =>
            {
                if (Both(s)) Moment(f, s.speaker, s.listener);
            },
            [FactsMath.People] = (s, f) =>
            {
                if (Both(s)) People(f, s.speaker, s.listener, s.people);
            },
            [FactsMath.Persons] = (s, f) =>
            {
                if (Both(s)) Persons(f, s.speaker, s.listener);
            },
            [FactsMath.Pair] = (s, f) =>
            {
                if (Both(s)) Pair(f, s.speaker, s.listener);
            },
            [FactsMath.Town] = (s, f) =>
            {
                if (s.listener != null) Town(f, s.listener, s.people, s.place);
            },
            [FactsMath.Land] = (s, f) =>
            {
                if (s.place != null) Land(f, s.place.Tile);
            },
            [FactsMath.Notables] = (s, f) =>
            {
                if (s.place != null && s.listener != null) Notables(f, s.listener, s.people, s.place);
            },
            [FactsMath.Places] = (s, f) =>
            {
                if (s.place != null) Places(f, s.place, s.people);
            },
            [FactsMath.Wars] = (s, f) => Wars(f, s.people),
            [FactsMath.Situations] = (s, f) => Situations(f, s.people),
            [FactsMath.Conditions] = (s, f) => Conditions(f, s.speaker != null ? s.speaker.MapHeld : s.colony),
            [FactsMath.Colony] = (s, f) =>
            {
                if (s.speaker != null) Colony(f, s.speaker, s.people);
            },
            [FactsMath.Hospital] = (s, f) =>
            {
                if (s.speaker != null) Hospital(f, s.speaker, TalkScene.Of(s.speaker.MapHeld));
            },
            [FactsMath.Deeds] = (s, f) =>
            {
                if (s.speaker != null) Deeds(f, s.speaker, s.people);
            },
            [FactsMath.Marks] = (s, f) =>
            {
                if (Both(s)) Marks(f, s.speaker, s.listener, s.people, s.place);
            },
            [FactsMath.Ages] = AgeTags,
            [FactsMath.Ties] = Ties,
            [FactsMath.History] = Lately,
            [FactsMath.News] = (s, f) => NewsNear(f, s.people, s.place)
        };

        /// <summary>Whether a provider of this name exists (for the scenes' lists).</summary>
        public static bool Has(string provider) => provider != null && Providers.ContainsKey(provider);

        /// <summary>
        /// What is true for the scene: the tone (tone:X), the speaker and listener by name ({COLONIST}, {OUTSIDER}), then
        /// each provider the scene asks for, in its order. Empty for no scene.
        /// </summary>
        public static FactSet For(FactScene scene)
        {
            var f = new FactSet();
            if (scene == null) return f;
            if (!scene.tone.NullOrEmpty()) f.Tag("tone:" + scene.tone);
            if (scene.speaker != null) f.Value("COLONIST", scene.speaker.LabelShort);
            if (scene.listener != null) f.Value("OUTSIDER", scene.listener.LabelShort);
            if (scene.asks == null) return f;
            foreach (var name in scene.asks)
            {
                if (name == null || !Providers.TryGetValue(name, out var add)) continue;
                try
                {
                    add(scene, f);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("facts: " + name, e);
                }
            }
            return f;
        }
    }
}
