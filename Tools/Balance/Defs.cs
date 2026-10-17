using System.Globalization;
using System.Xml.Linq;

namespace Balance
{
    /// <summary>A mood as its ThoughtDef gives it: its stages' moods, how long it lasts and how it stacks.</summary>
    public sealed class ThoughtInfo
    {
        public string name;
        public string file;
        public string thoughtClass;
        public string worker;
        public float[] stages = new float[0];
        public float durationDays;
        public int stackLimit = 1;
        public float stackMult = 0.75f;
        /// <summary>Gives something else instead of mood (Moods/RewardInstead.cs): no mood at all.</summary>
        public bool rewardInstead;
        /// <summary>The setback group (Council, Life, Friction), or null.</summary>
        public string setbackGroup;
        public bool notGentle;

        public bool Situational => worker != null;
        public float Stage(int i) => stages.Length == 0 ? 0f : stages[Math.Clamp(i, 0, stages.Length - 1)];
        public float First => Stage(0);
    }

    /// <summary>One effect of an answer: a mood for some of the colony, trust, or something that plays out on the map.</summary>
    public sealed class Effect
    {
        public string cls;
        public string target = "A";
        public string thought;
        public float amount;
        /// <summary>Share of the target group it reaches (trait filters, chance).</summary>
        public float share = 1f;
        public bool PlaysOut;
    }

    public sealed class Option
    {
        public string label;
        public List<Effect> effects = new List<Effect>();
        public float riskChance;
        public bool riskReplaces;
        public List<Effect> riskEffects = new List<Effect>();
    }

    /// <summary>
    /// Anything Rimfolk fires from a def with dilemma effects: a dilemma, an event, an encounter, a social event, a dream
    /// or a world event's beat. With more than one option it is a question for the council.
    /// </summary>
    public sealed class OutcomeDef
    {
        public string name;
        public string kind;
        public string file;
        public float weight = 1f;
        /// <summary>The share of the time its conditions hold (a tie, a memory, an illness, a camp...): see DefSet.Gate.</summary>
        public float gate = 1f;
        /// <summary>A dream about what really happened (a memory, a raid, an illness): four times as likely when it fits.</summary>
        public bool aboutEvents;
        /// <summary>Needs the two to share a bedroom (a friction over snoring): how often depends on how the colony sleeps.</summary>
        public bool sharesBedroom;
        public float cooldownDays;
        public int defaultOption;
        public List<Option> options = new List<Option>();
        public bool Choice => options.Count > 1;
        /// <summary>It came with an options list: an event that asks (a choice letter, even with one answer).</summary>
        public bool asks;
        // Dilemmas and events
        public bool stranger;
        public string strangerKind;
        public float withinDaysOfThreat;
        public List<string> seasons;
        public int minColonists;
        public float earliestDay;
        public bool battle;
        public string group;
        public string worker;
        public bool notable;
        public bool friction;
        public string dreamKind;
        public string initiatorThought;
        public string recipientThought;
        public string winnerThought;
        public string loserThought;
        public float tradition;
    }

    public sealed class WishInfo
    {
        public string name;
        public float weight = 1f;
        public float expiryDays = 10f;
        public float cooldownDays = 15f;
        public string grantedThought;
        public float inspirationChance;
        public bool luxury;
        public bool lasting;
        public bool triggered;
        public string worker;
        /// <summary>How often someone can ask for it (a prisoner, a lover, a lost limb, a camp...): Rates.WishGate.</summary>
        public float gate = 1f;
    }

    public sealed class AilmentCauseInfo
    {
        public string trigger;
        public float hours = 4f;
        public float chance = 0.03f;
    }

    public sealed class AilmentInfo
    {
        public string name;
        public bool serious;
        public bool quiet;
        public bool lingering;
        public float cooldownDays = 15f;
        public float startMin = 0.3f, startMax = 0.5f;
        public float initialSeverity = 0.4f;
        public float recoveryPerDay;
        public float bedFactor = 1f;
        public float tendPerDay;
        public bool immunizable;
        public float sevNotImmune, immunityPerDay, sevImmune;
        public float childFactor = 1f, oldFactor = 1f;
        public List<AilmentCauseInfo> causes = new List<AilmentCauseInfo>();
        public List<(string hediff, float chance, float sevMin, float sevMax)> after = new List<(string, float, float, float)>();
        public float[] moods = new float[0];
        public float[] stageMins = new float[0];

