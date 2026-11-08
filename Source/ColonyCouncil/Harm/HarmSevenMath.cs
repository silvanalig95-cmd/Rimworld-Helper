using System;

namespace ColonyCouncil
{
    /// <summary>The stranger who takes advantage of the open gate (Harm/GateTroubles.cs, 1.2.4).</summary>
    public enum GateStranger
    {
        /// <summary>A thief at the stores with a sack, chased off; a fistfight if caught.</summary>
        Thief,
        /// <summary>A drunk who picks a fight in the yard.</summary>
        Drunk
    }

    /// <summary>A mage's practice working that slips (Harm/Stories.cs, 4.8.1, A RimWorld of Magic), by the mage's element.</summary>
    public enum WorkingSlip
    {
        None,
        /// <summary>A fire mage: a burn on the hand.</summary>
        Fire,
        /// <summary>An ice mage: the game's frostbite on the hand.</summary>
        Ice,
        /// <summary>A storm mage: a shock, the game's burn, up the arm.</summary>
        Storm
    }

    /// <summary>
    /// The rates and small rules of harm, part 7: the last of the three batches of the review's remaining proposals the
    /// player took on 7 November 2026 ("all suggestions please"), tempers in the colony (1.1.2, 1.3.1, 1.3.2, 1.5.1,
    /// 3.5.2, 3.7.1, 3.9.1, 3.9.2, 3.10.1, 3.13.1, 4.1.3, 4.7.3, 3.12.2), visitors and outsiders (1.2.4, 2.2.2, 2.5.3,
    /// 2.6.2, 2.8.2, 2.8.3, 2.9.1, 2.9.2, 3.8.1, 3.13.2, 2.3.2, 2.3.3) and personal stories (2.10.1, 3.10.2, 3.10.3, 4.8.1,
    /// 2.6.1, 2.7.1). No game types, so Tools/Tests and Tools/Balance read them. Every chance here is a base rate: the
    /// game's side rolls the occasion once through the harm service with its kind (Harm.Roll, Harm.Chance), so the
    /// setting, the storyteller's lean and the kind's turn within the shared budget all apply once, and the shared cap
    /// holds. The three that hurt nobody of the colony (the nemesis's aim, the lost child, the courier) take the setting
    /// and the lean alone (Harm.Scale).
    /// </summary>
    public static class HarmSevenMath
    {
        public const int DayTicks = HarmMath.DayTicks, HourTicks = HarmMath.HourTicks;

        /// <summary>A hot temper: the persona's fighting temper of 2 or more (as the sore loser's).</summary>
        public const int HotTemper = 2;

        public static bool Hot(int temper) => temper >= HotTemper;

        /// <summary>
        /// The game's own chance that an insult between two comes to blows, for a pair at <paramref name="opinion"/>: the
        /// insult's base chance (0.04 in its def) times the game's own opinion factor (4 at -100, 1 at nothing, 0.6 at
        /// 100). The game's other factors (moving, manipulation, age, gender, traits) are left to the occasion.
        /// </summary>
        public const float InsultBase = 0.04f;

        public static float GameFightAt(int opinion)
        {
            float o = HarmMath.Clamp(opinion, -100f, 100f);
            float factor = o < 0f ? 1f + (-o / 100f) * 3f : 1f - (o / 100f) * 0.4f;
            return InsultBase * factor;
        }

        // ================= The peacemaker's sit-down goes wrong (1.1.2) =================

        /// <summary>One sit-down in ten of the worst pairs (a rivalry of 70 or more) comes to blows instead.</summary>
        public const float SitDownChance = 1f / 10f;

        /// <summary>A rivalry of this or less has cooled: nothing; of <see cref="SitDownWorst"/> or more, the worst pairs.</summary>
        public const float SitDownCool = 30f, SitDownWorst = 70f;

        /// <summary>How hot the pair still is: 0 cooled to 1 at the worst.</summary>
        public static float SitDownHeat(float strength) => HarmMath.Clamp((strength - SitDownCool) / (SitDownWorst - SitDownCool), 0f, 1f);

