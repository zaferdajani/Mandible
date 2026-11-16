namespace Mandible;

/// What the selection panel says about a creature: its numbers at its current level, and in a few words what it is
/// good at and what beats it. Text only; the rules live in Config and Sim.
public static class Matchups
{
    /// (strong against / good at, weak against).
    public static (string Strong, string Weak) Of(AntType t) => t switch
    {
        AntType.Worker => ("Gathering, digging, hauling", "Any fighter: keep it out of battle"),
        AntType.Soldier => ("Fast raids, swarming one target", "Majors, armoured shells, acid from range"),
        AntType.Major => ("Crushing soldiers and thick shells", "Slow: acid gunners and flyers pick at it"),
        AntType.Queen => ("Lays every ant; tough", "Alone she falls: the colony dies with her"),
        AntType.Flyer => ("Reaches wasps in the air, crosses any ground", "Thrown silk, trap webs, acid"),
        AntType.Gunner => ("Acid from range, burns armour and webs", "Fragile up close, cannot hit the air"),
        AntType.WebAnt => ("Pins flyers and holds big prey still", "A weak biter: needs fighters beside it"),
        AntType.Armoured => ("Shrugs off a third of every bite; holds a line", "Slow; acid and scorpion stings go through"),
        AntType.ScorpionAnt => ("Sting ignores armour and numbs", "Thin shell: swarms of soldiers"),
        AntType.Warlord => ("Cleaves crowds, 30% armour, War Cry", "Acid from range, being surrounded alone"),
        AntType.Matron => ("Splashing acid from range, venom pools", "Fast biters that reach her"),
        AntType.Marshal => ("Strikes from the air, rallies the army", "Thrown silk, trap webs"),
        AntType.Beetle => ("A hard shell and a heavy bite", "Many jaws at once; acid"),
        AntType.Spider => ("Throws silk that roots ants; strong fangs", "Majors, and attacks from several sides"),
        AntType.StagBeetle => ("Huge: tosses ants aside", "Slow to turn; patience and numbers"),
        AntType.Wasp => ("Stings from the air, out of ground reach", "Flying ants, thrown silk, the moment it lands"),
        AntType.Scorpion => ("Claws that hold, a tail that kills what faces it", "Silk on the tail; strike from behind"),
        AntType.Scorpling => ("Quick, with a sting", "Soldiers in numbers"),
        _ => ("", ""),
    };

    /// Two lines of numbers for one creature as it stands now (its level counted in).
    public static string Numbers(in Ant a)
    {
        var st = Config.Of(a.Type);
        float dmg = st.Damage * (a.DmgMul > 0 ? a.DmgMul : 1);
        float def = Config.DefenceAt(a.Type, a.Level) * 100;
        string reach = st.Reach > 3 ? "ranged" : "melee";
        return $"Attack {dmg:0.#} every {st.Cooldown:0.#}s ({reach})\nDefence {def:0}%  ·  Speed {st.Speed * (a.SpeedMul > 0 ? a.SpeedMul : 1):0.#}  ·  Level {a.Level}";
    }
}