        public float MoodAt(float severity)
        {
            if (moods.Length == 0) return 0f;
            int s = 0;
            for (int i = 0; i < stageMins.Length && i < moods.Length; i++)
                if (severity >= stageMins[i]) s = i;
            return moods[Math.Min(s, moods.Length - 1)];
        }
    }

    public sealed class CatchingInfo
    {
        public string name;
        public string hediff;
        public float weight = 1f;
        public float victimsMin = 0.2f, victimsMax = 0.4f;
        public int maxVictims = 3;
        public float minRefireDays = 20f;
        public bool serious;
    }

    public sealed class SituationInfo
    {
        public string name;
        public float durMin = 15f, durMax = 25f;
        public int beats;
        public int choiceBeats;
        public int endings;
        public List<string> thoughts = new List<string>();
    }

    /// <summary>
    /// Reads Rimfolk's defs: XML with Name/ParentName inheritance, as the game does. Content for other mods (the Mods_
    /// files and anything that MayRequires a mod) is left out: the report is for a game with the base game and DLCs.
    /// </summary>
    public sealed class DefSet
    {
        public readonly Dictionary<string, ThoughtInfo> thoughts = new Dictionary<string, ThoughtInfo>();
        public readonly List<OutcomeDef> dilemmas = new List<OutcomeDef>();
        public readonly List<OutcomeDef> events = new List<OutcomeDef>();
        public readonly List<OutcomeDef> encounters = new List<OutcomeDef>();
        public readonly List<OutcomeDef> socialEvents = new List<OutcomeDef>();
        public readonly List<OutcomeDef> dreams = new List<OutcomeDef>();
        public readonly List<OutcomeDef> moments = new List<OutcomeDef>();
        public readonly List<OutcomeDef> nuisances = new List<OutcomeDef>();
        public readonly List<WishInfo> wishes = new List<WishInfo>();
        public readonly Dictionary<string, AilmentInfo> ailments = new Dictionary<string, AilmentInfo>();
        public readonly List<CatchingInfo> catching = new List<CatchingInfo>();
        public readonly List<SituationInfo> situations = new List<SituationInfo>();
        public int skippedForMods;

        private readonly Dictionary<string, Dictionary<string, XElement>> abstracts = new Dictionary<string, Dictionary<string, XElement>>();
        private readonly List<(string file, XElement el)> all = new List<(string, XElement)>();

        public static float F(string s, float fallback = 0f) =>
            s != null && float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        public static (float, float) Range(string s, float a, float b)
        {
            if (string.IsNullOrWhiteSpace(s)) return (a, b);
            var parts = s.Split('~');
            float lo = F(parts[0], a);
            return (lo, parts.Length > 1 ? F(parts[1], lo) : lo);
        }

        public DefSet(string root)
        {
            foreach (var path in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(path);
                }
                catch
                {
                    continue;
                }
                string file = Path.GetFileName(path);
                foreach (var el in doc.Root.Elements())
                {
                    string tag = el.Name.LocalName;
                    string name = (string)el.Attribute("Name");
                    if (name != null)
                    {
                        if (!abstracts.TryGetValue(tag, out var d)) abstracts[tag] = d = new Dictionary<string, XElement>();
                        d[name] = el;
                    }
                    if ((string)el.Attribute("Abstract") == "True") continue;
                    all.Add((file, el));
                }
            }
            foreach (var (file, el) in all)
            {
                if (ForMods(file, el))
                {
                    skippedForMods++;
                    continue;
                }
                string tag = el.Name.LocalName;
                try
                {
                    switch (tag)
                    {
                        case "ThoughtDef": Thought(file, el); break;
                        case "ColonyCouncil.DilemmaDef": dilemmas.Add(Outcome(file, el, "dilemma")); break;
                        case "ColonyCouncil.EncounterDef": encounters.Add(Outcome(file, el, "encounter")); break;
                        case "ColonyCouncil.SocialEventDef": socialEvents.Add(Outcome(file, el, "social")); break;
                        case "ColonyCouncil.DreamDef": dreams.Add(Outcome(file, el, "dream")); break;
                        case "ColonyCouncil.NuisanceDef": nuisances.Add(Outcome(file, el, "nuisance")); break;
                        case "IncidentDef": Incident(file, el); break;
                        case "InteractionDef": Interaction(file, el); break;
                        case "ColonyCouncil.WishDef": Wish(el); break;
                        case "HediffDef": Hediff(file, el); break;
                        case "ColonyCouncil.WorldSituationDef": Situation(el); break;
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("could not read " + tag + " " + Get(el, "defName") + " in " + file + ": " + e.Message);
                }
            }
            // Ailment moods: one situational thought per ailment (ThoughtWorker_Ailment), by the hediff's stage.
            foreach (var (file, el) in all)
            {
                if (el.Name.LocalName != "ThoughtDef") continue;
                string hediff = Get(el, "hediff");
                if (hediff == null || !ailments.TryGetValue(hediff, out var a)) continue;
                var st = Find(el, "stages");
                if (st != null) a.moods = st.Elements("li").Select(li => F((string)li.Element("baseMoodEffect"))).ToArray();
            }
        }

