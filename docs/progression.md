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
| Wasp | Stinger ants | A venom sting |
| Fly | Winged ants | Flight |
| Beetle | Armoured ants | Thicker shell: higher armour and defence |
| Mantis (later) | Mantis ants | A hybrid ant-mantis; needs a newly built body |

- Mutations happen during play, and every mutation the queen has taken stays for the following levels.
- Each new unit adds its own branch to the evolve tree (below).

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
