// ============================================================
//  NY930Bridge — Strategy ⇄ AddOn integration point
// ------------------------------------------------------------
//  - Strategies publish immutable snapshots of their state.
//  - Views subscribe to the snapshot events.
//  - Views push action requests; strategies poll a queue and
//    execute them on the NT thread via TriggerCustomEvent.
//
//  Why a static bridge: NinjaTrader instantiates strategies
//  inside its own AppDomain; AddOns live in the same AppDomain.
//  Static state is the cleanest way to wire them without
//  introducing a hard reference (so each strategy still
//  compiles and runs without the AddOn).
// ============================================================

#region Using declarations
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    // ─────────────────────────────────────────────────────────
    //  Snapshots — value-types crossing the strategy/UI border
    // ─────────────────────────────────────────────────────────

    public sealed class NY930OpenRangeSnapshot
    {
        public string   Instrument;
        public DateTime Timestamp;
        public double   TickSize;
        public DateTime EntryTime;       // Today's scheduled entry (for countdown)
        public int      TicksLong;       // Distance config (for parameter form)
        public int      TicksShort;
        public int      StopLossLongTicks;
        public int      StopLossShortTicks;
        public int      TakeProfitLongTicks;
        public int      TakeProfitShortTicks;
        public int      Partial1Ticks;
        public int      Partial2Ticks;
        public int      Partial1Contracts;
        public int      Partial2Contracts;
        public bool     EnablePartials;
        public bool     EnableBreakeven;
        public bool     EnableTrailing;
        public bool     EnableTrailingTP;
        public bool     EnableTimeExit;
        public bool     EnableTpGapGuard;
        public bool     EnableSlGapGuard;
        public int      TpGapGuardTicks;
        public int      SlGapGuardTicks;
        public bool     EnableSingleStopReverseProtection;
        public int      SingleStopReverseTicks;
        public bool     EnableLong;
        public bool     EnableShort;

        // Pending entry orders (Stops)
        public double LongEntryStopPrice;
        public double ShortEntryStopPrice;
        public bool   LongEntryWorking;
        public bool   ShortEntryWorking;

        // Active position (after fill)
        public bool   InLong;
        public bool   InShort;
        public double EntryFill;
        public int    ContractsRemaining;
        public int    Quantity;

        // Exit reference levels
        public double SlPrice;
        public double TpPrice;
        public double P1Price;
        public double P2Price;
        public bool   Partial1Done;
        public bool   Partial2Done;

        // Live PnL
        public double LastPrice;
        public double UnrealizedTicks;
        public double UnrealizedCurrency; // accurate $ value computed by the strategy

        // Trade lifecycle
        public DateTime TradeStartTime;
        public bool     SessionDone;

        // Last finished trade summary (null until first close)
        public NY930TradeResult LastResult;
    }

    public sealed class NY930HedgeSnapshot
    {
        public string   Instrument;
        public DateTime Timestamp;
        public double   TickSize;
        public DateTime EntryTime;
        public int      StopLossTicks;
        public int      TakeProfitTicks;
        public int      Partial1Ticks;
        public int      Partial2Ticks;
        public int      Partial1Contracts;
        public int      Partial2Contracts;
        public bool     EnablePartials;
        public bool     EnableBreakeven;
        public bool     EnableTrailing;
        public bool     EnableTrailingTP;
        public bool     EnableTimeExit;
        public bool     EnableTpGapGuard;
        public bool     EnableSlGapGuard;
        public int      TpGapGuardTicks;
        public int      SlGapGuardTicks;
        public string   Direction; // "Long" / "Short" / "None"
        public int      Quantity;
        public int      ContractsRemaining;

        public bool   InPosition;
        public double EntryFill;
        public double SlPrice;
        public double TpPrice;
        public double P1Price;
        public double P2Price;
        public bool   Partial1Done;
        public bool   Partial2Done;

        public double LastPrice;
        public double UnrealizedTicks;
        public double UnrealizedCurrency;

        public DateTime TradeStartTime;
        public bool     SessionDone;

        public NY930TradeResult LastResult;
    }

    public sealed class NY930TradeResult
    {
        public string   Strategy;       // "Hedge" or "OpenRange"
        public string   Instrument;
        public string   Side;           // "Long" / "Short"
        public DateTime EntryTime;
        public DateTime ExitTime;
        public double   EntryPrice;
        public double   ExitPrice;
        public int      Contracts;
        public double   PnLTicks;
        public double   PnLCurrency;
        public bool     P1Hit;
        public bool     P2Hit;
        public bool     TpHit;
        public bool     SlHit;
        public string   ExitReason;     // free-text tag from the strategy
    }

    // ─────────────────────────────────────────────────────────
    //  Action requests — UI → Strategy
    // ─────────────────────────────────────────────────────────

    public enum NY930ActionType
    {
        // Open Range
        OpenRangeMoveBoth,
        OpenRangeAdjustSpread,
        OpenRangeCancelAll,
        OpenRangeBuyNow,
        OpenRangeSellNow,
        OpenRangeFlatten,
        OpenRangeBreakeven,
        OpenRangeTrailingTrigger,
        OpenRangePartialClose,
        OpenRangeApplyParameters,    // bulk parameter update

        // Hedge
        HedgeBuyNow,
        HedgeSellNow,
        HedgeFlatten,
        HedgeBreakeven,
        HedgeTrailingTrigger,
        HedgePartialClose,
        HedgeCancelEntry,
        HedgeApplyParameters         // bulk parameter update
    }

    // Bulk parameter update payload shared by both strategies.
    // Null fields mean "leave unchanged".
    public sealed class NY930Parameters
    {
        public int?    EntryHour;
        public int?    EntryMinute;
        public int?    EntrySecond;
        public int?    Quantity;
        public int?    StopLossTicks;
        public int?    TakeProfitTicks;
        // Open Range only
        public bool?   EnableLong;
        public bool?   EnableShort;
        public int?    TicksLong;
        public int?    TicksShort;
        public int?    StopLossLongTicks;
        public int?    StopLossShortTicks;
        public int?    TakeProfitLongTicks;
        public int?    TakeProfitShortTicks;
        // Hedge only
        public string  Direction;          // "Long" / "Short" / "None" (null = unchanged)
        // Common toggles
        public bool?   EnableBreakeven;
        public bool?   EnableTrailing;
        public bool?   EnableTrailingTP;
        public bool?   EnablePartials;
        public bool?   EnableTimeExit;
        public bool?   EnableTpGapGuard;
        public bool?   EnableSlGapGuard;
        public int?    TpGapGuardTicks;
        public int?    SlGapGuardTicks;
        public bool?   EnableSingleStopReverseProtection;
        public int?    SingleStopReverseTicks;
    }

    public sealed class NY930Action
    {
        public NY930ActionType  Type;
        public int              IntArg;     // ticks / contracts depending on Type
        public string           StringArg;  // optional (e.g. instrument filter)
        public NY930Parameters  Parameters; // for *ApplyParameters actions
    }

    // ─────────────────────────────────────────────────────────
    //  Bridge
    // ─────────────────────────────────────────────────────────

    public static class NY930Bridge
    {
        private static readonly object _sync = new object();

        // Latest snapshots (one per strategy type — Phase 1: one chart = one strategy)
        private static NY930OpenRangeSnapshot _openRange;
        private static NY930HedgeSnapshot     _hedge;

        // Pending actions per strategy type — drained by the strategy.
        private static readonly ConcurrentQueue<NY930Action> _openRangeActions = new ConcurrentQueue<NY930Action>();
        private static readonly ConcurrentQueue<NY930Action> _hedgeActions     = new ConcurrentQueue<NY930Action>();

        public static event Action<NY930OpenRangeSnapshot> OpenRangeChanged;
        public static event Action<NY930HedgeSnapshot>     HedgeChanged;

        // Strategies signal their availability so the UI can grey-out
        // controls when no instance is running on any chart.
        private static int _openRangeAttached;
        private static int _hedgeAttached;

        public static bool OpenRangeAttached { get { lock (_sync) return _openRangeAttached > 0; } }
        public static bool HedgeAttached     { get { lock (_sync) return _hedgeAttached     > 0; } }

        public static event Action AttachmentChanged;

        public static void RegisterOpenRange()
        {
            lock (_sync) _openRangeAttached++;
            AttachmentChanged?.Invoke();
        }

        public static void UnregisterOpenRange()
        {
            lock (_sync) _openRangeAttached = Math.Max(0, _openRangeAttached - 1);
            AttachmentChanged?.Invoke();
        }

        public static void RegisterHedge()
        {
            lock (_sync) _hedgeAttached++;
            AttachmentChanged?.Invoke();
        }

        public static void UnregisterHedge()
        {
            lock (_sync) _hedgeAttached = Math.Max(0, _hedgeAttached - 1);
            AttachmentChanged?.Invoke();
        }

        // ── Snapshot publication ─────────────────────────────
        public static void PublishOpenRange(NY930OpenRangeSnapshot snap)
        {
            if (snap == null) return;
            lock (_sync) _openRange = snap;
            try { OpenRangeChanged?.Invoke(snap); }
            catch (Exception ex)
            { NY930Log.Error("Bridge", "OpenRangeChanged handler threw: " + ex.Message); }
        }

        public static void PublishHedge(NY930HedgeSnapshot snap)
        {
            if (snap == null) return;
            lock (_sync) _hedge = snap;
            try { HedgeChanged?.Invoke(snap); }
            catch (Exception ex)
            { NY930Log.Error("Bridge", "HedgeChanged handler threw: " + ex.Message); }
        }

        public static NY930OpenRangeSnapshot GetOpenRange() { lock (_sync) return _openRange; }
        public static NY930HedgeSnapshot     GetHedge()     { lock (_sync) return _hedge; }

        // ── Action queue ─────────────────────────────────────
        public static void RequestOpenRangeAction(NY930Action a)
        {
            if (a == null) return;
            _openRangeActions.Enqueue(a);
            NY930Log.Debug("Bridge", "Open Range action queued: " + a.Type + " (" + a.IntArg + ")");
        }

        public static void RequestHedgeAction(NY930Action a)
        {
            if (a == null) return;
            _hedgeActions.Enqueue(a);
            NY930Log.Debug("Bridge", "Hedge action queued: " + a.Type + " (" + a.IntArg + ")");
        }

        public static IEnumerable<NY930Action> DrainOpenRangeActions()
        {
            var list = new List<NY930Action>();
            NY930Action a;
            while (_openRangeActions.TryDequeue(out a)) list.Add(a);
            return list;
        }

        public static IEnumerable<NY930Action> DrainHedgeActions()
        {
            var list = new List<NY930Action>();
            NY930Action a;
            while (_hedgeActions.TryDequeue(out a)) list.Add(a);
            return list;
        }
    }
}
