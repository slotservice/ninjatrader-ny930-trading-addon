# NY930 — Progress & Analysis Log

> Live record of analysis, decisions and deliverables for Phase 1 of the NY930
> NinjaTrader 8 AddOn project. Every change made under `NY930/` is summarised
> here so the work can be reviewed end-to-end at any point in time.

---

## 1. Inputs received

| File | Type | Status |
|------|------|--------|
| `files/hedge.cs` (1507 lines) | NinjaScript Strategy "Apertura" | Reviewed |
| `files/openrange.cs` (2168 lines) | NinjaScript Strategy "AperturaBreakout" | Reviewed |
| `files/OpenRangeControl.cs` (585 lines) | Standalone WPF panel proof of concept | Reviewed |
| `files/OrdersManagerPro_*.mqh / .mq5` | MT5 reference (manual partial / trailing UI) | Reviewed |
| `files/HOMEPAGE APP 2.png` | Approved AddOn home screen design | Reviewed |
| `files/flujo de la app.png` | App user-flow diagram | Reviewed |
| `files/botfree.png`, `files/openrange*.png`, etc. | Visual references (reference only, not target design) | Reviewed |
| `files/Openrange working.mp4` | Reference behaviour | Noted |
| `files/PROPUESTA FINAL.txt` | Three-phase proposal | Reviewed |
| `chat.md` | Full conversation with the client | Reviewed |
| `project_status&plan.md` | Current task brief | Reviewed |

---

## 2. State of the existing codebase (what is already implemented)

Both `hedge.cs` and `openrange.cs` already implement a substantial amount of
production logic. The reinforcement work must keep all of it intact:

- `IsUnmanaged = true`, OCO linking, manual SL/TP via `SubmitOrderUnmanaged`.
- Precise `System.Threading.Timer` + `TriggerCustomEvent` entry at `EntryHour:EntryMinute:EntrySecond`.
- Spurious-trigger guard for queued timer events from a previous instance.
- Full `Breakeven`, `TrailingStop` (stepwise) and `TrailingTP` (max/min + timeout).
- Two-level `Partials` (P1 + P2) with cumulative fill tracking and SL contract reduction (with a `slChangePending` polling retry every 300 ms).
- `EnableTimeExit` with three modes (`CloseAlways`, `CloseIfPositive`, `PlaceTPAfterTime`) and the `CloseIfBeyondTP` short-circuit.
- Full state persistence via `static` fields so `State.Transition` → `State.Realtime` (timeframe change) re-submits orders without losing SL/TP/P1/P2.
- Daily reset and validation of partial settings.
- Polished NT8 OCO ID handling (always regenerated on restore — avoids the `"OCO ID cannot be reused"` broker rejection).

`OpenRangeControl.cs` shows a pattern for the WPF control plane: WPF buttons
call `TriggerCustomEvent(o => DoMove(...), null)` so order modifications run
on the NT8 thread. **However** it is implemented as a `Strategy` rather than a
proper AddOn, lacks state persistence on TF change and uses the older
`OnMarketData` polling for entry timing instead of the precise `Timer` used
by the other two strategies.

---

## 3. Gap analysis vs. `project_status&plan.md`

### 3.1 Strategy reinforcement (Section 2.A / 2.B / 4 / 5 of the brief)