        /// <summary>The peacemaker's Social: the chance falls with skill (1.25 at nothing, 1 at 5, 0.5 at 15, a quarter at 20).</summary>
        public static float SocialEase(int social) => HarmMath.Clamp(1.25f - social * 0.05f, 0.25f, 1.25f);

        /// <summary>Two hot tempers come to blows; a pair with no hot temper between them half as often.</summary>
        public const float MildPair = 0.5f;

        public static float SitDown(float strength, int social, bool hotPair) => SitDownChance * SitDownHeat(strength) * SocialEase(social) * (hotPair ? 1f : MildPair);

        /// <summary>The peacemaker stepping between them takes a punch one blow-up in three: a bruise of 1 to 3 on an arm.</summary>
        public const float PeacemakerPunch = 1f / 3f, PunchLow = 1f, PunchHigh = 3f;

        // ================= Words with the speaker (1.3.1) =================

        /// <summary>Under demanding stakes, a fifth of the promises broken to a bitter, hot-tempered colonist bring them to the speaker.</summary>
        public const float SpeakerChance = 1f / 5f;

        public static float SpeakerWords(bool demanding, bool bitter, bool hot) => demanding && bitter && hot ? SpeakerChance : 0f;

        /// <summary>A speaker with good Social talks them down: a twentieth for each level, three in four at most.</summary>
        public static float TalksDown(int social) => HarmMath.Clamp(social / 20f, 0f, 0.75f);

        /// <summary>They go to find the speaker within a day.</summary>
        public const float SpeakerHours = 24f;

        // ================= The thing that went to someone else (1.3.2) =================

        /// <summary>For a week after, the first two times they meet (once a day at most) a sharp word, at the game's own fight chance for a pair at -20.</summary>
        public const int PassedOverOpinion = -20, PassedOverWords = 2;

        public const float PassedOverDays = 7f;

        public static float PassedOverChance => GameFightAt(PassedOverOpinion);

        /// <summary>Only the jealous or the abrasive pick a fight over it.</summary>
        public static bool PassedOverNature(bool jealous, bool abrasive) => jealous || abrasive;

        public static bool PassedOverKept(int made, int now) => made <= now && now - made < PassedOverDays * DayTicks;

        // ================= A meeting that boils over (1.5.1) =================

        /// <summary>Two rivals who both pitched, one idea winning: rarer than the hustings' one in eight, one meeting in twelve.</summary>
        public const float MeetingChance = 1f / 12f;

        /// <summary>The peacemaker at the table halves it; the chair's Social eases it, as the peacemaker's does a sit-down (never below a half).</summary>
        public static float Meeting(bool rivalsBothPitched, bool peacemakerThere, int chairSocial) =>
            rivalsBothPitched ? MeetingChance * (peacemakerThere ? 0.5f : 1f) * HarmMath.Clamp(SocialEase(chairSocial), 0.5f, 1.25f) : 0f;

        // ================= Friends stand by (3.5.2) =================

        /// <summary>One social fight in six, a close friend of one of them near steps in; one time in two they take a blow for it.</summary>
        public const float FriendSteps = 1f / 6f, FriendBlow = 1f / 2f;

        /// <summary>Close: a friendship of 45 or more (the social web's), or an opinion of 60; near: within this many cells.</summary>
        public const float CloseFriend = 45f, FriendRange = 12f;

        public const int CloseOpinion = 60;

        /// <summary>The blow: a bruise of 1 to 3 on an arm or the torso.</summary>
        public const float FriendLow = 1f, FriendHigh = 3f;

        // ================= Said to their face (3.7.1) =================

        /// <summary>Gossip run down to its subject's face: one telling in ten, a hot-tempered subject comes to blows with the teller.</summary>
        public const float FaceChance = 1f / 10f;

