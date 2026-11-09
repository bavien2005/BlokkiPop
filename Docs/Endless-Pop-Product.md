# Endless Pop: identity, onboarding and puzzle quality — 20/10/2026

## Player promise

Place blocks to pop every marked bubble within a fixed placement budget. See the coming pieces. Carry the remaining board into the next wave, so solving now and leaving useful space are both important.

Classic remains an alternative; Daily remains a short return visit. This update adds no shop, currency, forced ads between waves, account system, energy or online tracking.

## First play

New Endless players receive a separate four-placement lesson: pop a marked row, spend a placement, look at a real fourth piece in **Tiếp theo**, then use that piece for a two-row/two-bubble clear. Invalid placements spend nothing. Landing guides use the real board and the real piece transaction.

- The lesson has its own completion flag, independent of the Classic tutorial.
- Continue never interrupts an existing run with the lesson. Existing players with a completed-wave record also skip automatic onboarding.
- Completing the lesson presents the existing result page; Play starts a fresh Endless run, without tutorial score or board contents.
- Skip is optional and follows `Tutorial → Allow Skip`. Leaving midway without completing/skipping does not mark it completed.
- Home's Learn action replays the Pop lesson when Endless is the primary mode. Replaying/skipping a manual lesson never replaces the real saved Endless run.
- A practice lesson is not itself a saved game. Classic/Endless saves and records remain separate.

## Inspector / scene editing

Select `Assets/_Project/Resources/Gameplay/PopGameplayConfiguration.asset`:

- **Tutorial**: `Automatic On First Endless`, `Endless Lesson` (editable cells, bubbles, deck, actual solution), `Endless Instructions`, existing skip/highlight options. Build validation replays the configured lesson.
- **Endless**: `Alternate Lane Patterns`, `Multi Clear Weight`, `Setup Move Weight`, `Leftover Pocket Penalty`, alongside existing difficulty/move limits/breathing interval.
- **Bubble Feedback**: enable, burst duration, ring diameter, stagger, sound gain, pop color and wave-clear cue.

UI is authored in both existing UI prefabs:

```text
Blokki Pop UI / Endless Pop Overlay / Endless Pop Queue
└─ Gameplay Frame / Header / Lesson Coach
   ├─ Instruction
   └─ Skip
```

The Queue Inspector has **Hiện / ẩn bài học Pop trong Scene**. Select Scene Preview = Hud and Preview Endless HUD, then preview the coach. Edit its child RectTransforms, fonts, colors and button normally. The camera reserves the coach's actual bounds while it is visible. The coach is hidden in ordinary gameplay; no runtime code reconstructs these UI objects or resets designer offsets.

The installer only adds a missing coach; rerunning preserves existing Header/Upcoming Trays and all coach edits. Scenes are not rewritten by this update.

## Generator changes and limits

Candidates now alternate adjacent lanes, spaced lanes and crossing row/column arrangements where the difficulty permits multiple lanes. Breathing waves use the simpler arrangement. Bubble positions vary across target lanes instead of always using the left/bottom edge.

Each candidate still has a replayed legal witness under three-piece tray rules. Scoring also counts actual multi-line clears, setup moves, remaining occupied cells and small disconnected empty pockets after that witness. The generator preview displays these structural metrics. Weights are heuristics, not a guarantee of interesting decisions, shortest solutions, multiple viable paths or balanced human difficulty.

The budget and carried-board verified fallback remain bounded. Existing saved puzzles retain their entire deck, targets, witness and RNG state; changing ranking only affects future waves. No revision/schema bump or automatic deletion of existing saves is required because placement rules and serialized level structure do not change.

## Feedback

Popped bubbles emit short expanding/fading gold rings from their own cells, staggered for multi-pops. Rings use a bounded 64-renderer pool and scaled time so Pause freezes them. Distinct procedural audio cues for a bubble, a multi-pop and a wave clear respect the existing SFX source/volume. A restored pending transition does not replay the clear cue. No board/tray/camera transforms are modified by these effects.

## Human playtest still required

Use 10–15 people unfamiliar with the game for qualitative observation, not statistically reliable retention percentages. Give them the Android build and say only “play a little”; do not explain the rules first.

Record per person, without collecting names or personal identifiers:

1. Can they explain bubbles, placement budget and next tray after onboarding?
2. Do they actually consult the future pieces? Where do they misread the UI?
3. At loss, can they explain the missed opportunity? Was anything surprising/unfair?
4. Do they voluntarily start another run? Which decision felt clever?
5. On a later day, do they want another run or the Daily puzzle? Ask why, not just whether they liked it.

Note confusing seeds/waves using the generator preview. Revise those puzzle patterns before adding more content. Later, a small Android release can measure the first-wave / next-wave / replay / next-day-return funnel with an appropriately disclosed analytics design; no analytics service was installed in this update.

Native Editor screenshots and automated results belong in `.utmp/endless-product-20261020/`. Editor/compile checks do not establish Android performance, physical touch quality, retention or market demand.

## Verified handoff

- `product-verified.log`: **159** first-play/UI checks, including all four instruction strings at five screen ratios, actual next-tray consumption, score reward target, bubble burst, Pause, manual replay/Skip, exact Continue and non-repeating onboarding.
- `endless-final.log`: **4,758** checks, including 192 deterministic carried-board puzzles, witness replay, actual scene transitions, last-move win, pending-transition save/resume and isolation from Classic saves.
- `gameplay-final.log`: **576** regression checks: Classic exact resume, 20 Journey wins, Daily retry/calendar, original Classic tutorial, optional-ad rules and three Domain-Reload-off Play/Stop cycles.
- Editor and Android Development/Release runtime scripts compile successfully. No new APK was built or installed on Vivo in this update.
- All **853** previously authored RectTransform poses in the two UI prefabs were compared before migration and preserved. Only missing lesson-coach objects/references were added. Home/Game scene hashes were unchanged.
- Backups of the original two prefabs and gameplay config: `.utmp/endless-product-20261020/backups/`.
- Human-readable native preview: `Docs/Previews/endless-pop-lesson.png`.

For an existing player, replay via Home → gear → **Hướng dẫn chơi**; automatic onboarding deliberately does not replace an existing save or interrupt Continue.
