# Combat scale

The owner's rule (2026-11-09): every character has its own attack, defence and HP, and all three grow with its
level at that character's own rate. Source of truth: `Config.Stats` (level 1) and `Config.GrowthOf` (per level);
`Config.HpMul`, `DmgMul` and `DefenceAt` apply them, `Sim.Damage` caps defence at 75%. Attack is damage per second
(bite / cooldown); defence is the share of each blow the chitin shrugs off. Colony bonuses (Chitin, Forge, the queen's level as brood quality, the evolve tree's unit
branches, black-ant warriors) multiply on top; see [progression.md](progression.md). This table is generated from those numbers; regenerate it when they change.

| Character | Growth per level (HP / attack / defence) | Level 1 HP · atk/s · def | Max level HP · atk/s · def |
|---|---|---|---|
| Worker | +12% / +8% / +1.0 pts | 24 · 3.1 · 0% | L5: 36 · 4.1 · 4% |
| Soldier | +16% / +15% / +2.5 pts | 72 · 10.8 · 5% | L5: 118 · 17.3 · 15% |
| Major | +20% / +12% / +3.5 pts | 270 · 21.1 · 12% | L5: 486 · 31.2 · 26% |
| Queen | +15% / +10% / +3.0 pts | 3200 · 42.9 · 15% | L5: 5120 · 60.0 · 27% |
| Flying Ant | +12% / +18% / +1.5 pts | 60 · 12.7 · 3% | L5: 89 · 21.9 · 9% |
| Acid Gunner | +10% / +20% / +1.0 pts | 55 · 6.0 · 0% | L5: 77 · 10.8 · 4% |
| Web Ant (spider core) | +12% / +12% / +1.5 pts | 64 · 7.1 · 4% | L5: 95 · 10.6 · 10% |
| Armoured Ant (beetle core) | +20% / +10% / +4.0 pts | 190 · 10.6 · 32% | L5: 342 · 14.8 · 48% |
| Scorpion Ant (scorpion core) | +15% / +15% / +2.0 pts | 84 · 10.8 · 6% | L5: 134 · 17.2 · 14% |
| Ground Beetle | +40% / +22% / +3.0 pts | 290 · 16.7 · 25% | L5: 754 · 31.3 · 37% |
| Wolf Spider | +35% / +32% / +1.0 pts | 240 · 30.0 · 5% | L5: 576 · 68.4 · 9% |
| Stag Beetle | +40% / +25% / +3.0 pts | 800 · 36.0 · 30% | L5: 2080 · 72.0 · 42% |
| Hunting Wasp | +35% / +30% / +2.0 pts | 240 · 23.5 · 10% | L5: 576 · 51.7 · 18% |
| Scorpion (the Stone Mother is level 6) | +40% / +20% / +2.0 pts | 900 · 26.7 · 22% | L6: 2700 · 53.3 · 32% |
| Young Scorpion | +30% / +20% / +1.0 pts | 58 · 8.6 · 5% | L5: 128 · 15.4 · 9% |
| Warlord | +12% / +10% / +2.0 pts | 820 · 35.0 · 30% | L10: 1706 · 66.5 · 48% |
| Acid Matron | +10% / +14% / +1.0 pts | 620 · 20.0 · 10% | L10: 1178 · 45.2 · 19% |
| Sky Marshal | +10% / +13% / +1.5 pts | 620 · 31.4 · 18% | L10: 1178 · 68.2 · 32% |

Level 1's island queen is a wild queen at level 3 with 9% of her health left and 28% of her bite (`Expedition.QueenWounds`, `QueenBite`): a mutated queen's chitin and reach, beatable by Burr's squad at a cost.
