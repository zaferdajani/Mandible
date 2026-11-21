# Mandible: how to earn money without spoiling the fun

Monetization report and council recommendation, 3 November 2026. Research and writing only; no game code was changed.

## Executive summary

**The owner's rule:** "never lose fun of playing. I need to have the same feeling of playing any strategy game." Every option below was judged first against that rule and only then on money.

**Recommendation (council vote 5-0 on the lead, with conditions):**

1. **Lead with a premium PC release on Steam, about $14.99, with a free demo** (chapters 1-2 plus one skirmish map, single-player). The whole game is sold once, with no ads and nothing sold inside a match. This is how indie RTS games earn today: Empires of the Undergrowth, an ant RTS, sold 450k+ copies at $29.99 on Steam. The free-to-play RTS attempts (Stormgate, Battle Aces) failed despite tens of millions of dollars. Steam also bans rewarded ads and ad-funded games outright (Feb 2025), so on PC the only question is the price.
2. **Phones come later, as a free download with one "Full Colony" unlock** (about $5.99; about $2.49 in Jordan and Egypt through store-local prices). Act I (chapters 1-2), two skirmish maps and LAN/IP duels stay free. The unlock is restorable, Family-Shared, one purchase forever. Mobile ships only after the Steam 1.0 proves the game (85%+ positive reviews) and the touch build runs well on a low-end Android phone.
3. **No ads, on any platform, at launch.** This was unanimous. The owner asked whether ads could earn subtly. The evidence says they cannot earn enough to justify their cost:
   - Middle East rewarded eCPM on Android is about $2.40. That works out to roughly $7 a day per 1,000 daily players, even if every player watches three ads a day.
   - Ads need a GDPR consent tool, Apple's tracking prompt and an age rating chosen for the ad network, and they break the "no ads" promise already written in `docs/moba-plan.md`.
   - Revisit only if mobile reaches tens of thousands of daily players outside MENA. Even then, the only acceptable form is one optional ad in a menu that never gives gameplay power.
4. **Later extras that leave play untouched:**
   - New campaigns or species as DLC (year two, only if the base game sells about 5k copies; they add content and never cut it from the 12 chapters, and never touch 1v1 or Lane War balance).
   - One $4.99 Supporter Pack (soundtrack, art book, nest-interior skins; never the colours or emblems the campaign awards for stars).
   - A passive tip link.
