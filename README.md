# NY930 — Phase 1 Delivery

NY930 is a NinjaTrader 8 trading workspace that unifies the client's
two strategies (Open Range and Hedge) under a single themed control
plane built for the 9:30 NY open.

## What is in this folder

| File | Purpose |
|---|---|
| `PROGRESS_LOG.md` | Audit trail. Read this first — it lists every analysis decision, every gap discovered, and every change made. |
| `INSTALLATION.md` | Step-by-step install + verification guide. |
| `Strategies/Hedge.cs` | Reinforced replacement for `hedge.cs`. |
| `Strategies/OpenRange.cs` | Reinforced replacement for `openrange.cs`. |
| `AddOns/NY930/` | The NY930 WPF AddOn (single-folder install). |

## What changed vs. the originals

The original strategies are technically solid — Unmanaged + OCO,
manual SL/TP, BE / Trailing / Trailing TP, Partials with
broker-confirmed quantity reduction, Time Exit (3 modes),
state persistence across timeframe changes, polling retry of SL
contract reduction. **All of that is preserved.**

Phase 1 adds the safety net the client asked for in `chat.md`:

1. **TP Gap Guard** — close at market when price crosses the TP by
   N ticks or stays beyond it for Y seconds without filling.
2. **SL Gap Guard** — same protection on the stop side.
3. **Single-Stop Reverse-Tick Protection** (Open Range only) —
   cancel the lone Buy/Sell Stop if price moves against the entry
   by N ticks before it triggers (default N = the order's own stop
   offset).
4. **Structured logger** — every line is now prefixed by level
   (Info / Warn / Error) and warnings/errors are mirrored to the NT
   Log window where production issues belong.

…and the platform brief everyone agreed on:

5. **NY930 AddOn** — gold-on-black themed WPF panel that opens from
   any window's `New → NY930` menu, drives both strategies live,
   shows the trade progress (TP1 / TP2 / TP / SL / PnL / contracts /
   duration) and renders the trade result after each close.
6. **EN / ES bilingual** — every visible string is bound to a
   dictionary; switching language in Settings refreshes the UI in
   place. Preference persisted to disk.
7. **Live order control from the UI** — Move both stops, adjust the
   spread, cancel everything, manual Buy Now / Sell Now, Close
   position, partial close, Move SL → Breakeven. All thread-safe
   via `TriggerCustomEvent`.

## Out of Phase 1 scope (intentional)

- Persistent trade history view & PnL chart.
- Chart Trader-style per-chart docking via `IChartTabFactory`.
- Licensing / LemonSqueezy integration (Phase 2).
- Obfuscation / hardening (Phase 3).

## Quickstart

1. Drop `Strategies/*.cs` into `Documents\NinjaTrader 8\bin\Custom\Strategies\`.
2. Drop `AddOns/NY930/` into `Documents\NinjaTrader 8\bin\Custom\AddOns\`.
3. Compile (F5 in the NinjaScript Editor).
4. Restart NinjaTrader 8.
5. **New → NY930** opens the panel. Add `Apertura` / `AperturaBreakout`
   to a chart and Enable. The panel lights up automatically.

Full instructions, verification recipes and uninstall steps are in
`INSTALLATION.md`. Full audit trail (what was found, what was
decided, what was built, in this order) is in `PROGRESS_LOG.md`.
