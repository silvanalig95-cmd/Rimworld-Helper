#!/usr/bin/env python3
"""
How Rimfolk's part of a save grows over a long game: every collection the mod saves (each Scribe_Collections.Look in
Source/ColonyCouncil), with the rule that keeps it in bounds (a cap, a window of days, one entry per colonist or per
def, only while something lasts) and a model of how fast it fills in a large colony. It plays five in-game years (300
days) and prints each collection's entries after one, two and five years, a rough size in the save, and the total.

    python3 Tools/save_estimate.py               # writes docs/save-size.md
    python3 Tools/save_estimate.py --strict      # also fails if a saved collection has no model, or grows without end

A collection found in the code with no model here is listed as "not modelled": add it to MODEL with its rule (the
rules are in DESIGN.md, "Save size"). A model of kind "grows" marks something with no bound; there should be none.
Caps are read from the source where they are named constants, so the estimate follows the code.

The colony: 20 colonists the whole time, the game keeping 30 more people it knows (visitors, prisoners, family,
the dead with graves) and 5 more each year, 190 pairs of colonists, one home map. Rates are per in-game day and on
the generous side. Sizes are rough (an XML <li> of a number is about 25 bytes, a pawn reference about 40, a
dictionary entry about 90, a small record a few hundred).
"""
import argparse
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC = os.path.join(ROOT, "Source", "ColonyCouncil")

DAYS_PER_YEAR = 60
YEARS = 5
COLONISTS = 20
PAIRS = COLONISTS * (COLONISTS - 1) // 2


def known(day):
    """People the game still keeps (alive or dead) that Rimfolk may hold records of."""
    return COLONISTS + 30 + 5 * day / DAYS_PER_YEAR


def text(rel):
    p = os.path.join(SRC, rel)
    return open(p, encoding="utf-8-sig").read() if os.path.exists(p) else ""


def const(rel, name, fallback):
    # "const int MaxRaids = 5, MaxPlans = 24;" names more than one.
    m = re.search(r"\bconst\s+(?:int|float)\s+(?:\w+\s*=\s*[\d.]+f?\s*,\s*)*" + re.escape(name) + r"\s*=\s*([\d.]+)f?\s*[;,]", text(rel))
    return float(m.group(1)) if m else fallback


C = {
    "Chronicle": const("Lean/Caps.cs", "Chronicle", 300),
    "DepartedJournals": const("Lean/Caps.cs", "DepartedJournals", 30),
    "AnnalsYears": const("Lean/Caps.cs", "AnnalsYears", 40),
    "BestFriendPairs": const("Lean/Caps.cs", "BestFriendPairs", 200),
    "JournalLines": const("Journal.cs", "MaxEntries", 60),
    "ArcPast": const("Arcs/ArcTracker.cs", "MaxPast", 200),
    "DilemmaRecords": const("UI/DilemmaLog.cs", "MaxRecords", 60),
    "NewsWaiting": const("UI/News.cs", "MaxWaiting", 40),
    "SituationsActive": const("World/WorldSituations.cs", "MaxActive", 2),
    "LuckyPast": const("Quirks/LuckyItems.cs", "MaxPast", 40),
    "TrailDays": const("UI/WeekMemory.cs", "TrailDays", 8),
    "Calendar": 12 + 8 + 6,
    "DefenseRaids": const("Defense/DefenseLine.cs", "MaxRaids", 5),
    "DefensePlans": const("Defense/DefenseLine.cs", "MaxPlans", 24),
    "SpanSide": const("Defense/DefenseMath.cs", "MaxSpanSide", 14),
    "PersonaAxes": const("Persona/PersonaMath.cs", "Axes", 8),
    "PersonaMarks": const("Persona/PawnPersona.cs", "MaxMarks", 12),
    "SharedMemories": const("Persona/PersonaTracker.cs", "MaxMemories", 900),
    "TalkNotes": const("Social/Talk/TalkTracker.cs", "MaxNotes", 40),
    "PlayResults": const("PlayOut/PlayOutTracker.cs", "MaxResults", 30),
    "TimedKept": const("PlayOut/OnMapTimed.cs", "MaxKept", 12),
    "CourierTrips": const("Couriers/CourierMath.cs", "MaxTrips", 12),
    "CourierPeople": const("Couriers/CourierMath.cs", "MaxPeople", 3),
    "VisitorTrips": const("Visitors/VisitorMath.cs", "MaxTrips", 8),
    "VisitorPeople": const("Visitors/VisitorMath.cs", "MaxPeople", 6),
    "VisitorAnimals": const("Visitors/VisitorMath.cs", "MaxAnimals", 8),
    "KinNoteDays": const("World/MemoryMath.cs", "NoteDays", 30),
}

# How many defs of a kind there are, counted from the mod's own XML (more with other mods' content).
DEFS = {}


def count_defs():
    pat = re.compile(r"<(?:ColonyCouncil\.)?(\w+Def)\b[^>]*>")
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Defs")):
        for f in files:
            if not f.endswith(".xml"):
                continue
            for m in pat.finditer(open(os.path.join(dirpath, f), encoding="utf-8-sig", errors="replace").read()):
                if "/" in m.group(0):
                    continue
                DEFS[m.group(1)] = DEFS.get(m.group(1), 0) + 1


def defs(name, fallback=20):
    return DEFS.get(name, fallback)


# ---------------------------------------------------------------- the model ------------------------------------------
# key: "path/File.cs:field" -> (kind, args, bytes per entry, rule)
# kinds:
#   fixed n            always about n entries
#   colonists k        k per colonist (the gone are dropped)
#   known k            k per person the game still keeps (dropped when the game lets them go)
#   pairs k            k per pair of colonists
#   defs (Def, k)      k per def of that kind
#   cap (N, rate)      rate a day, never more than N
#   window (days, rate)  rate a day, each kept that many days
#   yearly rate        rate a day, emptied each in-game year
#   while n            only while something lasts (a camp, a meeting, a gathering): n at most, 0 after
#   settings n         in the mod's settings file, not the save (not counted)
#   grows rate         no bound: rate a day for ever (there should be none)
V, P, D, R = 25, 40, 90, 300  # a value, a pawn reference, a dictionary entry, a small record

