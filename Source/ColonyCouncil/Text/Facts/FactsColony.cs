using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The providers of the colony: its news, its hospital, what their people remember of it.
    public static partial class Facts
    {
        // ---------------- The colony ----------------

        private static void Colony(FactSet f, Pawn c, Faction p)
        {
            var map = c.MapHeld;
            var home = map?.Parent as Settlement;
            f.Value("COLONY", home != null && home.HasName ? home.Name : Faction.OfPlayer?.Name);
            int days = Now / GenDate.TicksPerDay;
            if (days < 30) f.Tag("colony:young");
            else if (days >= 180) f.Tag("colony:old");
            var scene = TalkScene.Of(map);
            var tr = TalkTracker.Instance;
            if (scene != null)
            {
                if (scene.foodDays < 4f) f.Tag("colony:hungry", 1.5f);
                else if (scene.foodDays >= 30f) f.Tag("colony:plenty");
                if (scene.children.Count > 0) f.Tag("colony:children");
                if (scene.tameAnimals >= 5) f.Tag("colony:animals");
                if (scene.prisoners.Count > 0) f.Tag("colony:prisoners");
                if (scene.expecting) f.Tag("colony:expecting", 1f);
                if (scene.bestCook != null) f.Value("COOK", scene.bestCook.LabelShort);
                if (scene.bestArtist != null) f.Value("ARTIST", scene.bestArtist.LabelShort);
                if (scene.leader != null)
                {
                    f.Value("OURLEADER", scene.leader.LabelShort);
                    if (scene.leader == c) f.Tag("us:leader");
                }
                if (!scene.disaster.NullOrEmpty())
                {
                    f.Tag("colony:disaster", 2f);
                    f.Value("DISASTER", scene.disaster);
                }
                if (scene.camp != null && !scene.campName.NullOrEmpty())
                {
                    f.Tag("colony:camp", 1.5f);
                    f.Value("CAMP", scene.campName);
                }
                if (!scene.traderPeople.NullOrEmpty() && scene.traderPeople != p?.Name)
                {
                    f.Tag("colony:traders", 1f);
                    f.Value("TRADERS", scene.traderPeople);
                }
                var nc = scene.newcomers.FirstOrDefault(n => n.pawn != c && n.days < 10f);
                if (nc.pawn != null)
                {
                    f.Tag("colony:newcomer", Fresh(nc.days, 10f, 2f));
                    f.Value("NEWCOMER", nc.pawn.LabelShort);
                }
                if (scene.calendarDay != null && !scene.calendarDay.label.NullOrEmpty())
                {
                    f.Tag("colony:day", 1.5f);
                    f.Tag(scene.calendarWhen == 0 ? "colony:daytoday" : scene.calendarWhen > 0 ? "colony:daysoon" : "colony:daypast", 1.5f);
                    f.Value("DAY", scene.calendarDay.label);
                }
                // Another of the colony who holds a title of the Empire's (the colonist's own is us:noble).
                Pawn noble = null;
                RoyalTitle nobleTitle = null;
                foreach (var o in scene.colonists)
                {
                    var t = o == c ? null : o.royalty?.MostSeniorTitle;
                    if (t?.def != null && (nobleTitle == null || t.def.seniority > nobleTitle.def.seniority))
                    {
                        noble = o;
                        nobleTitle = t;
                    }
                }
                if (noble != null)
                {
                    f.Tag("colony:noble");
                    f.Value("NOBLE", noble.LabelShort);
                    f.Value("NOBLETITLE", nobleTitle.Label);
                }
                // No big threat for a month.
                if (scene.daysSinceThreat >= 30f) f.Tag("colony:quiet");
            }
            if (tr != null)
            {
                var raid = tr.Latest("Rimfolk_Topic_Raid", 6f);
                if (raid != null)
                {
                    float d = Days(raid.tick);
                    f.Tag("colony:raid", Fresh(d, 6f, 3f));
                    var parts = (raid.tag ?? "").Split(',');
                    if (parts.Contains("fallen")) f.Tag("colony:raid:fallen", Fresh(d, 6f, 3f));
                    foreach (var k in new[] { "mechs", "insects", "animals", "tribal", "people" })
                        if (parts.Contains(k))
                            f.Tag("colony:raid:" + k);
                    // Who came, as their people's memory of the colony has it.
                    var by = FactionMemory.Instance?.deeds.Where(x => x != null && x.kind == "raided" && x.faction != null && Days(x.tick) <= 7f).OrderByDescending(x => x.tick).FirstOrDefault();
                    if (by != null)
                    {
                        f.Tag("colony:raidby", Fresh(d, 6f, 3f));
                        f.Value("RAIDERS", by.faction.Name);
                        if (by.faction == p) f.Tag("colony:raid:them", 3f);
                        else if (p != null && by.faction.HostileTo(p)) f.Tag("colony:raid:theirfoe", 2.5f);
                    }
                }
                Note(f, tr.Latest("Rimfolk_Topic_Dead", 12f), "colony:death", "DEAD", 12f, 2.5f);
                var dead = tr.Latest("Rimfolk_Topic_Dead", 12f);
                if (dead != null && dead.tag == "battle") f.Tag("colony:death:battle");
                var born = tr.Recent("Rimfolk_Topic_Children", 12f).FirstOrDefault(n => n.tag == "birth");
                Note(f, born, "colony:birth", "BORN", 12f, 2.5f);
                var wed = tr.Recent("Rimfolk_Topic_Love", 12f).FirstOrDefault(n => n.tag == "wedding");
                Note(f, wed, "colony:wedding", "WEDDING", 12f, 2.5f);
                var decree = tr.Recent("Rimfolk_Topic_Council", 7f).FirstOrDefault(n => n.tag == "decree");
                Note(f, decree, "colony:decree", "DECREE", 7f, 1.5f);
                var harvest = tr.Recent("Rimfolk_Topic_Food", 7f).FirstOrDefault(n => n.tag == "harvest");
                if (harvest != null) f.Tag("colony:harvest", Fresh(Days(harvest.tick), 7f, 2f));
                Note(f, tr.Latest("Rimfolk_Topic_Tech", 7f), "colony:research", "RESEARCH", 7f, 1.2f);
                Note(f, tr.Latest("Rimfolk_Topic_Project", 15f), "colony:project", "PROJECT", 15f, 1.2f);
            }
            // The council of the colony where it's said.
            var offices = ColonyState.For(map)?.offices;
            var speaker = offices?.Speaker;
            if (speaker != null)
            {
                f.Value("SPEAKER", speaker.LabelShort);
                if (Now - offices.lastElectionTick <= GenDate.TicksPerDay * 10) f.Tag("colony:election", Fresh(Days(offices.lastElectionTick), 10f, 2f));
                if (speaker == c) f.Tag("us:speaker");
            }
        }

        /// <summary>
        /// The colony's hospital once it has a name (Health/HealthTracker.cs: well spoken of, or known across the region),
        /// as the colony calls it: "hospital", or "sickroom" before medicine has an industry behind it. And the doctor the
        /// colony trusts most, at home with the colonist (us:doctor when it's the colonist).
        /// </summary>
        private static void Hospital(FactSet f, Pawn c, TalkScene scene)
        {
            var health = HealthTracker.Instance;
            if (health == null || !HealthTracker.ReputationOn) return;
            int level = health.HospitalLevel;
            if (level >= 1)
            {
                // A standing fact rather than news: no heat of its own, a little for a name across the region.
                f.Tag("colony:hospital");
                if (level >= 2) f.Tag("colony:hospital:renowned", 0.5f);
                f.Value("HOSPITAL", HealthTracker.HospitalWord);
            }
            if (scene == null) return;
            Pawn doctor = null;
            int best = 0, saved = 0;
            foreach (var o in scene.colonists)
            {
                var rec = health.RecordOf(o);
                int l = rec?.Level ?? 0;
                if (l < 1 || l < best || (l == best && rec.Saved <= saved)) continue;
                doctor = o;
                best = l;
                saved = rec.Saved;
            }
            if (doctor == null) return;
            f.Value("DOCTOR", doctor.LabelShort);
            f.Tag(doctor == c ? "us:doctor" : "colony:doctor");
        }

        private static void Note(FactSet f, TalkNote n, string tag, string key, float span, float top)
        {
            if (n == null) return;
            f.Tag(tag, Fresh(Days(n.tick), span, top));
            if (!n.subject.NullOrEmpty()) f.Value(key, FightNames.Spoken(n.subject));
        }

        // ---------------- What their people remember ----------------

        private static void Deeds(FactSet f, Pawn c, Faction p)
        {
            var mem = FactionMemory.Instance;
            if (p == null || mem == null || !FactionMemory.On || !FactionMemory.Remembers(p)) return;
            int now = Now;
            var mine = mem.Of(p).Where(d => d != null && d.StrengthAt(now) >= 2f).OrderByDescending(d => d.StrengthAt(now)).ToList();
            FactionDeed good = null, bad = null, owed = null;
            foreach (var d in mine)
            {
                float h = Days(d.tick) <= 15f ? 2f : 1f;
                f.Tag("deed:" + d.kind, h);
                if (d.side == DeedSide.Favor && good == null) good = d;
                else if (d.side == DeedSide.Wrong && bad == null) bad = d;
                else if (d.side == DeedSide.TheirFavor && !d.settled && owed == null) owed = d;
            }
            if (good != null)
            {
                f.Tag("deed:good", 1f);
                // The one {DEEDNAME} and {DEEDBY} come from, by its kind: a piece naming them asks for deedbest:KIND.
                f.Tag("deedbest:" + good.kind, 1f);
                if (!good.name.NullOrEmpty()) f.Value("DEEDNAME", good.name);
                if (good.by != null && KinMemory.Ours(good.by)) f.Value("DEEDBY", good.by.LabelShort);
            }
            if (bad != null)
            {
                f.Tag("deed:bad", 1f);
                f.Tag("wrongbest:" + bad.kind, 1f);
                if (!bad.name.NullOrEmpty()) f.Value("WRONGNAME", bad.name);
            }
            if (owed != null) f.Tag("deed:owed", 1f);
            var friend = KinMemory.Friend(p, out var deed);
            if (friend != null)
            {
                f.Tag("their:friend", 1f);
                f.Value("FRIEND", friend.LabelShort);
            }
            var byYou = KinMemory.DeedBy(p, c);
            if (byYou != null)
            {
                f.Tag("byyou", 2f);
                f.Tag("byyou:" + KinMemory.KindTag(byYou), 2f);
                if (!byYou.name.NullOrEmpty()) f.Value("YOURDEEDNAME", byYou.name);
            }
        }
    }
}