        public static float Face(bool against, int subjectTemper) => against && Hot(subjectTemper) ? FaceChance : 0f;

        /// <summary>The words are said first: the blows come this long after.</summary>
        public const int FaceDelayTicks = 300;

        // ================= Found out by the wronged one (3.9.1), told by the wrong friend (3.9.2) =================

        /// <summary>A secret that wronged the colony, let slip to one of it: one such reveal in four brings them to the owner, to blows; half with the peacemaker in office; nothing for one confessed.</summary>
        public const float WrongedChance = 1f / 4f;

        public static float Wronged(bool wrongs, bool confessed, bool peacemaker) => wrongs && !confessed ? WrongedChance * (peacemaker ? 0.5f : 1f) : 0f;

        /// <summary>A confidence let slip by the friend it was told to: one such leak in five brings the keeper to the friend, hard words, and blows half the time.</summary>
        public const float LeakChance = 1f / 5f, LeakBlows = 1f / 2f;

        /// <summary>They go to find them within this long.</summary>
        public const float FindHours = 36f;

        // ================= A rival in love comes to blows (3.10.1) =================

        /// <summary>One story in four, the stiff words between the two rivals become a shove; the one they both like thinks less of the one who started it.</summary>
        public const float LoveRivalChance = 1f / 4f;

        /// <summary>The words were said within this long (the stage's talk), and the two are still together.</summary>
        public const int LoveTalkTicks = 2 * HourTicks;

        // ================= Words at the altar (3.13.1) =================

        /// <summary>Once after the council names one faith the colony's, at an evening of it, one time in three a devout of another faith comes to blows with one of its zealots.</summary>
        public const float AltarChance = 1f / 3f;

        /// <summary>Its decree is looked at this long after it was declared (it lasts fifteen days).</summary>
        public const float AltarKeepDays = 20f;

        // ================= The feast that ends in a fistfight (4.1.3), the toast's second round (4.7.3) =================

        /// <summary>Rivals quarrelling at a day's table come to blows one quarrel in four when drink was poured.</summary>
        public const float FeastFistChance = 1f / 4f;

        public static float FeastFists(bool drink) => drink ? FeastFistChance : 0f;

        /// <summary>With strong drink in plenty (two cups in stock for each who drank), a toast pours a second round at one toast in three.</summary>
        public const float SecondRoundChance = 1f / 3f;

        public const int PlentyPerDrinker = 2;

        public static bool Plenty(int cups, int drinkers) => drinkers > 0 && cups >= PlentyPerDrinker * drinkers;

        /// <summary>Two rivals who both took the second round come to blows as a day's table does with drink: one time in four.</summary>
        public const float SecondRoundFists = FeastFistChance;

        /// <summary>The second round is poured once the first is done (the toast's first stretch), and its quarrel looked for this long after.</summary>
        public const int SecondRoundAfter = 1500, SecondRoundEnd = 3000, SecondQuarrelDelay = HourTicks;

        // ================= The soldier's nightmare (3.12.2) =================

        /// <summary>One nightmare in twenty of someone with a soldier's or an outlaw's past, in a shared bedroom, they strike out at whoever wakes them.</summary>
        public const float SoldierChance = 1f / 20f;

        /// <summary>The blow: a bruise of 1 to 3 on the roommate's arm, the mildest.</summary>
        public const float StrikeLow = 1f, StrikeHigh = 3f;

        /// <summary>A soldier's or an outlaw's past, by the backstory's title (whole words).</summary>
        public static readonly string[] SoldierWords =
        {
            "soldier", "mercenary", "marine", "veteran", "guerrilla", "warrior", "raider", "outlaw", "bandit", "pirate", "gang", "gangster", "assassin", "convict",
            "militia", "trooper", "legionary", "commando", "sniper", "knight", "sellsword", "brigand"
        };