        /// <summary>Content for another mod: a Mods_ file, or an element that MayRequires anything but a DLC.</summary>
        private static bool ForMods(string file, XElement el)
        {
            if (file.Contains("_Mods_") || file.StartsWith("Mods_") || file.Contains("Mods_") || file.EndsWith("_Mods.xml")) return true;
            string req = (string)el.Attribute("MayRequire") ?? (string)el.Attribute("MayRequireAnyOf");
            return req != null && req.Split(',').Any(r => !r.Trim().StartsWith("Ludeon.", StringComparison.OrdinalIgnoreCase));
        }

        // ---------------- Inheritance ----------------

        /// <summary>A child element of the def, or of its parents.</summary>
        public XElement Find(XElement el, string child)
        {
            var seen = 0;
            while (el != null && seen++ < 20)
            {
                var c = el.Element(child);
                if (c != null) return c;
                string parent = (string)el.Attribute("ParentName");
                if (parent == null) return null;
                el = abstracts.TryGetValue(el.Name.LocalName, out var d) && d.TryGetValue(parent, out var p) ? p : null;
            }
            return null;
        }

        public string Get(XElement el, string child) => (string)Find(el, child);

        private static string Short(string cls) => cls == null ? null : cls.Substring(cls.LastIndexOf('.') + 1);

        // ---------------- Thoughts ----------------

        private void Thought(string file, XElement el)
        {
            string name = Get(el, "defName");
            if (name == null) return;
            var t = new ThoughtInfo
            {
                name = name,
                file = file,
                thoughtClass = Short(Get(el, "thoughtClass")),
                worker = Short(Get(el, "workerClass")),
                durationDays = F(Get(el, "durationDays")),
                stackLimit = (int)F(Get(el, "stackLimit"), 1),
                stackMult = F(Get(el, "stackedEffectMultiplier"), 0.75f)
            };
            var st = Find(el, "stages");
            if (st != null) t.stages = st.Elements("li").Select(li => F((string)li.Element("baseMoodEffect"))).ToArray();
            var ext = Find(el, "modExtensions");
            if (ext != null)
                foreach (var li in ext.Elements("li"))
                {
                    string cls = (string)li.Attribute("Class") ?? "";
                    if (cls.EndsWith("RewardInstead")) t.rewardInstead = true;
                    if (cls.EndsWith("SetbackExtension"))
                    {
                        t.setbackGroup = (string)li.Element("group") ?? "Life";
                        t.notGentle = (string)li.Element("notGentle") == "true";
                    }
                }
            thoughts[name] = t;
        }

        public ThoughtInfo T(string name) => name != null && thoughts.TryGetValue(name, out var t) ? t : null;

        // ---------------- Effects ----------------

        /// <summary>Who a trait filter reaches: each listed trait about one colonist in fourteen (Rates.TraitShare).</summary>
        private static float TraitShare(XElement e)
        {
            float share = 1f;
            int only = e.Element("onlyTrait")?.Elements("li").Count() ?? 0;
            int except = e.Element("exceptTrait")?.Elements("li").Count() ?? 0;
            if (only > 0) share *= Math.Min(0.6f, only * Rates.TraitShare);
            if (except > 0) share *= 1f - Math.Min(0.6f, except * Rates.TraitShare);
            if (e.Element("degreeTrait") != null) share *= Rates.DegreeTraitShare;
            share *= F((string)e.Element("chance"), 1f);
            return share;
        }

        private static readonly string[] PlayOutEffects = { "Gathering", "Feast", "Bonfire", "Toast", "Words", "Visit", "Lesson", "Teach", "Mentorship", "Contest" };