| Requirement | hedge.cs | openrange.cs |
|---|---|---|
| Don't lose SL/TP/Stop entries on TF change | Implemented | Implemented |
| **TP Gap Guard by ticks** (close at market when price crosses TP by X ticks) | **Missing** | **Missing** |
| **SL Gap Guard by ticks** (mirror) | **Missing** | **Missing** |
| **Time Guard** (close if not filled in Y seconds after price reaches TP/SL) | Partial — only inside `TrailingTP` timeout | Partial — same |
| **Single-Stop Reverse-Tick Protection** (cancel pending Buy/Sell Stop if price moves against the order's offset by N ticks before trigger) | n/a | **Missing** |
| Structured logging (info / warn / error) | All `Print()` lines unlevelled | All `Print()` lines unlevelled |
| AddOn-friendly state publication for the UI | None | None |

### 3.2 NY930 WPF AddOn (Section c/d/e of the brief)

Nothing exists yet. The standalone `OpenRangeControl.cs` is the only WPF asset
available, and it is bound to a different strategy and language. All of this is
new work:

- NY930 home screen with the approved gold/black branding (cards: Open Range / Hedge).
- Hamburger menu (Settings → language EN/ES + info).
- Open Range section with floating control panel (move both, spread, cancel, live read-out of Buy Stop / Sell Stop / spread).
- Hedge section exposing every parameter visually: time HH:MM:SS, qty, direction, SL/TP ticks, BE / Trailing / Trailing TP / Partials / Time Exit toggles + values.
- Bilingual EN/ES with live switching and persisted preference.
- Trade-progress + trade-result visualisation.
- Thread-safe `TriggerCustomEvent` round-trip from UI to the strategy thread.

---

## 4. Architecture chosen

```
NY930/
├── PROGRESS_LOG.md           ← this file (live audit trail)
├── INSTALLATION.md           ← end-user install instructions
├── README.md                 ← one-page overview
├── Strategies/
│   ├── Hedge.cs              ← reinforced "Apertura" (drop-in replacement)
│   └── OpenRange.cs          ← reinforced "AperturaBreakout" (drop-in replacement)
└── AddOns/NY930/
    ├── NY930Bridge.cs        ← static event-aggregator: Strategies ⇄ AddOn (thread-safe)
    ├── NY930Logger.cs        ← structured logging (Info/Warn/Error) used by both sides
    ├── NY930Localization.cs  ← EN / ES string table + live switching
    ├── NY930Theme.cs         ← gold-on-black palette, brushes & button styles
    ├── NY930Settings.cs      ← persisted user preferences (language, default ticks, etc.)
    ├── NY930AddOn.cs         ← `NTAddOn`: registers the menu item + dockable window
    ├── NY930HomeView.cs      ← homepage (NY930 logo + Open Range / Hedge cards)
    ├── NY930OpenRangeView.cs ← Open Range control panel + live progress
    └── NY930HedgeView.cs     ← Hedge parameters + live progress
```

**Why this layout:**

- `NY930Bridge` is the single integration point. Strategies push immutable
  state snapshots and the AddOn subscribes; the AddOn publishes user actions
  and the strategies consume them via `TriggerCustomEvent`. No direct
  references, so each strategy still compiles and runs alone (drop-in safe).
- The AddOn lives entirely under `bin/Custom/AddOns/NY930/` so the user can
  install the whole subfolder via NinjaTrader's Import → Compile flow without
  touching anything else.
- All UI work happens on the WPF dispatcher; all order work is marshalled to
  the NT8 thread via `TriggerCustomEvent`.
- One chart = one strategy instance is preserved (unchanged from the brief).

---

## 5. New protections added to the strategies

### 5.1 TP / SL Gap Guard

Two identical guards (one per side) added to the `OnMarketData` hot path:

1. **Tick-based:** if `lastPrice` overshoots the working TP / SL price by
   `TpGapGuardTicks` / `SlGapGuardTicks`, the corresponding limit/stop order
   is cancelled and a market order is sent immediately.
2. **Time-based:** if the price has crossed the level but no fill arrived
   within `TpGapGuardSeconds` / `SlGapGuardSeconds`, the same market exit is
   triggered.

Each guard is opt-in via a `bool` toggle and trips at most once per trade
(latched by `tpGapGuardFired` / `slGapGuardFired`).

### 5.2 Single-Stop Reverse-Tick Protection (Open Range only)

Per the client's screenshot in `chat.md`:

> Cuando se selecciona una sola orden Stop (Buy o Sell) y el precio se mueve
> en contra X ticks antes del trigger → cancelar.

Configuration:

- `EnableSingleStopReverseProtection` (bool)
- `SingleStopReverseTicks` (int) — `0` means "use the entry's stop offset"
  (`TicksLong` for Buy Stop, `TicksShort` for Sell Stop), matching the
  client's intent ("X ticks = a la cantidad de ticks seleccionados para la orden Stop").

The guard is anchored at the price recorded when the entry order was placed
and only trips while the entry order is still working (no fill yet).

### 5.3 Structured logger

Replaces the ad-hoc `Print()` calls with `NY930Log.Info / Warn / Error /
Debug`, which:

- Prefixes every line with `[LEVEL][Apertura]` / `[LEVEL][AperturaBreakout]`.
- Optionally mirrors `Warn` and `Error` to NT8's `Log` window via
  `NinjaTrader.NinjaScript.NinjaScript.Log` so production issues surface in
  the right place.
- Stays a static helper so each strategy still works without the AddOn.

---

## 6. UI mapping

| Screen | What it shows | Backed by |
|---|---|---|
| **Home** | NY930 wordmark + tagline + Open Range / Hedge cards + hamburger menu | `NY930HomeView` |
| **Open Range** | Live Buy Stop / Sell Stop / spread, Move ▲▼ (1/5/10/25 ticks), Spread ←→, Cancel, Buy Now / Sell Now, progress strip (TP1/2/3, SL, PnL, contracts, duration) | `NY930OpenRangeView` |
| **Hedge** | HH:MM:SS, qty, direction (Long/Short), SL/TP ticks, BE / Trailing / Trailing TP / Partials / Time Exit toggles + numeric editors, Buy Now / Sell Now, progress strip | `NY930HedgeView` |
| **Settings** | Language EN/ES, About | hamburger menu |

Live language switching: every visible string is bound to
`NY930Localization.Current[key]` and the views subscribe to
`NY930Localization.LanguageChanged` to refresh in place.

---

## 7. Thread safety

- WPF buttons → `TriggerCustomEvent(o => action(), null)` → executes on NT8 thread.
- Strategy → UI: `NY930Bridge.PublishSnapshot(...)` raises a `.NET` event;
  the views marshal to `Application.Current.Dispatcher.InvokeAsync(...)`.
- All shared state is held in `lock(_sync)` blocks inside `NY930Bridge`.

---

## 8. Per-step delivery log

> Each entry below records a concrete change, the file touched, and the
> motivation, so the audit trail can be re-read top-to-bottom later.

- **2026-05-04 — Step 1: Repository scaffold created.**
  Added `NY930/`, `NY930/Strategies/`, `NY930/AddOns/NY930/` and this
  `PROGRESS_LOG.md` after a full read of every input file (chat, proposal,
  C# strategies, OpenRangeControl, MT5 references, images).

- **2026-05-04 — Step 2: Reinforced `Strategies/Hedge.cs`.**
  Drop-in replacement for `files/hedge.cs`. Keeps every existing feature
  (Unmanaged + OCO, Breakeven, Trailing, Trailing TP, Partials, Time Exit,
  TF-change persistence). Adds: structured logger (`NY930Log`), TP Gap Guard
  (tick + time), SL Gap Guard (tick + time), AddOn bridge hooks
  (`NY930Bridge.PublishHedgeState` + action subscription for Buy/Sell Now /
  Cancel / Close).

- **2026-05-04 — Step 3: Reinforced `Strategies/OpenRange.cs`.**
  Drop-in replacement for `files/openrange.cs`. Keeps the full original
  feature set untouched. Adds: structured logger, TP/SL Gap Guards (tick +
  time, per side), **Single-Stop Reverse-Tick Protection** (anchors price at
  entry-order placement, cancels the lone Buy / Sell Stop if price moves
  against by N ticks before trigger; default N = entry stop offset, override
  via `SingleStopReverseTicks`), AddOn bridge hooks (state publication +
  action consumption for Move / Spread / Cancel / Buy Now / Sell Now).

- **2026-05-04 — Step 4: Built the AddOn shared infrastructure.**
  `NY930Logger.cs` (level-prefixed Print + NT log mirror), `NY930Bridge.cs`
  (static thread-safe pub/sub, snapshot DTOs, action queue),
  `NY930Localization.cs` (EN/ES dictionaries, live language switching event),
  `NY930Theme.cs` (gold/black palette, button factories), `NY930Settings.cs`
  (persisted preferences via NT user data folder).

- **2026-05-04 — Step 5: Built the AddOn UI.**
  `NY930AddOn.cs` registers a `New → NY930` menu item under every NT window
  via `NTMenuItem` and opens `NY930HomeView` inside an `NTWindow` that the
  user can dock to the right of any chart (Chart-Trader-style). Implemented
  `NY930HomeView`, `NY930OpenRangeView`, `NY930HedgeView` (all bilingual,
  themed, bound to the bridge).

- **2026-05-04 — Step 6: Documentation.**
  `INSTALLATION.md` (where to copy each file, which compile order, how to
  load the AddOn), `README.md` (one-page summary for the client).

- **2026-05-04 — Step 7: Self-review pass.**
  - Removed dead code in `NY930AddOn.cs` (`window as NTMenuItem` /
    `if (window == null)` ordering bug).
  - Wired menu attachment to the host window's `Loaded` event so the
    visual tree is fully realised before walking it.
  - Added `TickSize` to both bridge snapshots; the Open Range view
    now derives the spread from the actual instrument tick size
    instead of a hard-coded fallback.
  - Removed the `Caption` setter from `NTWindow` (uses `Title`
    instead — `Caption` is not a public NTWindow property in current
    NT8 versions).
  - Verified all `using` directives align with what the strategies
    and the AddOn actually reference (`NinjaTrader.Cbi`,
    `NinjaTrader.Gui.Tools`, `NinjaTrader.NinjaScript.AddOns.NY930`).

## 10. Final file list

```
NY930/
├── PROGRESS_LOG.md            (this file)
├── INSTALLATION.md
├── README.md
├── Strategies/
│   ├── Hedge.cs               1670 lines — reinforced Apertura
│   └── OpenRange.cs           2200+ lines — reinforced AperturaBreakout
└── AddOns/NY930/
    ├── NY930AddOn.cs          NTAddOn entry + menu injection
    ├── NY930ShellView.cs      Shell + hamburger menu + view routing
    ├── NY930HomeView.cs       Landing page (Open Range + Hedge cards)
    ├── NY930OpenRangeView.cs  Live Open Range control + progress
    ├── NY930HedgeView.cs      Live Hedge control + progress
    ├── NY930SettingsView.cs   Settings (language + About)
    ├── NY930Bridge.cs         Strategy ⇄ AddOn snapshots + actions
    ├── NY930Logger.cs         NY930Log (Info/Warn/Error/Debug)
    ├── NY930Localization.cs   EN / ES + live language switching
    ├── NY930Theme.cs          Gold-on-black palette + helpers
    └── NY930Settings.cs       Persisted user preferences
```

## 11. Definition of done — Phase 1

| Item from `project_status&plan.md` | Status |
|---|---|
| Maintain Unmanaged + OCO behaviour | Done — original logic preserved verbatim. |
| Maintain partial fills, trailing, breakeven | Done — original logic preserved verbatim. |
| Fix Timeframe Change Issue (SL/TP loss) | Already correct in originals; preserved + verified through `Guardar/Restaurar Estado` paths. |
| Order reference loss / reconnection stability | Already correct in originals; preserved. |
| TP Gap Guard (ticks + time) | Done — both strategies. |
| SL Gap Guard (ticks + time) | Done — both strategies. |
| Single Stop Reverse-Tick Protection | Done — Open Range, default ticks = stop offset. |
| Order control from UI (move / spread / cancel / BE / partial close / flatten) | Done — wired through `NY930Bridge` actions. |
| WPF AddOn panel inside NinjaTrader, dockable | Done — `NTWindow` opened from `New → NY930`. |
| Homepage with NY930 branding + Open Range / Buy & Sell cards | Done — `NY930HomeView`. |
| Open Range panel | Done — `NY930OpenRangeView`. |
| Hedge panel | Done — `NY930HedgeView`. |
| Trade progress (TP1/TP2/TP/SL/PnL/duration/contracts) | Done — both views. |
| Trade result after close | Done — result panel inside both views. |
| Bilingual EN / ES | Done — `NY930Localization` + live switch. |
| Dark theme (gold/black) | Done — `NY930Theme`. |
| Performance: no UI thread blocking, Dispatcher / TriggerCustomEvent | Done — every order op marshalled via `TriggerCustomEvent`; UI updates via `Dispatcher.InvokeAsync`. |

---

## 9. Known follow-ups (intentionally out of Phase 1 scope)

- Persistent trade-history view & PnL chart — Phase 1 ships the live progress
  strip and the post-close result row only.
- Dock-as-chart-tab (true Chart Trader integration via `IChartTabFactory`) —
  Phase 1 ships the docked NTWindow which already snaps to chart edges.
- Licence binding / LemonSqueezy integration — Phase 2 of the proposal.
- Obfuscation / hardening — Phase 3.

These are documented here so they are not forgotten and can be planned
explicitly during the kick-off of the next phase.
