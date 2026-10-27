using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist's character page, drawn in the Story tab and the People roster (StoryView; the Social tab's Rimfolk
    /// column and the Culture tab draw some of its sections too): their character in words
    /// and on eight small bars (where their nature puts them, and where they are now), what shaped them, and the
    /// sections the other systems add (<see cref="CharacterPage"/>). With "Personalities and social life" off, only
    /// their nature shows.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CharacterView
    {
        private static readonly Color Track = new Color(0f, 0f, 0f, 0.4f);
        private static readonly Color Outline = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color Middle = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color NatureMark = new Color(0.85f, 0.85f, 0.85f, 0.9f);
        private static readonly Color Fill = new Color(0.95f, 0.85f, 0.6f, 0.45f);

        private const float BarRow = 30f;

        static CharacterView()
        {
            CharacterPage.Add("character", 10, Character);
            CharacterPage.Add("shaped", 20, Shaped);
        }

        // ---------------- The sections ----------------

        /// <summary>The page's sections for this colonist, in order: all of them, or their nature alone with social life off.</summary>
        public static List<CharacterSection> Sections(Pawn p)
        {
            var list = new List<CharacterSection>();
            if (!Persona.Eligible(p)) return list;
            if (!Persona.On)
            {
                var s = Character(p);
                if (s != null && !s.Empty)
                {
                    s.order = 10;
                    s.key = "character";
                    list.Add(s);
                }
                return list;
            }
            return CharacterPage.For(p);
        }

        public static Texture2D Icon(int order)
        {
            switch (order)
            {
                case 10: return Icons.People;
                case 20: return Icons.Journal;
                case 30: return Icons.Council;
                case 35: return Icons.CatIdeology ?? Icons.People;
                case 40: return Icons.Heart;
                case 50: return Icons.Bonds;
                case 60: return Icons.Chronicle;
                default: return Icons.People;
            }
        }

        /// <summary>Section 10: their character in words (and, once something has moved them, their nature beside it).</summary>
        public static CharacterSection Character(Pawn p)
        {
            var s = new CharacterSection { title = "CC_Char_Title".Translate() };
            var record = Persona.On ? Persona.Of(p) : null;
            string now = Persona.Words(p).CapitalizeFirst() + ".";
            s.Line(now, "CC_Char_WordsTip".Translate(p.Named("PAWN")).Resolve());
            if (record == null)
            {
                if (Persona.On) s.Line("CC_Char_NatureOnly".Translate(p.Named("PAWN")).Resolve().Colorize(UiKit.Faint));
                return s;
            }
            string nature = NatureWords(p);
            if (nature != Persona.Words(p)) s.Line("CC_Char_ByNature".Translate(nature).Resolve().Colorize(UiKit.Faint));
            return s;
        }

        /// <summary>The marks shown in full on the page; the rest are listed in one line's tooltip.</summary>
        public const int MaxMarksShown = 6;

        /// <summary>Section 20: what shaped them, turning points first, then the newest; past six, one line for the rest.</summary>
        public static CharacterSection Shaped(Pawn p)
        {
            var record = Persona.Of(p);
            if (record == null || record.marks.Count == 0) return null;
            var s = new CharacterSection { title = "CC_Char_Shaped".Translate(p.Named("PAWN")).Resolve() };
            var rest = new List<string>();
            foreach (var m in record.marks.Where(m => m?.def != null).OrderByDescending(m => m.def.major).ThenByDescending(m => m.tick))
            {
                string text = Persona.Shaped(p, m);
                if (text.NullOrEmpty()) continue;
                if (s.lines.Count >= MaxMarksShown)
                {
                    rest.Add(PersonaMarks.DateLabel(m.tick, p) + ": " + text);
                    continue;
                }
                string line = PersonaMarks.DateLabel(m.tick, p).Colorize(UiKit.Dim) + "  " + (m.def.major ? text : text.Colorize(UiKit.Faint));
                s.Line(line, MarkTip(m));
            }
            if (rest.Count > 0) s.Line("CC_Char_MoreMarks".Translate(rest.Count).Resolve().Colorize(UiKit.Faint), string.Join("\n\n", rest));
            return s;
        }

        private static string MarkTip(PersonaMark m)
        {
            var sb = new StringBuilder();
            sb.Append(m.def.LabelCap);
            if (m.def.major) sb.Append(" (").Append("CC_Char_TurningPoint".Translate()).Append(")");
            var ups = PersonaMath.All.Where(a => m.def.shifts.Of(a) > 0f).Select(a => ("CC_Persona_Axis_" + a).Translate().ToString()).ToList();
            var downs = PersonaMath.All.Where(a => m.def.shifts.Of(a) < 0f).Select(a => ("CC_Persona_Axis_" + a).Translate().ToString()).ToList();
            if (ups.Count > 0) sb.Append("\n").Append("CC_Char_Raised".Translate(TextUtil.JoinAnd(ups)).Resolve());
            if (downs.Count > 0) sb.Append("\n").Append("CC_Char_Lowered".Translate(TextUtil.JoinAnd(downs)).Resolve());
            sb.Append("\n\n").Append("CC_Char_MarkTip".Translate(Mathf.RoundToInt(m.def.lastingShare * 100f)).Resolve());
            return sb.ToString();
        }

        /// <summary>The words for their nature alone ("warm and cautious").</summary>
        private static string NatureWords(Pawn p)
        {
            var v = Persona.Nature(p);
            var words = PersonaMath.Strongest(v, 3).Select(i => Persona.Word((PersonaAxis)i, v[i])).Where(w => w != null).ToList();
            return words.Count == 0 ? "CC_Persona_Even".Translate().ToString() : TextUtil.JoinAnd(words);
        }

        // ---------------- Drawing ----------------

        /// <summary>
        /// Draws one section's body from the top of <paramref name="r"/>; returns the height used. <paramref name="skip"/>
        /// leaves out its first lines (the Story tab's overview shows their character in words already).
        /// </summary>
        public static float DrawSection(Rect r, Pawn p, CharacterSection s, int skip = 0)
        {
            if (s == null || p == null) return 0f;
            float y = r.y;
            Text.Font = GameFont.Small;
            for (int i = skip; i < s.lines.Count; i++)
            {
                string text = s.lines[i];
                float h = Text.CalcHeight(text, r.width);
                var lr = new Rect(r.x, y, r.width, h);
                Widgets.Label(lr, text);
                string tip = i < s.tips.Count ? s.tips[i] : null;
                if (!tip.NullOrEmpty())
                {
                    if (Mouse.IsOver(lr)) Widgets.DrawHighlight(lr);
                    TooltipHandler.TipRegion(lr, tip);
                }
                y += h + 2f;
            }
            if (s.order == 10)
            {
                y += 4f;
                y += DrawBars(new Rect(r.x, y, r.width, 0f), p);
            }
            return y - r.y;
        }

        /// <summary>The eight sides, two columns of four: the bar fills from the middle to where they are now; a pale tick is their nature.</summary>
        private static float DrawBars(Rect r, Pawn p)
        {
            var nature = Persona.Nature(p);
            var record = Persona.On ? Persona.Of(p) : null;
            var now = Persona.Values(p);
            const float gap = 14f;
            float colW = (r.width - gap) / 2f;
            for (int i = 0; i < PersonaMath.Axes; i++)
            {
                int col = i / 4, row = i % 4;
                var cell = new Rect(r.x + col * (colW + gap), r.y + row * BarRow, colW, BarRow - 2f);
                DrawBar(cell, p, (PersonaAxis)i, nature[i], now[i], record);
            }
            return BarRow * 4f;
        }

        // The bars' words, translated once (the page draws every frame); asked again when the language or the defs change.
        private static string[] axisLabels;
        private static readonly Dictionary<int, string> sideWords = new Dictionary<int, string>();
        private static LoadedLanguage wordsLanguage;
        private static int wordsGeneration = -1;

        private static void FreshWords()
        {
            if (axisLabels != null && wordsLanguage == LanguageDatabase.activeLanguage && wordsGeneration == DefRefs.Generation) return;
            wordsLanguage = LanguageDatabase.activeLanguage;
            wordsGeneration = DefRefs.Generation;
            axisLabels = new string[PersonaMath.Axes];
            sideWords.Clear();
        }

        /// <summary>A side's name ("Warmth"), translated the first time it's drawn.</summary>
        private static string AxisLabel(PersonaAxis axis)
        {
            int i = (int)axis;
            return axisLabels[i] ??= ("CC_Persona_Axis_" + axis).Translate().CapitalizeFirst();
        }

        /// <summary>The word for where they are on a side ("warm", or "neither" in the middle), kept per side and degree.</summary>
        private static string SideWord(PersonaAxis axis, float v)
        {
            int key = (int)axis * 8 + PersonaMath.Degree(v) + 2;
            if (!sideWords.TryGetValue(key, out var w))
                sideWords[key] = w = Persona.Word(axis, v) ?? "CC_Char_Neither".Translate().ToString();
            return w;
        }

        private static void DrawBar(Rect cell, Pawn p, PersonaAxis axis, float nature, float now, PawnPersona record)
        {
            FreshWords();
            string word = SideWord(axis, now);
            var old = Text.Font;
            UiKit.LabelFit(new Rect(cell.x, cell.y, cell.width * 0.5f, 18f), AxisLabel(axis), UiKit.Faint, GameFont.Tiny);
            UiKit.LabelFit(new Rect(cell.x + cell.width * 0.5f, cell.y, cell.width * 0.5f, 18f), word, PersonaMath.Degree(now) == 0 ? UiKit.Dim : UiKit.HeaderColor,
                GameFont.Tiny, TextAnchor.MiddleRight);
            Text.Font = old;
            var bar = new Rect(cell.x, cell.y + 19f, cell.width, 8f);
            Widgets.DrawBoxSolidWithOutline(bar, Track, Outline, 1);
            float mid = bar.x + bar.width / 2f;
            float X(float v) => mid + Mathf.Clamp(v, -PersonaMath.Max, PersonaMath.Max) / PersonaMath.Max * (bar.width / 2f - 1f);
            float to = X(now);
            if (Mathf.Abs(to - mid) >= 1f) Widgets.DrawBoxSolid(new Rect(Mathf.Min(mid, to), bar.y + 1f, Mathf.Abs(to - mid), bar.height - 2f), Fill);
            Widgets.DrawBoxSolid(new Rect(mid, bar.y - 1f, 1f, bar.height + 2f), Middle);
            Widgets.DrawBoxSolid(new Rect(X(nature) - 1f, bar.y - 2f, 2f, bar.height + 4f), NatureMark);
            if (record != null && Mathf.Abs(now - nature) >= 1f) Widgets.DrawBoxSolid(new Rect(to - 2f, bar.y - 1f, 4f, bar.height + 2f), UiKit.Gold);
            if (Mouse.IsOver(cell)) Widgets.DrawHighlight(cell);
            TooltipHandler.TipRegion(cell, new TipSignal(() => BarTip(p, axis), p.thingIDNumber * 16 + (int)axis + 1131));
        }

        // ---------------- What moved a side ----------------

        /// <summary>The tooltip of one side: what it means, where they are, and what put them there.</summary>
        public static string BarTip(Pawn p, PersonaAxis axis)
        {
            try
            {
                int i = (int)axis;
                float nature = Persona.Nature(p)[i];
                float now = Persona.Values(p)[i];
                var record = Persona.On ? Persona.Of(p) : null;
                var sb = new StringBuilder();
                sb.Append(("CC_Persona_Axis_" + axis).Translate().CapitalizeFirst()).Append(": ").Append(("CC_Char_Range_" + axis).Translate());
                sb.Append("\n\n").Append("CC_Char_Now".Translate(Word(axis, now), Signed(now)).Resolve());
                if (record != null) sb.Append("\n").Append("CC_Char_NatureIs".Translate(Word(axis, nature), Signed(nature)).Resolve());
                sb.Append("\n\n").Append("CC_Char_FromNature".Translate()).Append(":");
                foreach (var (why, amount) in NatureReasons(p, axis)) sb.Append("\n  ").Append(Signed(amount)).Append("  ").Append(why);
                if (record != null)
                {
                    float lasting = record.lasting[i], drift = record.drift[i];
                    var marks = record.marks.Where(m => m?.def != null && m.def.shifts.Of(axis) != 0f).OrderByDescending(m => m.tick).ToList();
                    if (marks.Count > 0 || Mathf.Abs(lasting) >= 0.5f || Mathf.Abs(drift) >= 0.5f)
                    {
                        sb.Append("\n\n").Append("CC_Char_FromLife".Translate(Signed(lasting + drift), Signed(lasting), Signed(drift)).Resolve());
                        foreach (var m in marks.Take(6))
                            sb.Append("\n  ").Append(m.def.shifts.Of(axis) > 0f ? "+" : "-").Append("  ").Append(m.def.label)
                                .Append(" (").Append(PersonaMarks.DateLabel(m.tick, p)).Append(")");
                    }
                    // A trait gained or lost took its part of the move into their nature (Persona/TraitChanges.cs).
                    string traits = TraitChanges.AbsorbedNote(p, axis);
                    if (traits != null) sb.Append("\n\n").Append(traits);
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("character page tooltip", e);
                return "";
            }
        }

        private static string Word(PersonaAxis axis, float v) => Persona.Word(axis, v) ?? "CC_Char_Neither".Translate().ToString();

        private static string Signed(float v)
        {
            int n = Mathf.RoundToInt(v);
            return n > 0 ? "+" + n : n.ToString();
        }

        /// <summary>
        /// Where their nature on one side comes from, as Persona.Nature works it out: their traits, the Social skill (warmth
        /// and humour), their age, and a small lean of their own.
        /// </summary>
        public static List<(string why, float amount)> NatureReasons(Pawn p, PersonaAxis axis)
        {
            var list = new List<(string, float)>();
            float seed = PersonaMath.Seed(p.thingIDNumber, axis, 12f);
            var traits = p.story?.traits;
            if (traits != null)
                foreach (var def in PersonaNatureDef.All)
                {
                    if (def.traits == null) continue;
                    foreach (var e in def.traits)
                    {
                        var td = e?.Def;
                        float amount = e?.Of(axis) ?? 0f;
                        if (td == null || amount == 0f) continue;
                        var t = traits.GetTrait(td);
                        if (t == null || t.Degree != e.degree) continue;
                        list.Add((t.LabelCap, amount));
                    }
                }
            var social = p.skills?.GetSkill(SkillDefOf.Social);
            if (social != null && !social.TotallyDisabled)
            {
                float amount = axis == PersonaAxis.Warmth ? (social.Level - 6) * 1.5f + (social.passion != Passion.None ? 5f : 0f)
                    : axis == PersonaAxis.Humour ? (social.Level - 6) * 1.2f : 0f;
                if (Mathf.Abs(amount) >= 0.5f) list.Add(("CC_Char_SocialSkill".Translate(social.Level).Resolve(), amount));
            }
            float years = p.ageTracker?.AgeBiologicalYearsFloat ?? 30f;
            float age = 0f;
            if (years < 13f) age = axis == PersonaAxis.Openness ? 20f : axis == PersonaAxis.Trust ? 15f : axis == PersonaAxis.Humour ? 10f : 0f;
            else if (years < 18f) age = axis == PersonaAxis.Openness ? 10f : axis == PersonaAxis.Temper ? 5f : 0f;
            else if (years >= 65f) age = axis == PersonaAxis.Openness ? -10f : axis == PersonaAxis.Temper ? -5f : axis == PersonaAxis.Nerve ? 5f : 0f;
            if (age != 0f) list.Add(("CC_Char_Age".Translate(Mathf.FloorToInt(years)).Resolve(), age));
            if (Mathf.Abs(seed) >= 0.5f) list.Add(("CC_Char_OwnLean".Translate().ToString(), seed));
            return list;
        }
    }
}