        private List<Effect> Effects(XElement list)
        {
            var result = new List<Effect>();
            if (list == null) return result;
            foreach (var li in list.Elements("li"))
            {
                if (ForMods("", li)) continue;
                string cls = Short((string)li.Attribute("Class")) ?? "";
                var e = new Effect
                {
                    cls = cls,
                    target = (string)li.Element("target") ?? "A",
                    thought = (string)li.Element("thought"),
                    amount = F((string)li.Element("amount")),
                    share = TraitShare(li)
                };
                e.PlaysOut = PlayOutEffects.Any(p => cls == "DilemmaEffect_" + p);
                result.Add(e);
            }
            return result;
        }

        private Option ReadOption(XElement li)
        {
            var o = new Option
            {
                label = (string)li.Element("label"),
                effects = Effects(li.Element("effects")),
                riskChance = F((string)li.Element("riskChance")),
                riskReplaces = (string)li.Element("riskReplaces") == "true",
                riskEffects = Effects(li.Element("riskEffects"))
            };
            return o;
        }

        /// <summary>The options (or the one set of effects) of a def, or of the extension that holds them.</summary>
        private void ReadOptions(OutcomeDef d, XElement holder)
        {
            if (holder == null) return;
            var opts = holder.Element("options");
            if (opts != null)
                foreach (var li in opts.Elements("li"))
                    if (!ForMods("", li)) d.options.Add(ReadOption(li));
            d.asks = d.options.Count > 0;
            var top = Effects(holder.Element("effects"));
            if (d.options.Count == 0) d.options.Add(new Option { effects = top });
            else if (top.Count > 0)
                foreach (var o in d.options) o.effects.InsertRange(0, top);
            d.defaultOption = (int)F((string)holder.Element("defaultOption"));
        }

        private OutcomeDef Outcome(string file, XElement el, string kind)
        {
            var d = new OutcomeDef
            {
                name = Get(el, "defName"),
                kind = kind,
                file = file,
                weight = F(Get(el, "weight"), 1f),
                cooldownDays = F(Get(el, "cooldownDays"), kind == "dilemma" ? 40f : kind == "encounter" ? 5f : kind == "nuisance" ? 4f : 0f),
                withinDaysOfThreat = F(Get(el, "withinDaysOfThreat")),
                minColonists = (int)F(Get(el, "minColonists"), kind == "dilemma" ? 3 : 1),
                notable = Get(el, "notable") == "true",
                dreamKind = Get(el, "kind"),
                tradition = F(Get(el, "traditionOption"), -1f),
                worker = Short(Get(el, "workerClass"))
            };
            var stranger = Find(el, "stranger");
            if (stranger != null)
            {
                d.stranger = true;
                d.strangerKind = (string)stranger.Element("kind");
            }
            var seasons = Find(el, "seasons");
            if (seasons != null) d.seasons = seasons.Elements("li").Select(x => x.Value).ToList();
            ReadOptions(d, el);
            d.gate = Gate(el);
            if (d.worker != null && Rates.WorkerGates.TryGetValue(d.worker, out float wg)) d.gate *= wg;
            // A custom of the land or the faith (DilemmaWorker_Custom's CustomConditions): only where the land fits, it
            // rains, or the colony's faith holds the right memes.
            var custom = el.Element("modExtensions")?.Elements("li").FirstOrDefault(x => ((string)x.Attribute("Class") ?? "").EndsWith("CustomConditions"));
            if (custom != null)
                foreach (var c in custom.Elements())
                    if (Rates.CustomGates.TryGetValue(c.Name.LocalName, out float cg)) d.gate *= cg;
            d.aboutEvents = el.Element("afterThoughts") != null || el.Element("afterThreat") != null || el.Element("hediffs") != null || el.Element("unconscious") != null;
            if (kind == "nuisance")
            {
                d.options.Clear();
                d.options.Add(new Option { effects = { new Effect { cls = "DilemmaEffect_Thought", target = "A", thought = Get(el, "thought") } } });
            }
            return d;
        }

