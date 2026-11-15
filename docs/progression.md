# Progression: mutation and evolution (owner's rules, 2026-11-15)

This is the one source of truth for how the colony grows. It replaces every earlier rule about mutagen amounts,
colony power from cores, the three-card mutation offer and generals ranking up from their own eating. If code or
another doc disagrees with this page, this page wins and the other is to be fixed.

There are two progressions. They use different things and give different rewards.

## 1. Mutation: a core gives the queen a new unit

- **Source:** a mutation core cut from a mutated enemy and carried to the queen. One core, one mutation. It is not
  a balance to save up: there is no "enough mutagen".
- **Effect:** the queen mutates and can lay a **new kind of unit**, a hybrid of ant and the creature the core came
  from. Mutation never gives plain stat boosts to the whole colony.
- **The units:**

| Core from | New unit | What it does |
|---|---|---|
| Spider | Web ants | Throw silk, spin trap webs, live in the Spinnery above ground |
| Fly | Winged ants | Flight |
| Wasp | Stinger ants | The flying ants grow a wasp's venom sting (owner, 2026-11-15: "the flying ants will have stingers then") |
| Scorpion | Scorpion ants (ground stingers) | The wasp's sting upgrades the flying ants only. Ground ants get a sting by defeating a scorpion and taking its stinger (owner, 2026-11-15). Rule: section 1d |
| Beetle | Armoured ants | Thicker shell: higher armour and defence |
| Mantis (later) | Mantis ants | A hybrid ant-mantis; needs a newly built body |

- **Art rule (owner, 2026-11-15):** every mutation unit is a new ant whose body combines the source insect's
  artwork with the ant's: a real merged body (the spider's spinnerets and legs on the web ant, the wasp's sting and
  banding on the stinger, the fly's wings and eyes on the winged ant, the beetle's shell plates on the armoured ant).
  Never a plain ant with a tint or an icon. Each needs an approved target (art-pipeline.md).
- Mutations happen during play, and every mutation the queen has taken stays for the following levels.
- Each new unit adds its own branch to the evolve tree (below).

## 1b. Where cores are found: the core is the level's strategy problem (owner, 2026-11-15)

- **Cores are found during play, in the level that needs them.** If a level can only be won with a unit the colony
  lacks (flying ants to cross ground no ant can walk, web ants to bring wasps down), that level holds the right core,
  and getting it is the heart of the level. The player is never stuck for want of a core that is not on the map.
- **Never easy.** A core is never handed over, lying unguarded, or won by sending everything at it. Reaching it,
  cutting it out and carrying it home must each ask for a plan.
- **It takes the whole colony.** Design every core so that the units the player already has must work together to
  win it, each doing what only it can do. Examples of the kind of problem wanted:
  - a guardian that a frontal attack cannot beat: lure it away, split its escort, or strike while it feeds;
  - ground that must be prepared first (a bridge built, webs cut, a gate held) while raids press the nest;
  - a slow, heavy carrier that must be escorted home through an ambush, so the army has to be divided between
    the escort and the nest;
  - a clock or a rival: the red colony wants the same core, or the body rots;
  - a counter that must be scouted: the guardian's weakness is found by watching it, not told outright.
- **Fair, not cheap.** Hard through decisions, not through hidden information or bigger numbers: the player can
  see the problem, has the tools, and loses only to a bad plan. Guidance names the goal, never the solution.
- Draw on real strategy-game craft (combined arms, tempo, scouting, economy against army, feints and flanks,
  holding a line while a strike force works) and on real ant behaviour.

## 1d. The scorpion's sting: ground stingers (owner approved 2026-11-15)

The owner's rule: the wasp's sting belongs to the flying ants; ground ants get theirs from a scorpion. The owner chose
each point marked (open) below as proposed: a new caste, the piercing numbing sting, chapter 11.

- **Who gets it (open).** Proposed: a new ground caste, the **scorpion ant**, grown on the soldier's body: a
  scorpion's segmented tail arched over its back, ending in the bulb and curved sting, and the soldier's jaws kept.
  One new unit from one core, like the web ant and the armoured ant. The alternative is that every soldier and major
  the queen lays from then on grows the tail (the way winged ants became stingers); that needs two merged bodies per
  colony and leaves no plain soldier.
- **What the sting does (open).** It must not be a second copy of the wasp's venom.

| | Stinger ants (wasp) | Scorpion ants (scorpion) |
|---|---|---|
| Where | in the air | on the ground; cannot reach anything airborne |
| The venom | burns: damage that keeps running after the strike | numbs: the victim walks and bites 40% slower for 4 s |
| Against a shell | armour takes its share | the sting goes between the plates: the strike ignores armour |
| Rhythm | every strike | bites like a soldier; the tail strikes once every 5 s |
| Best against | what heals (the Hornet Mother) and what flees | what is plated and heavy: beetles, majors, armoured ants, the Old Stag |
| Weak to | silk, gunners | the air, acid at range, a swarm of cheap workers (one sting, one victim) |

  Together they are the colony's two venoms: the ground sting holds a big enemy still and opens its shell, the
  winged sting burns it down.
- **The part (section 1c on main).** Each scorpion ant hatched costs one **scorpion's sting**, taken from a young
  scorpion. Young scorpions live under stones in the dry ground from the scorpion's chapter on. NOT BUILT: the
  parts rule is on main and this branch was cut before it; the young scorpions exist (`AntType.Scorpling`).
- **The tree.** A seventh branch, "Scorpion ants", with two nodes: *Deep Sting* (the numbness lasts 1 s longer per
  rank, 2 ranks) and *Nerve Venom* (a numbed enemy also takes 10% more from every bite per rank, 3 ranks). The
  backdrop now has eight tunnels (made from target P6's backdrop): the queen, the generals, five units, and one
  left empty for the mantis ants.
- **The colour tells the rank (owner, 2026-11-15: "stingers start black but upgrading them turned yellow and
  stronger").** A scorpion ant hatches with a black tail (target P8-B). Every rank bought in its branch turns the
  tail and sting further towards amber-yellow (target P8-A at the full five ranks), so an upgraded sting is seen
  on the field. (Read as the scorpion ants; the owner's sentence may have meant the winged stingers too.)
- **Where the core is found (open):** chapter 11, see level-design.md section 5.

## 2. Evolution: organic matter levels up the queen and the generals

- **Currency:** the colony's balance of organic matter (remains of other creatures). A high balance is what allows
  levelling up; it is spent through the **evolve tree**.
- **The queen levels up:** better brood. Her units hatch stronger (the quality of every unit she lays).
- **Generals level up:** their skills get stronger and new skills unlock.
- The organic balance decides how many skills and upgrades can be unlocked. The tree grows as mutations add new
  units and skills.
- Everything unlocked in the evolve tree stays for the following levels, the same as mutations.

## What this replaces (do not bring back)

- Cores with a strength of 1-3 that add colony-wide health and bite (`Colony.Power`).
- Mutagen as a counted reserve, and "one of three cards" after a queen kill.
- Mutation trees of stat or trait picks for the queen that are not new units.
- Generals gaining levels only from what each one eats itself; levels now come from the colony's organic balance
  through the evolve tree.