5. **Never:** paid power (including the consulting PDF's "+3 larvae" and "scout beetle" ad rewards), energy timers, daily-login or FOMO loops, a premium currency, loot boxes, locked heroes or castes, a campaign chopped into paid episodes, ads during play, or changing the deal after people have paid.

**The honest money picture.** One-time sales are not monthly income.
- **Likely case:** with no audience today (0 GitHub stars, 0-3 downloads per release asset), the year after a Steam launch is likely to bring **$2k-9k net**.
- **Good case:** a good launch (10-20k wishlists) brings **$25k-50k**.
- **Breakout:** a breakout brings **$85k-140k**.
- **Mobile:** adds little in any case: about $0.4k in the likely case and about $20k in a breakout.
- **Base rate:** two thirds of 2025 Steam releases grossed under $1,000.

What decides the outcome is the first impression and the wishlists, not the pricing model.

**What must be true before charging anything (the gates):**
- **Licence:** choose one, and confirm the rights to the AI art.
- **Steam page:** reserve it now ($100), but keep it unpublished.
- **Art:** the art pipeline and ant rebuild are finished.
- **Stranger playtests:** 30+ outside players, with 60%+ finishing chapter 2 unprompted. The first two hours are the Steam refund window.
- **Builds:** signed and notarised.
- **Trailer and store page:** a 60-90 second gameplay trailer and an honest AI-content disclosure.
- **Wishlists:** about 7,000 before a release date is set.

If fewer than 2,000 wishlists arrive within six months of the page going public, the problem is the game or its pitch, not the business model.

**The consulting PDF** gets the ethics right: a one-time unlock, no gacha, regional prices, and its own dissenting Devil's Advocate, who is the voice this council agrees with. It gets the business wrong:
- It never considers Steam.
- Its revenue model counts the same one-time buyers again every month, which inflates its totals several times over.
- Some of its numbers are wrong or unverifiable.
- Its ad rewards sell gameplay power.

See section 7.

---

## Contents

1. [Where Mandible stands today](#1-where-mandible-stands-today)
2. [Market evidence](#2-market-evidence)
3. [Scenario table](#3-scenario-table)
4. [Recommendation and rollout](#4-recommendation-and-rollout)
5. [What to never do](#5-what-to-never-do)
6. [The council debate](#6-the-council-debate)
7. [Critique of the consulting PDF](#7-critique-of-the-consulting-pdf)
8. [Risks](#8-risks)
9. [Decisions for the owner](#9-decisions-for-the-owner)
10. [Method and caveats](#10-method-and-caveats)
11. [Sources](#11-sources)

---

## 1. Where Mandible stands today

Verified from the repository on 3 November 2026.

| Fact | Why it matters for money |
|---|---|
| Original ant-war RTS: Colony War (skirmish vs AI), Lane War (a hero mode), a 12-chapter campaign with one persistent colony, named veterans, stars that unlock cosmetics, heroes and early castes; two races; 1v1 lockstep multiplayer over LAN or direct IP | A full, campaign-led single-player game, which is exactly the shape that sells as a premium product |
| Free on GitHub Releases for Windows, macOS, Android and iOS; v0.11.0 released today | Today's builds are unsigned (macOS not notarised, iOS sideloaded), so they cannot be sold as they are |
| 0 stars, 0 forks; release assets downloaded 0-3 times each | **There is no audience yet.** Every revenue figure is a projection onto players who do not exist yet |
| No `LICENSE` file | The code is "all rights reserved" by default: viewable but not reusable. That is good for selling, but the owner should pick a deliberate licence (section 9) |
| Story panels and the chamber soil texture are AI-generated; HUD portraits come from the owner's own reference image; the README calls the art "not an exact visual match" | Steam requires an AI-content disclosure. Strategy players judge on screenshots and refund within two hours. The art pass in progress is a gate, not a nice-to-have |
| The campaign already **awards** colony colours and emblems for stars (`docs/level-design.md`) | Selling the same category would cheapen what players earned. Sell only things stars never give |
| `docs/moba-plan.md` proposes a public promise: "no hero rotation, no locked heroes, no Talents, no ads, no gameplay power for money" | Any ad plan breaks a promise the project has already written down |
| `docs/level-design.md`: "There are no timers, energy or paywalls anywhere in the level path" | The rule is already in the design. Monetization must sit outside the level path |
| 0.7.0 to 0.11.0 shipped in three days | Speed is the project's strength and its risk: wide scope, thin polish |

## 2. Market evidence

All figures carry their source in section 11. Tracker estimates (Raijin, Gamalytic, AppGoblin) are good to about ±30-50%. Vendor ad data comes from companies that sell ads.

### 2.1 PC: premium strategy works; free-to-play RTS does not

- **Steam's terms.**
  - Valve takes 30% of revenue up to $10M.
  - Each app costs $100, paid back after $1,000 of revenue.
  - A refund is automatic within 14 days if the player has under 2 hours of play.
  - The median refund rate is 8% for full launches and 12.4% for Early Access.
- **Ads are banned on Steam.** The rules, made explicit in February 2025, forbid rewarded ads and ad-based business models. Paid DLC is allowed.
- **Demos and Next Fest.**
  - A demo is a separate free app. Wishlisters are notified when it launches.
  - **Demo players cannot play multiplayer against owners of the full game**, so the demo should be single-player.
  - A game may enter **only one** Next Fest.
  - A "prologue" or "Chapter One" cannot register for Next Fest; the base game registers with a demo.
- **Wishlists to sales.** First-week sales are about 0.15x wishlists at the median, and 0.10x for games over $10. About 7,000 wishlists is the commonly cited threshold for Steam's Popular Upcoming list.
- **The base rate is brutal.**
  - About 20,000 games launched on Steam in 2025.
  - An estimated 65.9% grossed under $1,000, and only about 8% passed $100k.
  - Nearly half got fewer than 10 reviews.
  - Underperformers had a median first-week user score of 67%, against 91% for games that beat expectations.

**Comparable games** (tracker estimates unless stated):

| Game | Model, price | Reviews | Est. copies / gross | Lesson |
|---|---|---|---|---|
| Empires of the Undergrowth (ant RTS) | Premium $29.99; Early Access 2017-2024; Hooded Horse publisher; paid DLC | 19.1k, 94% | 450k+ (publisher) / ~$7.7M | The closest comparable: an ant RTS with a campaign sells premium, given years of polish and a strategy publisher |
| Northgard | Premium about $30; six paid clan DLCs; free campaign expansion | 68.9k, 85% | ~1.33M Steam, 3M+ all platforms | Faction DLC on top of a complete game works |
| They Are Billions | Premium $29.99 | 51.6k, 85% | ~1.06M / ~$19M | Single-player-first RTS sells |
| Kingdom Two Crowns | Premium $19.99; campaign DLC $6.99-9.99 | 41.4k, 90% | ~796k | Campaign-style DLC works |
| Mindustry | **Open source (GPLv3); free or pay-what-you-want on itch; $9.99 on Steam** | 28.5k, 96% | ~570k / ~$4.6M | Players pay on Steam even when the game is free elsewhere |
| Bad North | Premium $14.99 | 14.5k, 93% | ~304k | Small, polished RTS at $14.99 |
| Rusted Warfare (solo developer) | Premium $4.99 Steam, $1.99-2.99 mobile; free demo app | Overwhelmingly positive | ~161-244k Steam | One person, no microtransactions, sold on both PC and phones |
| Tooth and Tail | Premium $19.99, competitive-leaning | 4.2k, 80% | ~70k | Competitive-first RTS sells weakly |
| Empire of the Ants (2024) | Premium $39.99, licensed | 1.2k, 76% | ~25k | Price and polish must match; ants alone do not sell |
| Stormgate | Free-to-play; paid campaign chapters and heroes; ~$40M raised | ~50% | n/a | Charging for chopped-up campaign content before earning goodwill backfired |
| Battle Aces | Free-to-play-style RTS | n/a | cancelled May 2025 | Even well-funded competitive RTS struggles to keep players |

**Early Access:**
- Of 225 games that left Early Access in 2025, only about 20% earned more in their 1.0 launch month than in their EA launch month.
- The median 1.0 launch month made about 40% of the EA launch month.
- The EA launch *is* the big launch, so it must not be spent on an unfinished-looking build.

**Open source and selling.** A public, open-source game can still sell on Steam: Mindustry and Cataclysm: DDA both do. But under an open licence a third party can legally list the game on Steam, which is what happened to 0 A.D. in 2022. Reserve the Steam page and the name first.

**Patreon and crowdfunding.**
- Small game Patreons usually earn tens of dollars a month.
- Video-game Kickstarters raised about $26M a year in 2024-2025 across a few hundred successful campaigns. Those campaigns had demos and existing audiences.
- In the ant-game niche, Ant Simulator's 2016 collapse is still remembered.

**Consoles.** Godot has no official console support. W4 Games costs from $800 a year per platform, but its C# support is beta and it lists Godot up to 4.6, while Mandible is on C# and Godot 4.7. Consoles are a year-three question at the earliest.

### 2.2 Phones: a one-time unlock is the fair shape; ads earn little

- **Fees.**
  - Apple's Small Business Program charges 15% under $1M (you must enroll).
  - Google charged 15% on the first $1M a year. From 30 June 2026 its US, UK and EEA terms changed to a 20%/15% structure, plus 5% if you use Play billing. Check the current Play Console terms.
  - In the US, both stores now allow external purchase links.
  - Apple charges $99 a year; Google charges $25 once.
  - New personal Google Play accounts must run a 12-tester, 14-day closed test before production.
- **Store rules fit a one-time unlock.**
  - A non-consumable unlock needs a Restore button.
  - Family Sharing can be enabled, but never disabled afterwards.
  - Rewarded ads are allowed only if each one is opted into, the reward is disclosed beforehand, and the reward has no cash value.
- **Premium mobile is small, but it lives.**
  - F2P accounted for 96% of mobile downloads in 2025, but premium releases rose 77% to about 750.
  - Kingdom Rush's studio calls premium "less viable each year".
  - Polytopia (free base game, one-time tribe purchases, firmly against pay-to-win) reached 25M downloads, mostly organic, over years. No revenue figure is published.
- **Trial-to-unlock conversion.** No credible published rate from 2024-2026 exists. Plan on 1.5-5% of installs (a rule of thumb) and measure.
- **Piracy.**
  - Monument Valley's famous "95%" was unpaid Android installs, not measured piracy. The game still made $5.8M.
  - A free download with a store-verified unlock loses little: pirates of an unlock mostly stand in for people who would not have paid.
- **Rewarded ads, the numbers** (vendor data):
  - US rewarded eCPM is about $13-30.
  - Middle East: about $8.40 on iOS and **about $2.40 on Android** (Q4 2024).
  - No country figures exist for Jordan or Egypt; expect the bottom of the range.
  - Ads-only ARPDAU is typically $0.01-0.05.
  - Three menu views a day at $2.40 gives about **$0.007 per daily player**: $7 a day per 1,000 players.
  - Ads need a certified consent tool for the EEA and UK, Apple's tracking prompt (about 38% of gamers opt in, so most iOS impressions are non-personalised and cheaper), and an age rating that keeps the game out of Families and Kids ad rules.
- **The Godot reality.**
  - C# on Android and iOS is still marked experimental in Godot 4.7.
  - The AdMob and billing plugins are community-maintained and GDScript or Java-first, so they need wrappers to call from C#.
  - Budget real engineering time for the store plumbing, and more for ads.
- **MENA.**
  - Per-player spend is UAE $84.6, Saudi $54.89 and Egypt $3.39; those averages are driven by F2P whales, not buyers of single unlocks.
  - About 35-50% of MENA purchases use carrier billing or wallets, and only about 19% of people own a credit card.
  - Store IAP is the route that reaches carrier billing (live in Saudi Arabia and Egypt; check Jordan).
  - Saudi players show the region's strongest preference for premium games (26%).

### 2.3 What players accept and what they hate

- **Accepted:**
  - Opt-in rewarded ads beat forced ads about 4 to 1 (Tapjoy). Only 5% preferred interstitial videos (Unity).
  - About 69% call cosmetic-only purchases "okay".
- **Hated:**
  - 68% see pay-to-win as a negative (NPD).
  - Energy timers drew a UK advertising ruling against EA's Dungeon Keeper.
- **Generations.** No rigorous survey splits ad or F2P attitudes by generation. The claim that "Gen Z accepts F2P and Gen X hates it" is folklore. What is measured: younger players spend more in-game on mobile (38% vs 26% for Gen X), while Gen X spends more on content packs.
- **Goodwill lost:**
  - Battlefront II: EA's reply became Reddit's most downvoted comment.
  - Diablo Immortal: user score 0.2-0.3.
  - Stormgate: campaign sold piecemeal.
  - Warcraft III: Reforged: quality and the changed terms.
  - C&C Rivals: a mobile cash-in.
  - Age of Empires Mobile: pay-to-win.
  - Age of Empires II's Three Kingdoms DLC: fell below 50% positive because new paid content touched ranked play.
  - Helldivers 2: changing the deal after purchase.
- **Goodwill kept:**
  - Polytopia: one-time tribes.
  - Path of Exile: cosmetic-only.
  - Deep Rock Galactic: premium plus supporter packs.
  - Factorio: never discounted.
  - Northgard: clan DLC.
  - Vampire Survivors mobile: optional ads only, "free-for-real".
  - Shattered Pixel Dungeon: cosmetic donations.
  - Rusted Warfare: solo premium RTS.
- **The pattern.** Goodwill survives one-time unlocks, cosmetics, supporter packs and optional ads. It breaks when money buys power, content is chopped up, friction is imposed, or the deal changes after purchase.
- **Regulation.**
  - Belgium bans paid loot boxes, and Brazil bans them in games likely to reach minors (2026).
  - The FTC fined Epic $245M over dark patterns.
  - The EU's 2025 principles require real-money pricing for in-game currencies.
  - A direct one-time purchase with no currency sidesteps nearly all of it.

## 3. Scenario table

**Scores** are the council's median, 1-5. For every column, 5 is best: no fun impact, high revenue, low effort, good fit, safe reputation.

**Revenue** is net to the developer in the 12 months after that channel launches, in three outcome cases. Shared assumptions:
- **Steam:** $14.99 list. **About $5.70-9.00 net per copy** after regional prices, VAT, launch and seasonal discounts, 8% refunds and Valve's 30%. Year-one units are about 3x week one; week one is 0.10x wishlists.
- **Mobile:** about $2.70-2.90 net per unlock after regional mix, VAT and a 15% store fee. Installs are 2-5k (likely), 20-40k (good) or 100-150k (breakout), converting at 1.5-3%, 3-4% or 4-5%.
- **Ads:** 6-15 active days per install, 0.3-0.8 menu views per active day, $3-9 blended eCPM.

The model is reproducible from these numbers. None of it recurs monthly.

| # | Scenario | Fun (5 = none) | Revenue | Effort (5 = easy) | Fit | Reputation (5 = safe) | Net, 12 months: likely / good / breakout |
|---|---|---|---|---|---|---|---|
| S1 | **Premium Steam, $14.99, free demo** | 5 | 4 | 3 | 5 PC | 5 | $2k-9k / $25k-50k / $85k-140k |
| S2 | **Free mobile + one-time Full Colony unlock** | 5 | 3 | 2 | 5 phones | 4 | $0.1k-0.4k / $2k-4k / $11k-22k |
| S3 | Cosmetic-only supporter items (one pack, never star-reward categories) | 4 (selling star-reward categories would drop it to 2) | 1 | 4 | 3 | 3 | <$0.1k / $0.5k-1k / $3k-4k |
| S4 | Optional rewarded ads, menu only, never forced (mobile only; banned on Steam) | 3 (2 with the PDF's gameplay rewards) | 1 | 2 | 1 PC / 3 phones | 2 | <$30 / $0.3k-0.9k / up to $15k, mostly outside MENA |
| S5 | Paid expansions: new campaigns or species as DLC (year two) | 5 if additive | 3 | 2 | 5 | 4 (1 if carved from the 12 chapters) | <$1k / $3k-5k / $15k-27k |
| S6 | Patreon / sponsors / tip jar | 5 | 1 | 4 | 3 | 4 | $0.1k-0.3k / $0.5k-1.5k / $3k-6k |
| S7 | Early Access on Steam | 4 | 3 | 3 | 4 PC | 2 | Like S1, but front-loaded: the 1.0 launch typically earns ~40% of the EA launch month |
| C1 | **Council plan: S1, then S5 + one S3 pack, S2 behind gates, S6 as a passive link; no S4, no S7** | 5 | 4 | 3 | 5 / 5 | 5 | Year one ≈ S1 + S6: **$2k-10k / $27k-55k / $90k-150k**; S2 and S5 add to later years |
| C2 | The consulting PDF's plan: mobile-first, free episodes + unlock, rewarded ads, Almanac, chapter packs, cosmetics | 3 | 2 | 1 | 2 PC / 4 phones | 3 | The PDF claims $1.3k-44k *a month*. Corrected, it is roughly S2 + S4 + S3: $0.2k-0.6k / $3k-6k / $25k-40k a year |
| C3 | The Skeptic's plan: nothing paid until strangers prove the fun, then S1 only, one store in year one | 5 | 3 | 4 | 5 PC | 5 | Same as S1, a few months later, with better odds of the good case |

Reading the table:
- Only S1 can carry the business, and only if the launch is good.
- S2 is worth having for reach and fairness, not for money.
- S4 is the worst trade on every axis except fun-if-done-perfectly.
- C1 and C3 differ only in timing. The council adopted C1 with C3's gates.

## 4. Recommendation and rollout

### 4.1 The model

**PC (Steam first; GOG and itch later):**
- **Price:**
  - **$14.99**, with Valve's suggested regional prices (check Jordan, Egypt and Turkey by hand).
  - A 10% launch discount, announced up front.
  - No discount deeper than 25% in the first six months; normal seasonal sales after that.
- **What the price buys:** the whole game: all 12 chapters, Colony War, Lane War, and LAN/IP duels.
- **The free demo:**
  - Chapters 1-2 (Act I, whose finale is chapter 2) plus one skirmish map against the AI.
  - Single-player, because Steam demos cannot play the full game online.
  - Saves carry over into the full game.
  - It is a demo, not a prologue, so it can enter Next Fest.
- **The pledge**, on the store page and in the menu, word for word: **"Buy once. No ads. No pay-to-win. No locked heroes. Ever."**

**Phones (only after the Steam gates in 4.2):**
- **The download is free.** Act I (chapters 1-2), two skirmish maps and **LAN/IP duels are free for everyone**, so a buyer's friend can always play with them.
- **One non-consumable "Full Colony" unlock** for everything else. That includes Colony War and Lane War against the AI, apart from the two free skirmish maps.
- **Price:** **$5.99** in Tier 1 markets and the Gulf; store-local prices of about **$2.49 (JOD 1.99 / E£ about 120-150)** in Jordan and Egypt.
- **Restore and Family Sharing:** a Restore button in Settings, and Family Sharing on (a deliberate, permanent choice).
- **No ad SDK.** Unlock buyers and free players see the same menus; there is simply nothing to sell inside them.

**Later, both platforms:**
- **One Supporter Pack, $4.99** (about $2.99 on mobile):
  - Soundtrack, digital art book, nest-interior skins and alternate carapace sets.
  - Never colony colours or emblems, which the campaign awards for stars.
  - One bundle with a fixed price and a clear list; no rotating shop.
- **Expansion DLC in year two:**
  - A new species campaign (for example a new rival or a new playable colony), $6.99-9.99 on PC and $2.99-4.99 on mobile.
  - It adds content and never removes or locks anything from the 12 chapters.
  - New playable species stay **out of 1v1 and Lane War** unless they are given free to everyone there.
- **A tip link** in the GitHub README and on itch. Never a Patreon with obligations.

**Ads:**
- None. This applies at launch and stays so until the trigger in section 9, decision 3.
- If that trigger is ever met, the only acceptable form: one opt-in ad, in a menu, never after a defeat, rewarding only something cosmetic or a "thank you" credit. Never larvae, units, food, XP or a daily map.

### 4.2 Rollout order and gates

Each step starts only when the gate before it is met. Dates are deliberately not given: the gates are the schedule.

| Step | What happens | Gate to move on |
|---|---|---|
| 0. Now (cost: $100, a few hours) | Choose and add a licence; confirm commercial rights to the AI texture, comic panels and the portrait reference; reserve the Steam app and the name "Mandible" (unpublished); trademark search | Licence committed; Steam app ID exists |
| 1. Freeze and polish | Feature freeze on Lane War and the MOBA plan. The art pipeline and ant rebuild finish. Polish the first two hours. Build a signed Windows build and a notarised macOS build | The art sessions sign off; nothing on screen in chapters 1-2 is placeholder |
| 2. Prove the fun | Closed playtest of 30+ strangers (Steam Playtest or private keys, not a public build); fix what they hit | **60%+ finish chapter 2 unprompted; most come back for a second session** |
| 3. Go public | Steam page live: capsule art from real gameplay, a 60-90 second trailer opening on swarms, the dug pit and a queen kill; honest AI disclosure, explained in a devlog; Discord; the pledge | Wishlists build. **Under 2,000 after six months means rethink the game or pitch, not the price** |
| 4. Demo and outreach | Demo live; reach the ant-RTS and RTS creators who cover Empires of the Undergrowth; pitch strategy publishers (for example Hooded Horse) **with the demo's data**, and write the pledge into any contract | About 7,000 wishlists before booking Next Fest and a date |
| 5. Next Fest, then 1.0 on Steam | One Next Fest, then launch 2-4 months later | First-week user score ≥85% |
| 6. Supporter Pack | A few weeks after launch | Launch reviews ≥85% |
| 7. Mobile | Free + Full Colony unlock; soft launch in one Tier 1 market and the Gulf, not Jordan alone (its low spend would make the data noise) | Steam 1.0 at ≥85% positive with ~100+ reviews; ≥6 months after launch; the touch HUD at 30 fps on a low-end Android; the C# billing path verified on a real device. Keep the unlock if ≥2% of installs buy it |
| 8. Expansion DLC | Year two | Base game ≥5,000 copies |

**Why Steam first.** Steam's wishlist, demo and Next Fest system is the only free discovery channel this project has. Steam reviews and the trailer then become the social proof for the mobile launch. Mobile-first would spend the store plumbing and the first impression on a channel where 96% of downloads go to free games, and where nobody is looking.

### 4.3 What the game needs before charging

1. **A licence and clean rights.**
   - The code licence is a choice; see section 9.
   - Assets stay all rights reserved.
   - Confirm commercial rights to every AI-generated image and to the reference image the HUD portraits come from.
2. **The art pipeline finished**, and the ant models rebuilt. The README's own words, "not an exact visual match", must no longer be true of anything in the trailer or the first two hours. Prefer replacing the AI comic panels in Act I with art from the approved pipeline. Disclosure is required, but it does not fix how they look.
3. **A first two hours that hook strangers.** This is the refund window and where the review score is decided.
4. **Signed builds.** A signed Windows build and a notarised macOS build. On mobile, store-signed builds; never sell an unsigned IPA.
5. **A store page and trailer** built from real gameplay, with the pledge and the AI disclosure.
6. **Scope discipline.** Colony War and the campaign are the headline. Lane War stays in the box, but it is not the pitch, and no more MOBA features until 1.0.
7. **Simple entitlement code**, the only code monetization needs on PC:
   - On PC there is none: Steam sells the app, the demo is a separate app, and DLC is a Steam DLC check.
   - On mobile, one `IStore` with a single non-consumable and Restore, kept entirely outside `src/Sim` so lockstep determinism is never touched.

## 5. What to never do

These are the hard lines. The council voted on all of them unanimously.

1. **Never sell power,** in any mode: no larvae, units, food, XP, heroes, castes, boosts or "scout beetles", for money or for ads. Skirmish included.
2. **Never put an ad in play:** no interstitials, banners, "watch to continue" or "watch to retry", and no ads after a defeat.
3. **No energy, stamina, waiting timers or daily-login streaks,** and no time-limited content or FOMO maps. The colony waits for the player, never the reverse.
4. **No premium currency, gacha, loot boxes or random paid items.** Every price is shown in real money.
5. **Never chop the 12-chapter story into paid episodes** (the Stormgate mistake). DLC adds; it never subtracts.
6. **No paid content that changes 1v1 or Lane War balance** (the Three Kingdoms DLC mistake).
7. **Never sell what the stars award:** colony colours, emblems, the heroes, the castes or the veteran perk.
8. **Never change the deal after purchase:** no new account requirement, no removal of free duels, no moving content behind a new paywall.
9. **No crowdfunding or pre-orders before a playable demo.** The ant niche remembers Ant Simulator.
10. **Never charge for an unsigned build,** hide the AI disclosure, or claim "studio-grade" on a store page before it is true.
11. **Never present one-time sales as monthly income** in any plan or pitch.

## 6. The council debate

**Method.** Five independent reviewer agents, each with a distinct role, received the same evidence pack. The pack held the game facts, the consulting PDF and about 8,000 words of cited research. Each wrote a position without seeing the others. Then each read all five positions and wrote a critique, concessions and a final vote. This section is the synthesis.

### 6.1 The seats

| Seat | Opening position in one line |
|---|---|
| **Player Advocate** (guards the fun) | Premium Steam at $14.99 with a real demo; mobile free + one unlock; no ads anywhere; "players forgive a price tag but never a first hour that feels like a prototype" |
| **Monetization Economist** | Premium Steam; about $5.70 net per $14.99 copy after everything; likely year one $2k-9k; nothing here recurs; ads earn $300-1,000 a year |
| **Mobile Platform Specialist** | Mobile is free + a non-consumable unlock (the only shape that survives Android piracy and reaches carrier billing), with free duels; no ad SDK; but earn the audience on Steam first |
| **Community and Marketing Lead** | Reviews matter more than price; one Next Fest, one first week; the pledge is marketing; pitch a strategy publisher; no public art before the art pass |
| **Skeptic** | Mandible has no audience and no stranger has played it; prices are premature; one store in year one; numeric gates; cut scope to Colony War and the campaign |

### 6.2 Where they agreed from the start

- Steam premium with a demo leads.
- The PDF's revenue model is wrong.
- Rewarded ads are not worth it.
- Never sell star-reward categories.
- Never touch multiplayer balance with paid content.
- No Early Access.

The Skeptic opened the debate round by warning that this convergence could be groupthink, since all five read the same evidence pack. That is a fair caution. It is why section 3 shows the outcome cases with numbers rather than an adjective.

### 6.3 Where they disagreed, and how it was settled

| Question | Positions | Resolution in this report |
|---|---|---|
| **Discounts** | Player Advocate: never below launch price for six months (Factorio's respect). Economist and Marketing: seasonal sales are a major free source of visibility and sales for an unknown game; Factorio could refuse because it already had demand | A 10% launch discount announced up front, nothing deeper than 25% for six months, then normal sales |
| **When mobile** | Opening positions said "3 months after PC". Skeptic: only after 500+ reviews at 85%. Economist and Marketing: 500 reviews is about 15-30k copies, the breakout case, so in practice "never" | Gate on ≥85% with ~100+ reviews, ≥6 months, plus a low-end-device test and a working C# billing path |
| **Gulf price** | Mobile Specialist: Tier 1 price ($6.99), since UAE and Saudi spend more per player than most Tier 1 markets. Economist: that spend comes from F2P whales, not buyers of single unlocks | One price for Tier 1 and the Gulf at $5.99; the Saudi premium-preference data (26%) supports not discounting the Gulf |
| **How much free on mobile** | Economist: all of Act I, chapters 1-3. Mobile Specialist: cut at a cliffhanger, not later | Act I is chapters 1-2 in `docs/level-design.md` (finale at 2), so the free part ends on Act I's finale |
| **itch pay-what-you-want now** | Skeptic: a cheap test of whether anyone pays. Everyone else: it spends the first impression on prototype art and anchors the price near zero | Skeptic conceded. Closed Steam Playtest instead; tip link only |
| **Publisher** | Marketing proposed pitching Hooded Horse; the Skeptic called it the most useful idea in the room; the Player Advocate warned that a publisher can override price, discounts and the pledge; the Economist wanted the 30-50% share in the per-copy maths | Pitch after the demo has data. Write the pledge into any contract. A publisher roughly halves net per copy but can multiply copies; worth it only if their reach is real |
| **Ads, ever?** | Player Advocate: never, on any platform. Mobile Specialist: never, unless tens of thousands of daily players outside MENA. Skeptic: they fail the PDF's own kill gate before launch | No ads at launch; a written trigger for revisiting (section 9) with a cosmetic-only form |
| **AI art** | Skeptic: disclosure is required but does not fix reception; replace the Act I comic panels. Marketing: address it openly in a devlog | Both: replace the store-facing AI panels through the art pipeline, and disclose honestly |
| **Scope** | Skeptic: freeze Lane War and the MOBA plans until Colony War and the campaign are excellent. Player Advocate: do not remove 1v1 from players who already have it | Feature freeze, not removal; Lane War stays in the game but out of the pitch |
| **Supporter Pack timing** | Economist: after about 5k copies. Others: a few weeks after launch, if reviews are good | After launch, once reviews are ≥85%; it costs little to make |

### 6.4 Final votes

| Motion | Player Advocate | Economist | Mobile | Marketing | Skeptic | Result |
|---|---|---|---|---|---|---|
| (a) Premium Steam with free demo as the lead | For | For | For | For | Conditional (after playtests and the art pass) | **Adopted** |
| (b) Free mobile with one-time unlock | Conditional | Conditional | Conditional | Conditional | Conditional | **Adopted behind gates** |
| (c) Optional menu-only rewarded ads on mobile | Against | Against | Against | Against | Against | **Rejected 0-5** |
| (d) One cosmetic Supporter Pack | Conditional | Conditional | Conditional | Conditional | Conditional | **Adopted behind gates** |
| (e) Paid campaign expansion DLC later | Conditional | Conditional | For | For | Conditional | **Adopted behind gates** |
| (f) Early Access | Against | Against (unless the art cannot finish) | Against | Against (same exception) | Against | **Rejected** |
| (g) Charge nothing until the art pipeline finishes | For | For (+ stranger playtest) | For | For (+ nothing public) | For (+ strangers prove the fun) | **Adopted 5-0** |

### 6.5 What each seat said the owner must hear

- **Player Advocate:** Players forgive a price tag, never a first hour that feels like a prototype. Finish the game, charge once, honestly, and never touch the deal again.
- **Economist:** Plan on about $5.70 per Steam copy and a year-one total of $2k-9k, from one-time sales that never recur. Choose each launch step to raise wishlists and protect reviews, not to add more ways to charge.
- **Mobile Specialist:** Phones are where an RTS goes to be cramped, pirated and invisible. Earn the audience on Steam first, and treat mobile as a port of a proven game.
- **Marketing:** You get one first-week review score and one Next Fest. Make the first two hours look and play finished before a stranger sees a store page.
- **Skeptic:** The problem is not how to charge; it is that nobody outside you has played Mandible yet. Find 30 strangers before you set any price.

## 7. Critique of the consulting PDF

The PDF ("Mandible: monetization review, consulting report", 3 Nov 2026) is a simulated six-partner panel. It was treated here as one opinion to test.

**What it gets right**
- A one-time, non-consumable unlock with a generous free start, the Polytopia shape.
- Store-native regional pricing, with deep discounts for Jordan and Egypt and one SKU (no cheaper second product to arbitrage).
- Hard rules: no gacha, no random paid items, no premium currency, no timers outside a match. It also proposes enforcing them as tests in code, and that is a good idea.
- It struck the ad-bought campaign retry and the expiring season pass. Unlock owners get every ad reward free.
- Monetization stays out of `src/Sim`, so lockstep determinism is safe.
- The Devil's Advocate dissent (premium with a free demo, no ads) is the best paragraph in the document. This council reached the same conclusion independently.

**What it gets wrong**

| Issue | Detail |
|---|---|
| **It ignores PC and Steam entirely** | Mandible's closest comparable, Empires of the Undergrowth, sold 450k+ copies on Steam, and the indie RTS money is there. The PDF plans only for phones |
| **The revenue model double-counts** | It applies 2-4.5% of *monthly active users* buying a one-time unlock *every month*. A player can buy once, so the same people are counted again each month. At its base case (40k MAU, 3% a month) it implies 14,400 buyers a year from a 40k active base, more than a third of all players paying every year, forever. The right base is new installs × a lifetime conversion rate. Its $1.3k-44k *per month* totals are inflated several times over |
| **The audience is assumed** | 10k-150k MAU for a game with 0 stars and 0-3 downloads per release asset |
| **The ad rewards sell power** | "+3 larvae" and a "scout beetle" are gameplay advantages, even in skirmish. A daily "Wild Expedition" map unlocked by an ad is a return-tomorrow hook. "Double Almanac XP" sells relief from a grind it creates. Together they break the owner's rule and the `moba-plan.md` promise |
| **The Almanac and chapter packs** | A cosmetic track competes with the colours and emblems the campaign already awards. "Chapter packs" at $1.99-2.99 risk the Stormgate pattern of a sliced campaign. Many small SKUs split a tiny audience before anyone has bought the base game |
| **Soft launch in Jordan** | Low spend, unconfirmed carrier billing and near-zero installs: a 1.5% conversion gate on that sample is noise. Soft-launch in a Tier 1 market and the Gulf |
| **Factual errors** | Unity's rewarded-ad engagement growth was **3.2%**, not 32% (the URL slug dropped the decimal). Google's fee terms changed in the US, UK and EEA in 2026. The Gulf eCPMs ($13.80/$9.20) and Egypt ($0.25) are unverifiable; Appodeal shows Saudi at $8.26 iOS and $3.78 Android |
| **Unverifiable citations** | "Strategy daily caps 8-12" (Kongregate's data says 6-8); "Glu 2007: 6-12% of demo players bought"; "AppLixir mid-core D30 8-15%"; "Polytopia ~$70K/month" (AppGoblin puts Android under $10K); "27% of MENA prefer F2P with payments and ads" (coverage says about 61% prefer F2P with IAP) |
| **Partly right** | Panzerdog's 3x is real, but it measures converting to installs of a free game, not to purchases. Both stores' 15% tiers are real, but need enrollment and are now outdated for Google in the US, UK and EEA. Next Games' 75-80% opt-in is real, but it is a 2016 free-to-play game with no daily cap |

**Net.** The PDF's ethics are sound and its guardrails are worth keeping. Its business case should not be used for planning.

## 8. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Launching before the game looks finished: a "Mixed" first week and refunds that never recover | High if rushed | Severe | Gates 1-2: the art pass and 30-stranger playtests before any public page |
| No audience: fewer than 2,000 wishlists | High (base rates) | Revenue near zero | Outreach to ant-RTS creators, a devlog, a publisher pitch with demo data; under 2,000 after six months means rethink the pitch, not the price |
| AI-art backlash in a genre wary of fast AI-built games | Medium | Reviews, press | Replace store-facing AI panels via the art pipeline; disclose and explain openly |
| A third party lists the game on Steam (if the code is open-licensed) | Low-medium | Brand confusion | Reserve the Steam page and the name now; trademark search |
| Solo developer stretched across four stores | High | Burnout, slow patches | One store in year one (Steam); mobile only behind gates |
| Godot C# on mobile is experimental; billing plugins need wrappers | Medium | A port that plays badly | Low-end-device gate; verify billing on a real device before launch |
| Pressure to add ads or paid power later when sales are slow | Medium | Goodwill | The public pledge; section 5's lines; any change goes through the owner, never an SDK default |
| Paid DLC that splits 1v1 players | Low | Reviews (Three Kingdoms) | DLC species out of 1v1 and Lane War, or free there for everyone |
| Publisher overrides the price or pledge | Low-medium | Reputation | Pledge written into the contract |
| Cash timing: one-time spikes, payouts 5-8 weeks after launch | Certain | Personal finances | Treat Mandible as a project with upside, not income; keep the day income |

## 9. Decisions for the owner

1. **Licence.** Pick one:
   - **Source-visible, all rights reserved.** This is what the repo is today. It is the simplest for selling: anyone can read and build the code, nobody may redistribute it or sell it.
   - **A non-commercial source licence** (for example PolyForm Noncommercial): friendlier to modders.
   - **GPL, like Mindustry:** fully open. Anyone may then sell copies too, so reserve the name first.

   Assets stay all rights reserved in every case.
2. **GitHub builds.** Free full builds are on GitHub today, with near-zero downloads. Recommended:
   - Leave the old releases up; removing them changes the deal for nobody, but deleting is unnecessary.
   - From now on, publish only source and the demo build there.
   - Mindustry shows that a free path does not kill Steam sales, but there is no reason to keep shipping signed full builds for free once the store version exists.
3. **Ads.** Confirm "no ads at launch". The trigger to revisit: mobile above about 20,000 daily players with most of them outside MENA. Even then, only one opt-in, cosmetic-reward menu ad.
4. **Price.** Confirm $14.99 on Steam and $5.99 for the Full Colony unlock (about $2.49 in Jordan and Egypt).
5. **The pledge.** Adopt "Buy once. No ads. No pay-to-win. No locked heroes. Ever." It cannot be taken back once public.
6. **Scope.** Confirm a feature freeze on Lane War and the MOBA plan until 1.0.
7. **Publisher.** Decide whether to pitch strategy publishers once the demo has data.

## 10. Method and caveats

- **Inputs.**
  - The consulting PDF, read in full.
  - The repository (README, `docs/level-design.md`, `docs/moba-plan.md`, `docs/colony-command-campaign.md`, provenance notes).
  - GitHub release statistics.
  - Three research passes (PC, mobile, player sentiment with a fact-check of the PDF), each citing a URL for every claim.
  - The council's five positions and five debate replies.
- **The revenue figures are estimates**, not forecasts.
  - Tracker sales figures carry ±30-50% error.
  - Ad data comes from ad vendors.
  - No credible recent public figure exists for mobile trial-to-unlock conversion, or for rewarded eCPM in Jordan and Egypt.
  - The Epic-Google settlement terms were still being finalised.
- **Survivorship bias.** Every comparable named here is a success that was visible enough to be measured.
- **The base rate is the honest anchor:** most Steam games earn under $1,000.
- Nothing from any other game (names, characters, art, levels) is proposed for Mandible. The comparables are business lessons only.

## 11. Sources

**Steam and PC**
- Steam revenue tiers: https://www.pcgamer.com/what-indies-think-of-valve-giving-more-money-to-big-sellers
- Steam Direct fee: https://partner.steamgames.com/doc/gettingstarted/appfee
- Steam refunds: https://store.steampowered.com/steam_refunds/
- Refund-rate survey (GameDiscoverCo): https://gamedevreports.substack.com/p/gamediscoverco-steam-refunds-research
- Regional pricing: https://partner.steamgames.com/doc/store/pricing
- Demos (incl. no multiplayer with full game): https://partner.steamgames.com/doc/store/application/demos
- Next Fest rules (one per game, no prologues): https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest
- Demo visibility update 2024: https://www.pcgamesn.com/steam/demos-update-free-games
- Prologue tactic: https://howtomarketagame.com/2023/04/03/how-to-use-the-prologue-trick-to-potentially-earn-thousands-of-wishlists/ ; https://newsletter.gamediscover.co/p/steam-prologues-do-they-really-help
- Steam bans ad-based models: https://www.pcgamer.com/gaming-industry/valve-bans-games-that-rely-on-in-game-ads-from-steam-so-no-watch-this-to-continue-playing-stuff-will-be-making-its-way-to-our-pcs ; https://gamingonlinux.com/2025/02/valve-ban-advertising-based-business-models-on-steam-no-forced-adverts-like-in-mobile-games
- Steam AI disclosure (2026 rewrite): https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/ ; https://www.videogameschronicle.com/news/valve-has-significantly-rewritten-steams-rules-for-how-developers-much-disclose-ai-use
- Wishlist conversion, state of Steam (GameDiscoverCo, Nov 2025): https://gamedevreports.substack.com/p/gamediscoverco-the-state-of-steam
- Releases in 2025, 7k wishlist threshold: https://howtomarketagame.com/2026/01/08/how-many-games-were-released-in-2025/ ; https://www.gamingonlinux.com/2025/12/over-19000-games-have-released-on-steam-in-2025-with-nearly-half-seeing-fewer-than-10-reviews
- Revenue distribution (Gamalytic): https://opencritic.com/news/22399/only-8-of-steam-games-gross-over-100-thousand-40-make-less-than-steam-listing-fees ; https://gamesradar.com/games/over-5-000-games-released-on-steam-this-year-didnt-make-enough-money-to-recover-the-usd100-fee-to-put-a-game-on-valves-store-research-estimates
- Early Access graduates: https://newsletter.gamediscover.co/p/the-state-of-steam-early-access-graduates ; https://nichegamer.com/new-data-shows-only-21-of-early-access-games-perform-better-after-full-release/
- Empires of the Undergrowth: https://raijin.gg/app/463530/Empires_of_the_Undergrowth ; https://wnhub.io/news/stores-and-publishing/item-45773 ; https://www.yardbarker.com/video_games/articles/indie_strategy_games_thrive_as_hooded_horse_reports_45_million_sales_in_twelve_months/s1_17144_41051600 ; https://en.wikipedia.org/wiki/Empires_of_the_Undergrowth
- Northgard: https://raijin.gg/app/466560/Northgard ; https://www.gonintendo.com/contents/13706-northgard-adds-expansive-campaign-evolved-clan-system-today-game-hits-3-million-sold ; https://www.gamespress.com/ko-KR/More-clans-will-soon-join-the-fight-for-the-continent
- They Are Billions: https://raijin.gg/app/644930/They_Are_Billions
- Kingdom Two Crowns and DLC: https://raijin.gg/app/701160/Kingdom_Two_Crowns ; https://www.dekudeals.com/items/kingdom-two-crowns-norse-lands
- Mindustry: https://raijin.gg/app/1127400/Mindustry ; https://anuke.itch.io/mindustry
- Bad North: https://raijin.gg/app/688420/Bad_North
- Tooth and Tail: https://raijin.gg/app/286000/Tooth_and_Tail
- Empire of the Ants: https://raijin.gg/app/2287330/Empire_of_the_Ants
- Diplomacy is Not an Option: https://raijin.gg/app/1272320/Diplomacy_is_Not_an_Option
- Stormgate: https://esportsinsider.com/what-happened-to-stormgate ; https://www.windowscentral.com/gaming/pc-gaming/stormgate-dev-blames-flop-industry-issues-reviews-suggest-otherwise
- Battle Aces cancelled: https://www.pcgamesn.com/battle-aces/shut-down-rts-game
- Ant Simulator: https://www.gamespot.com/articles/biz-partners-stole-money-for-booze-and-strippers-d/1100-6434397/
- Cataclysm DDA on Steam: https://cataclysmdda.org/steam_faq.md
- 0 A.D. listed by a third party: https://www.gamingonlinux.com/2022/10/someone-released-the-foss-rts-0-ad-on-steam-without-speaking-to-the-developers/comment_id=234211
- Kickstarter games 2024-25: https://newsletter.gamediscover.co/p/what-worked-for-video-game-crowdfunding ; https://gamedevreports.substack.com/p/kickstarter-in-2025-and-before
- Patreon for games: https://www.gamedeveloper.com/business/the-patreon-report-1 ; https://www.gamedeveloper.com/business/the-benefits-and-risks-of-using-patreon-to-crowdfund-games
- Godot consoles: https://docs.godotengine.org/en/4.5/tutorials/platform/consoles.html ; https://www.w4games.com/w4consoles

**Mobile**
- Apple Small Business Program: https://developer.apple.com/app-store/small-business-program/?page=2
- Google Play service fees: https://support.google.com/googleplay/android-developer/answer/112622
- Google fee changes 2026: https://9to5google.com/2026/03/04/google-vs-epic-games-android-app-stores-lower-fees/
- US anti-steering rulings: https://www.medianama.com/2025/05/223-us-court-permanent-injunction-apple-app-store-commissions-external-purchases/ ; https://www.engadget.com/big-tech/supreme-court-denies-googles-request-to-pause-play-store-changes-while-it-appeals-epic-case-121502132.html
- Apple EU terms 2026: https://aa.com.tr/en/science-technology/apple-revamps-eu-app-store-fees-to-resolve-regulatory-dispute/4031035
- App Review Guidelines (trials, restore, rewarded ads, odds): https://developer.apple.com/app-store/review/guidelines/
- Family Sharing for IAP: https://developer.apple.com/help/app-store-connect/configure-in-app-purchase-settings/turn-on-family-sharing-for-in-app-purchases
- Google Play Better Ads / rewarded: https://support.google.com/googleplay/android-developer/answer/12271244 ; AdMob rewarded rules: https://support.google.com/admanager/answer/7496282
- Google Play Game Trials: https://developer.android.com/games/distribute/game-trials
- Google Play 12-tester rule: https://www.rapidnative.com/deploy/google-play-12-tester-requirement
- Apple Arcade payouts: https://mobilegamer.biz/inside-apple-arcade-axed-games-declining-payouts-disillusioned-studios-and-an-uncertain-future/
- Netflix games drops indies: https://9to5mac.com/2025/06/24/netflix-is-pulling-the-plug-on-21-indie-games/
- Premium mobile 2025 (AppMagic): https://gamedev.net/news/4367/
- Ironhide on premium: http://www.pocketgamer.biz/ironhide-co-founder-pablo-realini-talks-kingdom-rush-5-and-premiums-place-in-2024/
- Polytopia: https://www.pocketgamer.com/the-battle-of-polytopia/25-million-downloads ; https://polytopia.io/support/ ; https://appgoblin.info/apps/air.com.midjiwan.polytopia
- Rusted Warfare: https://raijin.gg/app/647960/Rusted_Warfare_RTS ; https://steamdb.info/app/647960
- Northgard mobile: https://toucharcade.com/2021/04/13/northgard-mobile-out-now-ios-price-free-dlc-roadmap-playdigious-shiro-games/ ; https://www.pocketgamer.com/articles/087066/northgard-the-excellent-viking-strategy-game-will-launch-for-android-in-august/
- Rewarded eCPM benchmarks: https://blog.playio.co/mobile-game-ecpm-benchmarks-2026 ; https://appodeal.com/the-mobile-ecpm-report-updated-q4-2024-view ; https://www.playwire.com/blog/admob-ecpm-benchmarks-what-publishers-should-expect
- ARPDAU benchmarks: https://blog.playio.co/arpdau-benchmarks-mobile-games
- Rewarded ads and IAP (ironSource): https://unity.com/es/blog/understanding-the-impact-of-rewarded-ads-on-iap-retention-and-engagement
- GDPR consent for AdMob: https://support.google.com/admanager/answer/13554116?hl=en
- ATT opt-in 2025: https://www.adjust.com/blog/att-opt-in-rates-2025
- Families ad policy: https://support.google.com/googleplay/android-developer/answer/9900633/
- Godot C# platform status: https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html
- Godot AdMob plugin: https://github.com/Poing-Studios/godot-admob-plugin ; Play Billing plugin: https://github.com/godot-sdk-integrations/godot-google-play-billing
- Retention benchmarks (GameAnalytics): https://gamedevreports.substack.com/p/gameanalytics-mobile-and-pc-game
- Top-grossing 2025: https://mobilegamer.biz/the-top-grossing-mobile-games-of-2025/
- MENA market (Niko): https://gameworldobserver.com/2025/07/04/forecast-in-2025-residents-of-saudi-arabia-the-uae-and-egypt-will-spend-2-2-billion-on-games ; https://gamedevreports.substack.com/p/xsolla-and-niko-partners-mena-3-market
- MENA payments: https://www.boku.com/boku-knows/carrier-billing-market-report-middle-east-north-africa
- Google Play price ranges: https://support.google.com/googleplay/android-developer/answer/6334373?hl=en
- Monument Valley Android: https://www.pocketgamer.com/monument-valley/update-ustwo-95-percent-of-android-players-didnt-pay-for-monument-valley/
- Play automatic protection: https://support.google.com/googleplay/android-developer/answer/10183279

**Player sentiment, goodwill, regulation**
- Tapjoy rewarded vs forced: https://medium.com/tapjoy/mobile-gamers-prefer-rewarded-ads-by-4-to-1-margin-tapjoy-research-finds-bb107c6266dc
- Unity 2015 ad format survey: https://gamesbeat.com/?p=119232
- Unity 2024 placements (3.2% growth): https://www.pocketgamer.biz/unity-global-rewarded-ad-engagement-rose-by-32-in-2023
- NPD on pay-to-win: https://www.pushsquare.com/news/2018/02/new_study_suggests_only_6_percent_of_people_dont_spend_money_on_microtransactions
- Cosmetics poll: https://www.mcvuk.com/business-news/only-1-of-players-in-favour-of-microtransactions-model-qutee-report-says/
- Newzoo generations: https://gamedevreports.substack.com/p/newzoo-how-different-generations-d7f?open=false
- Dungeon Keeper ASA ruling: https://www.engadget.com/2014-07-02-uk-regulator-rules-free-dungeon-keeper-ad-was-misleading.html
- Battlefront II: https://www.thegamer.com/ea-guinness-world-record-most-downvoted-reddit-comment/
- Diablo Immortal: https://www.techspot.com/news/95186-diablo-immortal-earned-almost-50-million-first-month.html
- Warcraft III Reforged: https://shacknews.com/article/116127/warcraft-3-reforged-garners-a-record-low-05-metacritic-user-rating
- C&C Rivals: https://kotaku.com/new-command-conquer-mobile-game-is-getting-mauled-onl-1826708607
- Age of Empires Mobile: https://www.notebookcheck.net/Age-of-Empires-Mobile-appears-to-be-a-pay-to-win-game.809663.0.html
- Helldivers 2 PSN reversal: https://www.tweaktown.com/news/98061/sony-officially-cancels-helldivers-2-psn-requirement/index.html
- AoE II Three Kingdoms DLC: https://forums.ageofempires.com/t/3-kingdoms-rating-has-dropped-below-50-recommendation-for-the-first-time-since-release/277240
- Path of Exile revenue: https://en.wikipedia.org/wiki/Grinding_Gear_Games
- Deep Rock Galactic: https://gameworldobserver.com/2024/01/12/deep-rock-galactic-sales-8-million-copies-ghost-ship-games
- Factorio no discounts: https://www.gamesradar.com/factorio-the-base-building-hit-that-never-goes-on-sale-is-now-raising-its-price-to-account-for-inflation
- Kingdom Rush hero IAP criticism: https://androidauthority.com/kingdom-rush-vengeance-review-928335
- Vampire Survivors mobile: https://kotaku.com/vampire-survivors-free-iphone-steam-mobile-smartphone-1849955308 ; https://gameworldobserver.com/2023/01/17/vampire-survivors-mobile-3-million-downloads-appmagic
- Shattered Pixel Dungeon donations: https://shatteredpixel.com/blog/donations-in-shattered-pixel-dungeon.html
- Belgium loot boxes: https://gamesradar.com/belgium-deems-loot-boxes-dangerous-to-children-and-society-threatens-publishers-with-prison-and-fines
- Brazil ECA Digital: https://www.mattosfilho.com.br/en/unico/brazils-eca-digital/
- FTC v Epic: https://www.ftc.gov/node/80648
- EU CPC virtual currency principles: https://connectontech.bakermckenzie.com/european-consumer-protection-network-issues-new-key-principles-on-in-game-virtual-currencies-impact-for-gaming-and-gambling-entities-in-belgium-the-eu-and-beyond/
- Next Games rewarded opt-in: https://www.pocketgamer.biz/next-games-ads-drive-revenue/
- Kongregate rewarded frequency: https://medium.com/googleplaydev/diversify-your-mobile-games-revenue-with-rewarded-ads-abaeca615040
- Panzerdog instant demo: https://developer.android.com/stories/games/panzerdog
- Saudi Appodeal eCPM: https://ru.appodeal.com/blog/mobile-ecpm-report-app-ad-monetization-worldwide-performance

**Project files**
- `README.md`, `docs/level-design.md` (persistent colony, star unlocks, "no timers, energy or paywalls"), `docs/moba-plan.md` §3.8 (monetisation stances and the "no ads" promise), `docs/colony-command-campaign.md`, `docs/interior-graphics.md` (asset provenance); GitHub release statistics for v0.9.0-v0.11.0.
