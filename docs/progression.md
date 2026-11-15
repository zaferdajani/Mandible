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

## 1c. Mutation units cost their own body part (owner, 2026-11-15)

- A core only unlocks the unit. **Each one hatched also needs a matching part taken from the related insect**, so
  the player cannot mass-produce them:

| Unit | Part needed per ant | Taken from |
|---|---|---|
| Winged ants | A pair of wings | Flies |
| Stinger ants | A sting | Wasps |
| Web ants | A silk gland | Spiders |
| Armoured ants | Shell plates | Beetles |

- Parts are a kind of organic matter with their own count per kind. They come from killing that insect; workers
  carry the part home with the remains. One part, one ant.
- The hatch button shows the part and how many are in store, and says what to hunt when there are none.
- Plain castes (workers, soldiers, majors) never need parts.

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
