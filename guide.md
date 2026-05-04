# NY930 User Guide

This is the operations manual for the NY930 platform. It assumes you already
have the files installed (if not, start with `INSTALLATION.md` first). Read
top to bottom on first install, then keep it as a reference for the
parameters and the new safety features.

---

## 1. What you got

The two strategies you sent (`hedge.cs` and `openrange.cs`) come back as
`Hedge.cs` and `OpenRange.cs`. The class names and namespaces are kept
intact (`Apertura` and `AperturaBreakout`) so any workspace, template or
saved chart that already references them keeps working without changes.

On top of that you get:

- Three new safety features (TP Gap Guard, SL Gap Guard, Single-Stop
  Reverse Cancel) wired directly into the strategy.
- A WPF AddOn called NY930 that opens from the Control Center's New menu
  and gives you live control over both strategies from one panel.
- Bilingual interface (English / Spanish) with live switching.
- Structured logging at three levels (Info / Warn / Error) so production
  issues don't get lost in the noise.

Nothing of the original trading logic was changed. Same Unmanaged + OCO
order routing, same Breakeven, same Trailing Stop, same Trailing TP, same
Partials with cumulative fill tracking, same Time Exit in three modes, same
state persistence across timeframe changes. The new code only adds
protections; it never replaces existing flow.

---

## 2. First time you open NinjaTrader

After the install, open NT and look at the Control Center's top menu.
Click **New**. The NY930 entry appears at the bottom of the list. Click
it. A new window opens with the NY930 wordmark and two cards: OPEN RANGE
and HEDGE.

If the NY930 entry is missing, you skipped step 7 of the install — close
NinjaTrader completely (not just the Control Center) and reopen it. The
AddOn registers its menu item when NT starts; existing windows from before
the compile won't show it.

A small status section at the bottom of the home page tells you which of
the two strategies is currently attached to a chart:

```
Open Range: No
Hedge: No
```

Both say No until you attach a strategy to a chart.

---

## 3. Attaching a strategy to a chart

The strategies still belong to a chart, the way they always did. The AddOn
panel just talks to whichever instance is running.

1. Open a chart on your instrument.
2. Right-click the chart background, choose **Strategies** (or press
   **Ctrl+S**).
3. From the Available list, pick `AperturaBreakout` (the Open Range
   strategy) or `Apertura` (the Hedge strategy). Click **Add**.
4. Configure the parameters in the right pane (see sections 6 and 7
   below).
5. Scroll down to the **Setup** section, set **Enabled** to True.
6. Click **OK**.

The chart's strategy bar at the top should now show
`AperturaBreakout(...)` (or `Apertura(...)`) without a `(D)` prefix. The
NY930 panel's bottom indicator flips from `Open Range: No` to
`Open Range: Yes` (or the equivalent for Hedge) within about a second. If
the indicator stays at No for more than 5 seconds, the strategy is still
in Historical replay — see the troubleshooting section.

---

## 4. The new safety features

These are the three additions you specifically asked for. Each one is
opt-in and configured per strategy through the standard NinjaScript
parameters dialog.

### 4.1 TP Gap Guard

Group: `9. NY930 Gap Guards` (Hedge) or `10. NY930 Gap Guards` (Open
Range).

Two protections that fire independently:

- **Tick guard.** If the price moves past the working TP by
  `TpGapGuardTicks` ticks while the limit order has not filled, the
  position is closed at market immediately. Set the value to 0 to disable
  the tick check while keeping the time check active.
- **Time guard.** If the price has been past the TP for
  `TpGapGuardSeconds` seconds without a fill, same market exit. Set to 0
  to disable.

Default values are 3 ticks and 2 seconds. The guard latches once per
trade so you can't trigger it twice on the same position. The market exit
is tagged `TP_GAP_TICKS` or `TP_GAP_TIME` in the order log so you can tell
which check fired.

### 4.2 SL Gap Guard

Mirror of the TP guard, on the SL side. Same fields with `Sl` prefix
instead of `Tp`. Same defaults (3 ticks / 2 seconds). Same single-fire
latch. Market exit tag is `SL_GAP_TICKS` or `SL_GAP_TIME`.

### 4.3 Single-Stop Reverse Cancel (Open Range only)

Group: `11. Single-Stop Reverse`.

Active only when exactly one of `Habilitar Long` or `Habilitar Short` is
checked. While the entry Stop is still pending (no fill yet), if the price
moves against the entry by N ticks the strategy cancels the order
automatically.

- `Habilitar Single-Stop Reverse Cancel`: enable / disable.
- `Ticks en contra (0 = usar offset)`: how many ticks against the anchor
  trigger the cancel. Default 0 means "use the same offset as the entry"
  (so 40 ticks if `TicksLong` or `TicksShort` is 40, matching the spec
  you wrote). Override to a smaller number for tighter protection.

The anchor is the price recorded at the moment the entry was placed. The
guard fires once per session and logs the actual anchor, current price and
threshold so you can review the call after the fact.

---

## 5. The NY930 panel

Open it from Control Center → New → NY930. The panel has three views:
home, Open Range, Hedge. Settings is reachable from the hamburger menu in
the top right.

