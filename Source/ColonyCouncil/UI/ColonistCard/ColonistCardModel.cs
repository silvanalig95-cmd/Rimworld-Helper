using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A line a feature adds under a colonist's kit on their card (<see cref="ColonistCardHooks.KitLines"/>), or one of the
    /// card's own (an empty slot of the weapon kit, a better weapon): an icon, a line cut to the column with the whole of it
    /// in its tooltip, the thing it is about (its picture; a click opens its card), and an act at its right, greyed with why
    /// not. Made when the card's model is built, never while it is drawn.
    /// </summary>
    public sealed class ColonistCardLine
    {
        public Texture2D icon;
        public Color color = Color.white;
        public string text, tip;
        public Thing thing;
        /// <summary>The act's label, why it can't be done now (null when it can), and what it does.</summary>
        public string act, whyNot, actTip;
        public Action run;
    }

    /// <summary>
    /// Where other parts of Rimfolk add to a colonist's card. Apparel suggestions (the next step of docs/design/apparel.md,
    /// another piece of work) go in through <see cref="KitLines"/>: lines under the apparel, each with its act ("Wear",
    /// "Fetch"), asked once every few seconds while the card shows, as the weapon suggestions above them are. Call it once,
    /// from a static constructor with [StaticConstructorOnStartup]; a line source added again by the same id replaces the first.
    /// </summary>
    public static class ColonistCardHooks
    {
        internal static readonly List<(string id, Func<Pawn, IEnumerable<ColonistCardLine>> lines)> kit = new List<(string, Func<Pawn, IEnumerable<ColonistCardLine>>)>();

        /// <summary>Lines under a colonist's apparel on their card (an apparel suggestion), built with the card's kit.</summary>
        public static void KitLines(string id, Func<Pawn, IEnumerable<ColonistCardLine>> lines)
        {
            if (id.NullOrEmpty() || lines == null) return;
            kit.RemoveAll(x => x.id == id);
            kit.Add((id, lines));
        }
    }

    /// <summary>
    /// What a colonist's card shows (UI/ColonistCard/ColonistCardWindow.cs), built when the card opens and again now and then
    /// while it shows: the live parts twice a second (their mood and the heart, the header's state), the heavy parts every
    /// three seconds (who they are, their skills, kit and life, the lines and acts other parts add) or at once after an act on
    /// the card. Nothing is worked out or translated while the card is drawn: it reads this.
    /// </summary>
    public sealed class ColonistCardModel
    {
        /// <summary>A skill's row: its name, level, the bar's fill, its passion's mark, whether it is one they're best at or can't do, and its tooltip.</summary>
        public sealed class SkillRow
        {
            public string label, level, tip;
            public float fill;
            public bool disabled, best;
            public Texture2D passion;
            public Color passionColor = Color.white;
        }

        /// <summary>A weapon or a worn piece: the thing, its name and what it is to them, its rating and theirs, and its tooltip.</summary>
        public sealed class GearRow
        {
            public Thing thing;
            public string name, second;
            public float rating = float.NaN;
            public Color nameColor = Color.white;
            public bool weapon;
            public WornSlot slot;
            public Func<string> tip;
        }

        public Pawn pawn;
        public float liveAt = -1f, heavyAt = -1f;
        private int staled = -1;

        // ---- The header ----
        public string name, who, whoTip;
        public readonly List<UiKit.PillSpec> chips = new List<UiKit.PillSpec>();
        public bool heartOn;
        public float strain, good;
        public Color strainColor, goodColor;
        public string heartTip;
        /// <summary>The colonists either side in the colonist bar's order, for the arrows (null: none to step to).</summary>
        public Pawn prev, next;
        public string prevTip, nextTip;

        // ---- Now ----
        public PawnMood mood;
        public MoodGlance glance;
        public string noMood;

        // ---- Skills ----
        public readonly List<SkillRow> skills = new List<SkillRow>();
        public string best, noSkills;

        // ---- Kit ----
        public readonly List<GearRow> weapons = new List<GearRow>();
        public readonly List<GearRow> worn = new List<GearRow>();
        public readonly List<ColonistCardLine> kitLines = new List<ColonistCardLine>();
        public readonly List<ColonistCardLine> hookLines = new List<ColonistCardLine>();
        public string noWeapon, noWorn, weather, weatherTip;
        public Color weatherColor = Color.white;

        // ---- Life ----
        public StoryGlance story;
        /// <summary>What other parts of Rimfolk say about them on a colonist's card (a ward, a ransom, a match), lines with links.</summary>
        public readonly List<string> also = new List<string>();

        // ---- The foot ----
        /// <summary>What other parts let the colony do from a colonist's card (a ward's guardian, an envoy recalled), each greyed with why not.</summary>
        public readonly List<CardAct> acts = new List<CardAct>();
        /// <summary>Why the game's tabs can't open for them now (in a caravan, away), or null.</summary>
        public string tabsWhyNot;
        /// <summary>The foot's buttons' tooltips, said for this colonist.</summary>
        public readonly string[] footTips = new string[FootKeys.Length];

        /// <summary>The foot's buttons: the game's four tabs, going to them, selecting them, their whole story, and what else (a menu).</summary>
        public static readonly string[] FootKeys =
        {
            "CC_CCard_Foot_Mood", "CC_CCard_Foot_Story", "CC_CCard_Foot_Gear", "CC_CCard_Foot_Health", "CC_CCard_Foot_GoTo", "CC_CCard_Foot_Select",
            "CC_CCard_Foot_WholeStory", "CC_CCard_Foot_More"
        };

        private static string T(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        /// <summary>A card shows someone else (the arrows): everything is built again at the next draw.</summary>
        public void Show(Pawn p)
        {
            pawn = p;
            liveAt = heavyAt = -1f;
        }

        /// <summary>An act on the card changed what it shows: everything is built again at the next draw.</summary>
        public void Stale() => liveAt = heavyAt = -1f;

        /// <summary>Builds what is due: the live parts twice a second, the heavy parts every three seconds (or after a promise).</summary>
        public void Refresh(Pawn p)
        {
            if (p != pawn) Show(p);
            float now = Time.realtimeSinceStartup;
            if (staled != StoryView.Staled)
            {
                staled = StoryView.Staled;
                heavyAt = -1f;
            }
            if (ColonistCardMath.Due(now, heavyAt, ColonistCardMath.HeavyEvery))
            {
                heavyAt = now;
                Part("who they are", Who);
                Part("skills", Skills);
                Part("kit", Kit);
                Part("life", Life);
                Part("what else is said", Others);
            }
            if (ColonistCardMath.Due(now, liveAt, ColonistCardMath.LiveEvery))
            {
                liveAt = now;
                Part("their mood", Now);
                Part("the header", Header);
            }
        }

        private void Part(string what, Action build)
        {
            try
            {
                build();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's card: " + what, e);
            }
        }

        // ---------------- The header ----------------

        private void Who()
        {
            var p = pawn;
            name = p.Name is NameTriple full ? full.ToStringFull : p.LabelCap;
            var comp = CouncilGameComponent.Instance;
            var line = comp != null ? StoryView.Who(comp, p) : null;
            who = line?.line;
            whoTip = line?.tip;
            tabsWhyNot = p.SpawnedOrAnyParentSpawned ? null : T("CC_Card_Why_NotOnMap", p.LabelShort.Named("NAME"));
            for (int i = 0; i < FootKeys.Length; i++) footTips[i] = T(FootKeys[i] + "_Tip", p.Named("PAWN"));
        }

        private void Header()
        {
            var p = pawn;
            chips.Clear();
            if (p.InMentalState) chips.Add(new UiKit.PillSpec(T("CC_CCard_InBreak", (p.MentalStateDef?.label ?? "?").Named("BREAK")), UiKit.Danger, Icons.Warning));
            else if (p.Downed) chips.Add(new UiKit.PillSpec(T("CC_CCard_Downed"), UiKit.Danger, Icons.Warning));
            if (p.Drafted) chips.Add(new UiKit.PillSpec(T("CC_CCard_Drafted"), UiKit.Cool));
            if (ModsConfig.IdeologyActive && p.Ideo != null)
            {
                var role = p.Ideo.GetRole(p);
                if (role != null)
                    chips.Add(new UiKit.PillSpec(role.LabelForPawn(p).CapitalizeFirst(), p.Ideo.Color, role.Icon, T("CC_CCard_RoleTip", p.Named("PAWN"), role.LabelForPawn(p).Named("ROLE"),
                        p.Ideo.name.Named("IDEO"))));
            }
            // The Empire keeps its own titles and honour.
            var title = ModsConfig.RoyaltyActive ? p.royalty?.MostSeniorTitle : null;
            if (title?.def != null)
            {
                string label = title.def.GetLabelCapFor(p);
                chips.Add(new UiKit.PillSpec(label, UiKit.Gold, Icons.CatRoyalty,
                    title.faction != null ? T("CC_CCard_TitleTip", p.Named("PAWN"), label.Named("TITLE"), title.faction.Name.Named("FACTION")) : label));
            }
            if (!p.SpawnedOrAnyParentSpawned)
            {
                var caravan = p.GetCaravan();
                chips.Add(caravan != null
                    ? new UiKit.PillSpec(T("CC_CCard_InCaravan", caravan.LabelCap.Named("CARAVAN")), UiKit.World, Icons.Away)
                    : new UiKit.PillSpec(T("CC_CCard_Away"), UiKit.World, Icons.Away));
            }
            else if (p.MapHeld != Find.CurrentMap && p.MapHeld?.Parent != null)
                chips.Add(new UiKit.PillSpec(T("CC_CCard_OnMap", p.MapHeld.Parent.LabelCap.Named("PLACE")), UiKit.World, Icons.Away));

            // The heart's line under the portrait, in the colonist bar's colours.
            var h = Heart.ForView(p);
            heartOn = h != null && h.evaluated && Heart.Eligible(p);
            if (heartOn)
            {
                strain = h.strain;
                good = GoodHeart.On ? h.good : 0f;
                var band = h.Band;
                strainColor = band >= StrainBand.Strained ? HeartView.BandColor(band) : UiKit.WithAlpha(HeartView.StrainedColor, 0.55f);
                goodColor = HeartView.GoodColor;
                heartTip = MoodGlance.HeartLine(p, h);
            }

            // The arrows: the colonists either side, past those whose card is open in another window.
            var order = ColonistCardWindow.Order();
            int at = order.IndexOf(p);
            int pi = ColonistCardMath.Step(order.Count, at, -1, i => order[i] != p && ColonistCardWindow.For(order[i]) != null);
            int ni = ColonistCardMath.Step(order.Count, at, 1, i => order[i] != p && ColonistCardWindow.For(order[i]) != null);
            prev = pi >= 0 && order[pi] != p ? order[pi] : null;
            next = ni >= 0 && order[ni] != p ? order[ni] : null;
            prevTip = prev != null ? T("CC_CCard_PrevTip", prev.Named("PAWN")) : T("CC_CCard_NoOther");
            nextTip = next != null ? T("CC_CCard_NextTip", next.Named("PAWN")) : T("CC_CCard_NoOther");
        }

        // ---------------- Now ----------------

        private void Now()
        {
            var p = pawn;
            mood = p.needs?.mood != null ? MoodInsight.MoodOf(p) : null;
            glance = MoodGlance.For(p, mood);
            noMood = mood == null && glance.h == null ? T("CC_CCard_NoMood", p.Named("PAWN")) : null;
        }

        // ---------------- Skills ----------------

        private void Skills()
        {
            var p = pawn;
            skills.Clear();
            best = null;
            noSkills = null;
            var list = p.skills?.skills;
            if (list == null || list.Count == 0)
            {
                noSkills = T("CC_CCard_NoSkills", p.Named("PAWN"));
                return;
            }
            var records = list.Where(s => s?.def != null).OrderByDescending(s => s.def.listOrder).ToList();
            var levels = new List<int>(records.Count);
            var passions = new List<int>(records.Count);
            var disabled = new List<bool>(records.Count);
            foreach (var s in records)
            {
                bool off = s.TotallyDisabled;
                int level = off ? 0 : s.Level;
                levels.Add(level);
                passions.Add(People.PassionLevel(s));
                disabled.Add(off);
                var row = new SkillRow
                {
                    label = s.def.skillLabel.NullOrEmpty() ? s.def.LabelCap.Resolve() : s.def.skillLabel.CapitalizeFirst(), disabled = off,
                    level = off ? "-" : level.ToString(), fill = off ? 0f : ColonistCardMath.SkillFill(level)
                };
                if (!off) row.passion = Passions.Icon(s.passion, out row.passionColor);
                row.tip = SkillTip(p, s, off, level);
                skills.Add(row);
            }
            var top = ColonistCardMath.Best(levels, passions, disabled);
            foreach (int i in top) skills[i].best = true;
            if (top.Count > 0)
                best = T("CC_CCard_Best", p.Named("PAWN"), top.Select(i => skills[i].label.UncapitalizeFirst() + " " + levels[i]).ToList().ToCommaList(true).Named("SKILLS"));
        }

        /// <summary>A skill's tooltip: its level and what the game calls it, their passion and how fast it makes them learn, how far to the next level, an aptitude.</summary>
        private static string SkillTip(Pawn p, SkillRecord s, bool off, int level)
        {
            string head = (s.def.skillLabel.NullOrEmpty() ? s.def.LabelCap.Resolve() : s.def.skillLabel.CapitalizeFirst()).Colorize(ColoredText.TipSectionTitleColor);
            if (off) return head + "\n" + T("CC_CCard_SkillCant", p.Named("PAWN"));
            var lines = new List<string>(5) { head + "  " + T("CC_CCard_SkillLevel", level.Named("LEVEL"), s.LevelDescriptor.Named("WORD")) };
            if (!s.def.description.NullOrEmpty()) lines.Add(s.def.description.Colorize(ColoredText.SubtleGrayColor));
            string passion = Passions.Label(s.passion);
            float rate = 1f;
            try
            {
                rate = s.LearnRateFactor(false);
            }
            catch
            {
                rate = 1f;
            }
            lines.Add(passion.NullOrEmpty() ? T("CC_CCard_SkillRate", rate.ToStringPercent().Named("RATE"))
                : T("CC_CCard_SkillPassion", passion.CapitalizeFirst().Named("PASSION"), rate.ToStringPercent().Named("RATE")));
            if (level < SkillRecord.MaxLevel)
                lines.Add(T("CC_CCard_SkillNext", Mathf.RoundToInt(s.xpSinceLastLevel).Named("XP"), Mathf.RoundToInt(s.XpRequiredForLevelUp).Named("NEED")));
            int aptitude = s.Aptitude;
            if (aptitude != 0) lines.Add(T("CC_CCard_SkillAptitude", aptitude.ToString("+0;-0").Named("APTITUDE")));
            return string.Join("\n", lines);
        }

        // ---------------- Kit ----------------

        private void Kit()
        {
            var p = pawn;
            weapons.Clear();
            worn.Clear();
            kitLines.Clear();
            hookLines.Clear();
            noWeapon = noWorn = weather = weatherTip = null;
            Weapons(p);
            Suggestions(p);
            Worn(p);
            Weather(p);
            foreach (var (id, lines) in ColonistCardHooks.kit)
            {
                try
                {
                    var made = lines(p);
                    if (made != null) hookLines.AddRange(made.Where(l => l != null && !l.text.NullOrEmpty()));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a colonist's card: the kit's lines from " + id, e);
                }
            }
        }

        private void Weapons(Pawn p)
        {
            var seen = new HashSet<Thing>();
            void Add(Thing t, string tag)
            {
                if (t == null || !seen.Add(t)) return;
                var row = new GearRow { thing = t, weapon = true, name = t.LabelCapNoCount };
                var r = WeaponLive.Rateable(t.def) ? WeaponLive.Of(t) : null;
                string forThem = null;
                if (r?.Rating != null)
                {
                    row.rating = r.Rating.Overall;
                    string cant = WeaponLive.CantUse(p, t, r);
                    var fc = cant == null ? WeaponLive.ForColonist(p, r) : null;
                    forThem = cant != null ? T("CC_CCard_CantUse") : fc != null ? T("CC_CCard_ForThem", p.Named("PAWN"), WeaponForMath.RatingText(fc.Rating).Named("RATING")) : null;
                    var report = r;
                    var weapon = t;
                    row.tip = () => WeaponUi.Tip(report, p, weapon, true);
                }
                else
                {
                    var weapon = t;
                    row.tip = () => weapon.LabelCapNoCount + "\n\n" + T("CC_CCard_ClickCard");
                }
                row.second = forThem.NullOrEmpty() ? tag : tag + "  ·  " + forThem;
                weapons.Add(row);
            }
            Add(p.equipment?.Primary, T("CC_CCard_InHand"));
            if (WeaponMods.DualWieldLoaded) Add(WeaponMods.OffHand(p), T("CC_CCard_OffHand"));
            foreach (var t in WeaponMods.Carried(p))
                Add(t, T(WeaponMods.SidearmsLoaded && WeaponMods.Remembered(p, t) ? "CC_CCard_Sidearm" : "CC_CCard_Carried"));
            if (weapons.Count == 0) noWeapon = p.WorkTagIsDisabled(WorkTags.Violent) ? T("CC_Weapon_CantFight", p.Named("PAWN")) : T("CC_CCard_NoWeapon", p.Named("PAWN"));
        }

        /// <summary>
        /// The kit's gaps and the better weapons the colony's choice has for them (Weapons/WeaponPick.cs, as the gear tab shows
        /// them), each with its act; an empty slot nothing in storage fills, said plainly.
        /// </summary>
        private void Suggestions(Pawn p)
        {
            if (!WeaponPick.On || !p.Spawned || !p.IsColonistPlayerControlled) return;
            var standing = WeaponPick.Standing(p);
            var picks = WeaponPick.Current(p.Map);
            if (picks == null) return;
            foreach (var s in standing.OrderBy(s => s.Gap.HasValue ? 0 : 1).Take(WeaponPickGearTab.MaxRows))
            {
                var pick = s;
                bool onTheWay = WeaponPick.OnTheWay(picks, s);
                kitLines.Add(new ColonistCardLine
                {
                    icon = s.Gap.HasValue ? HudIcons.KitGap ?? Icons.Warning : Icons.TrendUp, color = s.Gap.HasValue ? UiKit.Soon : UiKit.Good,
                    text = WeaponPick.Head(picks, s), tip = WeaponPickGearTab.Tip(picks, s), thing = picks.Thing(s.Weapon),
                    act = WeaponPickGearTab.ActLabel(picks, s), whyNot = onTheWay ? T("CC_WeaponPick_OnTheWay") : WeaponPick.WhyNot(picks, s),
                    actTip = T("CC_WeaponPick_Act_" + s.Kind + "_Tip", p.Named("PAWN"), (picks.Pawn(s.Other)?.LabelShort ?? "").Named("OTHER")),
                    run = () =>
                    {
                        WeaponPick.DoConfirmed(picks, pick);
                        WeaponPickGearTab.Refresh();
                        Stale();
                    }
                });
            }
            var kit = picks.Kit(p);
            if (kit == null) return;
            foreach (var v in kit)
            {
                if (v.State != KitState.Missing || standing.Any(s => WeaponKitMath.Fills(picks.input, s, v.Slot))) continue;
                var slot = v.Slot;
                kitLines.Add(new ColonistCardLine
                {
                    icon = HudIcons.KitGap ?? Icons.Warning, color = UiKit.Soon, text = T("CC_CCard_GapNothing", WeaponPick.GapWords(slot).Named("GAP")),
                    tip = WeaponKitWords.LookedTip(picks, p, new List<KitSlot> { slot })
                });
            }
        }

        private void Worn(Pawn p)
        {
            var list = p.apparel?.WornApparel;
            if (list == null || list.Count == 0)
            {
                noWorn = p.apparel == null ? null : T("CC_CCard_NoWorn", p.Named("PAWN"));
                return;
            }
            var rows = new List<GearRow>(list.Count);
            foreach (var a in list)
            {
                if (a?.def?.apparel == null) continue;
                var ap = a.def.apparel;
                bool Covers(string group) => ap.bodyPartGroups != null && ap.bodyPartGroups.Any(g => g?.defName == group);
                var slot = ColonistCardMath.SlotOf(ap.layers != null && ap.layers.Contains(ApparelLayerDefOf.Belt), Covers("Torso"), Covers("Legs"),
                    Covers("FullHead") || Covers("UpperHead") || Covers("Eyes") || Covers("Teeth") || Covers("Mouth"), Covers("Hands") || Covers("LeftHand") || Covers("RightHand"),
                    Covers("Feet"));
                var row = new GearRow { thing = a, slot = slot, name = a.LabelCapNoCount };
                var parts = new List<string>(3) { T("CC_CCard_Slot_" + slot) };
                var r = ApparelLive.Of(a);
                if (r?.Rating != null)
                {
                    row.rating = r.Rating.Overall;
                    var fc = ApparelLive.ForColonist(p, r, a).rating;
                    if (fc != null) parts.Add(T("CC_CCard_ForThem", p.Named("PAWN"), WeaponForMath.RatingText(fc.Rating).Named("RATING")));
                    var report = r;
                    row.tip = () => ApparelUi.Tip(report, p, a, true);
                }
                else row.tip = () => a.LabelCapNoCount + "\n\n" + T("CC_CCard_ClickCard");
                int stage = ApparelLive.DamageStage(a);
                if (stage >= 0)
                {
                    row.nameColor = stage >= 1 ? UiKit.Sour : UiKit.Soon;
                    parts.Add(T("CC_CCard_Worn", ((float)a.HitPoints / Mathf.Max(1, a.MaxHitPoints)).ToStringPercent().Named("LEFT")));
                }
                row.second = string.Join("  ·  ", parts);
                rows.Add(row);
            }
            foreach (var slot in ColonistCardMath.SlotOrder)
                worn.AddRange(rows.Where(x => x.slot == slot));
        }

        /// <summary>Whether what they wear keeps them comfortable through the season where they are ("no warm coat"), by the game's comfortable range.</summary>
        private void Weather(Pawn p)
        {
            if (p.apparel == null || !p.RaceProps.Humanlike) return;
            var (has, now, low, high) = ApparelLive.Climate(p);
            if (!has) return;
            float min = p.GetStatValue(StatDefOf.ComfyTemperatureMin), max = p.GetStatValue(StatDefOf.ComfyTemperatureMax);
            int verdict = ColonistCardMath.Weather(min, max, low, high);
            string range = T("CC_CCard_WeatherRange", min.ToStringTemperature("F0").Named("MIN"), max.ToStringTemperature("F0").Named("MAX"),
                low.ToStringTemperature("F0").Named("LOW"), high.ToStringTemperature("F0").Named("HIGH"), now.ToStringTemperature("F0").Named("NOW"));
            switch (verdict)
            {
                case -1:
                    weather = T("CC_CCard_WeatherCold", p.Named("PAWN"));
                    weatherColor = UiKit.Soon;
                    break;
                case 1:
                    weather = T("CC_CCard_WeatherHot", p.Named("PAWN"));
                    weatherColor = UiKit.Soon;
                    break;
                case 2:
                    weather = T("CC_CCard_WeatherBoth", p.Named("PAWN"));
                    weatherColor = UiKit.Soon;
                    break;
                default:
                    weather = T("CC_CCard_WeatherFine", p.Named("PAWN"));
                    weatherColor = UiKit.Faint;
                    break;
            }
            weatherTip = weather + "\n\n" + range;
        }

        // ---------------- Life, and what else is said ----------------

        private void Life()
        {
            var comp = CouncilGameComponent.Instance;
            story = comp != null ? StoryView.Glance(comp, pawn, false) : null;
        }

        /// <summary>The colonist card's sections of lines and its acts other parts of Rimfolk add (a ward, a ransom, a match), less what the card shows itself.</summary>
        private void Others()
        {
            var r = ThingRefs.Of(pawn);
            also.Clear();
            acts.Clear();
            foreach (var e in CardSections.registry.Of(CardKind.Colonist))
            {
                if (e.value.lines == null || Shown.Contains(e.id)) continue;
                try
                {
                    var lines = e.value.lines(r);
                    if (lines != null) also.AddRange(lines.Where(l => !l.NullOrEmpty()));
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("a colonist's card: the section " + e.id, ex);
                }
            }
            foreach (var e in CardActs.registry.In(CardKind.Colonist, CardSlot.Acts))
            {
                if (Shown.Contains(e.id)) continue;
                var a = e.value;
                try
                {
                    if (a.shows != null && !a.shows(r)) continue;
                    var thing = r;
                    acts.Add(new CardAct { label = a.label(thing), why = a.whyNot?.Invoke(thing), run = () => a.act(thing), tip = a.tip });
                }
                catch (Exception ex)
                {
                    CouncilLog.ErrorOnce("a colonist's card: the act " + e.id, ex);
                }
            }
        }

        /// <summary>What the small card's sections and acts say that this card shows in its own way: the heart's lines and day off (Now), the name they're known by (the header).</summary>
        private static readonly HashSet<string> Shown = new HashSet<string> { "heart-good", "heart-dayoff", "knownas" };
    }

    /// <summary>
    /// A passion's mark and name: the game's own two, or Vanilla Skills Expanded's (and Alpha Skills', which builds on it)
    /// from its passion defs, read softly by name; without it, or for a passion it doesn't know, the game's marks by how
    /// strong the passion is (People.PassionLevel).
    /// </summary>
    public static class Passions
    {
        private static bool looked;
        private static FieldInfo vse;
        private static readonly Dictionary<int, (Texture2D icon, string label)> kept = new Dictionary<int, (Texture2D, string)>();

        private static object VseDef(int passion)
        {
            if (!looked)
            {
                looked = true;
                vse = GenTypes.GetTypeInAnyAssembly("VSE.Passions.PassionManager")?.GetField("Passions", BindingFlags.Public | BindingFlags.Static);
            }
            return vse?.GetValue(null) is Array defs && passion >= 0 && passion < defs.Length ? defs.GetValue(passion) : null;
        }

        private static (Texture2D icon, string label) Of(Passion p)
        {
            int key = (int)p;
            if (kept.TryGetValue(key, out var k)) return k;
            Texture2D icon = null;
            string label = null;
            try
            {
                var def = VseDef(key);
                if (def != null)
                {
                    var t = def.GetType();
                    icon = t.GetProperty("Icon")?.GetValue(def, null) as Texture2D;
                    if (icon == null && t.GetField("iconPath")?.GetValue(def) is string path && !path.NullOrEmpty()) icon = ContentFinder<Texture2D>.Get(path, false);
                    label = (def as Def)?.label;
                }
            }
            catch
            {
                icon = null;
            }
            if (label == null && key >= 0 && key <= 2)
            {
                try
                {
                    label = SkillUI.GetLabel(p);
                }
                catch
                {
                    label = null;
                }
            }
            k = (icon, label);
            kept[key] = k;
            return k;
        }

        /// <summary>A passion's mark: Vanilla Skills Expanded's own, else the game's for an interest or a burning passion; null for none.</summary>
        public static Texture2D Icon(Passion p, out Color tint)
        {
            tint = Color.white;
            if (p == Passion.None) return null;
            var own = Of(p).icon;
            if (own != null) return own;
            if (p == Passion.Minor) return SkillUI.PassionMinorIcon;
            if (p == Passion.Major) return SkillUI.PassionMajorIcon;
            return null;
        }

        /// <summary>A passion's name ("burning"), or null.</summary>
        public static string Label(Passion p) => Of(p).label;
    }
}