MODEL = {
    # The council
    "CouncilGameComponent.cs:wishes": ("cap", (COLONISTS, 1), 600, "open wishes only (one per colonist at most)"),
    "CouncilGameComponent.cs:chronicle": ("cap", (C["Chronicle"], 8), 350, "Caps.Chronicle; the newest 200 always, milestones longest"),
    "CouncilGameComponent.cs:history": ("window", (120, 1), 120, "a sample a day, 120 days"),
    "CouncilGameComponent.cs:lastAtHome": ("colonists", 1, D, "colonists still ours"),
    "CouncilGameComponent.cs:cooldowns": ("window", (20, 3), D, "dropped once over"),
    "CouncilGameComponent.cs:recentWishDefs": ("window", (20, 2), V, "recent wishes, 20 days"),
    "CouncilGameComponent.cs:recentWishTicks": ("window", (20, 2), V, "with the above"),
    "CouncilGameComponent.cs:everWished": ("defs", ("WishDef", 1), 35, "one per wish def"),
    "CouncilSettings.cs:savedRooms": ("settings", 10, 2000, "the mod's settings file, not the save: rooms the player saved"),
    "CouncilSettings.cs:planMaterials": ("settings", 30, 40, "the mod's settings file, not the save"),
    "CouncilSettings.cs:councilPageState": ("settings", 30, 40, "the mod's settings file, not the save"),
    # The on-screen panels (Hud/): their layout in the settings file, the mood trend in the save
    "CouncilSettings.cs:hudPanels": ("settings", 8, 400, "the mod's settings file, not the save: one per panel"),
    "Hud/HudPanelState.cs:hidden": ("settings", 3, 30, "per panel: the rows or columns switched off (settings file)"),
    "Hud/HudComponent.cs:samples": ("cap", (const("Hud/HudComponent.cs", "MaxSamples", 168), 24), 20,
                                    "the mood trend: one packed number an hour, the last week (HudComponent.MaxSamples)"),
    "Hud/HudComponent.cs:stock": ("cap", (const("Hud/HudComponent.cs", "MaxStockPerMap", 5), 4), 1800,
                                  "the inventory panel's stock: one packed line (about 120 items) every six hours per home map, the last five; only while that panel is on"),
    "Hud/HudPanelState.cs:pins": ("settings", 8, 30, "the inventory's pinned items (settings file)"),
    "Hud/HudPanelState.cs:folds": ("settings", 4, 30, "the inventory's folded categories (settings file)"),
    "Hud/HudPanelState.cs:alertsHidden": ("settings", 3, 50, "alerts hidden in the alerts panel, by class (settings file)"),
    "Hud/HudPanelState.cs:alertsShown": ("settings", 1, 50, "alerts hidden by default that the player shows again (settings file)"),
    "Journal.cs:entries": ("fixed", C["JournalLines"], 160, "per journal: 60 lines (counted under journals)"),
    "Journal.cs:journals": ("cap", (COLONISTS + C["DepartedJournals"], 0.5), 160 * C["JournalLines"], "the living's, and the 30 departed written in most lately"),
    "Journal.cs:griefSeen": ("cap", (500, 0.5), 50, "cleared at 500"),
    "Journal.cs:everydaySeen": ("window", (24, COLONISTS), D, "24 days"),
    "Standing.cs:recentGrants": ("window", (30, 0.5), V, "per colonist, 30 days"),
    "Standing.cs:log": ("colonists", 8, 120, "8 lines per colonist"),
    "Standing.cs:asked": ("colonists", 15, 35, "per colonist, one per wish def asked for"),
    "Standing.cs:standings": ("colonists", 1, R, "one per colonist, the gone dropped daily"),
    "Annals.cs:present": ("colonists", 1, P, "the year in progress"),
    "Annals.cs:joined": ("yearly", 0.1, 40, "the year in progress"),
    "Annals.cs:died": ("yearly", 0.07, 40, "the year in progress"),
    "Annals.cs:gone": ("yearly", 0.05, 40, "the year in progress"),
    "Annals.cs:decrees": ("yearly", 0.2, 40, "the year in progress"),
    "Annals.cs:quit": ("yearly", 0.05, 40, "the year in progress"),
    "Annals.cs:written": ("cap", (C["AnnalsYears"], 1 / DAYS_PER_YEAR), 2500, "Caps.AnnalsYears written years"),
    "Annals.cs:traditionsKept": ("yearly", 0.1, 40, "the year in progress"),
    "Annals.cs:traditionsBroken": ("yearly", 0.05, 40, "the year in progress"),
    "Annals.cs:traditionsBorn": ("yearly", 0.03, 40, "the year in progress"),
    "Annals.cs:tiesFormed": ("yearly", 0.2, 60, "the year in progress"),
    "Annals.cs:omens": ("yearly", 0.1, 60, "the year in progress"),
    "Annals.cs:goals": ("yearly", 0.1, 60, "the year in progress"),
    "Annals.cs:secretsOut": ("yearly", 0.05, 60, "the year in progress"),
    # Secrets and vendettas
    "Secrets/Secrets.cs:knowers": ("fixed", 3, P, "per secret (counted under secrets)"),
    "Secrets/Secrets.cs:secrets": ("colonists", 1, 400, "one per colonist, ended with their death"),
    "Secrets/Secrets.cs:rolled": ("known", 1, 35, "known pawns only"),
    "Secrets/Vendettas.cs:vendettas": ("cap", (40 + 5, 0.05), 400, "40 settled, avengers dead or discarded dropped"),
    "Secrets/Vendettas.cs:eulogies": ("cap", (5, 0.05), 200, "until given"),
    "Secrets/Vendettas.cs:known": ("known", 0.2, P, "known pawns only"),
    # Calendar
    "Calendar/CalendarDay.cs:fallen": ("fixed", 2, 40, "per day on the calendar (counted under days)"),
    "Calendar/CalendarDay.cs:knew": ("fixed", 4, 40, "per day on the calendar"),
    "Calendar/CalendarDay.cs:kin": ("fixed", 2, 40, "per day on the calendar"),
    "Calendar/CalendarTracker.cs:days": ("cap", (C["Calendar"], 0.2), 600, "12 the colony's, 8 the player's, traditions and the founding"),
    "Calendar/CalendarTracker.cs:harvested": ("colonists", 1, D, "the year's harvest, per colonist; cleared yearly"),
    "Calendar/CalendarTracker.cs:restHours": ("colonists", 1, 120, "days taken off by hand: only the player's clicks"),
    "Calendar/CalendarTracker.cs:forgotten": ("fixed", 10, 40, "days put away by hand: only the player's clicks"),
    "Calendar/CalendarWatch.cs:awaySince": ("colonists", 0.5, D, "the colony's people away now; the dead and those no longer ours dropped"),
    "Calendar/CalendarWatch.cs:huntKills": ("colonists", 1, D, "one per colonist, those no longer ours dropped daily"),
    "Calendar/CalendarWatch.cs:knownCrops": ("fixed", 40, 40, "one per crop the colony has grown (crop defs)"),
    "Calendar/CalendarWatch.cs:pendingCrops": ("fixed", 5, D, "new crops until their first harvest (crop defs)"),
    "Calendar/CalendarDay.cs:things": ("fixed", 3, 40, "a camp gift's things, per day on the calendar (CalendarMath.MaxDayThings, at most 2 such days)"),
    "Calendar/CalendarLived.cs:captives": ("colonists", 0.2, 120, "the colony's people carried off, until home or lost to us"),
    # Dreams
    "Dreams/Dreams.cs:recent": ("cap", (80, 3), 250, "80 recent dreams"),
    "Dreams/Dreams.cs:omens": ("cap", (10, 0.2), 200, "until they come"),
    "Dreams/Dreams.cs:seen": ("known", 1, D, "dream counts of known pawns"),
    # Offices, epithets
    "Offices/Offices.cs:holders": ("defs", ("OfficeDef", 1), D, "one per office"),
    "Offices/Offices.cs:elected": ("defs", ("OfficeDef", 1), 40, "one per office"),
    "Epithets/Epithets.cs:holders": ("defs", ("EpithetDef", 1), D, "one per title"),
    "Epithets/Epithets.cs:founders": ("fixed", 6, 40, "those here in the colony's first two days (by ThingID)"),
    # Heart
    "Heart/HeartData.cs:recentBuild": ("fixed", 8, V, "per heart: one per strain source (counted under hearts)"),
    "Heart/HeartData.cs:recentRelief": ("fixed", 8, V, "per heart"),
    "Heart/HeartData.cs:today": ("fixed", 8, V, "per heart"),
    "Heart/HeartData.cs:feelingSinceKeys": ("fixed", 4, 40, "per heart: feelings felt now"),
    "Heart/HeartData.cs:feelingSinceTicks": ("fixed", 4, V, "per heart"),
    "Heart/HeartData.cs:needLack": ("fixed", 4, V, "per heart: the four needs"),
    "Heart/HeartTracker.cs:hearts": ("colonists", 1, 1400, "one per colonist, the gone dropped hourly"),
    # Camps (one camp while it stays)
    "Camps/Camp.cs:members": ("while", 8, P, "the camp's people, while it stays"),
    "Camps/Camp.cs:plans": ("while", 4, 200, "while the camp stays"),
    "Camps/Camp.cs:warmth": ("while", 8, D, "while the camp stays"),
    "Camps/Camp.cs:willing": ("while", 8, P, "while the camp stays"),
    "Camps/Camp.cs:joined": ("while", 3, P, "while the camp stays"),
    "Camps/Camp.cs:arrested": ("while", 2, P, "while the camp stays"),
    "Camps/Camp.cs:prey": ("while", 6, P, "while the camp stays"),
    "Camps/Camp.cs:kills": ("while", 20, P, "while the camp stays (a stay is days, not years)"),
    "Camps/Camp.cs:trees": ("while", 10, P, "while the camp stays"),
    "Camps/Camp.cs:felled": ("while", 60, 30, "trees felled during one stay"),
    "Camps/Camp.cs:felledTicks": ("while", 60, V, "with the above"),
    "Camps/Camp.cs:owned": ("while", 40, V, "the camp's goods, pruned of the gone at saving"),
    "Camps/Camp.cs:asksMade": ("while", 15, 40, "asks made of the camp (once each, or once a day), while it stays"),
    "Camps/CampRequests.cs:errandPeople": ("while", 8, P, "those of the camp on an errand into the colony, while it lasts"),
    "Camps/CampRequests.cs:errandLines": ("while", 4, 120, "what they say on the errand, while it lasts"),
    "Camps/CampLook.cs:herd": ("while", 5, P, "the camp's animals (CampLookMath.MaxAnimals), while it stays"),
    "Camps/CampLook.cs:props": ("while", 6, P, "goods the camp put out (a few stacks), pruned of the gone, while it stays"),
    "Camps/CampLook.cs:walkSeen": ("while", 4, P, "colonists the farewell round looked in on (the walk's count), while the camp stays"),
    # Wishes kept
    "Wishes/Kept.cs:things": ("fixed", 2, P, "per watch (counted under watches)"),
    "Wishes/Kept.cs:cells": ("fixed", 4, 30, "per watch"),
    "Wishes/Kept.cs:watches": ("window", (15, 0.5), 400, "granted wishes still watched (grace days)"),
    "Wishes/Wish.cs:coSigners": ("fixed", 2, P, "per open wish (counted under wishes)"),
    # Aspirations (life goals)
    "Aspirations/Aspirations.cs:all": ("known", 1, 350, "goals of the dead and departed end; finished ones of the dead kept 3 years"),
    "Aspirations/Aspirations.cs:counters": ("known", 3, D, "counters of known pawns"),
    "Aspirations/Aspirations.cs:events": ("window", (30, 1), D, "sparks, 30 days"),
    "Aspirations/Aspirations.cs:inspiredBy": ("known", 0.3, D, "known pawns only"),
    # The council window's memory and the news
    "UI/DilemmaLog.cs:plays": ("fixed", 4, V, "per dilemma record (counted under dilemmas)"),
    "UI/DilemmaLog.cs:started": ("fixed", 2, V, "per dilemma record"),
    "UI/News.cs:waiting": ("cap", (C["NewsWaiting"], 10), 200, "the evening's news, 40 at most, sent each evening"),
    "UI/News.cs:items": ("cap", (C["NewsWaiting"], 10), 200, "per news letter; the game's archive keeps 200 letters"),
    "UI/WeekMemory.cs:pinnedPawns": ("fixed", 4, 40, "pins: only the player's clicks, the gone dropped"),
    "UI/WeekMemory.cs:pinnedWishes": ("fixed", 2, V, "pins of open wishes"),
    "UI/WeekMemory.cs:dilemmas": ("cap", (C["DilemmaRecords"], 0.5), 300, "DilemmaLog.MaxRecords"),
    "UI/WeekMemory.cs:trustKeys": ("colonists", 1, 40, "a trail per colonist, forgotten after 10 days away"),
    "UI/WeekMemory.cs:trustValues": ("colonists", 1, 25 + 8 * C["TrailDays"], "8 days of trust each"),
    "Scenarios/Scenarios.cs:events": ("fixed", 3, 40, "a scenario part's events"),
    "Scenarios/ScenarioParts.cs:days": ("fixed", 3, 150, "a scenario part's days, fixed by the scenario"),
    "Scenarios/ScenarioParts.cs:offices": ("fixed", 4, 40, "a scenario part's seats, fixed by the scenario"),
    # Quirks
    "Quirks/Quirks.cs:quirks": ("colonists", 2, 200, "2 per colonist, the dead and discarded dropped"),
    "Quirks/Quirks.cs:luckyPast": ("cap", (C["LuckyPast"], 0.05), 150, "LuckyItems.MaxPast"),
    "Quirks/Quirks.cs:downedTick": ("colonists", 0.5, D, "within the longest quirk window"),
    "Quirks/Quirks.cs:battleTick": ("colonists", 1, D, "within the longest quirk window"),
    # Marks on a person's character (what the marks watch for; the marks themselves are kept on the persona)
    "Persona/Marks.cs:watches": ("colonists", 1, 120, "one per person watched; known pawns only, empty ones dropped"),
    "Persona/Marks.cs:fights": ("colonists", 8, V, "the last 8 fights won per person, a year at most"),
    "Persona/Marks.cs:addictions": ("colonists", 0.3, 40, "the addictions they have now"),
    "Persona/Marks.cs:once": ("known", 0.3, 40, "skills mastered; known pawns only, 800 at most"),
    # Faith among the colony's people (a colonist's faith moments, a faith's demands watched, crises waiting to be felt)
    "Faith/FaithPeople.cs:people": ("colonists", 1, 160, "one per colonist whose faith moved or is watched; known pawns only, empty records dropped daily and at saving"),
    "Quirks/Quirks.cs:practice": ("colonists", 1, 140, "one per colonist who works or keeps late or early hours; dead and departed dropped daily and at saving, faded-out ones removed"),
    "Quirks/Practice.cs:kinds": ("fixed", const("Quirks/Practice.cs", "MaxKinds", 8), 30, "per colonist: the kinds of work they've done lately, 8 at most (counted under practice)"),
    "Quirks/Practice.cs:hours": ("fixed", const("Quirks/Practice.cs", "MaxKinds", 8), 20, "per colonist: hours at each kind of work, in step with kinds (counted under practice)"),
    "Faith/FaithPeople.cs:leanings": ("fixed", const("Faith/LeaningMath.cs", "MaxLeanings", 6), 120, "per colonist: the ideoligions they're drawn to, the strongest 6 at most, faded ones forgotten daily (counted under people)"),
    "Faith/FaithPeople.cs:notes": ("fixed", const("Faith/FaithPeopleMath.cs", "MaxNotes", 5), 170, "per colonist: the newest faith moments, 120 days at most (counted under people)"),
    "Faith/FaithPeople.cs:waiting": ("cap", (COLONISTS, 0.15), 140, "crises waiting to be felt: one per kind per colonist, let go after 6 days"),
    # A person's record (sides moved, marks, views) and what pairs went through together
    "Persona/PersonaTracker.cs:list": ("known", 0.6, 100 + 2 * C["PersonaAxes"] * V + C["PersonaMarks"] * 0.6 * 200 + 24 * 70,
                                       "the colony's own people the game still knows; empty records dropped at saving"),
    "Persona/PawnPersona.cs:l": ("fixed", C["PersonaAxes"], V, "per person: one per side (counted under people)"),
    "Persona/PawnPersona.cs:d": ("fixed", C["PersonaAxes"], V, "per person: one per side, fading (counted under people)"),
    "Persona/PawnPersona.cs:marks": ("fixed", C["PersonaMarks"], 200, "per person: PawnPersona.MaxMarks, each let go after its keeping (counted under people)"),
    "Persona/PawnPersona.cs:stances": ("fixed", 24, 70, "per person: one per topic (23), and per newcomer for 60 days (counted under people)"),
    "Persona/PersonaTracker.cs:memories": ("cap", (C["SharedMemories"], 5), 320, "PersonaTracker.MaxMemories, 8 per pair and 3 of a kind; past it by 60 the faintest go at once; faded ones and those of people the game no longer knows dropped at saving"),
    "Social/Talk/TalkTracker.cs:notes": ("cap", (C["TalkNotes"], 4), 200, "the talk's news: TalkTracker.MaxNotes, a month at most"),
    # Traits that change (the changes made, and what each colonist is watched for)
    "Persona/TraitChanges.cs:changes": ("cap", (const("Persona/TraitChanges.cs", "MaxChanges", 200), 0.1), 1200, "known pawns only, 8 per person, 200 at most; with their story"),
    "Persona/TraitChanges.cs:absorbed": ("fixed", 8, V, "per change: how the trait moved each side (counted under changes)"),
    "Persona/TraitChanges.cs:watches": ("colonists", 1, 3400, "one per colonist with its deeds and ten snapshots; known pawns only"),
    "Persona/TraitChanges.cs:tallies": ("fixed", 3, 80, "per colonist: deeds counted, 240 days, 12 per kind (counted under watches)"),
    "Persona/TraitChanges.cs:snapshots": ("fixed", 10, 60, "per colonist: 10 snapshots, every ten days (counted under watches)"),
    "Persona/TraitChanges.cs:values": ("fixed", 3, D, "per snapshot: the records a pace reads (counted under watches)"),
    "Persona/TraitChanges.cs:once": ("fixed", 2, 40, "the changes that come once in a colony's life"),
    # The world
    "World/FactionMemory.cs:deeds": ("fixed", 12 * 12, 200, "12 deeds per people (12 peoples), fading out"),
    "World/FactionMemory.cs:notes": ("window", (C["KinNoteDays"], 1), 200, "who tended or freed whom: 30 days, 60 at most, dropped when the stranger dies"),
    # Reputation and gossip
    "Social/Gossip/ReputationTracker.cs:deeds": ("cap", (min(220, 10 * COLONISTS), COLONISTS * 0.3), 300, "10 per colonist, 220 in all, faded ones dropped"),
    "Social/Gossip/Deed.cs:heard": ("cap", (min(220, 10 * COLONISTS) * 10, COLONISTS * 3), 110, "who knows each deed (30 at most a deed), with its deed"),
    "Social/Gossip/ReputationTracker.cs:told": ("colonists", 1, 60, "one packed number per colonist, the gone dropped daily"),
    "World/WorldSituation.cs:flags": ("fixed", 4, 40, "per situation (counted under active and history)"),
    "World/WorldSituation.cs:deeds": ("fixed", 4, 40, "per situation"),
    "World/WorldSituation.cs:beats": ("fixed", 4, 200, "per situation"),
    "World/WorldSituation.cs:sufferers": ("fixed", 4, P, "per situation"),
    "World/WorldSituation.cs:held": ("fixed", 4, P, "per situation"),
    "World/WorldSituation.cs:numbers": ("fixed", 4, D, "per situation"),
    "World/WorldSituation.cs:followUps": ("fixed", 2, 40, "per past situation: what followed from it, once per def (counted under history)"),
    "World/WorldSituations.cs:active": ("fixed", C["SituationsActive"], 1500, "2 running"),
    "World/WorldSituations.cs:history": ("cap", (40, 0.1), 250, "40 past"),
    "World/WorldSituations.cs:lastEnded": ("defs", ("WorldSituationDef", 1), D, "one per situation def"),
    # Rimfolk's events
    "Happenings/Happenings.cs:group": ("fixed", 1, 300, "per raid or visit (counted with them)"),
    "Happenings/Happenings.cs:things": ("fixed", 3, P, "per departed record"),
    "Happenings/Happenings.cs:touched": ("fixed", 3, P, "per visit"),
    "Happenings/Happenings.cs:raids": ("cap", (5, 0.05), 400, "until they come"),
    "Happenings/Happenings.cs:departed": ("window", (120, 0.05), 400, "the departed 2 years"),
    "Happenings/Happenings.cs:grudges": ("window", (120, 0.05), 200, "grudges 2 years"),
    "Happenings/Happenings.cs:visits": ("window", (30, 0.2), 300, "visits a month"),
    "Happenings/Happenings.cs:memos": ("window", (180, 0.3), 200, "memos 3 years (children's 10), used ones a day"),
    "Happenings/Happenings.cs:seenQuests": ("fixed", 60, V, "quests the game still keeps"),
    "Happenings/Workers_Chains2.cs:flags": ("fixed", 4, 40, "per story chain (counted under chains)"),
    # Answers playing out
    "PlayOut/PlayOutLord.cs:lines": ("while", 6, 120, "while the gathering lasts"),
    "PlayOut/PlayOutLord.cs:served": ("while", COLONISTS, P, "while the gathering lasts"),
    "PlayOut/PlayOutLord.cs:fed": ("while", COLONISTS, P, "while the gathering lasts"),
    "PlayOut/PlayOutLord.cs:abstained": ("while", 4, P, "while the gathering lasts"),
    "PlayOut/PlayOutTracker.cs:plays": ("cap", (8, 0.5), 600, "waiting ones capped, done ones dropped"),
    "PlayOut/PlayOutTracker.cs:results": ("cap", (C["PlayResults"], 1), 120, "how the last ones went: PlayOutTracker.MaxResults"),
    # Couriers: trips waiting or on the map (CourierMath.MaxTrips), each dropped two days after it ends at most
    "Couriers/CourierTracker.cs:trips": ("cap", (C["CourierTrips"], 0.5), 900, "trips waiting or under way: CourierMath.MaxTrips; ended ones dropped within two days"),
    "Couriers/CourierTrip.cs:people": ("while", C["CourierPeople"], P, "a trip's people, while it lasts"),
    "Couriers/CourierTrip.cs:things": ("while", 8, P, "particular things a trip came for (a neighbour's cargo), while it lasts"),
    "Couriers/CourierTrip.cs:toUnload": ("while", C["CourierPeople"] + 1, P, "who still carries the colony's goods, while the trip lasts"),
    # Visitors: visits waiting or on the map (VisitorMath.MaxTrips), each dropped a day after it ends at most
    "Visitors/VisitorTracker.cs:trips": ("cap", (C["VisitorTrips"], 0.4), 700, "visits waiting or under way: VisitorMath.MaxTrips; ended ones dropped within a day"),
    "Visitors/VisitorTrip.cs:people": ("while", C["VisitorPeople"], P, "a visit's people, while it lasts"),
    "Visitors/VisitorTrip.cs:animals": ("while", C["VisitorAnimals"], P, "a visit's animals (a pack beast, a family's dog, a herd), while it lasts"),
    "PlayOut/PlayOuts.cs:invited": ("fixed", 8, P, "per play-out (counted under plays)"),
    "PlayOut/PlayOuts.cs:spots": ("fixed", 2, 60, "per play-out"),
    "PlayOut/PlayOuts.cs:lines": ("fixed", 4, 120, "per play-out"),
    "PlayOut/PlayOuts.cs:moods": ("fixed", 4, 120, "per play-out"),
    "PlayOut/PlayOuts.cs:things": ("fixed", 2, P, "per play-out: the thing handed over, or the dead still to carry out (OnMapMath.MaxDead at most)"),
    "PlayOut/PlayOuts.cs:plots": ("fixed", 2, 40, "per play-out: where a burial's graves lie (OnMapMath.MaxDead at most), burials only"),
    "PlayOut/PlayOuts.cs:round": ("fixed", 2, P, "per play-out: those a round goes to (OnMapMath.MaxRounds at most), rounds only"),
    # Things kept on the map for a while: lanterns for a night, a fire, the pyre, a bolted door, an animal at heel
    "PlayOut/OnMapTimed.cs:kept": ("cap", (C["TimedKept"], 0.3), 300, "kept a night to a few days, at most OnMapTimed.MaxKept at once"),
    "PlayOut/OnMapTimed.cs:things": ("fixed", 3, P, "per thing kept: its lanterns (OnMapMath.MaxLanterns at most), the fire, the door"),
    "PlayOut/PlayOuts.cs:named": ("fixed", 2, P, "per play-out: those the answer names (counted under plays)"),
    "PlayOut/PlayOuts.cs:hands": ("fixed", 6, 200, "per play-out: who went (counted under plays)"),
    "PlayOut/PlayOuts.cs:route": ("fixed", 6, 40, "per searcher: the places they look over (counted under plays)"),
    "PlayOut/PlayOuts.cs:tasks": ("fixed", 3, 30, "per work party: its kinds of work (counted under plays)"),
    "PlayOut/PlayOuts.cs:game": ("fixed", 3, P, "per hunt: 3 animals at most (counted under plays)"),
    # Ambitions and meetings
    "Ambitions/Ambitions.cs:baselines": ("fixed", 4, V, "per ambition's goals"),
    "Ambitions/Ambitions.cs:lastOffered": ("defs", ("AmbitionDef", 1), D, "one per ambition def"),
    "Ambitions/Ambitions.cs:options": ("while", 3, 40, "the meeting letter's options, while it waits"),
    "Ambitions/Ambitions.cs:proposers": ("while", 3, P, "while the letter waits"),
    "Ambitions/Ambitions.cs:voterPawns": ("while", COLONISTS, P, "while the letter waits"),
    "Ambitions/Ambitions.cs:voterChoice": ("while", COLONISTS, V, "while the letter waits"),
    "Ambitions/Ambitions.cs:absentProposers": ("while", 2, P, "while the letter waits"),
    "Ambitions/CouncilMeeting.cs:voters": ("while", COLONISTS, P, "while a meeting is called"),
    "Ambitions/CouncilMeeting.cs:pitches": ("while", 3, 200, "while a meeting is called"),
    "Ambitions/CouncilMeeting.cs:attendees": ("while", COLONISTS, P, "while a meeting is called"),
    "Ambitions/CouncilMeeting.cs:remarks": ("while", 10, 120, "while a meeting is called"),
    "Ambitions/CouncilMeeting.cs:invites": ("while", COLONISTS, 80, "while a meeting is called"),
    # Personal stories
    "Arcs/Arc.cs:told": ("fixed", 6, 40, "per story (counted under active and past)"),
    "Arcs/Arc.cs:heldOr": ("fixed", 4, V, "per story"),
    "Arcs/ArcTracker.cs:active": ("colonists", 0.5, 500, "stories under way"),
    "Arcs/ArcTracker.cs:past": ("cap", (C["ArcPast"], 0.3), 200, "ArcTracker.MaxPast"),
    "Arcs/ArcTracker.cs:eulogies": ("cap", (5, 0.05), 200, "until given"),
    # Ailments
    "Ailments/AilmentTracker.cs:last": ("window", (120, COLONISTS * 0.05 * 2), D, "last ailments 120 days, known pawns only"),
    "Ailments/AilmentTracker.cs:freshDay": ("colonists", 1, D, "known pawns only"),
    "Ailments/AilmentTracker.cs:seenDay": ("colonists", 1, D, "known pawns only"),
    "Ailments/AilmentTracker.cs:sweets": ("colonists", 0.5, D, "faded below 0.05 dropped"),
    "Ailments/AilmentTracker.cs:announced": ("colonists", 1, V, "only hediffs colonists still have"),
    "Ailments/AilmentTracker.cs:woundsRolled": ("colonists", 1, V, "only hediffs colonists still have"),
    "Ailments/AilmentTracker.cs:bitesRolled": ("colonists", 0.2, V, "only hediffs colonists still have"),
    "Ailments/AilmentTracker.cs:doctors": ("colonists", 0.5, D, "only hediffs colonists still have"),
    "Ailments/AilmentTracker.cs:had": ("known", 3, 50, "known pawns only"),
    "Ailments/AilmentTracker.cs:carrying": ("known", 0.1, 60, "illnesses had now that an ailment may follow; let go when over, known pawns only"),
    "Ailments/AilmentTracker.cs:healing": ("known", 0.3, 120, "injuries an ailment may follow; let go when healed, known pawns only"),
    # Advisor, building
    "Advisor/AdvisorCore.cs:wealthHistory": ("window", (60, 1), V, "60 days of wealth"),
    "Building/CouncilPlanQueue.cs:queue": ("while", 30, 150, "blueprints waiting to be placed"),
    "Building/BuildPriority.cs:levels": ("while", 40, D, "blueprints and frames with a build priority set, per map, until built or cancelled"),
    "Building/CustomRooms.cs:pieces": ("settings", 30, 120, "per saved room, in the mod's settings file"),
    "Building/PersonalTouches.cs:pending": ("cap", (10, 0.2), 200, "until built or dropped"),
    # Dilemmas and decrees
    "Dilemmas/Dilemmas.cs:traditionCount": ("defs", ("DilemmaDef", 0.3), D, "per tradition"),
    "Dilemmas/Dilemmas.cs:traditionBreaks": ("defs", ("DilemmaDef", 0.1), D, "per tradition"),
    "Dilemmas/Dilemmas.cs:traditions": ("defs", ("DilemmaDef", 0.1), 40, "per tradition"),
    "Dilemmas/Dilemmas.cs:lastTick": ("defs", ("DilemmaDef", 1), D, "one per dilemma def"),
    "Decrees/Decrees.cs:useDefs": ("window", (15, 0.3), 40, "uses within the fatigue window"),
    "Decrees/Decrees.cs:useTicks": ("window", (15, 0.3), V, "with the above"),
    "Decrees/Decrees.cs:active": ("fixed", 3, 150, "decrees in force (3 at most)"),
    "Decrees/Decrees.cs:readyAt": ("defs", ("DecreeDef", 1), D, "one per decree def"),
    "Decrees/Decrees.cs:requests": ("fixed", 3, 150, "one per decree at most"),
    "Decrees/Decrees.cs:threatTicks": ("fixed", const("Decrees/Decrees.cs", "MaxThreats", 8), V, "DecreeTracker.MaxThreats: the big threats of the last half month, for how needed a decree is"),
    "Decrees/Decrees.cs:nightHours": ("colonists", 1, 150, "a curfew's night hours, per colonist while it lasts; after it only those away (the dead, the gone and the unknown let go)"),
    # Other mods
    "Integrations/ModLinks.cs:lastWater": ("colonists", 1, D, "today and yesterday only"),
    "Integrations/ModLinks.cs:lastMend": ("colonists", 1, D, "today and yesterday only"),
    "Integrations/SocialInteractions.cs:bestFriends": ("cap", (C["BestFriendPairs"], 0.2), 60, "Caps.BestFriendPairs, known pawns"),
    # Setbacks
    "Setbacks/SetbackTracker.cs:sowers": ("fixed", 3, P, "per sown field (counted under sown)"),
    "Setbacks/SetbackTracker.cs:ticks": ("fixed", 3, V, "per sown field"),
    "Setbacks/SetbackTracker.cs:built": ("window", (20, 3), 200, "what colonists built, while it stands and is recent"),
    "Setbacks/SetbackTracker.cs:sown": ("window", (20, 2), 200, "fields sown, until harvested or lost"),
    "Setbacks/SetbackTracker.cs:last": ("window", (20, COLONISTS * 0.5), D, "20 days"),
    "Setbacks/SetbackTracker.cs:friends": ("pairs", 0.2, 50, "pairs of friends today"),
    "Setbacks/SetbackTracker.cs:bestAt": ("fixed", 12, D, "one per skill"),
    "Setbacks/SetbackTracker.cs:lastGathering": ("fixed", 1, D, "one per home map"),
    "Setbacks/SetbackTracker.cs:grantVoiced": ("window", (20, 0.5), V, "20 days"),
    "Setbacks/SetbackTracker.cs:grantTicks": ("window", (20, 0.5), V, "20 days"),
    "Setbacks/SetbackTracker.cs:grantWho": ("window", (20, 0.5), 40, "20 days"),
    # Legacy
    "Lives/Legacy.cs:heirlooms": ("cap", (10, 0.02), 300, "until the heir dies or is discarded, or the thing is lost"),
    "Lives/Legacy.cs:children": ("known", 0.1, 40, "known pawns only"),
    "Lives/Legacy.cs:cameOfAge": ("window", (6, 0.02), P, "6 days"),
    "Lives/Legacy.cs:cameOfAgeTick": ("window", (6, 0.02), D, "6 days"),
    # Character
    "Character/Character.cs:values": ("fixed", 5, V, "five ways"),
    "Character/Character.cs:tiers": ("fixed", 5, V, "five ways"),
    "Character/Character.cs:recent": ("cap", (30, 1), 150, "30 recent shifts"),
    "Character/ColonyTraits.cs:rates": ("defs", ("ColonyTraitDef", 1), D, "one per colony trait def"),
    "Character/ColonyTraits.cs:last": ("defs", ("ColonyTraitDef", 1), D, "one per colony trait def"),
    "Character/ColonyTraits.cs:known": ("defs", ("ColonyTraitDef", 0.5), 40, "traits the colony grew into"),
    "Character/ColonyTraits.cs:today": ("fixed", 3, D, "today's tallies, emptied each day"),
    # Strangers
    "Strangers/Encounters.cs:lastTick": ("defs", ("EncounterDef", 1), D, "one per encounter def"),
    "Strangers/Nemeses.cs:all": ("cap", (30 + 3, 0.05), 300, "30 that have ended"),
    "Strangers/Nuisances.cs:last": ("window", (10, COLONISTS * 0.1), D, "10 days"),
    "Strangers/Pets.cs:known": ("fixed", 30, P, "animals known: the dead get their farewell, the discarded are dropped"),
    "Strangers/Pets.cs:fought": ("fixed", 5, P, "animals in the last fight"),
    # Social
    "Social/Autonomy.cs:pawns": ("window", (2, COLONISTS * 0.5), P, "who acted when: the last 2 days"),
    "Social/Autonomy.cs:lastActed": ("window", (2, COLONISTS * 0.5), V, "with the above"),
    "Social/Autonomy.cs:plans": ("cap", (10, 0.2), P, "plans until built"),
    "Social/Moments.cs:pairs": ("pairs", 0.5, 120, "pair tones forgotten after 4 quiet days or when one is gone"),
    "Social/SocialWeb.cs:history": ("fixed", 10, 60, "per tie (counted under ties)"),
    "Social/SocialWeb.cs:ties": ("pairs", 0.3, 400, "ties of the dead dropped daily, of the gone faded out"),
    "Social/SocialWeb.cs:lost": ("cap", (40, 0.05), 200, "40 lost bonds"),
    "Social/SocialWeb.cs:fighters": ("while", COLONISTS, P, "while a fight lasts"),
    "Social/SocialWeb.cs:battleFallen": ("while", 5, 40, "while a fight lasts"),
    # Faith and the council (Faith/FaithCouncilTracker.cs)
    "Faith/FaithCouncilTracker.cs:faithEvenings": ("fixed", 6, D, "each faith's last evening of faith: one per faith, gone after 60 days or with the faith"),
    "Faith/FaithCouncilTracker.cs:plans": ("while", 1, R, "the evening of faith waiting to be held: one at a time, let go when its gathering is over"),
    "Faith/FaithCouncilTracker.cs:heard": ("cap", (const("Faith/FaithCouncilTracker.cs", "MaxHeard", 40), 0.6), 80,
                                             "who heard which faith spoken for lately: one per colonist, 30 days, FaithCouncilTracker.MaxHeard"),
    # Groups (Social/Groups/)
    "Social/Groups/GroupTracker.cs:groups": ("cap", (6, 0.1), 2500, "groups of 3 to 9, the dead and departed dropped daily, ended below two"),
    "Social/Groups/GroupTracker.cs:newcomers": ("cap", (5, 0.1), 150, "newcomers, 40 days at most"),
    "Social/Groups/GroupTracker.cs:past": ("cap", (12, 0.05), 400, "12 past groups, members the game still knows"),
    "Social/Groups/GroupTracker.cs:work": ("colonists", 1, 400, "one work log per colonist, 6 kinds each"),
    "Social/Groups/SocialGroup.cs:members": ("fixed", 6, 200, "per group, and per past group by ID (counted under groups)"),
    "Social/Groups/SocialGroup.cs:history": ("fixed", 14, 120, "per group: 14 lines (counted under groups)"),
    "Social/Groups/SocialGroup.cs:norms": ("fixed", 3, 40, "per group: 3 ways (counted under groups)"),
    "Social/Groups/SocialGroup.cs:views": ("fixed", 3, 40, "per group: 3 views (counted under groups)"),
    "Social/Groups/SocialGroup.cs:rivals": ("fixed", 3, 150, "per group: rivalries with groups still there (counted under groups)"),
    "Social/Groups/SocialGroup.cs:formerNames": ("fixed", 10, 30, "per group: 10 names (counted under groups)"),
    "Social/Groups/SocialGroup.cs:kinds": ("fixed", 6, 30, "per work log: 6 kinds (counted under work)"),
    "Social/Groups/SocialGroup.cs:counts": ("fixed", 6, 25, "per work log (counted under work)"),
    # Health
    "Health/HealthTracker.cs:losses": ("fixed", 3, V, "per healer: 30 days (counted under healers)"),
    "Health/HealthTracker.cs:tends": ("fixed", 4, D, "per case: who tended it (counted under cases)"),
    "Health/HealthTracker.cs:healers": ("known", 0.4, 200, "known pawns only"),
    "Health/HealthTracker.cs:cases": ("cap", (5, 0.03), 300, "cases while the patient lives"),
    "Health/HealthTracker.cs:lastDoctor": ("window", (3, COLONISTS * 0.5), D, "tends 3 days"),
    "Health/HealthTracker.cs:lastTend": ("window", (3, COLONISTS * 0.5), D, "tends 3 days"),
    "Health/HealthTracker.cs:bedHours": ("colonists", 0.2, D, "known pawns only, while laid up"),
    "Health/HealthTracker.cs:known": ("colonists", 1, 40, "known pawns only"),
    "Health/HealthTracker.cs:partsSeen": ("colonists", 0.5, V, "missing parts colonists still have"),
    "Health/HealthTracker.cs:setbacks": ("colonists", 0.1, D, "convalescences colonists still have"),
    "Health/HealthTracker.cs:lastSetback": ("window", (20, 0.1), D, "20 days"),
    "Health/HealthTracker.cs:convalescing": ("colonists", 0.2, 40, "known pawns only"),
    "Health/HealthTracker.cs:visits": ("window", (30, 1), D, "30 days"),
    "Health/HealthTracker.cs:lastTrustLine": ("window", (20, 0.2), D, "20 days"),
    "Health/HealthTracker.cs:worst": ("known", 0.1, D, "hard times not over yet (hunger, cold, untended illness); known pawns only"),
    # Everyday pressures
    "Pressures/Pressures.cs:meals": ("fixed", 12, 40, "per colonist: 12 meals (counted under people)"),
    "Pressures/Pressures.cs:mealTicks": ("fixed", 12, V, "per colonist"),
    "Pressures/Pressures.cs:workKinds": ("fixed", 6, 40, "per colonist"),
    "Pressures/Pressures.cs:workCounts": ("fixed", 6, V, "per colonist"),
    "Pressures/Pressures.cs:people": ("colonists", 1, 900, "one per colonist, the gone dropped daily"),
    "Pressures/Pressures.cs:apartSince": ("pairs", 0.05, D, "pairs apart now"),
    "Pressures/Pressures.cs:firstSeen": ("while", 30, D, "plans while they stand"),
    "Pressures/Pressures.cs:lastProgress": ("while", 30, D, "frames while they stand"),
    "Pressures/Pressures.cs:lastWork": ("while", 30, D, "frames while they stand"),
    # Stories and missions
    "Missions/Missions.cs:goals": ("fixed", 3, 80, "per mission (counted under missions)"),
    "Missions/Missions.cs:group": ("fixed", 4, P, "per mission"),
    "Missions/StoriesComponent.cs:chains": ("window", (120, 0.1), 500, "chains 120 days after they end"),
    "Missions/StoriesComponent.cs:missions": ("window", (2, 0.2), 500, "missions 2 days after they end"),
    # Defense lines (per home map, gone with it)
    "Weather/WeatherBook.cs:days": ("fixed", const("Weather/WeatherMath.cs", "MaxDays", 30), 70, "WeatherMath.MaxDays: the last month, a short line a day, per home map"),
    "Weather/WeatherBook.cs:quarters": ("fixed", const("Weather/WeatherMath.cs", "MaxQuarters", 12), 90, "WeatherMath.MaxQuarters: three years of seasons, a short line each, per home map"),
    "Weather/WeatherDynamics.cs:pulls": ("fixed", const("Weather/WeatherDynamicsMath.cs", "MaxPulls", 6), 40, "WeatherDynamicsMath.MaxPulls: events' pulls on the weather, a few days each, per home map"),
    "Defense/DefenseLines.cs:lines": ("fixed", 4, 300 + C["SpanSide"] * 30 + 8 * 80 + 3 * 40 + C["DefensePlans"] / 2 * 180 + C["DefenseRaids"] * 1100,
                                      "lines the player made (only their clicks), per map; gone with the map"),
    "Defense/DefenseLine.cs:span": ("fixed", C["SpanSide"], 30, "per line: the cells across its gap, 29 at most (counted under lines)"),
    "Defense/DefenseLine.cs:done": ("defs", ("DefenseStepDef", 1), 80, "per line: one per step (counted under lines)"),
    "Defense/DefenseLine.cs:skipped": ("defs", ("DefenseStepDef", 0.5), 40, "per line: steps set aside by hand, one per step (counted under lines)"),
    "Defense/DefenseLine.cs:plans": ("fixed", C["DefensePlans"], 180, "per line: DefenseLine.MaxPlans (counted under lines)"),
    "Defense/DefenseLine.cs:raids": ("fixed", C["DefenseRaids"], 1100, "per line: the last DefenseLine.MaxRaids raids (counted under lines)"),
    "Defense/DefenseLine.cs:downed": ("fixed", 4, 120, "per raid: 4 at most (counted under lines)"),
    "Defense/DefenseLine.cs:lessons": ("fixed", 3, 40, "per raid: 3 at most (counted under lines)"),
    "Defense/DefenseLinesWatch.cs:running": ("while", 4, 600, "one per line while a raid lasts"),
    "Defense/DefenseLinesWatch.cs:downedSeen": ("while", COLONISTS, V, "colonists downed in the raid under way, cleared when it ends"),
}