        private void Incident(string file, XElement el)
        {
            var exts = Find(el, "modExtensions");
            if (exts == null) return;
            foreach (var li in exts.Elements("li"))
            {
                string cls = (string)li.Attribute("Class") ?? "";
                if (cls.EndsWith("HappeningExtension"))
                {
                    if (li.Element("mods") != null) return;
                    var d = new OutcomeDef
                    {
                        name = Get(el, "defName"),
                        kind = "event",
                        file = file,
                        weight = F(Get(el, "baseChance")),
                        cooldownDays = F(Get(el, "minRefireDays")),
                        earliestDay = F(Get(el, "earliestDay")),
                        minColonists = (int)F((string)li.Element("minColonists"), 1),
                        battle = (string)li.Element("battle") == "true",
                        group = (string)li.Element("group"),
                        worker = Short(Get(el, "workerClass"))
                    };
                    var seasons = li.Element("seasons");
                    if (seasons != null) d.seasons = seasons.Elements("li").Select(x => x.Value).ToList();
                    ReadOptions(d, li);
                    events.Add(d);
                    return;
                }
                if (cls.EndsWith("AilmentIncident"))
                {
                    var c = new CatchingInfo
                    {
                        name = Get(el, "defName"),
                        hediff = Get(el, "diseaseIncident"),
                        weight = F((string)li.Element("weight"), 1f),
                        maxVictims = (int)F(Get(el, "diseaseMaxVictims"), 3),
                        minRefireDays = F(Get(el, "minRefireDays"), 20f)
                    };
                    (c.victimsMin, c.victimsMax) = Range(Get(el, "diseaseVictimFractionRange"), 0.2f, 0.4f);
                    catching.Add(c);
                    return;
                }
            }
        }

        /// <summary>
        /// How often a def's conditions hold, roughly: each condition it names is a factor (a tie between two people
        /// 0.6, a memory of a death 0.15, a serious illness 0.02...). Rimfolk checks the real conditions in the game;
        /// the simulator only knows a def asks for something, so these are estimates (Rates.cs has the reasoning).
        /// </summary>
        public static float Gate(XElement el)
        {
            float g = 1f;
            foreach (var c in el.Elements())
            {
                string n = c.Name.LocalName;
                if (n == "tone")
                {
                    if (c.Value == "mend") g *= 0.2f;
                    continue;
                }
                if (n == "about" && c.Value != "None") g *= 0.5f;
                if (n == "shares" && c.Value == "work") g *= 0.5f;
                if (n == "maxOpinion" && F(c.Value, 100) <= 50) g *= 0.65f;
                if (n == "minOpinion" && F(c.Value, -100) >= 10) g *= 0.7f;
                if (n == "stranger")
                {
                    foreach (var s in c.Elements())
                        if (Rates.StrangerGates.TryGetValue(s.Name.LocalName, out float f)) g *= f;
                    continue;
                }
                if (Rates.Gates.TryGetValue(n, out float factor)) g *= factor;
                // A name in the colony (rep:cruel, rep:brave...) shows for a few colonists, not most.
                if ((n == "initiatorTags" || n == "recipientTags") && c.Value.Contains("rep:")) g *= Rates.RepTagShare;
            }
            return Math.Max(0.01f, g);
        }

        private void Interaction(string file, XElement el)
        {
            var exts = Find(el, "modExtensions");
            var li = exts?.Elements("li").FirstOrDefault(x => ((string)x.Attribute("Class") ?? "").EndsWith("MomentExtension"));
            if (li == null || li.Element("mods") != null) return;
            moments.Add(new OutcomeDef
            {
                gate = Gate(li) * GroupGate(exts),
                sharesBedroom = (string)li.Element("shares") == "bedroom",
                name = Get(el, "defName"),
                kind = "moment",
                file = file,
                weight = F((string)li.Element("weight"), 0.03f),
                cooldownDays = F((string)li.Element("cooldownDays"), 3f),
                friction = file.Contains("Friction") || (string)li.Element("friction") == "true",
                initiatorThought = Get(el, "initiatorThought"),
                recipientThought = Get(el, "recipientThought"),
                winnerThought = (string)li.Element("winnerThought"),
                loserThought = (string)li.Element("loserThought")
            });
        }

        /// <summary>A group moment comes only between the people its group conditions name (Rates.GroupGates); others: 1.</summary>
        private static float GroupGate(XElement exts)
        {
            var g = exts?.Elements("li").FirstOrDefault(x => ((string)x.Attribute("Class") ?? "").EndsWith("GroupMomentExtension"));
            if (g == null) return 1f;
            float f = Rates.GroupGates.TryGetValue((string)g.Element("between") ?? "sameGroup", out float v) ? v : 0.3f;
            if (g.Element("initiatorRole") != null) f *= Rates.GroupRoleGate;
            if (g.Element("recipientMoodBelow") != null) f *= 0.4f;
            return f;
        }

