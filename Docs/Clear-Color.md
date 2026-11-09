# Clear animation color — 20/10/2026

The dragged/placed block owns the entire cleared row/column's hue, including mixed-color lines and row/column intersections. Resolve the palette/theme color once in `Board.Place` and pass that same color through the clear transaction; do not read a global theme effect color after cells have been emptied.

- Cell dissolve keeps the placed color throughout its staggered fade. Waiting cells receive that color immediately, rather than flashing their original mixed colors before their delay expires.
- Line flashes and fragments use the same hue. White highlights/alpha fades remain allowed; multi-clear tiers and cosmetic style tints cannot replace the hue with green/pink/gold.
- Movement, scan delays, shrinking, clear duration, score calculation, bubble targets and board rules are unchanged.
- Fever's separate board-border/ambient celebration and gold bubble-target feedback remain distinct effects. They do not decide the dissolving cells' color.
- Retired `Board.lineClearEffectColor` is removed. No scene/prefab layout or saved run needs migration.

Focused QA: `PopClearColorValidation.Run`, guarded to the isolated regression project. It tests seven placement colors against mixed-color rows, columns, crosses and four-line clears, with deliberately green legacy theme/style tints. It checks actual renderers during preview, immediately on placement and during the dissolve, line flashes, emitted particle hue, final hiding and occupancy. Logs/native screenshots: `.utmp/clear-color-20261020/`.

Result: **3,039 checks passed across 28 clear scenarios**. Home/Game scene hashes unchanged. Native preview: `Docs/Previews/clear-red-column.png`.

Runtime compile checks cover Editor, Android Development and Android Release. This change is not automatically installed on a phone; an already installed APK needs rebuilding/reinstalling to include it.
