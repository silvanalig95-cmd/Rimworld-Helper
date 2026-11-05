using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a journey is for, as the planner reads it from where it goes.</summary>
    public enum JourneyAim : byte
    {
        /// <summary>Nowhere in particular, or a place to look over.</summary>
        Travel,
        /// <summary>A fight: a site with hostiles, a raiders' camp, a hostile town, a call to arms.</summary>
        Fight,
        /// <summary>Trade: a town that trades, a trading post, a trade pact's people.</summary>
        Trade,
        /// <summary>Talks: peace talks, word carried in person, a feast or wedding they were asked to.</summary>
        Talk,
        /// <summary>People walked safely somewhere: settlers to their hamlet, a pilgrim home.</summary>
        Escort,
        /// <summary>A mission's own work: hands for a harvest, an apprentice, a precious lump to dig out.</summary>
        Errand
    }

    /// <summary>How dangerous the place is, against the home's own threat.</summary>
    public enum JourneyDanger : byte
    {
        None,
        Low,
        Some,
        High
    }

    /// <summary>Where a colonist stands for this journey.</summary>
    public enum AdviceGroup : byte
    {
        Recommended,
        CanGo,
        BetterNot
    }

    /// <summary>The one line of reason beside a colonist or an animal in the planner.</summary>
    public enum AdviceWhy : byte
    {
        None,
        // Recommended
        Named,
        BestFighter,
        Fighter,
        Doctor,
        Negotiator,
        Speaker,
        Talker,
        Hands,
        Carrier,
        Guard,
        Able,
        // Can go
        CanGo,
        NotNeeded,
        Unarmed,
        Child,
        Spare,
        // Better not
        Downed,
        Mental,
        Wounds,
        InBed,
        Sick,
        Hurt,
        Pain,
        Slow,
        Pregnant,
        ChildDanger,
        Breaking,
        NoViolence,
        OnlyDoctor,
        OnlyCook,
        BabyAlone,
        KeepHome,
        // Animals
        PackLoad,
        Mount,
        PackSpare,
        Pet,
        Young,
        AnimalHurt
    }

    /// <summary>What a choice of who goes leaves the home without.</summary>
    public enum HomeWarning : byte
    {
        NoDoctor,
        NoCook,
        NoGrower,
        NoWarden,
        FewDefenders,
        BabyAlone,
        Empty
    }

    /// <summary>What the journey asks of the people who go.</summary>
    public struct JourneyWants
    {
        public JourneyAim aim;
        public JourneyDanger danger;
        /// <summary>A doctor good enough to be the party's own: someone picked with this much Medicine already is.</summary>
        public const int DoctorSkill = 6;

        /// <summary>A doctor along: a fight, a rescue, a long or dangerous road.</summary>
        public bool doctor;
        /// <summary>A skill the place calls for (<see cref="JourneyAdviceMath.Plants"/>, <see cref="JourneyAdviceMath.Mining"/>), or -1.</summary>
        public int skill;
        /// <summary>The one the errand names (their id), or -1.</summary>
        public int named;
        /// <summary>The errand's own number of grown colonists (two for an escort, a feast, a harvest, a call to arms), or 0.</summary>
        public int minPeople;
        /// <summary>The days on the road.</summary>
        public float days;
        /// <summary>The growing season now (a grower left at home matters), and prisoners held (a warden does).</summary>
        public bool growing, prisoners;
    }

    /// <summary>
    /// Who to bring on a journey and what it costs the home (Journeys/JourneyAdvice.cs reads the colonists and the place,
    /// UI/JourneyDialog.cs shows it): each colonist read as a few numbers (health, mind, the skills a journey uses, gear,
    /// needs on the road, what they do at home), put in a group (recommended, can go, better not) with one reason; the
    /// party's size by what the journey is for, the danger and the size of the colony; the home never left without a doctor,
    /// a cook or enough defenders when another choice would do; pack animals by the load and mounts by the people; medicine
    /// by the party and the danger; trade goods by the room; which of the stores and the stalls an item is taken from.
    /// Pure, so Tools/Tests can check it.
    /// </summary>
    public static class JourneyAdviceMath
    {
        public const int Shooting = 0, Melee = 1, Medicine = 2, Social = 3, Animals = 4, Plants = 5, Mining = 6, SkillCount = 7;

        /// <summary>Offices that matter while away: none, the marshal, the speaker, another seat.</summary>
        public const int NoOffice = 0, Marshal = 1, Speaker = 2, OtherOffice = 3;

        /// <summary>A colonist as the advice reads them.</summary>
        public sealed class Person
        {
            public int id;
            public bool grown = true;
            // Now
            public bool downed, mental;
            // Health
            /// <summary>The game's summary of their health (0 to 1), pain (0 to 1), and how well they walk (1 whole).</summary>
            public float health = 1f, pain, moving = 1f;
            public bool sick, inBed, wounds, pregnant;
            public int injuries, missing;
            // Mind
            public float mood = 0.6f, minor = 0.35f, major = 0.2f;
            /// <summary>The heart's band (StrainBand): 0 steady, 1 strained, 2 worn, 3 breaking.</summary>
            public int band;
            // Skills: the level, or -1 where they can't do the work.
            public int[] skills = new int[SkillCount];
            // Gear
            public bool armed, ranged, violent = true;
            /// <summary>Their weapon's quality (0 awful to 6 legendary), -1 without one or a quality.</summary>
            public int quality = -1;
            /// <summary>The best sharp armour of what they wear (0 to 2).</summary>
            public float armour;
            /// <summary>Degrees past comfortable their clothes leave them on the road (0 when covered).</summary>
            public float clothesShort;
            // On the road
            public bool addicted, drugsShort;
            /// <summary>What they can carry, in kilograms.</summary>
            public float capacity = 35f;
            // At home
            public bool doctor, cook, grower, warden;
            public int office;
            /// <summary>The only parent at home of a baby there; a lover who stays home.</summary>
            public bool babyAlone, loverStays;

            public int Skill(int s) => skills != null && s >= 0 && s < skills.Length ? skills[s] : -1;

            /// <summary>Able to fight: grown or not, willing to do violence.</summary>
            public bool Fights => violent && !downed;
        }

        /// <summary>One colonist's place in the advice.</summary>
        public struct Placed
        {
            public int id;
            public AdviceGroup group;
            public AdviceWhy why;
            public float score;
        }

        /// <summary>The whole advice on who goes: each colonist placed, the recommended in order, and what that choice leaves home without.</summary>
        public sealed class Advice
        {
            public List<Placed> placed = new List<Placed>();
            public List<int> recommended = new List<int>();
            public List<HomeWarning> warnings = new List<HomeWarning>();
            public int min, ideal;

            public Placed Of(int id)
            {
                foreach (var p in placed)
                    if (p.id == id) return p;
                return new Placed { id = id, group = AdviceGroup.CanGo, why = AdviceWhy.CanGo };
            }
        }

        // ---------------- The danger and the party ----------------

        /// <summary>The danger of a place by its threat points against the home's: none without hostiles, low under half, some up to a little over the home's, high past that.</summary>
        public static JourneyDanger DangerOf(float points, float homePoints)
        {
            if (points <= 0f) return JourneyDanger.None;
            if (homePoints <= 0f) return JourneyDanger.Some;
            float r = points / homePoints;
            return r < 0.5f ? JourneyDanger.Low : r < 1.1f ? JourneyDanger.Some : JourneyDanger.High;
        }

        /// <summary>Grown colonists who should stay home to keep it: none with one, one with two or three, two in five otherwise.</summary>
        public static int KeepHome(int grown) => grown <= 1 ? 0 : grown <= 3 ? 1 : (int)Math.Ceiling(grown * 0.4 - 1e-6);

        /// <summary>The most grown colonists to send and still keep the home.</summary>
        public static int MostToSend(int grown) => Math.Max(0, grown - KeepHome(grown));

        /// <summary>
        /// How many go, at least and ideally: by what the journey is for, the danger (a fight takes more the stronger the
        /// place, any other journey a guard more where there is danger), the errand's own number, and never more than the
        /// home can spare (unless the errand's own number asks it).
        /// </summary>
        public static (int min, int ideal) PartySize(JourneyAim aim, JourneyDanger danger, int grown, int minPeople)
        {
            int min, ideal;
            switch (aim)
            {
                case JourneyAim.Fight:
                    min = danger == JourneyDanger.High ? 3 : 2;
                    ideal = danger == JourneyDanger.High ? (grown >= 10 ? 6 : 5) : danger == JourneyDanger.Some ? 3 : 2;
                    break;
                case JourneyAim.Trade:
                    min = 1;
                    ideal = 2;
                    break;
                case JourneyAim.Talk:
                    min = 1;
                    ideal = 1;
                    break;
                case JourneyAim.Escort:
                    min = 2;
                    ideal = 2;
                    break;
                case JourneyAim.Errand:
                    min = 1;
                    ideal = 1;
                    break;
                default:
                    min = 1;
                    ideal = 2;
                    break;
            }
            if (aim != JourneyAim.Fight && danger >= JourneyDanger.Some) ideal += danger == JourneyDanger.High ? 2 : 1;
            // The danger's number gives way to the home; the errand's own doesn't.
            int spare = Math.Max(1, MostToSend(grown));
            min = Math.Max(Math.Min(min, spare), minPeople);
            ideal = Math.Max(Math.Min(ideal, spare), min);
            int all = Math.Max(1, grown);
            return (Math.Min(min, all), Math.Min(ideal, all));
        }

        /// <summary>A doctor good enough to be the party's own: someone picked with this much Medicine already is.</summary>
        public const int DoctorSkill = 6;

        /// <summary>A doctor along: a fight, a rescue, an escort or any journey with danger, or a road of four days or more.</summary>
        public static bool WantsDoctor(JourneyAim aim, JourneyDanger danger, float days) =>
            aim == JourneyAim.Fight || danger >= JourneyDanger.Some || days >= 4f || (aim == JourneyAim.Escort && danger > JourneyDanger.None);

        // ---------------- One colonist ----------------

        /// <summary>How good a fighter: the better of their shooting and melee (the one their weapon uses counts whole), armed, armoured, a good weapon. 0 for one who won't fight.</summary>
        public static float FighterScore(Person p)
        {
            if (!p.Fights) return 0f;
            float shoot = Math.Max(0, p.Skill(Shooting)), melee = Math.Max(0, p.Skill(Melee));
            float s = Math.Max(shoot * (p.armed && p.ranged ? 1f : 0.7f), melee * (p.armed && !p.ranged ? 1f : 0.7f));
            if (p.armed) s += 3f + (p.quality >= 4 ? 1f : 0f);
            s += Math.Min(1.2f, p.armour) * 5f;
            return s;
        }

        /// <summary>Why someone had better not go, whoever else could: down, not in their right mind, hurt or ill, with child, a child where it's dangerous, near a break, or unwilling to fight in a fight.</summary>
        public static AdviceWhy BetterNot(Person p, JourneyWants w)
        {
            if (p.downed) return AdviceWhy.Downed;
            if (p.mental) return AdviceWhy.Mental;
            if (p.wounds) return AdviceWhy.Wounds;
            if (p.inBed) return AdviceWhy.InBed;
            if (p.sick) return AdviceWhy.Sick;
            if (p.moving < 0.6f) return AdviceWhy.Slow;
            if (p.health < 0.6f) return AdviceWhy.Hurt;
            if (p.pain > 0.35f) return AdviceWhy.Pain;
            if (p.pregnant) return AdviceWhy.Pregnant;
            if (!p.grown && (w.aim == JourneyAim.Fight || w.aim == JourneyAim.Escort || w.danger >= JourneyDanger.Some)) return AdviceWhy.ChildDanger;
            if (p.band >= 3 || p.mood < p.minor) return AdviceWhy.Breaking;
            if (w.aim == JourneyAim.Fight && !p.violent) return AdviceWhy.NoViolence;
            if (p.babyAlone) return AdviceWhy.BabyAlone;
            return AdviceWhy.None;
        }

        /// <summary>What a colonist's going takes away besides them: a seat, a lover left behind, their work at home.</summary>
        public static float HomeCost(Person p, JourneyWants w)
        {
            float c = 0f;
            if (p.office == Marshal && w.aim != JourneyAim.Fight) c += 1.5f;
            if (p.office == Speaker && w.aim != JourneyAim.Talk) c += 1.5f;
            if (p.office == OtherOffice) c += 0.5f;
            if (p.loverStays) c += 1f;
            if (p.cook) c += 0.5f;
            if (p.grower && w.growing) c += 0.5f;
            if (p.doctor) c += 0.5f;
            return c;
        }

        /// <summary>How tired of things they are: a strained heart, a worn one, a mood low but not yet breaking.</summary>
        public static float MindCost(Person p) => (p.band == 1 ? 1f : p.band == 2 ? 3f : 0f) + (p.mood < p.minor + 0.1f ? 2f : 0f);

        /// <summary>How well someone suits the journey, by what it is for (the higher the better), less what their going costs.</summary>
        public static float Score(Person p, JourneyWants w)
        {
            float fighter = FighterScore(p);
            float social = Math.Max(0, p.Skill(Social)), med = Math.Max(0, p.Skill(Medicine));
            float s;
            switch (w.aim)
            {
                case JourneyAim.Fight:
                    s = fighter;
                    break;
                case JourneyAim.Trade:
                    s = social * 0.8f + p.capacity / 10f + fighter * 0.3f;
                    break;
                case JourneyAim.Talk:
                    s = social + fighter * 0.3f + (p.office == Speaker ? 3f : 0f);
                    break;
                case JourneyAim.Escort:
                    s = fighter + social * 0.2f;
                    break;
                case JourneyAim.Errand:
                    s = (w.skill >= 0 ? Math.Max(0, p.Skill(w.skill)) : social * 0.5f) + fighter * 0.3f;
                    break;
                default:
                    s = 4f + fighter * 0.5f + med * 0.3f + Math.Max(0, p.Skill(Animals)) * 0.1f;
                    break;
            }
            if (w.danger >= JourneyDanger.Some && w.aim != JourneyAim.Fight) s += fighter * 0.3f;
            if (p.clothesShort > 5f) s -= 1f;
            if (p.addicted && p.drugsShort) s -= 2f;
            return s - MindCost(p) - HomeCost(p, w) - (p.injuries > 0 ? 1f : 0f) - (p.missing > 0 ? 1f : 0f);
        }

        // ---------------- Who goes ----------------

        private static bool Does(Person p, Func<Person, bool> work) => p != null && work(p);

        /// <summary>
        /// What a choice of who goes leaves the home without: nobody who doctors, cooks, grows in the growing season or
        /// keeps the prisoners, fewer grown colonists able to fight than <see cref="KeepHome"/> keeps, a baby without a
        /// parent, or nobody at all. <paramref name="people"/> is everyone at home who could go.
        /// </summary>
        public static List<HomeWarning> HomeLeft(IList<Person> people, ICollection<int> going, JourneyWants w)
        {
            var list = new List<HomeWarning>();
            if (people == null || going == null || going.Count == 0) return list;
            var stay = people.Where(p => p != null && !going.Contains(p.id)).ToList();
            if (stay.Count == 0)
            {
                list.Add(HomeWarning.Empty);
                return list;
            }
            if (people.Any(p => Does(p, x => x.doctor)) && !stay.Any(p => p.doctor)) list.Add(HomeWarning.NoDoctor);
            if (people.Any(p => Does(p, x => x.cook)) && !stay.Any(p => p.cook)) list.Add(HomeWarning.NoCook);
            if (w.growing && people.Any(p => Does(p, x => x.grower)) && !stay.Any(p => p.grower)) list.Add(HomeWarning.NoGrower);
            if (w.prisoners && people.Any(p => Does(p, x => x.warden)) && !stay.Any(p => p.warden)) list.Add(HomeWarning.NoWarden);
            int grown = people.Count(p => p != null && p.grown);
            int keep = KeepHome(grown);
            if (keep > 0 && stay.Count(p => p.grown && p.Fights) < keep) list.Add(HomeWarning.FewDefenders);
            if (people.Any(p => p != null && p.babyAlone && going.Contains(p.id))) list.Add(HomeWarning.BabyAlone);
            return list;
        }

        /// <summary>Whether sending this one as well leaves the home without something it had (a doctor, a cook, enough defenders).</summary>
        private static bool Costs(IList<Person> people, HashSet<int> going, Person p, JourneyWants w)
        {
            var before = HomeLeft(people, going, w);
            going.Add(p.id);
            var after = HomeLeft(people, going, w);
            going.Remove(p.id);
            foreach (var h in after)
                if ((h == HomeWarning.NoDoctor || h == HomeWarning.NoCook || h == HomeWarning.FewDefenders) && !before.Contains(h))
                    return true;
            return false;
        }

        /// <summary>The reason of a recommended one, by the slot they fill.</summary>
        private static AdviceWhy WhyPicked(Person p, JourneyWants w, float bestFighter)
        {
            switch (w.aim)
            {
                case JourneyAim.Fight:
                case JourneyAim.Escort:
                    float f = FighterScore(p);
                    return f >= bestFighter - 0.01f && f > 0f ? AdviceWhy.BestFighter : p.armed ? AdviceWhy.Fighter : AdviceWhy.Able;
                case JourneyAim.Trade:
                    return AdviceWhy.Carrier;
                case JourneyAim.Talk:
                case JourneyAim.Errand:
                case JourneyAim.Travel:
                default:
                    return w.danger >= JourneyDanger.Some && p.armed ? AdviceWhy.Guard : AdviceWhy.Able;
            }
        }

        /// <summary>
        /// Who to bring: each colonist placed (better not for their own reasons, else can go), then the recommended picked
        /// in order (the errand's own named one; the lead by what the journey is for: the best talker for trade and talks, the
        /// best hands for an errand's skill, the best fighter otherwise; a doctor where the journey wants one; then the best
        /// by score), up to the party's ideal size. Someone whose going would leave the home without a doctor, a cook or
        /// enough defenders is passed over while another would do, and placed better not for it; past the party's least
        /// size the home's need gives way, and the warnings say so.
        /// </summary>
        public static Advice Pick(IList<Person> people, JourneyWants w)
        {
            var a = new Advice();
            if (people == null || people.Count == 0) return a;
            int grown = people.Count(p => p != null && p.grown);
            (a.min, a.ideal) = PartySize(w.aim, w.danger, grown, w.minPeople);
            // A doctor wanted on a journey of one: a second goes, where the home can spare them.
            if (w.doctor && a.ideal < 2 && MostToSend(grown) >= 2) a.ideal = 2;
            var why = new Dictionary<int, AdviceWhy>();
            var score = new Dictionary<int, float>();
            var able = new List<Person>();
            foreach (var p in people)
            {
                if (p == null) continue;
                var no = BetterNot(p, w);
                score[p.id] = Score(p, w);
                if (no != AdviceWhy.None) why[p.id] = no;
                else able.Add(p);
            }
            float bestFighter = able.Count == 0 ? 0f : able.Max(FighterScore);
            var going = new HashSet<int>();
            var passed = new Dictionary<int, AdviceWhy>();

            // force: the errand names them, or the party is short of its least size whatever the home loses.
            bool TryTake(Person p, AdviceWhy reason, bool force)
            {
                if (p == null || going.Contains(p.id) || why.ContainsKey(p.id)) return false;
                // Children go only where it is safe, and never fill the party's slots.
                if (!p.grown && !force) return false;
                if (!force && Costs(people, going, p, w))
                {
                    passed[p.id] = p.doctor && !people.Any(x => x != null && x.id != p.id && x.doctor && !going.Contains(x.id)) ? AdviceWhy.OnlyDoctor
                        : p.cook && !people.Any(x => x != null && x.id != p.id && x.cook && !going.Contains(x.id)) ? AdviceWhy.OnlyCook : AdviceWhy.KeepHome;
                    return false;
                }
                going.Add(p.id);
                a.recommended.Add(p.id);
                why[p.id] = reason;
                passed.Remove(p.id);
                return true;
            }

            // The lead: the first who costs the home nothing it needs, else the first.
            void Lead(IEnumerable<Person> ordered, AdviceWhy reason)
            {
                var list = ordered.ToList();
                foreach (var p in list)
                    if (TryTake(p, reason, false))
                        return;
                if (going.Count < a.min && list.Count > 0) TryTake(list[0], reason, true);
            }

            // The errand's own named one.
            if (w.named >= 0) TryTake(able.FirstOrDefault(p => p.id == w.named), AdviceWhy.Named, true);
            var order = able.Where(p => p.grown).OrderByDescending(p => score[p.id]).ThenBy(p => p.id).ToList();
            // The lead.
            IEnumerable<Person> By(int skill) => able.Where(p => p.grown && p.Skill(skill) >= 0).OrderByDescending(p => p.Skill(skill)).ThenByDescending(p => score[p.id]).ThenBy(p => p.id);
            switch (w.aim)
            {
                case JourneyAim.Trade:
                    Lead(By(Social), AdviceWhy.Negotiator);
                    break;
                case JourneyAim.Talk:
                    var speaker = able.FirstOrDefault(p => p.grown && p.office == Speaker && p.Skill(Social) >= 4);
                    if (speaker == null || !TryTake(speaker, AdviceWhy.Speaker, false)) Lead(By(Social), AdviceWhy.Talker);
                    break;
                case JourneyAim.Errand when w.skill >= 0:
                    Lead(By(w.skill), AdviceWhy.Hands);
                    break;
            }
            // A doctor along, when the journey wants one and nobody picked is one already: never the home's last.
            if (w.doctor && going.Count < Math.Max(a.ideal, a.min) && !able.Any(p => going.Contains(p.id) && p.Skill(Medicine) >= DoctorSkill))
                foreach (var p in able.Where(p => p.grown && p.Skill(Medicine) >= 3 && !going.Contains(p.id)).OrderByDescending(p => p.Skill(Medicine)).ThenByDescending(p => score[p.id]).ThenBy(p => p.id))
                {
                    var g = new HashSet<int>(going) { p.id };
                    if (HomeLeft(people, g, w).Contains(HomeWarning.NoDoctor) && !HomeLeft(people, going, w).Contains(HomeWarning.NoDoctor))
                    {
                        passed[p.id] = AdviceWhy.OnlyDoctor;
                        continue;
                    }
                    if (TryTake(p, AdviceWhy.Doctor, false)) break;
                }
            // The rest by score, to the ideal size; to the least size even where the home's need gives way.
            foreach (var p in order)
            {
                if (going.Count >= a.ideal) break;
                if (score[p.id] <= 0f && going.Count >= a.min) break;
                TryTake(p, WhyPicked(p, w, bestFighter), false);
            }
            foreach (var p in order)
            {
                if (going.Count >= a.min) break;
                TryTake(p, WhyPicked(p, w, bestFighter), true);
            }
            a.warnings = HomeLeft(people, going, w);
            foreach (var p in people)
            {
                if (p == null) continue;
                var placed = new Placed { id = p.id, score = score.TryGetValue(p.id, out float s) ? s : 0f };
                if (going.Contains(p.id))
                {
                    placed.group = AdviceGroup.Recommended;
                    placed.why = why[p.id];
                }
                else if (why.TryGetValue(p.id, out var no))
                {
                    placed.group = AdviceGroup.BetterNot;
                    placed.why = no;
                }
                else if (passed.TryGetValue(p.id, out var cost))
                {
                    placed.group = AdviceGroup.BetterNot;
                    placed.why = cost;
                }
                else
                {
                    placed.group = AdviceGroup.CanGo;
                    placed.why = !p.grown ? AdviceWhy.Child : w.aim == JourneyAim.Fight && !p.armed ? AdviceWhy.Unarmed : a.recommended.Count >= a.ideal ? AdviceWhy.Spare : AdviceWhy.CanGo;
                }
                a.placed.Add(placed);
            }
            return a;
        }

        /// <summary>The best level of a skill among these colonists (-1 when none can).</summary>
        public static int Best(IEnumerable<Person> people, int skill)
        {
            int best = -1;
            if (people != null)
                foreach (var p in people)
                    if (p != null)
                        best = Math.Max(best, p.Skill(skill));
            return best;
        }

        /// <summary>A skill shown as the colony's best: the highest level there is, and worth something.</summary>
        public static bool IsBest(int level, int best) => level >= 0 && level == best && best >= 3;

        // ---------------- Animals ----------------

        /// <summary>An animal as the advice reads it.</summary>
        public sealed class Beast
        {
            public int id;
            public bool pack, rideable, young, pregnant, hurt;
            /// <summary>What it can carry (kilograms), and how much faster it carries a rider (1 none).</summary>
            public float capacity, ride = 1f;
        }

        /// <summary>One animal's place in the advice.</summary>
        public struct PlacedBeast
        {
            public int id;
            public AdviceGroup group;
            public AdviceWhy why;
        }

        /// <summary>The share of the room the load may fill before a pack animal is wanted: a caravan near its limit crawls.</summary>
        public const float LoadMargin = 0.9f;

        /// <summary>
        /// Which animals to bring: pack animals for the load the people can't carry within <see cref="LoadMargin"/> of their
        /// room (the strongest first), a mount for each traveller where there are any (the fastest first; a caravan rides
        /// as fast as its riders); the rest can go or had better not (young, with young, hurt or ill, or a pet that carries
        /// nothing).
        /// </summary>
        public static List<PlacedBeast> PickBeasts(IList<Beast> beasts, float load, float peopleRoom, int people)
        {
            var list = new List<PlacedBeast>();
            if (beasts == null) return list;
            var why = new Dictionary<int, AdviceWhy>();
            var fit = new List<Beast>();
            foreach (var b in beasts)
            {
                if (b == null) continue;
                if (b.hurt) why[b.id] = AdviceWhy.AnimalHurt;
                else if (b.pregnant) why[b.id] = AdviceWhy.Pregnant;
                else if (b.young) why[b.id] = AdviceWhy.Young;
                else if (!b.pack && !b.rideable) why[b.id] = AdviceWhy.Pet;
                else fit.Add(b);
            }
            var going = new Dictionary<int, AdviceWhy>();
            float room = Math.Max(0f, peopleRoom);
            // Mounts: one for each traveller, the fastest first.
            foreach (var b in fit.Where(b => b.rideable && b.ride > 1f).OrderByDescending(b => b.ride).ThenByDescending(b => b.capacity).ThenBy(b => b.id))
            {
                if (going.Count >= people) break;
                going[b.id] = AdviceWhy.Mount;
                room += b.capacity;
            }
            // Pack animals for what the people and their mounts can't carry.
            foreach (var b in fit.Where(b => b.pack && !going.ContainsKey(b.id)).OrderByDescending(b => b.capacity).ThenBy(b => b.id))
            {
                if (load <= room * LoadMargin) break;
                going[b.id] = AdviceWhy.PackLoad;
                room += b.capacity;
            }
            foreach (var b in beasts)
            {
                if (b == null) continue;
                if (going.TryGetValue(b.id, out var g)) list.Add(new PlacedBeast { id = b.id, group = AdviceGroup.Recommended, why = g });
                else if (why.TryGetValue(b.id, out var no)) list.Add(new PlacedBeast { id = b.id, group = AdviceGroup.BetterNot, why = no });
                else list.Add(new PlacedBeast { id = b.id, group = AdviceGroup.CanGo, why = AdviceWhy.PackSpare });
            }
            return list;
        }

        // ---------------- What to bring ----------------

        /// <summary>Medicine for the party: the planner's own (half a dose each and one for every eight days), and more by the danger: half a dose each where there is some, a dose each where it is high.</summary>
        public static int MedicineFor(int people, float days, JourneyDanger danger, bool fight)
        {
            int baseCount = JourneyMath.Medicine(people, days);
            if (people <= 0) return 0;
            if (danger == JourneyDanger.High) return baseCount + people;
            if (danger == JourneyDanger.Some || fight) return baseCount + (people + 1) / 2;
            return baseCount;
        }

        /// <summary>Silver for trade: half of the colony's, in tens, at least a hundred where there is that much.</summary>
        public static int SilverFor(int stock)
        {
            if (stock <= 0) return 0;
            int half = stock / 2 / 10 * 10;
            return Math.Min(stock, Math.Max(Math.Min(100, stock), half));
        }

        /// <summary>A kind of goods to trade: one's weight and worth, how many there are, and whether it lies on a stall already (set out for sale).</summary>
        public struct Goods
        {
            public float mass, value;
            public int count;
            public bool stall;
        }

        /// <summary>The share of the room left that trade goods may take: some is kept for what they buy.</summary>
        public const float GoodsRoom = 0.6f;

        /// <summary>
        /// Trade goods for the room left: what lies on the colony's stalls first (set out for sale already), then the most
        /// worth for its weight; each as many as fit into <see cref="GoodsRoom"/> of the room. Each entry is the goods' index
        /// and how many.
        /// </summary>
        public static List<(int goods, int count)> PickGoods(IList<Goods> goods, float room)
        {
            var list = new List<(int, int)>();
            if (goods == null) return list;
            float left = Math.Max(0f, room) * GoodsRoom;
            foreach (int i in Enumerable.Range(0, goods.Count).Where(i => goods[i].count > 0 && goods[i].value > 0f)
                         .OrderByDescending(i => goods[i].stall).ThenByDescending(i => goods[i].mass > 0f ? goods[i].value / goods[i].mass : float.MaxValue).ThenBy(i => i))
            {
                var g = goods[i];
                int n = g.mass <= 0f ? g.count : Math.Min(g.count, (int)Math.Floor(left / g.mass + 1e-4));
                if (n <= 0) continue;
                list.Add((i, n));
                left -= n * Math.Max(0f, g.mass);
                if (left <= 0f) break;
            }
            return list;
        }

        /// <summary>
        /// Where an item is taken from: the stores first, then the colony's stalls in turn (what RimMarket's stalls hold
        /// on their counters), each as much as it has. <paramref name="want"/> is how many are still to take; returns how
        /// many from the stores and from each stall, and how many are short.
        /// </summary>
        public static (int stores, int[] stalls, int shortBy) TakeFrom(int want, int inStores, IList<int> onStalls)
        {
            int n = onStalls?.Count ?? 0;
            var stalls = new int[n];
            if (want <= 0) return (0, stalls, 0);
            int fromStores = Math.Min(want, Math.Max(0, inStores));
            int left = want - fromStores;
            for (int i = 0; i < n && left > 0; i++)
            {
                int t = Math.Min(left, Math.Max(0, onStalls[i]));
                stalls[i] = t;
                left -= t;
            }
            return (fromStores, stalls, left);
        }

        /// <summary>A recommended count against what is on the list: how many more to add (none when the list has enough).</summary>
        public static int StillToAdd(int recommended, int onList) => Math.Max(0, recommended - Math.Max(0, onList));
    }
}