# Collections kept per something counted elsewhere (inside a record already sized above), not added to the total.
NESTED_HINT = "(counted under"


def entries(kind, args, day):
    if kind in ("fixed", "settings"):
        return args
    if kind == "colonists":
        return args * COLONISTS
    if kind == "known":
        return args * known(day)
    if kind == "pairs":
        return args * PAIRS
    if kind == "defs":
        name, k = args
        return k * defs(name)
    if kind == "cap":
        n, rate = args
        return min(n, rate * day)
    if kind == "window":
        days, rate = args
        return rate * min(days, day)
    if kind == "yearly":
        return args * DAYS_PER_YEAR  # its fullest, at the year's end
    if kind == "while":
        return args
    if kind == "grows":
        return args * day
    raise ValueError(kind)


def saved_collections():
    rx = re.compile(r'Scribe_Collections\.Look\(ref ([A-Za-z0-9_.]+), "([^"]+)"')
    found = []
    for dirpath, _, files in os.walk(SRC):
        for f in sorted(files):
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, SRC).replace(os.sep, "/")
            for m in rx.finditer(open(p, encoding="utf-8-sig").read()):
                found.append(f"{rel}:{m.group(1).split('.')[-1]}")
    return sorted(set(found))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default=os.path.join(ROOT, "docs", "save-size.md"))
    ap.add_argument("--strict", action="store_true")
    args = ap.parse_args()
    count_defs()
    found = saved_collections()
    missing = [k for k in found if k not in MODEL]
    stale = [k for k in MODEL if k not in found]
    growing = [k for k, v in MODEL.items() if v[0] == "grows"]
    rows = []
    days = [DAYS_PER_YEAR, 2 * DAYS_PER_YEAR, YEARS * DAYS_PER_YEAR]
    total = [0.0, 0.0, 0.0]
    for k in found:
        if k not in MODEL:
            continue
        kind, a, size, rule = MODEL[k]
        n = [entries(kind, a, d) for d in days]
        nested = NESTED_HINT in rule or kind == "settings"
        kb = n[2] * size / 1024
        if not nested:
            for i in range(3):
                total[i] += n[i] * size / 1024
        rows.append((k, kind, n, kb, rule, nested))

    L = ["# Save size over a long game (estimate)", ""]
    L.append("Written by `Tools/save_estimate.py` (run `python3 Tools/save_estimate.py` from the repository root; `--strict` fails")
    L.append("on a saved collection with no model, or one that grows without end). Every collection Rimfolk saves, the rule that")
    L.append(f"keeps it in bounds, and how many entries it holds after one, two and five in-game years ({DAYS_PER_YEAR}-day years) in a")
    L.append(f"colony of {COLONISTS}, with the game keeping {int(known(0)) - COLONISTS} more people it knows and 5 more each year. Rates are")
    L.append("generous guesses; sizes are rough XML sizes. Caps named as constants are read from the source. \"Counted under\" rows are")
    L.append("kept inside another record (a journal's lines, a heart's lists) and are already in that record's size.")
    L.append("")
    L.append(f"Saved collections found: {len(found)}; modelled: {len(found) - len(missing)}; growing without end: {len(growing)}.")
    L.append(f"Rimfolk's part of the save, roughly: **{total[0]:,.0f} KB** after a year, **{total[1]:,.0f} KB** after two, "
             f"**{total[2]:,.0f} KB** after five.")
    L.append("")
    if missing:
        L.append("Not modelled (add them to MODEL in the script): " + ", ".join(f"`{m}`" for m in missing))
        L.append("")
    L.append("| Collection | Rule | Kind | 1 year | 2 years | 5 years | KB at 5 years |")
    L.append("|---|---|---|---|---|---|---|")
    for k, kind, n, kb, rule, nested in sorted(rows, key=lambda r: (-r[3] if not r[5] else 1e9, r[0])):
        L.append(f"| `{k}` | {rule} | {kind} | {n[0]:,.0f} | {n[1]:,.0f} | {n[2]:,.0f} | {('(settings file)' if kind == 'settings' else '(in its record)') if nested else f'{kb:,.1f}'} |")
    L.append("")
    out = "\n".join(L) + "\n"
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(out)
    print(f"{len(found)} saved collections, {len(missing)} not modelled, {len(growing)} growing without end, {len(stale)} models for collections no longer saved")
    print(f"about {total[0]:,.0f} KB after a year, {total[1]:,.0f} KB after two, {total[2]:,.0f} KB after five")
    for m in missing:
        print("not modelled: " + m)
    for g in growing:
        print("grows without end: " + g)
    for s in stale:
        print("no longer saved: " + s)
    print("wrote " + os.path.relpath(args.out, ROOT))
    if args.strict and (missing or growing):
        sys.exit(1)


if __name__ == "__main__":
    main()
