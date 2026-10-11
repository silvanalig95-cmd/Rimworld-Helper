using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The Decrees tab: what each decree does, who will love or hate it, and the buttons to enact or repeal.</summary>
    public static class DecreePanel
    {
        private static Vector2 scroll;
        private static float height = 600f;

        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Good = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color Bad = new Color(0.95f, 0.55f, 0.45f);

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "CC_Decrees_Header".Translate());
            Text.Font = GameFont.Small;
            if (Prefs.DevMode && Widgets.ButtonText(new Rect(rect.xMax - 180f, rect.y, 180f, 28f), "CC_Dev_Dilemma".Translate()))
            {
                if (!comp.dilemmas.TryFire(true)) Messages.Message("No dilemma could happen right now.", MessageTypeDefOf.RejectInput, false);
            }
            float y = rect.y + 34f;
            y += Label(rect.x, y, rect.width, "CC_Decrees_Sub".Translate(DecreeTracker.MaxActive), Faint) + 4f;

            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && p.needs?.mood != null).ToList();
            if (pawns.Count > 0)
            {
                float mood = pawns.Average(p => p.needs.mood.CurLevelPercentage);
                float standards = pawns.Average(p => Standards.Of(p));
                string line = "CC_Decrees_State".Translate(mood.ToStringPercent(), ("CC_Standards_" + Standards.LevelFor(standards)).Translate());
                y += Label(rect.x, y, rect.width, line, Color.white) + 8f;
            }

            var outRect = new Rect(rect.x, y, rect.width, rect.yMax - y);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float cy = 0f;
            if (ColonyCouncilMod.Settings.offices) cy += DrawOffices(comp, view.width, cy) + 12f;
            foreach (var def in DefDatabase<DecreeDef>.AllDefsListForReading.OrderBy(d => d.order))
                cy += DrawDecree(def, comp, pawns, view.width, cy) + 8f;
            height = Mathf.Max(cy, 100f);
            Widgets.EndScrollView();
        }

        private static float DrawDecree(DecreeDef def, CouncilGameComponent comp, List<Pawn> pawns, float width, float y)
        {
            var tracker = comp.decrees;
            var active = tracker.Get(def);
            float textW = width - 190f;
            float start = y;
            float h = 0f;

            // Measure first, so the background can go behind the text.
            string title = def.LabelCap;
            if (active != null) title += "  " + "CC_Decree_InForce".Translate(TextUtil.DaysString(active.endTick - Find.TickManager.TicksGame));
            string effects = string.Join("\n", def.effects.Select(e => "- " + e));
            string reactions = Reactions(def, pawns);
            var req = tracker.RequestFor(def);
            string petition = req?.pawn == null ? null : (req.repeal ? "CC_Decree_RepealLine" : "CC_Decree_PetitionLine").Translate(req.pawn.Named("PAWN")).Resolve() + (req.line != null ? " \"" + req.line + "\"" : "");
            int fatigue = active == null ? tracker.Fatigue(def) : 0;
            string warning = fatigue > 0 ? "CC_Decree_FatigueWarn".Translate(fatigue, Mathf.RoundToInt(DecreeTracker.FatigueDays), tracker.UnrestChance(def, false).ToStringPercent()).Resolve() : null;
            Text.Font = GameFont.Small;
            h += Text.CalcHeight(title, textW) + Text.CalcHeight(def.description, textW) + Text.CalcHeight(effects, textW) + Text.CalcHeight(reactions, textW) + 22f;
            if (petition != null) h += Text.CalcHeight(petition, textW) + 2f;
            if (warning != null) h += Text.CalcHeight(warning, textW) + 2f;
            h = Mathf.Max(h, 70f);
            var box = new Rect(0f, y, width, h);
            if (active != null) Widgets.DrawBoxSolid(box, new Color(0.3f, 0.45f, 0.3f, 0.25f));
            else Widgets.DrawLightHighlight(box);

            float cy = y + 6f;
            cy += Label(8f, cy, textW, title, active != null ? Good : Color.white);
            if (petition != null) cy += Label(8f, cy, textW, petition, new Color(1f, 0.85f, 0.45f)) + 2f;
            if (warning != null) cy += Label(8f, cy, textW, warning, Bad) + 2f;
            cy += Label(8f, cy, textW, def.description, Faint) + 2f;
            cy += Label(8f, cy, textW, effects, Color.white) + 2f;
            Label(8f, cy, textW, reactions, Faint);

            var btn = new Rect(width - 175f, y + 8f, 165f, 32f);
            if (active != null)
            {
                if (Widgets.ButtonText(btn, "CC_Decree_Repeal".Translate())) tracker.Repeal(def, comp, true);
            }
            else
            {
                string why = tracker.CannotEnact(def);
                if (why == null)
                {
                    if (Widgets.ButtonText(btn, "CC_Decree_Enact".Translate(TextUtil.DaysString(Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay)))))
                        tracker.Enact(def, comp);
                }
                else
                {
                    // Only the cooldown stands in the way: it can be forced through, at a price.
                    bool forcible = tracker.CannotEnact(def, true) == null;
                    var whyRect = forcible ? new Rect(btn.x, btn.y - 4f, btn.width, 24f) : btn;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Faint;
                    Widgets.Label(whyRect, why);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (forcible)
                    {
                        var forceBtn = new Rect(btn.x, btn.y + 22f, btn.width, 28f);
                        string chance = tracker.UnrestChance(def, true).ToStringPercent();
                        TooltipHandler.TipRegion(forceBtn, "CC_Decree_ForceTip".Translate(chance));
                        if (Widgets.ButtonText(forceBtn, "CC_Decree_Force".Translate()))
                        {
                            var against = pawns.Where(p => def.ReactionOf(p) <= DecreeReaction.Dislike).Select(p => p.LabelShort).ToList();
                            string who = against.Count > 0 ? string.Join(", ", against) : "CC_Decree_ForceNobody".Translate().ToString();
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                                "CC_Decree_ForceConfirm".Translate(def.label.Named("DECREE"), who.Named("WHO"), chance.Named("CHANCE")).Resolve(),
                                () => tracker.Enact(def, comp, true), true));
                        }
                    }
                }
            }
            return h;
        }

        private static float DrawOffices(CouncilGameComponent comp, float width, float y)
        {
            float start = y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "CC_Offices_Header".Translate());
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(width - 200f, y, 200f, 28f), "CC_Office_CallElection".Translate()))
            {
                var map = Find.AnyPlayerHomeMap;
                if (map != null) comp.offices.Hold(comp, GenLocalDate.Year(map), false);
            }
            y += 32f;
            y += Label(0f, y, width, "CC_Offices_Sub".Translate(), Faint) + 4f;
            foreach (var office in OfficeTracker.Offices)
            {
                var holder = comp.offices.HolderOf(office);
                var row = new Rect(0f, y, width, 28f);
                Widgets.DrawLightHighlight(row);
                Widgets.Label(new Rect(8f, y + 3f, 150f, 24f), office.LabelCap);
                if (holder != null)
                {
                    Widgets.ThingIcon(new Rect(160f, y + 2f, 24f, 24f), holder);
                    Widgets.Label(new Rect(190f, y + 3f, 180f, 24f), holder.LabelShort);
                    GUI.color = Faint;
                    Widgets.Label(new Rect(370f, y + 3f, 120f, 24f), "CC_Office_Competence".Translate(office.Competence(holder).ToString("0")));
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = Faint;
                    Widgets.Label(new Rect(190f, y + 3f, 180f, 24f), "CC_Office_Empty".Translate());
                    GUI.color = Color.white;
                }
                TooltipHandler.TipRegion(row, office.description + "\n\n" + string.Join("\n", office.duties.Select(d => "- " + d)));
                if (Widgets.ButtonText(new Rect(width - 230f, y + 1f, 120f, 26f), "CC_Office_Appoint".Translate()))
                {
                    var options = PawnsFinder.AllMaps_FreeColonistsSpawned
                        .Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.IsSlave)
                        .OrderByDescending(p => office.Competence(p))
                        .Select(p => new FloatMenuOption(p.LabelShort + " (" + "CC_Office_Competence".Translate(office.Competence(p).ToString("0")) + ")", () => comp.offices.Appoint(office, p, comp)))
                        .ToList();
                    if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
                }
                if (holder != null && Widgets.ButtonText(new Rect(width - 105f, y + 1f, 100f, 26f), "CC_Office_Dismiss".Translate()))
                    comp.offices.Dismiss(office, comp);
                y += 30f;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y + 6f, width, 30f), "CC_Decrees_Header".Translate());
            Text.Font = GameFont.Small;
            y += 38f;
            return y - start;
        }

        private static string Reactions(DecreeDef def, List<Pawn> pawns)
        {
            var parts = new List<string>();
            void Add(DecreeReaction r, string key)
            {
                var names = pawns.Where(p => def.ReactionOf(p) == r).Select(p => p.LabelShort).ToList();
                if (names.Count > 0) parts.Add(key.Translate(string.Join(", ", names)));
            }
            Add(DecreeReaction.Love, "CC_Decree_Loves");
            Add(DecreeReaction.Like, "CC_Decree_Likes");
            Add(DecreeReaction.Dislike, "CC_Decree_Dislikes");
            Add(DecreeReaction.Hate, "CC_Decree_Hates");
            return parts.Count == 0 ? "" : string.Join("   ", parts);
        }

        private static float Label(float x, float y, float width, string text, Color color)
        {
            if (text.NullOrEmpty()) return 0f;
            float h = Text.CalcHeight(text, width);
            GUI.color = color;
            Widgets.Label(new Rect(x, y, width, h), text);
            GUI.color = Color.white;
            return h;
        }
    }
}
