using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Hospitality's guests on This week (the council window's first page), beside the missions and the camp: one line per
    /// party staying at a home map, how many, from where, and how the visit is going by Hospitality's own score; a click
    /// goes to them. Only read, never touched: the guests stay in Hospitality's own hands. Nothing without Hospitality.
    /// </summary>
    public static class GuestsWeek
    {
        public static void AddRows(WeekCard card)
        {
            if (card == null || !People.ModActive(Guests.Mod)) return;
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                // One party per lord (Hospitality keeps each visiting group in a lord of its own).
                var parties = new Dictionary<object, List<Pawn>>();
                foreach (var g in Strangers.On(map, StrangerKind.Visitor))
                {
                    if (!Strangers.IsHospitalityGuest(g) || g.Dead || !g.Spawned) continue;
                    object key = (object)g.GetLord() ?? g;
                    if (!parties.TryGetValue(key, out var list)) parties[key] = list = new List<Pawn>();
                    list.Add(g);
                }
                foreach (var party in parties.Values)
                {
                    var first = party[0];
                    var faction = Strangers.FactionOf(first);
                    string from = faction?.Name ?? "?";
                    float sum = 0f;
                    int read = 0;
                    foreach (var g in party)
                    {
                        float s = Guests.VisitScore(g);
                        if (s < 0f) continue;
                        sum += s;
                        read++;
                    }
                    string note = null;
                    Color noteColor = UiKit.Faint;
                    if (read > 0)
                    {
                        float score = sum / read;
                        if (score >= 0.65f)
                        {
                            note = "CC_Week_GuestsWell".Translate();
                            noteColor = UiKit.Good;
                        }
                        else if (score < 0.35f)
                        {
                            note = "CC_Week_GuestsPoorly".Translate();
                            noteColor = UiKit.Bad;
                        }
                        else note = "CC_Week_GuestsSo".Translate();
                    }
                    var target = new GlobalTargetInfo(first);
                    card.rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = UiKit.StrangerColor,
                        text = (party.Count == 1 ? "CC_Week_GuestOne" : "CC_Week_Guests").Translate(from.Named("FROM"), party.Count.Named("COUNT")).Resolve(),
                        note = note,
                        noteColor = noteColor,
                        tip = "CC_Week_GuestsTip".Translate(string.Join(", ", party.Select(g => g.LabelShort))).Resolve() + "\n\n"
                              + "CC_Week_ClickPlace".Translate().ToString().Colorize(ColoredText.SubtleGrayColor),
                        click = WeekModel.Go(target),
                        order = 0
                    });
                }
            }
        }
    }
}