### 5.1 Home

Two cards. Click OPEN RANGE to manage an attached `AperturaBreakout`
instance, click HEDGE to manage an attached `Apertura` instance. The
status footer shows which strategy is currently attached.

### 5.2 Open Range view

This view is what the original `OpenRangeControl.cs` did, rebuilt into the
NY930 theme and connected to the real `AperturaBreakout` strategy.

- **Status banner.** Tells you what the strategy is doing right now:
  waiting for the entry time, orders working, in long position, in short
  position, session done.
- **BUY STOP / Spread / SELL STOP.** Live read-out of the two pending
  stops and the gap between them in ticks. Empty until the strategy fires.
- **MOVE BOTH ▲▼.** Move both stops up or down by N ticks. Distance
  between them stays constant. The chips below the value (1, 5, 10, 25)
  pick the step size.
- **SPREAD + −.** Widen or narrow the gap between the two stops while
  keeping the midpoint constant. Same chip selector.
- **CANCEL ORDERS.** Cancels both pending stops. The other side cancels
  itself by OCO almost instantly.
- **BUY NOW / SELL NOW.** Manual market entry that overrides the
  scheduled entry (only valid before the timer fires).
- **CLOSE POSITION.** Flatten the open position. Cancels SL/TP/parciales
  and submits a market exit.
- **Partial close.** Type a contract count in the box and click the
  button to close that many contracts at market. The SL is reduced
  automatically through the same path the strategy uses for parciales.
- **Trade progress strip.** Live read-out of TP1, TP2, TP, SL, PnL
  in ticks, contracts remaining and trade duration.
- **Result box.** Appears after the first trade closes. Shows ticks won
  or lost, currency PnL, entry and exit prices, and the reason the trade
  closed (TP, SL, time exit, manual, gap guard).

### 5.3 Hedge view

Direct entry strategy. The view matches the Open Range view structure:
same status banner, same trade progress strip, same result box.

The action buttons differ because there is no pending range to manage:

- **BUY NOW / SELL NOW.** Place the market order immediately, override
  the scheduled time.
- **Cancel pending entry.** If the timer is armed and you want to abort
  before it fires.
- **Move SL → Breakeven.** Manually move the working SL to the entry
  price (offset by `BreakevenOffsetTicks`).
- **CLOSE POSITION + Partial close.** Same as Open Range.

The strategy parameters (entry time, quantity, SL/TP ticks, Breakeven
config, Trailing config, Partials, Time Exit, Gap Guards) are configured
through the standard NinjaScript dialog the way you've always done it.
The panel only exposes live actions.

### 5.4 Settings

Hamburger menu → Settings. Two options:

- Language: English or Spanish. Click and the entire UI refreshes in
  place. The choice is written to
  `Documents\NinjaTrader 8\NY930\ny930.settings` and survives NT
  restarts.
- About box.

---

## 6. Hedge strategy reference (Apertura)

All parameters from the original strategy are preserved. New ones are in
group 9.

| Group | Parameter | Default | Notes |
|---|---|---|---|
| 1. Horario | EntryHour / Minute / Second | 9 / 29 / 58 | High-precision timer fires here |
| 2. General | Quantity | 15 | Total contracts |
| 2. General | StopLossTicks | 90 | Distance from fill price |
| 2. General | TakeProfitTicks | 61 | Distance from fill price |
| 3. Operacion | Direccion | SinOperacion | Long / Short / SinOperacion |
| 4. Breakeven | EnableBreakeven, TriggerTicks, OffsetTicks | off / 30 / 2 | Same logic as before |
| 5. Trailing Stop | Enable, TriggerTicks, StepTicks | off / 35 / 2 | Step trail, broker-confirmed |
| 6. Trailing TP | Enable, DistanceTicks, TimeoutSeconds | off / 4 / 2 | Tracks extreme + market exit on timeout |
| 7. Parciales | Enable, P1/P2 ticks and contracts | off / 30,3,50,3 | Cumulative fill tracking + SL reduction |
| 8. Salida por Tiempo | Enable, MinDurationSeconds, ExitMode, CloseIfBeyondTP | off / 10 / PlaceTPAfterTime / true | Three modes preserved |
| **9. NY930 Gap Guards** | EnableTpGapGuard / Ticks / Seconds | on / 3 / 2 | New |
| **9. NY930 Gap Guards** | EnableSlGapGuard / Ticks / Seconds | on / 3 / 2 | New |

---

## 7. Open Range strategy reference (AperturaBreakout)

Same as Hedge for groups 1-9 (with Long and Short configured per side
under groups 3 and 4 instead of a single Direccion enum). Group 10
contains the Gap Guards. Group 11 contains the Single-Stop Reverse.

