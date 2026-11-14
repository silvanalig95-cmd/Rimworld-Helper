using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Each colonist's own belief in the gods the colony knows (the player, 14 November 2026: "pawns should have and develop
    /// kind of a relationship in a way, or rather a belief to gods ... the more they pray and interact to a god, that they
    /// kind of, yeah, get a stronger belief in that god", then "pawn belief should be reflected in book and culture tab",
    /// "gods effect should be noticeable"; docs/gods-next-proposals.md, section E, round 3). The rules are PawnGodsMath's;
    /// the record is PawnGodsBook's. A belief is the pawn's own, a pawn fact the unseen rule allows: never the god's favour,
    /// wants or rules.
    /// <list type="bullet">
    /// <item>Drawn once, the first time someone is among the colony's people (<see cref="Of"/>): their traits, passions,
    /// what they are, their past, the gods of the people they came from, their faith, a bond with an animal. Never their
    /// parents or kin. An older save's turning after a crisis of faith carries over as a belief.</item>
    /// <item>Grown and faded through the worship and the moments that already are (the seams: Prayers, Offerings, Vows,
    /// Festivals, FaithHeart, GodDreams, GodTales, GodHands, GodWorldGifts, Gods.Remember and Gods.Moment), so what round 2
    /// builds on those plugs in. A moment counts for the god its witnesses took it for, which may not be the god it was.</item>
    /// <item>Whom they pray to and swear by (<see cref="Held"/>, through Worship.GodOf and Gods.SwearBy): their own first
    /// god, or one of the moment's domain they hold; with nothing drawing them, the god of the moment's domain, or of their
    /// work's.</item>
    /// <item>What it does (Gods/Belief/PawnGodsActs.cs): a small mood when the god they hold acts before them or is slighted
    /// (its place gone, its feast day passed), their own small rite, rival devotees bickering in the game's own talk, a
    /// conversion after a strong moment, and dread: keeping away from its place and flinching at its signs.</item>
    /// <item>How it shows: the Culture tab (UI/ITab_Pawn_Culture.cs, "Their gods") and each chapter of the Gods' book
    /// (Gods/GodLorePage.cs), in words only, for a god the colony has heard of, by the colony's name for it.</item>
    /// </list>
    /// Only the colony's own people (Worship.Ours): guests and prisoners hold nothing here.
    /// </summary>
    public static class PawnGods
    {
        /// <summary>The gods and their worship are on.</summary>
        public static bool On => Worship.On;

        private static PawnGodsBook Book => On ? PawnGodsBook.Instance : null;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= The record =================

        /// <summary>Someone's record, drawn the first time they are among the colony's people; null for anyone else (a guest, a prisoner) or with the gods off.</summary>
        public static PawnBelief Of(Pawn p, bool create = true)
        {
            var b = Book;
            if (b == null || p == null) return null;
            var r = b.Find(p);
            if (r != null) return r;
            if (!create || !Worship.Ours(p)) return null;
            r = b.Add(p);
            try
            {
                Draw(r, p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: what draws someone to a god", e);
            }
            return r;
        }

        /// <summary>What draws them, once: PawnGodsMath.Draw of their sketch; an older save's turning after a crisis of faith, as a belief held.</summary>
        private static void Draw(PawnBelief r, Pawn p)
        {
            r.drawn = true;
            int now = Now;
            foreach (var d in PawnGodsMath.Draw(Sketch(p)))
            {
                var g = Gods.ByKey(d.god);
                if (g == null || g.kind == GodKind.Dead) continue;
                r.gods.Add(new HeldGod { god = g.id, hold = d.amount, drawn = d.amount, grew = now, why = DrawWhy(d) });
            }
            int turned = Worship.Book?.TurnedTo(p) ?? -1;
            var t = Gods.ById(turned);
            if (t != null)
            {
                var h = r.Of(t.id) ?? Add(r, t);
                h.hold = Math.Max(h.hold, PawnGodsMath.HoldsFrom + 5f);
                h.drawn = Math.Max(h.drawn, h.hold);
                h.why = "CC_Belief_Why_Turned".Translate().Resolve();
            }
            Trim(r);
        }

        private static string DrawWhy(BeliefDraw d)
        {
            string what = d.what.NullOrEmpty() ? null : d.what;
            string key = "CC_Belief_Why_" + d.kind + (what == null && d.kind != DrawKind.Bond && d.kind != DrawKind.Turned ? "_None" : "");
            return PawnGodsMath.Short(key.Translate((what ?? "").Named("WHAT")).Resolve());
        }

        /// <summary>What they are, as the draws read it.</summary>
        public static BeliefSketch Sketch(Pawn p)
        {
            var s = new BeliefSketch { devout = Worship.Devout(p), scoffer = Worship.Scoffer(p) };
            if (p?.story?.traits != null)
                foreach (var t in p.story.traits.allTraits)
                    if (t?.def != null)
                        s.traits.Add((t.def.defName, t.Degree, t.Label));
            if (p?.skills?.skills != null)
                foreach (var sk in p.skills.skills)
                    if (sk?.def != null && (int)sk.passion > 0)
                        s.passions.Add((sk.def.defName, (int)sk.passion >= 2 ? 2 : 1, sk.def.skillLabel ?? sk.def.label));
            try
            {
                if (ModsConfig.BiotechActive && p?.genes?.Xenotype != null)
                {
                    s.xenotype = p.genes.Xenotype.defName;
                    s.xenotypeLabel = p.genes.XenotypeLabel;
                }
            }
            catch
            {
                // What they are unread: the rest still draws them.
            }
            foreach (var bs in new[] { p?.story?.Childhood, p?.story?.Adulthood })
            {
                if (bs == null) continue;
                string title = bs.TitleFor(p.gender);
                s.backstories.Add((bs.defName + " " + title + " " + (bs.titleShort ?? ""), title));
            }
            // The people they came from (the game's people they were made among), and the gods those people revere.
            var fdef = p?.kindDef?.defaultFactionDef;
            if (fdef != null && !fdef.isPlayer)
            {
                var f = Find.FactionManager?.FirstFactionOfDef(fdef);
                if (f != null && !f.IsPlayer)
                {
                    s.peopleGods.AddRange(Gods.Of(f).Where(g => g.kind != GodKind.Dead).Select(g => g.Key));
                    s.people = f.Name;
                }
            }
            // Their faith's memes and precepts (Ideology), read as the domains they lean toward.
            try
            {
                if (ModsConfig.IdeologyActive && p?.Ideo != null)
                {
                    var scores = GodsMath.NewScores();
                    foreach (var m in p.Ideo.memes ?? new List<MemeDef>())
                        if (m != null)
                            GodsMath.ScoreMeme(m.defName, m.label, 1f, scores);
                    GodsMath.ScorePrecepts(p.Ideo.PreceptsListForReading?.Select(x => x?.def?.defName), 0.5f, scores);
                    s.faith = PawnGodsMath.FaithLean(scores, Gods.All.Where(g => g.kind != GodKind.Dead).Select(g => (g.Key, g.domain, g.second)));
                    s.faithName = p.Ideo.name;
                }
            }
            catch
            {
                // Their faith unread.
            }
            if (p?.relations?.DirectRelations != null)
                foreach (var rel in p.relations.DirectRelations)
                {
                    if (rel?.def != PawnRelationDefOf.Bond || rel.otherPawn?.RaceProps?.Animal != true || rel.otherPawn.Dead) continue;
                    if (IsCat(rel.otherPawn)) s.catBond = true;
                    else s.animalBond = true;
                }
            return s;
        }

        /// <summary>A cat: the game's, Odyssey's, any animal called a cat.</summary>
        public static bool IsCat(Pawn a) => a?.def != null && a.RaceProps?.Animal == true
                                            && (a.def.defName == "Cat" || a.def.label == "cat" || (a.def.label ?? "").EndsWith(" cat", StringComparison.OrdinalIgnoreCase));

        private static HeldGod Add(PawnBelief r, God g)
        {
            var h = new HeldGod { god = g.id };
            r.gods.Add(h);
            return h;
        }

        /// <summary>The strongest kept, the faded let go.</summary>
        private static void Trim(PawnBelief r)
        {
            if (r.gods.Count == 0) return;
            var keep = PawnGodsMath.Keep(r.gods.Select(h => (h.hold, h.dread)).ToList());
            if (keep.Count == r.gods.Count) return;
            var kept = keep.Select(i => r.gods[i]).ToList();
            r.gods.Clear();
            r.gods.AddRange(kept);
        }

        // ================= Reading =================

        /// <summary>How they hold a god (0 to 100), and dread it.</summary>
        public static float HoldOf(Pawn p, God g) => g == null ? 0f : Of(p)?.Of(g.id)?.hold ?? 0f;

        public static float DreadOf(Pawn p, God g) => g == null ? 0f : Of(p)?.Of(g.id)?.dread ?? 0f;

        /// <summary>They dread this god more than they hold it.</summary>
        public static bool Dreads(Pawn p, God g)
        {
            var h = g == null ? null : Of(p)?.Of(g.id);
            return h != null && PawnGodsMath.Dreads(h.hold, h.dread);
        }

        /// <summary>A god a believer can name and turn to: the colony has heard of it (or seen it, and has a name for it in its book), and it is not gone.</summary>
        internal static bool Usable(God g) => g != null && g.kind != GodKind.Dead && (Gods.Known(g) || GodLore.Instance?.PageOf(g) != null);

        /// <summary>
        /// The name someone of the colony knows a god by: the colony's book's name for it (its chapter), else their own people's
        /// (GodLore.NameOf); under a wrong name where the colony has it so.
        /// </summary>
        public static string NameOf(God g, Pawn p) => g == null ? "" : GodLore.NameOf(g, p) ?? Gods.Name(g);

        /// <summary>
        /// The god they pray to and swear by, by their own belief (PawnGodsMath.Choose: their first, or one of the moment's
        /// domain they hold), among the gods the colony knows; null with nothing drawing them that far (the caller falls back
        /// on the domain).
        /// </summary>
        public static God Held(Pawn p, GodDomain about = GodDomain.None)
        {
            var r = Of(p);
            if (r == null || r.gods.Count == 0) return null;
            var list = new List<(float, float, bool)>();
            var gods = new List<God>();
            foreach (var h in r.gods)
            {
                var g = Gods.ById(h.god);
                if (!Usable(g)) continue;
                gods.Add(g);
                list.Add((h.hold, h.dread, about != GodDomain.None && (g.domain == about || g.second == about)));
            }
            int i = PawnGodsMath.Choose(list, about != GodDomain.None);
            return i < 0 ? null : gods[i];
        }

        /// <summary>Their first god: the one they hold most (at least leaning toward it, not dreaded), or null.</summary>
        public static God First(Pawn p) => Held(p);

        /// <summary>A scoffer won over to a god scoffs no more.</summary>
        public static bool WonOver(Pawn p) => (Of(p, false)?.wonOver ?? -1) >= 0;

        /// <summary>How they hold a god, in words (None for a god the colony hasn't heard of).</summary>
        public static BeliefWord WordOf(Pawn p, God g)
        {
            if (!Usable(g)) return BeliefWord.None;
            var h = Of(p)?.Of(g.id);
            return h == null ? BeliefWord.None : PawnGodsMath.Word(h.hold, h.dread);
        }

        /// <summary>The domain of the work they're best at, for whom they turn to with nothing else to go by (PawnGodsMath.WorkDomain).</summary>
        public static GodDomain WorkDomain(Pawn p)
        {
            if (p?.skills?.skills == null || !Worship.Ours(p)) return GodDomain.None;
            var best = p.skills.skills.Where(s => s?.def != null && !s.TotallyDisabled).OrderByDescending(s => s.Level + (int)s.passion * 3).FirstOrDefault();
            return best == null ? GodDomain.None : PawnGodsMath.WorkDomain(best.def.defName);
        }

        /// <summary>A belief as it shows: the god, the colony's name for it, how they hold it, and why.</summary>
        public struct Shown
        {
            public God god;
            public string name;
            public BeliefWord word;
            public string why;
            public float strength;
        }

        /// <summary>Their gods as the Culture tab shows them: the gods the colony has heard of that they hold or dread in words, strongest first.</summary>
        public static List<Shown> ShownFor(Pawn p)
        {
            var list = new List<Shown>();
            var r = Of(p);
            if (r == null) return list;
            foreach (var h in r.gods)
            {
                var g = Gods.ById(h.god);
                if (!Usable(g)) continue;
                var w = PawnGodsMath.Word(h.hold, h.dread);
                if (w == BeliefWord.None) continue;
                bool fear = PawnGodsMath.Fears(w);
                list.Add(new Shown { god = g, name = NameOf(g, p), word = w, why = fear ? h.dreadWhy ?? h.why : h.why, strength = fear ? h.dread : h.hold });
            }
            return list.OrderByDescending(x => PawnGodsMath.Fears(x.word) ? 0 : 1).ThenByDescending(x => x.strength).ToList();
        }

        /// <summary>The colony's people who hold a god or dread it (in words), strongest first, for its chapter of the book.</summary>
        public static List<(Pawn pawn, BeliefWord word)> HoldersOf(God g)
        {
            var list = new List<(Pawn, BeliefWord, float)>();
            var b = Book;
            if (b == null || !Usable(g)) return new List<(Pawn, BeliefWord)>();
            foreach (var r in b.people)
            {
                var p = r?.pawn;
                if (p == null || !Worship.Ours(p)) continue;
                var h = r.Of(g.id);
                if (h == null) continue;
                var w = PawnGodsMath.Word(h.hold, h.dread);
                if (w == BeliefWord.None || Worship.Scoffer(p) && !PawnGodsMath.Fears(w)) continue;
                list.Add((p, w, Math.Max(h.hold, h.dread)));
            }
            return list.OrderByDescending(x => x.Item3).Select(x => (x.Item1, x.Item2)).ToList();
        }

        public static string WordKey(BeliefWord w) => "CC_Belief_Word_" + w;

        // ================= Moving =================

        private static string Date(int tick) => GodLorePage.Date(tick);

        /// <summary>
        /// Someone's belief in a god moves by an event (PawnGodsMath.Amount, times <paramref name="scale"/>): a god the colony
        /// hasn't heard of isn't held; a step big enough (PawnGodsMath.WhyFrom), or the first, sets the line of why.
        /// </summary>
        public static HeldGod Move(Pawn p, God g, BeliefEvent e, string why = null, float scale = 1f)
        {
            if (!Usable(g)) return null;
            var r = Of(p);
            if (r == null) return null;
            try
            {
                float amount = PawnGodsMath.Amount(e) * scale;
                var h = r.Of(g.id);
                if (h == null)
                {
                    if (amount <= 0f) return null;
                    h = Add(r, g);
                }
                h.hold = PawnGodsMath.Grow(h.hold, amount);
                if (amount > 0f) h.grew = Now;
                if (!why.NullOrEmpty() && (Math.Abs(amount) >= PawnGodsMath.WhyFrom && amount > 0f || h.why.NullOrEmpty()))
                    h.why = PawnGodsMath.Short(why);
                Trim(r);
                return r.Of(g.id);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("gods: a belief moved", ex);
                return null;
            }
        }

        /// <summary>Their dread of a god grows (a god's anger that hurt them or happened beside them); passing into dread is in their journal.</summary>
        public static void Fear(Pawn p, God g, float amount, string why)
        {
            if (!Usable(g) || amount <= 0f) return;
            var r = Of(p);
            if (r == null) return;
            var h = r.Of(g.id) ?? Add(r, g);
            bool was = PawnGodsMath.Word(h.hold, h.dread) == BeliefWord.Dread;
            h.dread = PawnGodsMath.Clamp(h.dread + amount);
            h.feared = Now;
            if (!why.NullOrEmpty()) h.dreadWhy = PawnGodsMath.Short(why);
            Trim(r);
            if (!was && PawnGodsMath.Word(h.hold, h.dread) == BeliefWord.Dread)
            {
                string line = Worship.Line("Rimfolk_Pool_Belief_DreadJournal", p, g);
                if (!line.NullOrEmpty()) CouncilGameComponent.Instance?.journals.WriteText(p, line);
            }
        }

        /// <summary>Dev: someone's belief and dread in a god set outright.</summary>
        public static void DevSet(Pawn p, God g, float hold, float dread)
        {
            var r = Of(p);
            if (r == null || g == null) return;
            var h = r.Of(g.id) ?? Add(r, g);
            h.hold = PawnGodsMath.Clamp(hold);
            h.dread = PawnGodsMath.Clamp(dread);
            h.grew = h.feared = Now;
            h.why = "CC_Belief_Why_Told".Translate("(dev)".Named("TELLER")).Resolve();
            if (dread > 0f) h.dreadWhy = "CC_Belief_Why_Dread".Translate(Date(Now).Named("DATE"), "(dev)".Named("WHAT")).Resolve();
            Trim(r);
        }

        // ================= The seams: worship =================

        /// <summary>A prayer said (Prayers.Said): more at the god's own place.</summary>
        public static void Prayed(Pawn p, God g, bool atShrine) =>
            Move(p, g, atShrine ? BeliefEvent.PrayedAtShrine : BeliefEvent.Prayed, "CC_Belief_Why_FirstPrayer".Translate(Date(Now).Named("DATE")).Resolve());

        /// <summary>A prayer answered (Prayers.TryAnswer): a strong moment, that may win someone over to the god they prayed to.</summary>
        public static void Answered(Pawn p, God g, string about)
        {
            string why = (about.NullOrEmpty() ? "CC_Belief_Why_Answered" : "CC_Belief_Why_AnsweredFor").Translate((about ?? "").Named("ABOUT"), Date(Now).Named("DATE")).Resolve();
            if (Move(p, g, BeliefEvent.Answered, why) != null) PawnGodsActs.MaybeWonOver(p, g, BeliefEvent.Answered, why);
        }

        /// <summary>What they prayed for came to pass on its own (Prayers.Credit).</summary>
        public static void Credited(Pawn p, God g) => Move(p, g, BeliefEvent.Credited, "CC_Belief_Why_Credited".Translate(Date(Now).Named("DATE")).Resolve());

        /// <summary>A prayer unanswered (FaithHeart.Unanswered), and worse, a crisis of faith.</summary>
        public static void Unanswered(Pawn p, God g, bool crisis) => Move(p, g, crisis ? BeliefEvent.Crisis : BeliefEvent.Unanswered);

        /// <summary>Two crises and they turned from one god to another (FaithHeart.TurnAway).</summary>
        public static void Turned(Pawn p, God from, God to)
        {
            Move(p, from, BeliefEvent.TurnedFrom);
            var h = Move(p, to, BeliefEvent.TurnedTo, "CC_Belief_Why_TurnedTo".Translate(NameOf(from, p).Named("FROM"), Date(Now).Named("DATE")).Resolve());
            // The god turned to is their first now.
            float other = Of(p)?.gods.Where(x => x.god != (to?.id ?? -1)).Select(x => x.hold).DefaultIfEmpty(0f).Max() ?? 0f;
            if (h != null) h.hold = PawnGodsMath.Clamp(Math.Max(h.hold, other + 5f));
        }

        /// <summary>The god they'd turn to from one that failed them: another they hold (not dreaded), or null.</summary>
        public static God NextHeld(Pawn p, God from)
        {
            var r = Of(p);
            if (r == null) return null;
            return r.gods.Where(h => h.god != (from?.id ?? -1) && h.hold >= PawnGodsMath.LeansFrom && !PawnGodsMath.Dreads(h.hold, h.dread))
                .OrderByDescending(h => h.hold).Select(h => Gods.ById(h.god)).FirstOrDefault(Usable);
        }

        /// <summary>An offering carried and set down (Offerings.Placed).</summary>
        public static void Offered(Pawn p, God g) => Move(p, g, BeliefEvent.Offered, "CC_Belief_Why_FirstGift".Translate(Date(Now).Named("DATE")).Resolve());

        /// <summary>A vow kept (a great one more) or broken (Vows.End): a broken vow leaves guilt, and a little fear.</summary>
        public static void Vowed(Pawn p, God g, bool kept, bool great)
        {
            if (kept)
            {
                Move(p, g, great ? BeliefEvent.GreatVowKept : BeliefEvent.VowKept, "CC_Belief_Why_VowKept".Translate(Date(Now).Named("DATE")).Resolve());
                return;
            }
            Move(p, g, BeliefEvent.VowBroken);
            Fear(p, g, 6f, "CC_Belief_Why_VowBroken".Translate(Date(Now).Named("DATE")).Resolve());
        }

        /// <summary>A festival kept at home (Festivals.Kept): everyone at home keeps it, believers of the god the more.</summary>
        public static void Festival(IEnumerable<Pawn> home, God g, string name)
        {
            if (home == null || g == null) return;
            string why = "CC_Belief_Why_Festival".Translate((name ?? "").Named("NAME"), Date(Now).Named("DATE")).Resolve();
            foreach (var p in home.ToList())
            {
                if (!Worship.Ours(p) || Worship.Scoffer(p) || Dreads(p, g)) continue;
                Move(p, g, BeliefEvent.Festival, why, HoldOf(p, g) >= PawnGodsMath.LeansFrom ? 1f : 0.5f);
            }
        }

        /// <summary>The festival's rite led at the god's place (Festivals.RiteDone).</summary>
        public static void RiteLed(Pawn p, God g) => Move(p, g, BeliefEvent.RiteLed, "CC_Belief_Why_RiteLed".Translate(Date(Now).Named("DATE")).Resolve());

        /// <summary>A dream of a god, told at breakfast (GodDreams).</summary>
        public static void Dreamt(Pawn p, God g) => Move(p, g, BeliefEvent.Dreamt, "CC_Belief_Why_Dreamt".Translate(Date(Now).Named("DATE")).Resolve());

        /// <summary>A piece of a god's tale, a line of what it is, a saying (GodTales): the listener drawn a little, a colonist telling it the more sure.</summary>
        public static void Told(Pawn listener, Pawn teller, God g)
        {
            if (g == null) return;
            if (listener != null && Worship.Ours(listener) && !Worship.Scoffer(listener))
                Move(listener, g, BeliefEvent.Told, "CC_Belief_Why_Told".Translate((teller?.LabelShort ?? "").Named("TELLER")).Resolve());
            if (teller != null && Worship.Ours(teller) && !Worship.Scoffer(teller)) Move(teller, g, BeliefEvent.Telling);
        }

        /// <summary>A scoffer's mockery (FaithHeart.Mock): a little doubt.</summary>
        public static void Mocked(Pawn believer, God g) => Move(believer, g, BeliefEvent.Mocked);

        /// <summary>
        /// A gift of a god's to them (GodWorldGifts.Give), under the god they take it for (the dream that brought it may not
        /// have named it, so maybe wrongly: GodLoreMath.Attribute): a strong moment, that may win them over.
        /// </summary>
        public static void Gift(Pawn p, God g, string label)
        {
            var lore = GodLore.Instance;
            if (g == null || lore == null) return;
            var taken = Gods.CardName(g, p?.MapHeld) != null ? g : Gods.ByKey(GodLoreMath.Attribute(g.Key, lore.KnownOf(), Rand.Value));
            string why = "CC_Belief_Why_Gift".Translate((label ?? "").Named("GIFT"), Date(Now).Named("DATE")).Resolve();
            if (Move(p, taken, BeliefEvent.Gift, why) != null) PawnGodsActs.MaybeWonOver(p, taken, BeliefEvent.Gift, why);
        }

        // ================= The seams: moments seen =================

        /// <summary>A god's moment (the presence part's) as an act's kind.</summary>
        public static GodActKind KindOf(PresenceKind k) => k switch
        {
            PresenceKind.Appear => GodActKind.Appear,
            PresenceKind.Act => GodActKind.Act,
            PresenceKind.Reveal => GodActKind.Reveal,
            PresenceKind.Depart => GodActKind.Depart,
            PresenceKind.Blessing => GodActKind.Blessing,
            PresenceKind.Wrath => GodActKind.Wrath,
            _ => GodActKind.Omen
        };

        /// <summary>
        /// Something of a god's seen on the land (Gods.Remember, Gods.Moment, GodHands.Show), by those who saw it, under the god
        /// they took it for (<paramref name="taken"/>, maybe the wrong one; null for what nobody could name: nothing to hold).
        /// A kindness or a sight of it draws them (plainly seen, the more, and a strong moment may win them over; a scoffer moved
        /// only by a god seen plainly); its anger leaves dread, the more on whoever it fell beside; its sign makes one who dreads
        /// it flinch. Their own god acting before them gives a small good mood.
        /// </summary>
        public static void Saw(IEnumerable<Pawn> pawns, God taken, GodActKind kind, bool plain, string words, IntVec3 cell, Map map)
        {
            if (!Usable(taken) || pawns == null || Book == null) return;
            try
            {
                string why = words.NullOrEmpty() ? null : "CC_Belief_Why_Saw".Translate(Date(Now).Named("DATE"), words.Named("WHAT")).Resolve();
                foreach (var p in pawns.Where(x => x != null).Distinct().ToList())
                {
                    if (!Worship.Ours(p)) continue;
                    bool scoffer = Worship.Scoffer(p);
                    switch (kind)
                    {
                        case GodActKind.Wrath:
                        {
                            bool onThem = cell.IsValid && p.Spawned && p.Position.InHorDistOf(cell, 2.5f);
                            bool beside = !onThem && cell.IsValid && p.Spawned && p.Position.InHorDistOf(cell, 6f);
                            if (scoffer && !plain && !onThem) break;
                            Fear(p, taken, PawnGodsMath.DreadAmount(onThem, beside, HoldOf(p, taken)),
                                words.NullOrEmpty() ? null : "CC_Belief_Why_Dread".Translate(Date(Now).Named("DATE"), words.Named("WHAT")).Resolve());
                            break;
                        }
                        case GodActKind.Omen:
                            if (Dreads(p, taken)) PawnGodsActs.Flinch(p, taken, cell);
                            else if (!scoffer) Move(p, taken, BeliefEvent.SawOmen, why);
                            break;
                        case GodActKind.Blessing:
                        {
                            if (scoffer && !plain) break;
                            var e = plain ? BeliefEvent.SawPlain : BeliefEvent.SawBlessing;
                            Move(p, taken, e, why, scoffer ? 0.5f : 1f);
                            Acted(p, taken);
                            PawnGodsActs.MaybeWonOver(p, taken, e, why);
                            break;
                        }
                        default:
                        {
                            if (scoffer && !plain) break;
                            var e = plain ? BeliefEvent.SawPlain : BeliefEvent.Saw;
                            Move(p, taken, e, why, scoffer ? 0.5f : 1f);
                            if (kind != GodActKind.Depart && kind != GodActKind.Mark) Acted(p, taken);
                            if (plain) PawnGodsActs.MaybeWonOver(p, taken, e, why);
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a belief, from what was seen", e);
            }
        }

        /// <summary>The god they hold to acted before them: a small good mood (once a day at most, the thought's own limit).</summary>
        private static void Acted(Pawn p, God g)
        {
            if (First(p) != g || HoldOf(p, g) < PawnGodsMath.HoldsFrom || Dreads(p, g)) return;
            CouncilGameComponent.GiveThought(p, PawnGodsDefOf.Rimfolk_Belief_GodActed);
        }

        // ================= The passes =================

        /// <summary>The region's hour, at each home (Worship.Hourly): the small rites, the dreaded places kept away from, a god's place gone.</summary>
        public static void Hourly(Map map, int now)
        {
            if (Book == null || map == null) return;
            PawnGodsActs.Rites(map, now);
            PawnGodsActs.KeepAway(map, now);
        }

        /// <summary>The region's day (Worship.Daily): beliefs and dread faded with neglect, what someone lived through, the departed let go.</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            b.people.RemoveAll(r => r?.pawn == null || r.pawn.Dead || r.pawn.Discarded || !Worship.Ours(r.pawn));
            b.Changed();
            foreach (var r in b.people)
            {
                foreach (var h in r.gods)
                {
                    h.hold = PawnGodsMath.Fade(h.hold, h.drawn, h.grew < 0 ? 999f : (now - h.grew) / (float)GenDate.TicksPerDay);
                    h.dread = PawnGodsMath.FadeDread(h.dread, h.feared < 0 ? 999f : (now - h.feared) / (float)GenDate.TicksPerDay);
                    if (h.dread < PawnGodsMath.Forget) h.dreadWhy = null;
                }
                Trim(r);
                LivedThrough(r.pawn);
            }
        }

        /// <summary>
        /// What someone came through draws them to the god their people tie it to, when the colony knows that god: a cold that
        /// nearly took them (Hush), a heat that nearly did (Helios), a deadly sickness they are over (Morel). Once a month each.
        /// </summary>
        private static void LivedThrough(Pawn p)
        {
            var hs = p?.health?.hediffSet;
            if (hs == null || p.Dead) return;
            foreach (var h in hs.hediffs.ToList())
            {
                if (h?.def == null) continue;
                string god = null, what = null;
                if (h.def == HediffDefOf.Hypothermia && h.Severity >= 0.35f)
                {
                    god = "Hush";
                    what = "CC_Belief_Lived_Cold".Translate();
                }
                else if (h.def == HediffDefOf.Heatstroke && h.Severity >= 0.35f)
                {
                    god = "Helios";
                    what = "CC_Belief_Lived_Heat".Translate();
                }
                else if (h.def.makesSickThought && h.def.lethalSeverity > 0f && h.Severity > 0.3f * h.def.lethalSeverity
                         && (p.health.immunity?.GetImmunityRecord(h.def)?.immunity ?? 0f) >= 0.85f)
                {
                    god = "Morel";
                    what = h.LabelBase;
                }
                var g = Gods.ByKey(god);
                if (g == null || !Interplay.Ready("belief-lived|" + p.thingIDNumber + "|" + god, 30f)) continue;
                string why = "CC_Belief_Why_Lived".Translate((what ?? "").Named("WHAT"), Date(Now).Named("DATE")).Resolve();
                Move(p, g, BeliefEvent.LivedThrough, why);
                PawnGodsActs.MaybeWonOver(p, g, BeliefEvent.LivedThrough, why);
            }
        }
    }

    [DefOf]
    public static class PawnGodsDefOf
    {
        public static ThoughtDef Rimfolk_Belief_GodActed;
        public static ThoughtDef Rimfolk_Belief_ShrineLost;
        public static ThoughtDef Rimfolk_Belief_FeastPassed;
        public static ThoughtDef Rimfolk_Belief_Flinched;
        public static ThoughtDef Rimfolk_Belief_WonOver;
        public static JobDef Rimfolk_Belief_Rite;

        static PawnGodsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PawnGodsDefOf));
    }
}
