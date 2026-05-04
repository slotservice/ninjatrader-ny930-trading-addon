# NY930 — Installation Guide (Phase 1)

This guide walks through installing the reinforced strategies and the
NY930 AddOn into a clean NinjaTrader 8 environment.

> **Tested with** NinjaTrader 8.1.x. The AddOn relies only on public
> NT8 APIs (NTAddOn, NTWindow, NTMenuItem) and standard WPF.

---

## 1. Files in this delivery

```
NY930/
├── PROGRESS_LOG.md            — full audit trail of analysis + changes
├── INSTALLATION.md            — this file
├── README.md                  — one-page overview
├── Strategies/
│   ├── Hedge.cs               — reinforced Apertura
│   └── OpenRange.cs           — reinforced AperturaBreakout
└── AddOns/NY930/
    ├── NY930AddOn.cs          — entry point (registers menu item)
    ├── NY930ShellView.cs      — main panel container + hamburger menu
    ├── NY930HomeView.cs       — landing page (Open Range / Hedge cards)
    ├── NY930OpenRangeView.cs  — Open Range live control panel
    ├── NY930HedgeView.cs      — Hedge live control panel
    ├── NY930SettingsView.cs   — Settings (language, About)
    ├── NY930Bridge.cs         — strategy ⇄ AddOn integration
    ├── NY930Logger.cs         — structured logging (Info/Warn/Error)
    ├── NY930Localization.cs   — EN / ES dictionaries
    ├── NY930Theme.cs          — gold-on-black palette + WPF helpers
    └── NY930Settings.cs       — persisted user preferences
```

---

## 2. Where each file goes

NinjaTrader compiles everything under
`Documents\NinjaTrader 8\bin\Custom`. The folder structure inside
`Custom/` mirrors namespaces — keep it intact.

| Source file | Destination on disk |
|---|---|
| `Strategies/Hedge.cs`      | `bin\Custom\Strategies\Hedge.cs` |
| `Strategies/OpenRange.cs`  | `bin\Custom\Strategies\OpenRange.cs` |
| `AddOns/NY930/*.cs`        | `bin\Custom\AddOns\NY930\` (whole folder) |

> **Replace** the old `hedge.cs` and `openrange.cs` if they were
> imported earlier — the class names (`Apertura` and
> `AperturaBreakout`) and namespaces are intentionally preserved so
> existing NT8 workspaces keep working.

If you previously installed the proof-of-concept
`OpenRangeControl.cs`, you can leave it or remove it — the AddOn
supersedes it.

---

## 3. Compile order

Open NinjaTrader 8, then do **one** of the following:

### Option A — In-app NinjaScript Editor

1. Open **New → NinjaScript Editor**.
2. In the *Solution Explorer* tab on the left, right-click the root
   solution and choose **Compile**. NinjaTrader compiles the entire
   `NinjaTrader.Custom.dll` in one pass; the order between
   `AddOns/`, `Strategies/`, etc. is handled automatically.
3. Wait for the *NinjaScript compilation* window to report
   `0 errors, 0 warnings`. Any compile error is shown in the
   *Errors* tab.

### Option B — Force a rebuild

If you just dropped the files into the folder while NinjaTrader was
running, the auto-watcher usually recompiles within a few seconds.
You can force it with **F5** in the NinjaScript Editor or by
pressing **Compile** in the editor toolbar.

---

## 4. Loading the AddOn

After a successful compile:

1. Restart NinjaTrader (the AddOn registers in
   `OnWindowCreated`, which only fires for windows opened **after**
   the assembly is loaded).
2. Open any chart or the Control Center.
3. Click **New** in the top menu — you should see a
   **NY930** entry. Click it.
4. The NY930 home window appears (gold + black, NY930 wordmark, two
   cards).

If the menu entry is missing, open *Control Center → Tools → NinjaScript Output*
and look for a line like `[INFO ][NY930][AddOn] NY930 AddOn active.`
to confirm the AddOn started.

---

## 5. Running the strategies

The AddOn does not start strategies on its own — that is intentional
(per chat.md: one chart = one strategy, started from the chart's
strategy panel). The flow is:

1. Open a chart on the instrument you want to trade.
2. Add the strategy: **Strategies → Apertura** (Hedge) or
   **AperturaBreakout** (Open Range).
3. Configure the parameters (entry time, qty, SL/TP ticks, gap
   guards, single-stop reverse, etc.). Defaults are sane for an ES
   futures contract on the 9:30 NY open.
4. Press **Enable**. The strategy logs `Estrategia lista` and arms
   its high-precision timer.
5. The NY930 panel automatically starts receiving live snapshots
   and lights up the **Open Range:** / **Hedge:** indicators on the
   home screen.

You can now use the buttons on the NY930 panel:

- **Open Range view**:
  - Move both stops ▲▼ (1/5/10/25 tick chips).
  - Adjust spread ←→ (1/5/10/25 tick chips).
  - **Cancel orders**, **Buy now**, **Sell now**, **Close
    position**, **Partial close**.
- **Hedge view**:
  - **Buy now** / **Sell now** (overrides scheduled timer).
  - **Cancel pending entry**, **Move SL → Breakeven**, **Close
    position**, **Partial close**.

All live actions cross the strategy/UI boundary via
`TriggerCustomEvent` — the broker call always runs on the NT8
thread, never on the WPF dispatcher.

---

## 6. Switching language

Hamburger menu → **Settings** → choose **English** or **Spanish**.
Every visible string refreshes instantly. The choice is persisted
to:

```
Documents\NinjaTrader 8\NY930\ny930.settings
```

Delete the file to reset to defaults.

---

## 7. Verifying the new safety features

### TP / SL Gap Guard (both strategies)
- Set tighter values for testing: `TpGapGuardTicks = 1`,
  `TpGapGuardSeconds = 1`. Force the price to overshoot the TP in
  Sim. The strategy logs `[WARN ] TP GAP GUARD disparado` and
  closes at market within one tick.
- Same procedure for the SL guard.

### Single-Stop Reverse-Tick Protection (Open Range)
- Disable Long, keep Short on (`EnableLong = false`,
  `EnableShort = true`).
- Wait for the SellStop to be placed at 09:29:58.
- Push the price up against the entry (the strategy logs
  `[WARN ] Single-Stop Reverse — cancelando SELL STOP`).
- Default threshold is the order's own offset (`TicksShort`); set
  `SingleStopReverseTicks` to override.

---

## 8. Uninstall

Delete the files in:

- `bin\Custom\Strategies\Hedge.cs`
- `bin\Custom\Strategies\OpenRange.cs`
- `bin\Custom\AddOns\NY930\` (entire folder)

Then **Compile** in the NinjaScript Editor. NinjaTrader removes the
compiled types and the menu item disappears on next window open.

User preferences live in `Documents\NinjaTrader 8\NY930\` — delete
that folder if you want a clean slate.