| Group | Parameter | Default | Notes |
|---|---|---|---|
| 1. Horario | EntryHour / Minute / Second | 9 / 29 / 58 | |
| 2. General | Quantity | 10 | |
| 3. Long | Habilitar, Ticks, StopLoss, TakeProfit | true / 40 / 90 / 61 | BuyStop above price |
| 4. Short | Habilitar, Ticks, StopLoss, TakeProfit | true / 40 / 90 / 61 | SellStop below price |
| 5. Breakeven, 6. Trailing Stop, 7. Parciales, 8. Trailing TP, 9. Salida por Tiempo | same as Hedge | | |
| **10. NY930 Gap Guards** | TP/SL Gap Guard fields | on / 3 / 2 | New |
| **11. Single-Stop Reverse** | Habilitar, Ticks en contra | on / 0 (= use entry offset) | New |

When both Long and Short are enabled the entries share an OCO group, so
the side that fills first cancels the other automatically. When only one
side is enabled the Single-Stop Reverse guard becomes active.

---

## 8. Logs and where to read them

The structured logger writes everything to NT's NinjaScript Output (Output
1 tab). Lines look like:

```
[19:50:58.031][INFO ][AperturaBreakout] Ordenes colocadas
[19:50:58.243][INFO ][AperturaBreakout]   BuyStop  (Long)   : 7260  (+40 ticks)
[19:50:58.243][INFO ][AperturaBreakout]   SellStop (Short)  : 7240  (-40 ticks)
[19:50:58.243][INFO ][AperturaBreakout]   OCO Entrada       : ENTRY_4bd5c138 (compartido)
```

Warnings and errors are duplicated to the Control Center's Log tab so
issues stand out:

```
[WARN ][AperturaBreakout] TP GAP GUARD disparado (LONG)
[WARN ][AperturaBreakout]   TP            : 7321
[WARN ][AperturaBreakout]   Precio actual : 7325 (over 4 ticks)
[WARN ][AperturaBreakout]   Motivo        : ticks
```

User preferences sit in `Documents\NinjaTrader 8\NY930\ny930.settings`.
That's a small text file you can delete to reset to defaults.

---

## 9. Quick verification you can run in 10 minutes (sim mode)

1. Connect to your simulation account. Open a chart on a liquid contract
   (ES, NQ, MES). Bar interval doesn't matter; 30-second is fine.
2. Open the chart's Data Series settings (Ctrl+F). Drop **Days to load**
   to 1. This makes Historical replay fast.
3. Open the Strategies dialog (Ctrl+S). Add `AperturaBreakout`. Set the
   entry time 2-3 minutes ahead of your clock. Set Enabled = True. Click
   OK.
4. Watch the NY930 panel. Within ~5 seconds the bottom indicator flips to
   `Open Range: Yes`.
5. At the scheduled time the strategy places BuyStop and SellStop. Both
   orders appear on the chart and in Control Center → Orders. The panel's
   BUY STOP / Spread / SELL STOP fields populate with live values.
6. Click the ▲ button under MOVE BOTH. Both stop prices increase by the
   chip value (default 5 ticks). Verify in the Orders tab.
7. Click + under SPREAD. Distance widens by the chip value. Midpoint
   stays constant.
8. Click CANCEL ORDERS. Both stops cancel within a second.

If all eight steps pass, the integration is fully working on your
machine.

---

## 10. Troubleshooting

**The NY930 menu item didn't appear after install.**
You compiled while NT was running but didn't restart it afterward. The
AddOn registers its menu item in `OnWindowCreated`, which only fires for
windows opened after the assembly was loaded. Close NT completely and
reopen.

**The panel says `Open Range: No` but the strategy is enabled.**
The strategy is still in Historical replay. With many days of data
loaded, replay can take 30-60 seconds. Either wait or reduce **Days to
load** in the Data Series settings.

**The panel says `Open Range: No` and Historical is not the issue.**
Open Control Center → Log tab. Look for any orange or red rows mentioning
`AperturaBreakout`. Most likely a configuration error (Partials values
out of range, etc.). The error message tells you what to fix.

**NinjaScript Output is empty even though the strategy is running.**
NT sometimes filters output during rapid state transitions (disable /
re-enable cycles). Check the Log tab — warnings and errors are mirrored
there. For info-level lines, click into Output 1, scroll to the top.

**`Accion ignorada — ordenes no activas en ambos lados` warnings.**
Not a bug. You clicked Move/Spread faster than the broker confirmed the
previous change. The race-condition guard refuses the second click
instead of corrupting the order state. Wait half a second between
clicks.

**The strategy keeps restoring stale state from a previous session.**
The state lives in the C# process's static memory. It clears on NT
restart or on calendar day rollover. Cancel any leftover orders, close
NT, reopen.

**Strategy lost connection and restarted automatically.**
That's NT's auto-recovery. My `RestaurarEstado` method resubmits the
SL/TP/parciales at the same prices once the connection comes back.
You'll see `Estado restaurado tras cambio de temporalidad` in the log.

---

## 11. What is not in this delivery

This is Phase 1. The following items are explicitly out of scope and
belong to Phases 2 and 3 of the proposal:

- License server with key generation and machine binding
- LemonSqueezy webhook integration
- Web admin panel for license management
- Hybrid client/server architecture with strategy logic on the server
- DLL obfuscation and anti-tamper hardening

The Personal Version mentioned in the proposal as a Phase 1 bonus is
identical to what's in this repo, because there are no licensing checks
to remove yet.
