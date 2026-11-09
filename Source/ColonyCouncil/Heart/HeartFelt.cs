using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart, felt (docs/design/heart-felt.md, release 1; the player's words of 9 November 2026: "i feel like this impact
    /// of strain and all the emotions is currently neglible, or hardly noticeable"). The rules are in HeartFeltMath; this is
    /// where they meet the game, once an hour for each colonist from HeartLogic:
    /// <list type="bullet">
    /// <item>The lasting weight: a loss, being downed (<see cref="Weigh"/>), and what lasts while it does, their own sickness,
    /// someone close badly ill, a fight at home going badly (<see cref="Hold"/>). Rest can't take strain below it; it lifts
    /// over days.</item>
    /// <item>A message when someone becomes worn down or near breaking, or in good heart, saying why, and when they come
    /// back from it (<see cref="Tell"/>, the setting "Messages when a heart turns").</item>
    /// <item>A line when their band turns, and now and then while it holds, naming the cause where there is one, in their own
    /// voice (<see cref="Lines"/>, "They say how they feel").</item>
    /// <item>A sign over the head for a strong feeling, the way the game shows a thought (<see cref="Sign"/>, "A sign over
    /// the head").</item>
    /// </list>
    /// Nothing here touches the game's mood need, its mental breaker or its break picker. What is saved is on the heart
    /// (the weight, what holds, the bands last told); the rest is worked out again after a load.
    /// </summary>
    public static class HeartFelt
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public const string BandPool = "Rimfolk_Pool_HeartBand";

        // ---------------- The lasting weight ----------------

        /// <summary>
        /// A heavy thing weighs on them, with what weighs already (HeartFeltMath.Combine), held a while by how heavy it is.
        /// <paramref name="why"/> is it in a few words ("losing Ana"); <paramref name="about"/> whom it is about, by name.
        /// </summary>
        public static void Weigh(PawnHeart h, WeightKind kind, float amount, string why, string about, int now)
        {
            if (h == null || amount <= 0f || float.IsNaN(amount)) return;
            float before = h.weight;
            h.weight = HeartFeltMath.Combine(h.weight, amount);
            h.weightUntil = Mathf.Max(h.weightUntil, now + Mathf.RoundToInt(HeartFeltMath.HoldDaysFor(h.weight) * GenDate.TicksPerDay));
            if (h.weightKind == 0 || amount >= before)
            {
                h.weightKind = (int)kind;
                h.weightAbout = about;
            }
            h.weightWhy = Two(why, before >= 10f ? h.weightWhy : null);
        }

        /// <summary>The newest words first, and the last heavy thing's after them, two at most ("losing Ana|being hurt").</summary>
        private static string Two(string newest, string had)
        {
            if (newest.NullOrEmpty()) return had;
            if (had.NullOrEmpty()) return newest;
            string first = had.Split('|')[0];
            return first == newest ? had : newest + "|" + first;
        }

        /// <summary>
        /// A death or a loss the game gave them a memory of this hour, by whom it is about: the heaviest memory of each
        /// person (the memories of one death come together), as the hour collects them (HeartLogic.Memories).
        /// </summary>
        public static void NoteLoss(Dictionary<int, (float weight, string name)> losses, Thought_Memory mem)
        {
            float w = HeartFeltMath.LossWeight(mem?.def?.defName);
            if (w <= 0f || losses == null) return;
            var other = mem.otherPawn;
            int key = other?.thingIDNumber ?? mem.def.shortHash;
            if (losses.TryGetValue(key, out var had) && had.weight >= w) return;
            losses[key] = (w, other?.LabelShort);
        }

        /// <summary>
        /// The hour's losses weigh, each person once in a few days (a memory of the same death in a later hour adds only what
        /// it weighs beyond the first), by their nature (a psychopath takes nothing from a death) and the strength setting.
        /// </summary>
        public static void WeighLosses(PawnHeart h, Dictionary<int, (float weight, string name)> losses, float speed, StrainTraits traits, int now)
        {
            if (h == null || losses == null || losses.Count == 0) return;
            float factor = speed * HeartMath.TraitFactor(StrainSource.Loss, traits);
            if (factor <= 0f) return;
            h.weighedFor ??= new Dictionary<int, (int tick, float weight)>();
            foreach (var kv in losses)
            {
                float w = kv.Value.weight;
                if (h.weighedFor.TryGetValue(kv.Key, out var was) && now - was.tick < GenDate.TicksPerDay * 3)
                {
                    if (w <= was.weight) continue;
                    w -= was.weight;
                }
                if (h.weighedFor.Count > 30) h.weighedFor.Clear();
                h.weighedFor[kv.Key] = (now, kv.Value.weight);
                string name = kv.Value.name;
                string why = name.NullOrEmpty() ? "CC_Felt_Why_LossSomeone".Translate().ToString() : "CC_Felt_Why_Loss".Translate(name.Named("LOST")).Resolve();
                Weigh(h, WeightKind.Loss, w * factor, why, name, now);
            }
        }

        /// <summary>Being downed weighs once a downing (seen at the hour's look).</summary>
        public static void Downed(Pawn p, PawnHeart h, float speed, StrainTraits traits, int now)
        {
            bool down = p.Downed;
            if (down && !h.wasDowned)
            {
                float w = HeartFeltMath.DownedWeight * speed * HeartMath.TraitFactor(StrainSource.Downed, traits);
                Weigh(h, WeightKind.Downed, w, "CC_Felt_Why_Downed".Translate(), null, now);
            }
            h.wasDowned = down;
        }

        /// <summary>
        /// What weighs while it lasts, this hour: their own sickness, someone close badly ill on the same map, a fight at home
        /// with one of theirs down. The weight is lifted to it while it lasts, and lifts slowly a day and a half after.
        /// </summary>
        public static void Hold(Pawn p, PawnHeart h, HeartMapContext m, float speed, StrainTraits traits, int now)
        {
            float target = 0f;
            var words = new List<string>(3);
            var kind = WeightKind.None;
            string about = null;
            float own = SickShare(p, out string illness);
            if (own > 0f)
            {
                target = HeartFeltMath.SickSelfWeight(own) * HeartMath.TraitFactor(StrainSource.Pain, traits);
                string label = Find.ActiveLanguageWorker.WithDefiniteArticle(illness ?? "");
                words.Add("CC_Felt_Why_SickSelf".Translate(label.Named("ILLNESS")).Resolve());
                kind = WeightKind.SickSelf;
                about = illness;
            }
            if (m?.sick != null && m.map != null && p.Map == m.map)
            {
                float worst = 0f;
                Pawn worstWho = null;
                foreach (var (o, share, _) in m.sick)
                {
                    if (o == p || !Close(p, o)) continue;
                    float w = HeartFeltMath.SickCloseWeight(share) * HeartMath.TraitFactor(StrainSource.Grief, traits);
                    if (w <= 0f) continue;
                    target = HeartFeltMath.Combine(target, w);
                    if (w > worst)
                    {
                        worst = w;
                        worstWho = o;
                    }
                }
                if (worstWho != null)
                {
                    words.Add("CC_Felt_Why_SickClose".Translate(worstWho.LabelShort.Named("SICK")).Resolve());
                    if (kind == WeightKind.None)
                    {
                        kind = WeightKind.SickClose;
                        about = worstWho.LabelShort;
                    }
                }
            }
            if (m != null && m.threat && m.downed > 0 && p.Spawned && p.Map == m.map)
            {
                float w = HeartFeltMath.RaidWeight(m.downed) * HeartMath.TraitFactor(StrainSource.Combat, traits);
                if (w > 0f)
                {
                    target = HeartFeltMath.Combine(target, w);
                    words.Add("CC_Felt_Why_Raid".Translate());
                    if (kind == WeightKind.None) kind = WeightKind.Raid;
                }
            }
            target *= speed;
            if (target <= 0f || target < h.held - 1f) return;
            h.held = Mathf.Max(h.held, Mathf.Min(HeartMath.Max, target));
            h.heldUntil = now + Mathf.RoundToInt(HeartFeltMath.HeldHoldDays * GenDate.TicksPerDay);
            h.heldKind = (int)kind;
            h.heldAbout = about;
            h.heldWhy = string.Join("|", words.Take(2));
        }

        /// <summary>
        /// Their worst sickness that can kill, as a share of where it kills (0 when none): the plague, the flu, an infection,
        /// any mod's that the game counts as sickness and that can be fatal.
        /// </summary>
        public static float SickShare(Pawn p, out string label)
        {
            label = null;
            var list = p?.health?.hediffSet?.hediffs;
            if (list == null) return 0f;
            float best = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var hd = list[i];
                var def = hd?.def;
                if (def == null || !def.makesSickThought || def.lethalSeverity <= 0f || !hd.Visible) continue;
                float share = Mathf.Clamp01(hd.Severity / def.lethalSeverity);
                if (share <= best) continue;
                best = share;
                label = def.label ?? def.defName;
            }
            return best;
        }

        /// <summary>Close: a partner, kin by blood, or a friend (their opinion 20 or more).</summary>
        public static bool Close(Pawn p, Pawn o)
        {
            if (p?.relations == null || o == null || o == p) return false;
            if (ArcUtil.Partners(p, o) || ArcUtil.Kin(p, o)) return true;
            return p.relations.OpinionOf(o) >= 20;
        }

        /// <summary>What rest can't take strain below now.</summary>
        public static float Floor(PawnHeart h) => h == null ? 0f : HeartFeltMath.Floor(h.weight, h.held);

        /// <summary>The hour's end: what good things lifted, and the weight lifting with time, slowly while it holds.</summary>
        public static void Ease(PawnHeart h, float lifted, float hours, int now)
        {
            if (lifted > 0f)
            {
                h.weight = Mathf.Max(0f, h.weight - lifted);
                h.held = Mathf.Max(0f, h.held - lifted);
            }
            h.weight = HeartFeltMath.Eased(h.weight, now < h.weightUntil, hours);
            h.held = HeartFeltMath.Eased(h.held, now < h.heldUntil, hours);
            if (h.weight <= 0f)
            {
                h.weightWhy = h.weightAbout = null;
                h.weightKind = 0;
            }
            if (h.held <= 0f)
            {
                h.heldWhy = h.heldAbout = null;
                h.heldKind = 0;
            }
        }

        /// <summary>
        /// An answer, a day off or a cry that eases strain from outside the hour lifts the weight too, by the share good
        /// things lift it.
        /// </summary>
        public static void Lift(PawnHeart h, float eased)
        {
            if (h == null || eased <= 0f) return;
            float lift = eased * HeartFeltMath.GoodEasesWeight;
            h.weight = Mathf.Max(0f, h.weight - lift);
            h.held = Mathf.Max(0f, h.held - lift);
        }

        /// <summary>What weighs on them in words, what lasts first: "the plague, Ana being so ill and losing Bo". Null when nothing does.</summary>
        public static string Why(PawnHeart h)
        {
            if (h == null) return null;
            var parts = new List<string>(4);
            if (h.held >= 5f && !h.heldWhy.NullOrEmpty()) parts.AddRange(h.heldWhy.Split('|'));
            if (h.weight >= 5f && !h.weightWhy.NullOrEmpty())
                foreach (var w in h.weightWhy.Split('|'))
                    if (!parts.Contains(w)) parts.Add(w);
            if (parts.Count > 3) parts.RemoveRange(3, parts.Count - 3);
            return parts.Count == 0 ? null : TextUtil.JoinAnd(parts);
        }

        /// <summary>Why they are strained, for a message or a line: what weighs on them, else what built it lately.</summary>
        public static string StrainWhy(PawnHeart h) => Why(h) ?? HeartView.SourcesText(h, 2);

        /// <summary>
        /// The weight in a sentence for the heart's page and the strain's tooltip: "A weight of 42 from losing Ana: rest won't
        /// ease strain below it; it lifts over the days." Null with none to speak of.
        /// </summary>
        public static string WeightLine(Pawn p, PawnHeart h)
        {
            float floor = Floor(h);
            if (floor < 5f) return null;
            string why = Why(h);
            return why == null
                ? "CC_Felt_WeightLine".Translate(floor.ToString("0").Named("WEIGHT"), p.Named("PAWN")).Resolve()
                : "CC_Felt_WeightLineWhy".Translate(floor.ToString("0").Named("WEIGHT"), why.Named("WHY"), p.Named("PAWN")).Resolve();
        }

        // ---------------- The hour on the map ----------------

        /// <summary>After the hour (HeartLogic.Evaluate): a message if their heart turned, a line, a sign, an act.</summary>
        public static void AfterHour(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            try
            {
                // The first look after a load only takes note: no line or sign for what was already so.
                bool first = h.lastBand < 0;
                Tell(p, h);
                Lines(p, h, now);
                Sign(p, h, now, first);
                HeartActs.Hour(p, h, m, now);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("the heart on the map", e);
            }
        }

        // ---------------- A message when a heart turns ----------------

        public static void Tell(Pawn p, PawnHeart h)
        {
            var tell = HeartFeltMath.Tell(ref h.toldStrain, ref h.toldGood, h.strain, h.good, GoodHeart.On);
            if (tell == HeartTell.None || !S.heartMessages || !p.IsColonist) return;
            string text;
            MessageTypeDef type = MessageTypeDefOf.NeutralEvent;
            var importance = Importance.Notable;
            switch (tell)
            {
                case HeartTell.Worn:
                case HeartTell.Breaking:
                {
                    string why = StrainWhy(h);
                    string key = tell == HeartTell.Worn ? "CC_Felt_Tell_Worn" : "CC_Felt_Tell_Breaking";
                    text = why.NullOrEmpty() ? (key + "_NoWhy").Translate(p.Named("PAWN")).Resolve() : key.Translate(p.Named("PAWN"), why.Named("WHY")).Resolve();
                    type = MessageTypeDefOf.NegativeEvent;
                    break;
                }
                case HeartTell.Back:
                    text = "CC_Felt_Tell_Back".Translate(p.Named("PAWN")).Resolve();
                    type = MessageTypeDefOf.PositiveEvent;
                    break;
                default:
                {
                    string why = GoodHeart.BuiltText(h, 2);
                    string key = tell == HeartTell.HighSpirits ? "CC_Felt_Tell_HighSpirits" : "CC_Felt_Tell_GoodHeart";
                    text = why.NullOrEmpty() ? (key + "_NoWhy").Translate(p.Named("PAWN")).Resolve() : key.Translate(p.Named("PAWN"), why.Named("WHY")).Resolve();
                    type = MessageTypeDefOf.PositiveEvent;
                    break;
                }
            }
            Notify.Message(text, p, type, importance);
        }

        // ---------------- A line when the band turns ----------------

        /// <summary>
        /// When their band turns (strain's, either way, or the good side's up to in good heart or in high spirits), a line
        /// waits up to a few hours for them to be in view and free; while a band holds, now and then another, at most once in
        /// a day and a half. Every line through the touches' one rate for what is said over a head.
        /// </summary>
        public static void Lines(Pawn p, PawnHeart h, int now)
        {
            var band = h.Band;
            var good = GoodHeart.Band(h);
            bool first = h.lastBand < 0;
            string turn = null;
            if (!first)
            {
                var before = (StrainBand)h.lastBand;
                var goodBefore = (GoodBand)h.lastGood;
                if (band > before && band >= StrainBand.Strained) turn = "turn";
                else if (band < before && before >= StrainBand.Strained) turn = "back";
                else if (good > goodBefore && good >= GoodBand.GoodHeart) turn = "good";
            }
            h.lastBand = (int)band;
            h.lastGood = (int)good;
            if (!S.heartSays || !Speech.On)
            {
                h.turnLine = null;
                return;
            }
            if (turn != null)
            {
                h.turnLine = turn;
                h.turnLineUntil = now + HeartFeltMath.TurnLineHours * GenDate.TicksPerHour;
            }
            if (h.turnLine != null && now >= h.turnLineUntil) h.turnLine = null;
            bool holds = band >= StrainBand.Strained || good >= GoodBand.GoodHeart;
            if (h.turnLine == null && (!holds || now - h.lastLineTick < GenDate.TicksPerDay * 3 / 2 || !Rand.Chance(0.12f))) return;
            if (Say(p, h, h.turnLine, now)) h.turnLine = null;
        }

        /// <summary>The line said now if they're in view and free to speak; false when it waits.</summary>
        private static bool Say(Pawn p, PawnHeart h, string turn, int now)
        {
            var vt = VisualTouches.Instance;
            if (vt == null || !VisualTouches.InView(out var map, out var view) || map != p.Map || !vt.Free(p, map, view, now)) return false;
            string line = BandLine(p, h, turn);
            if (line.NullOrEmpty()) return turn != null;
            if (!Speech.Say(p, line)) return false;
            vt.Spoke(p, now + GenDate.TicksPerDay / 2);
            h.lastLineTick = now;
            return true;
        }

        /// <summary>
        /// A line for how they are, from Rimfolk_Pool_HeartBand: tagged [band:strained], [band:worn], [band:breaking],
        /// [good:goodheart], [good:highspirits], [turn] or [back] when it just changed, [both] with both sides at once, and the
        /// cause where it is known, [why:loss] ({LOST} the one lost), [why:sickself] ({ILLNESS}), [why:sickclose] ({SICK}),
        /// [why:downed], [why:raid], [why:fight], [why:sleep], [why:letdowns], [why:friends], [why:purpose], [why:gathering].
        /// Null when nothing fits.
        /// </summary>
        public static string BandLine(Pawn p, PawnHeart h, string turn)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(BandPool);
            if (pool?.lines == null) return null;
            var band = h.Band;
            var good = GoodHeart.Band(h);
            var tags = Flavor.TagsFor(p);
            // A turn of the good side speaks for the good side, and so does someone with no strain to speak of; a turn of
            // strain speaks for strain; while both hold, either now and then, and lines for both at once.
            bool goodSpeaks = good >= GoodBand.GoodHeart && turn != "back" && (band < StrainBand.Strained || turn == "good" || (turn == null && Rand.Chance(0.4f)));
            if (!goodSpeaks && band < StrainBand.Strained && turn != "back") return null;
            // The side that speaks gives its band (strain's lines [band:...], the good side's [good:...]); [both] fits either.
            if (goodSpeaks) tags.Add("good:" + good.ToString().ToLowerInvariant());
            else tags.Add("band:" + band.ToString().ToLowerInvariant());
            if (band >= StrainBand.Strained && good >= GoodBand.GoodHeart) tags.Add("both");
            if (turn == "turn" || turn == "good") tags.Add("turn");
            else if (turn == "back") tags.Add("back");
            string lost = "", illness = "", sick = "";
            if (!goodSpeaks)
            {
                var kind = (WeightKind)(h.held >= h.weight ? h.heldKind : h.weightKind);
                string about = h.held >= h.weight ? h.heldAbout : h.weightAbout;
                if (HeartFelt.Floor(h) >= 10f && kind != WeightKind.None)
                {
                    switch (kind)
                    {
                        case WeightKind.Loss:
                            if (!about.NullOrEmpty())
                            {
                                tags.Add("why:loss");
                                lost = about;
                            }
                            break;
                        case WeightKind.SickSelf:
                            tags.Add("why:sickself");
                            illness = about ?? "";
                            break;
                        case WeightKind.SickClose:
                            tags.Add("why:sickclose");
                            sick = about ?? "";
                            break;
                        case WeightKind.Downed: tags.Add("why:downed"); break;
                        case WeightKind.Raid: tags.Add("why:raid"); break;
                    }
                }
                else
                {
                    var top = HeartView.TopSources(h, 1);
                    if (top.Count > 0) tags.Add(WhyTag(top[0].source));
                }
            }
            else
            {
                var top = GoodHeart.TopSources(h, 1);
                if (top.Count > 0) tags.Add(WhyTag(top[0].source));
            }
            tags.Remove(null);
            if (!Flavor.AnyFits(pool.lines.AllLines(), tags)) return null;
            string raw = pool.lines.Pick(ToneUtility.ToneFor(p), tags, p);
            if (raw.NullOrEmpty()) return null;
            return raw.Formatted(p.Named("PAWN"), lost.Named("LOST"), illness.Named("ILLNESS"), sick.Named("SICK")).Resolve().CapitalizeFirst();
        }

        private static string WhyTag(StrainSource s)
        {
            switch (s)
            {
                case StrainSource.Combat:
                case StrainSource.Fear: return "why:fight";
                case StrainSource.BadSleep:
                case StrainSource.Exhaustion: return "why:sleep";
                case StrainSource.Letdowns:
                case StrainSource.Council: return "why:letdowns";
                case StrainSource.Pain:
                case StrainSource.Downed: return "why:pain";
                default: return null;
            }
        }

        private static string WhyTag(GoodSource s)
        {
            switch (s)
            {
                case GoodSource.Belonging:
                case GoodSource.Friends: return "why:friends";
                case GoodSource.Purpose:
                case GoodSource.Deeds: return "why:purpose";
                case GoodSource.Gathering: return "why:gathering";
                default: return null;
            }
        }

        // ---------------- A sign over the head ----------------

        /// <summary>
        /// A small sign over their head for a strong feeling, as the game shows a thought: its own icon in the feeling's
        /// colour, in a good thought's bubble for a bright feeling and a bad one's for a hard one. When it grows strong, and
        /// now and then while it holds; once in eight hours at most; only on the map being looked at.
        /// </summary>
        public static void Sign(Pawn p, PawnHeart h, int now, bool first = false)
        {
            FeelingNow strong = null;
            for (int i = 0; i < h.feelings.Count; i++)
                if (h.feelings[i].Share >= HeartFeltMath.StrongFeeling && (strong == null || h.feelings[i].intensity > strong.intensity))
                    strong = h.feelings[i];
            bool just = !first && strong != null && strong.def != h.lastStrong;
            h.lastStrong = strong?.def;
            if (strong == null || !S.heartSigns || !p.Spawned || p.Map != Find.CurrentMap || p.InMentalState || p.Downed) return;
            int hours = (now - h.lastSignTick) / GenDate.TicksPerHour;
            if (!HeartFeltMath.SignNow(just, hours, Rand.Value)) return;
            if (ShowSign(p, strong.def)) h.lastSignTick = now;
        }

        /// <summary>The bubble itself (also from dev mode). False when it couldn't show.</summary>
        public static bool ShowSign(Pawn p, FeelingDef def)
        {
            if (p?.Map == null || def == null || p.Map.moteCounter.SaturatedLowPriority) return false;
            var tex = FeelingIcons.For(def);
            var bubble = def.bright ? ThingDefOf.Mote_ThoughtGood : ThingDefOf.Mote_ThoughtBad;
            if (tex == null || bubble == null) return false;
            if (!(ThingMaker.MakeThing(bubble) is MoteBubble mote)) return false;
            mote.SetupMoteBubble(tex, null, def.color);
            mote.Attach(p);
            GenSpawn.Spawn(mote, p.Position, p.Map);
            return true;
        }
    }

    /// <summary>
    /// The feelings' signs over the head (Textures/ColonyCouncil/Icons/Feelings, drawn by Tools/make_feeling_icons.py): a
    /// handful of shapes shared by feelings alike (a tear for grief, a bolt for anger, a heart for love), tinted in each
    /// feeling's own colour. Loaded once, at startup.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FeelingIcons
    {
        private static readonly Dictionary<string, Texture2D> loaded = new Dictionary<string, Texture2D>();

        /// <summary>Which shape a feeling wears, by its key; any other feeling a star if bright, a cloud if hard.</summary>
        public static string GlyphOf(string key, bool bright)
        {
            switch (key)
            {
                case "Grief":
                case "Despair": return "Tear";
                case "Fear":
                case "Dread":
                case "Worry": return "Fear";
                case "Anger":
                case "Frustration":
                case "Envy": return "Bolt";
                case "Love":
                case "Tenderness":
                case "Gratitude": return "Love";
                case "Joy":
                case "Contentment":
                case "Relief": return "Sun";
                case "Homesick":
                case "Nostalgia": return "Home";
                case "Loneliness": return "Alone";
                case "Fellowship": return "Together";
                default: return bright ? "Star" : "Cloud";
            }
        }

        public static Texture2D For(FeelingDef def)
        {
            if (def == null) return null;
            string glyph = GlyphOf(def.key, def.bright);
            if (loaded.TryGetValue(glyph, out var tex)) return tex;
            tex = ContentFinder<Texture2D>.Get("ColonyCouncil/Icons/Feelings/" + glyph, false) ?? Icons.Heart;
            loaded[glyph] = tex;
            return tex;
        }
    }
}