        private void Wish(XElement el)
        {
            wishes.Add(new WishInfo
            {
                name = Get(el, "defName"),
                weight = F(Get(el, "weight"), 1f),
                expiryDays = F(Get(el, "expiryDays"), 10f),
                cooldownDays = F(Get(el, "cooldownDays"), 15f),
                grantedThought = Get(el, "grantedThought"),
                inspirationChance = F(Get(el, "inspirationChance")),
                luxury = Get(el, "luxury") == "true",
                lasting = Get(el, "lasting") == "true",
                triggered = Find(el, "triggers") != null,
                worker = Short(Get(el, "workerClass")),
                gate = Rates.WishGate(Get(el, "defName"), Short(Get(el, "workerClass")))
            });
        }

        private void Hediff(string file, XElement el)
        {
            var comps = Find(el, "comps");
            if (comps == null) return;
            XElement Comp(string suffix) => comps.Elements("li").FirstOrDefault(c => ((string)c.Attribute("Class") ?? "").EndsWith(suffix));
            var main = Comp("HediffCompProperties_Ailment");
            // Ailments of other mods' lands (<biomes>) never come on a base-game map.
            if (main == null || main.Element("biomes") != null) return;
            var a = new AilmentInfo
            {
                name = Get(el, "defName"),
                serious = (string)main.Element("serious") == "true",
                quiet = (string)main.Element("quiet") == "true",
                lingering = (string)main.Element("lingering") == "true",
                cooldownDays = F((string)main.Element("cooldownDays"), 15f),
                childFactor = F((string)main.Element("childFactor"), 1f),
                oldFactor = F((string)main.Element("oldFactor"), 1f),
                initialSeverity = F(Get(el, "initialSeverity"), 0.4f)
            };
            (a.startMin, a.startMax) = Range((string)main.Element("startSeverity"), 0.3f, 0.5f);
            var causes = main.Element("causes");
            if (causes != null)
                foreach (var c in causes.Elements("li"))
                    a.causes.Add(new AilmentCauseInfo
                    {
                        trigger = (string)c.Element("trigger"),
                        hours = F((string)c.Element("hours"), 4f),
                        chance = F((string)c.Element("chance"), 0.03f)
                    });
            var after = main.Element("afterEffects");
            if (after != null)
                foreach (var c in after.Elements("li"))
                {
                    var (lo, hi) = Range((string)c.Element("severity"), 0.2f, 0.4f);
                    a.after.Add(((string)c.Element("hediff"), F((string)c.Element("chance"), 1f), lo, hi));
                }
            var rec = Comp("AilmentRecovery");
            if (rec != null)
            {
                a.recoveryPerDay = F((string)rec.Element("severityPerDay"), -0.1f);
                a.bedFactor = F((string)rec.Element("bedFactor"), 1f);
            }
            var med = Comp("AilmentMedicine");
            if (med != null) a.tendPerDay = F((string)med.Element("severityPerDayTended"), -0.1f);
            var imm = Comp("Immunizable");
            if (imm != null)
            {
                a.immunizable = true;
                a.sevNotImmune = F((string)imm.Element("severityPerDayNotImmune"));
                a.immunityPerDay = F((string)imm.Element("immunityPerDaySick"));
                a.sevImmune = F((string)imm.Element("severityPerDayImmune"));
            }
            var stages = Find(el, "stages");
            if (stages != null) a.stageMins = stages.Elements("li").Select(li => F((string)li.Element("minSeverity"))).ToArray();
            ailments[a.name] = a;
        }

        private void Situation(XElement el)
        {
            var s = new SituationInfo { name = Get(el, "defName") };
            (s.durMin, s.durMax) = Range(Get(el, "durationDays"), 15f, 25f);
            var beats = Find(el, "beats");
            if (beats != null)
                foreach (var b in beats.Elements("li"))
                {
                    s.beats++;
                    if ((b.Element("options")?.Elements("li").Count() ?? 0) > 1) s.choiceBeats++;
                }
            var endings = Find(el, "endings");
            s.endings = endings?.Elements("li").Count() ?? 1;
            foreach (var t in el.Descendants("thought")) s.thoughts.Add(t.Value);
            situations.Add(s);
        }
    }
}