        public static bool SoldierPast(string titles)
        {
            if (string.IsNullOrEmpty(titles)) return false;
            foreach (var raw in titles.ToLowerInvariant().Split(new[] { ' ', '-', '/', '(', ')', ',', '.', '\'' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string w = raw.EndsWith("s") && raw.Length > 3 && Array.IndexOf(SoldierWords, raw) < 0 ? raw.Substring(0, raw.Length - 1) : raw;
                if (Array.IndexOf(SoldierWords, w) >= 0) return true;
            }
            return false;
        }

        // ================= Open gates, and who walks in (1.2.4) =================

        /// <summary>At most one stranger an open-gates decree, about half the time; half with the warden of guests in office, half again under the on-guard decree.</summary>
        public const float GatesChance = 1f / 2f;

        public static float Gates(bool warden, bool onGuard) => GatesChance * (warden ? 0.5f : 1f) * (onGuard ? 0.5f : 1f);

        public static GateStranger StrangerOf(float roll) => roll < 0.5f ? GateStranger.Thief : GateStranger.Drunk;

        /// <summary>A thief seen at the stores by one of the colony within this many cells is chased off; caught, it comes to blows half the time.</summary>
        public const float ThiefSeen = 15f, ThiefFights = 1f / 2f;

        /// <summary>The stranger's walk to the stores, and the wait there.</summary>
        public const int ThiefTicks = 900;

        /// <summary>The decree is looked at until it ends, and its one stranger remembered this long.</summary>
        public const float GatesKeepDays = 12f;

        // ================= The gambler's game turns (2.2.2) =================

        /// <summary>The colony playing the gambler, anyone who wants: one game in six a cheat is caught and a hot-tempered loser starts a fistfight.</summary>
        public const float GamblerChance = 1f / 6f;

        /// <summary>The games run in the evening; the gambler is watched for a day and a half.</summary>
        public const float GamblerKeepDays = 1.5f;

        public static bool Evening(int hour) => hour >= 18 && hour <= 23;

        // ================= The barber's nick (2.5.3) =================

        /// <summary>One cut in thirty the razor nicks (a cut of 1 on the shoulder), twice for a poor barber; clippers that never nick in a spacer's hands.</summary>
        public const float NickChance = 1f / 30f;

        /// <summary>A poor barber: their Artistic short of this.</summary>
        public const int PoorBarberSkill = 6;

        public static float Barber(bool poor, bool spacer) => spacer ? 0f : NickChance * (poor ? 2f : 1f);

        public const float NickLow = 1f, NickHigh = 1f;

        // ================= The guest of rank's enemy (2.6.2) =================

        /// <summary>One dignitary's visit in six draws a hothead of their rivals to the gate with a challenge.</summary>
        public const float RankEnemyChance = 1f / 6f;

        /// <summary>The challenger comes within a day or two of the guest, and waits a day.</summary>
        public const float RankEnemyDays = 1.5f;

        // ================= A fistfight that draws a crowd (2.8.2) =================

        /// <summary>One fistfight in five after hard words, others of both sides near join in, up to four in all (one more pair); half with the warden near.</summary>
        public const float CrowdChance = 1f / 5f;

        public const int CrowdMax = 4;

        public const float CrowdRange = 10f;

        public static float Crowd(bool wardenNear) => CrowdChance * (wardenNear ? 0.5f : 1f);

        /// <summary>The pairs that join: one for each of ours and theirs free and willing, up to four in all with the first two.</summary>
        public static int CrowdJoins(int ours, int theirs) => Math.Max(0, Math.Min(Math.Min(ours, theirs), (CrowdMax - 2) / 2));

        /// <summary>Only those with a temper to them join: a fighting temper of 1 or more.</summary>
        public const int CrowdTemper = 1;

        // ================= First blood, not stopped (2.8.3) =================

        /// <summary>One duel to first blood in twenty with a bloodthirsty fighter goes on until someone yields.</summary>
        public const float PastBloodChance = 1f / 20f;

        public static float PastBlood(bool bloodlust) => bloodlust ? PastBloodChance : 0f;

        // ================= A challenge by letter (2.9.1) =================

        /// <summary>One slight in four (hard words with a notable themself, or one of their people beaten in a duel by one of ours) is held, and a challenge written.</summary>
        public const float ChallengeHold = 1f / 4f;

        /// <summary>The letter comes ten to thirty days after; the challenger two to five days after it, unannounced.</summary>
        public const float LetterAfterMin = 10f, LetterAfterMax = 30f, ComesAfterMin = 2f, ComesAfterMax = 5f;

        public static int LetterTicks(float roll) => (int)((LetterAfterMin + HarmMath.Clamp(roll, 0f, 1f) * (LetterAfterMax - LetterAfterMin)) * DayTicks);

        public static int ComesTicks(float roll) => (int)((ComesAfterMin + HarmMath.Clamp(roll, 0f, 1f) * (ComesAfterMax - ComesAfterMin)) * DayTicks);

        /// <summary>A challenge kept this long at most; refused, the notable thinks this much less of the colonist.</summary>
        public const float ChallengeKeepDays = 60f, RefusedRegard = -6f;

        // ================= The heir's grudge (2.9.2) =================

        /// <summary>A notable the colony wronged (their regard of it at -20 or worse) dies: one heir in three carries it on.</summary>
        public const float HeirChance = 1f / 3f, HeirWronged = -20f;

        /// <summary>While it holds, each visit of their people the guards are short with the colony, and one in five comes to a fistfight.</summary>
        public const float HeirVisitFight = 1f / 5f;

        public const float HeirKeepDays = 120f;

        // ================= A trader of the hated people (3.8.1) =================

        /// <summary>The sworn people at peace: one visit in four, the avenger picks a fight with one of them.</summary>
        public const float HatedChance = 1f / 4f;

        // ================= Zeal at the gate (3.13.2) =================

        /// <summary>A devout of the colony's faith and a visitor of another: hard words one visit in ten, half under the best-behavior decree; the outsiders' fistfight chance after.</summary>
        public const float ZealChance = 1f / 10f;

        public static float Zeal(bool manners) => ZealChance * (manners ? 0.5f : 1f);

        // ================= Hungry hands in the fields (2.3.2) =================

        /// <summary>In a famine near the colony, one night in fifteen hungry strangers come for the fields or the stores (once or twice a famine); half with a night watch, half with lights in the fields; never while alms or famine bread is given out.</summary>
        public const float HungryNightChance = 1f / 15f;

        public static float Hungry(bool fed, bool watch, bool lit) => fed ? 0f : HungryNightChance * (watch ? 0.5f : 1f) * (lit ? 0.5f : 1f);

        /// <summary>The night: ten to three.</summary>
        public static bool HungryHour(int hour) => hour >= 22 || hour < 3;

        /// <summary>One of the colony who meets them (within this many cells) is struck before they flee one time in two: a bruise of 2 to 4.</summary>
        public const float HungryMet = 6f, HungryStrike = 1f / 2f, HungryLow = 2f, HungryHigh = 4f;

        /// <summary>Two or three of them, an hour at most in the fields before they go.</summary>
        public const int HungryMin = 2, HungryMax = 3, HungryTicks = 2500;

        // ================= A brawl at the creek (2.3.3) =================

        /// <summary>Two parties of prospectors on the colony's land at once: once a rush, half the time, their quarrel becomes a brawl; one of ours within a few cells is caught in it.</summary>
        public const float CreekChance = 1f / 2f, CreekCaught = 4f, CreekLow = 2f, CreekHigh = 5f;

        /// <summary>A rush is remembered this long (its one look).</summary>
        public const float CreekKeepDays = 30f;

        /// <summary>The marshal at home and free goes over and breaks it up before anyone of ours is caught.</summary>
        public static bool CreekCatches(bool near, bool marshalThere) => near && !marshalThere;

        // ================= The nemesis seeks the one they hurt (2.10.1, outside the budget) =================

        /// <summary>A returning nemesis in a fight goes for the one it hurt, once a return, if they are on the field and not behind cover.</summary>
        public static bool Seeks(bool victimHere, bool inCover, bool alreadyLooked) => victimHere && !inCover && !alreadyLooked;

        /// <summary>Behind cover: under a roof indoors, or cover between them blocking this much or more.</summary>
        public const float CoverBlock = 0.4f;

        /// <summary>The push lasts this long at most; the game's own fight after.</summary>
        public const int SeekTicks = 1500;

        // ================= The worst night, alone (3.10.2) =================

        /// <summary>The worst night with the friend not at their side: half the time, the walk to the stores for the drug and the game's own binge.</summary>
        public const float WorstNightChance = 1f / 2f;

        /// <summary>The worst of the night: eleven to two.</summary>
        public static bool WorstHour(int hour) => hour >= 23 || hour < 2;

        /// <summary>At their side: within this many cells.</summary>
        public const float SideRange = 8f;

        // ================= First steps alone (3.10.3) =================

        /// <summary>First steps on the new leg without the friend, near the gate: one time in three a fall, a bruise of 2 to 4.</summary>
        public const float FirstStepsChance = 1f / 3f, StepsLow = 2f, StepsHigh = 4f, GateNear = 10f;

        public static bool Daytime(int hour) => hour >= 9 && hour <= 17;

        // ================= A working gone wrong (4.8.1, A RimWorld of Magic) =================

        /// <summary>The long practice at dawn: one working in 25 slips; half with the teacher there.</summary>
        public const float WorkingChance = 1f / 25f;

        public static float Working(WorkingSlip slip, bool teacher) => slip == WorkingSlip.None ? 0f : WorkingChance * (teacher ? 0.5f : 1f);

        public static WorkingSlip SlipOf(bool fire, bool ice, bool storm) => fire ? WorkingSlip.Fire : ice ? WorkingSlip.Ice : storm ? WorkingSlip.Storm : WorkingSlip.None;

        /// <summary>A burn or the frostbite on a hand, 2 to 4; a storm's shock 2 to 4 up an arm.</summary>
        public const float WorkingLow = 2f, WorkingHigh = 4f;

        /// <summary>Dawn: five to seven; the practice a while in the open.</summary>
        public static bool Dawn(int hour) => hour >= 5 && hour <= 7;

        public const int PracticeTicks = 900;

        // ================= The lost child, found hurt (2.6.1, outside the budget) =================

        /// <summary>Found only after the first night out: about half of them hurt; found early, unhurt.</summary>
        public const float LostHurtChance = 1f / 2f;

        /// <summary>A night has passed out there: from the hour they were lost, through the next dawn (four in the morning).</summary>
        public static bool NightPassed(int startHour, float hoursOut)
        {
            float toDawn = startHour < 4 ? 4 - startHour : 28 - startHour;
            return hoursOut >= toDawn;
        }

        /// <summary>What it is, by the night they had: frostnip in a frost, a chill in the wet or cold, else a turned ankle (a child's scraped knee).</summary>
        public static int LostWhich(float coldest, bool wet)
        {
            if (coldest <= 0f) return 0;
            if (wet || coldest <= 8f) return 1;
            return 2;
        }

        public const float ScrapeLow = 1f, ScrapeHigh = 2f;

        // ================= Set upon at the edge (2.7.1, outside the budget) =================

        /// <summary>Half of the messengers waylaid on a dangerous road are set upon at the colony's own edge instead of coming in hurt.</summary>
        public const float SetUponShare = 1f / 2f;

        /// <summary>One of the colony within this many cells of where they come in scares the bandits off before they strike.</summary>
        public const float SetUponHelp = 25f;

        /// <summary>Two or three bandits; the courier's cut as before, 3 to 6.</summary>
        public const int BanditsMin = 2, BanditsMax = 3;

        public const float SetUponLow = 3f, SetUponHigh = 6f;
    }
}
