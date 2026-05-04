// ============================================================
//  Apertura — NY930 reinforced edition
//  ------------------------------------------------------------
//  Drop-in replacement for the original hedge.cs (class name and
//  namespace preserved for backwards compatibility with existing
//  workspaces).
//
//  Original feature set (UNCHANGED):
//    - SubmitOrderUnmanaged + OCO
//    - Market entry at fixed time via System.Threading.Timer
//    - Long / Short / SinOperacion
//    - Breakeven (trigger + offset, broker-confirmed)
//    - Trailing Stop (stepwise, broker-confirmed)
//    - Trailing TP (extreme tracking + timeout to market)
//    - Partials P1 + P2 (cumulative fill tracking, SL contract reduction)
//    - Time Exit (CloseAlways / CloseIfPositive / PlaceTPAfterTime)
//    - Persistence between timeframe changes via static fields
//    - Polling retry of SL contract reduction every 300 ms
//
//  NY930 additions:
//    1. Structured logger (NY930Log: Info / Warn / Error / Debug)
//    2. TP Gap Guard  (ticks + seconds) — close at market when
//       lastPrice crosses TP by N ticks, or stays beyond TP for
//       Y seconds without filling.
//    3. SL Gap Guard  (ticks + seconds) — same protection on SL.
//    4. NY930Bridge integration: publishes a snapshot every tick
//       and consumes Buy Now / Sell Now / Flatten / BE / Trailing
//       / Partial Close / Cancel actions sent from the AddOn UI.
//
//  *** Solo modo REAL-TIME. NO optimizable. ***
// ============================================================

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.AddOns.NY930;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    // ── Enum direccion de entrada ─────────────────────────────────
    public enum DireccionEntrada
    {
        [Display(Name = "1 - Tipo de Operacion")]
        SinOperacion,

        [Display(Name = "2 - Long")]
        Long,

        [Display(Name = "3 - Short")]
        Short
    }

    // ── Modos de salida por tiempo ────────────────────────────────
    public enum TimeExitMode { CloseAlways, CloseIfPositive, PlaceTPAfterTime }

    public class Apertura : Strategy
    {
        // Source tag for the structured logger
        private const string SRC = "Apertura";

        // ── Referencias a ordenes ────────────────────────────────
        private Order entryOrder  = null;
        private Order slOrder     = null;
        private Order tpOrder     = null;
        private Order p1Order     = null;
        private Order p2Order     = null;

        // ── OCO IDs ──────────────────────────────────────────────
        private string ocoExit = string.Empty;

        // ── Control de estado ────────────────────────────────────
        private bool     ordersPlaced     = false;
        private bool     exitOrdersPlaced = false;
        private bool     sessionDone      = false;
        private DateTime lastDate         = DateTime.MinValue;

        // ── Precios ──────────────────────────────────────────────
        private double lastPrice    = 0;
        private double entryFill    = 0;

        // ── Precios activos de SL y TP ────────────────────────────
        private double currentSlPrice  = 0;
        private double currentTpPrice  = 0;
        private double currentP1Price  = 0;
        private double currentP2Price  = 0;

        // ── Breakeven ────────────────────────────────────────────
        private bool   breakevenApplied = false;
        private bool   breakevenSent    = false;
        private double bePricePending   = 0;

        // ── Trailing Stop ─────────────────────────────────────────
        private bool   trailActive     = false;
        private bool   trailSent       = false;
        private double trailCurrentSl  = 0;
        private double trailPreviousSl = 0;

        // ── Trailing TP ───────────────────────────────────────────
        private bool     tpTrailActive   = false;
        private double   tpTrailExtreme  = 0;
        private DateTime tpCrossedTime   = DateTime.MinValue;
        private bool     tpTimeoutFired  = false;

        // ── Salida por Tiempo ─────────────────────────────────────
        private bool     tpRetained      = false;
        private double   retainedTP      = 0;
        private int      retainedTpQty   = 0;
        private DateTime tradeStartTime  = DateTime.MinValue;
        private bool     timeExitFired   = false;
        private Order    delayedTpOrder  = null;
        private DateTime _timeCheckRetry = DateTime.MinValue;

        // ── Parciales ─────────────────────────────────────────────
        private bool partial1Done       = false;
        private bool partial2Done       = false;
        private int  contratosRestantes = 0;
        private bool closingPosition    = false;
        private int  partial1FilledQty  = 0;
        private int  partial2FilledQty  = 0;

        // ── Control reduccion SL ──────────────────────────────────
        private bool     slChangePending = false;
        private int      slTargetQty     = 0;
        private DateTime _slRetryTime    = DateTime.MinValue;

        // ── Timer de entrada ──────────────────────────────────────
        private Timer _entryTimer = null;

        // ── NY930 Gap Guard state ─────────────────────────────────
        // Tick-based: latched once per side per trade.
        private bool     tpGapGuardFired   = false;
        private bool     slGapGuardFired   = false;
        // Time-based: track when the price first crossed the level
        // without filling, to compute the elapsed-overshoot duration.
        private DateTime tpOvershootSince  = DateTime.MinValue;
        private DateTime slOvershootSince  = DateTime.MinValue;
        // Throttle the bridge snapshot to avoid spamming the UI thread.
        private DateTime _lastSnapshotPush = DateTime.MinValue;

        // ── NY930 Last finished trade ─────────────────────────────
        private NY930TradeResult _lastResult;

        // ── Estado persistente (static) ───────────────────────────
        private static bool   _saveOrdersPlaced  = false;
        private static bool   _saveExitPlaced    = false;
        private static bool   _saveSessionDone   = false;
        private static double _saveFillPrice     = 0;
        private static double _saveSlStop        = 0;
        private static double _saveTpLimit       = 0;
        private static double _saveP1Price       = 0;
        private static double _saveP2Price       = 0;
        private static int    _saveContratos     = 0;
        private static bool   _savePartial1Done  = false;
        private static bool   _savePartial2Done  = false;
        private static int    _savePartial1Filled = 0;
        private static int    _savePartial2Filled = 0;
        private static DateTime _saveTradeStart    = DateTime.MinValue;
        private static bool     _saveTpRetained    = false;
        private static double   _saveRetainedTP    = 0;
        private static int      _saveRetainedQty   = 0;
        private static bool     _saveTimeExitFired = false;

        // ────────────────────────────────────────────────────────
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Entrada a mercado a hora fija. Long / Short. BE, Trail, Parciales, Salida por Tiempo + NY930 Gap Guards.";
                Name        = "Apertura";

                EntryHour   = 9;
                EntryMinute = 29;
                EntrySecond = 58;

                Quantity        = 15;
                StopLossTicks   = 90;
                TakeProfitTicks = 61;
                Direccion       = DireccionEntrada.SinOperacion;

                EnableBreakeven       = false;
                BreakevenTriggerTicks = 30;
                BreakevenOffsetTicks  = 2;

                EnableTrailing    = false;
                TrailTriggerTicks = 35;
                TrailStepTicks    = 2;

                EnableTrailingTP         = false;
                TrailingTPDistanceTicks  = 4;
                TrailingTPTimeoutSeconds = 2;

                EnablePartials    = false;
                Partial1Ticks     = 30;
                Partial1Contracts = 3;
                Partial2Ticks     = 50;
                Partial2Contracts = 3;

                EnableTimeExit     = false;
                MinDurationSeconds = 10;
                ExitMode           = TimeExitMode.PlaceTPAfterTime;
                CloseIfBeyondTP    = true;
                MinProfitTicks     = 1;

                // ── NY930 Gap Guards (default ON, sane values) ───
                EnableTpGapGuard   = true;
                TpGapGuardTicks    = 3;
                TpGapGuardSeconds  = 2;
                EnableSlGapGuard   = true;
                SlGapGuardTicks    = 3;
                SlGapGuardSeconds  = 2;

                Calculate                    = Calculate.OnEachTick;
                IsUnmanaged                  = true;
                IsExitOnSessionCloseStrategy = true;
                BarsRequiredToTrade          = 0;
            }

            else if (State == State.Realtime)
            {
                // Wire the structured logger to NinjaScript output
                NY930Log.PrintSink = msg => Print(msg);
                NY930Log.LogSink   = (msg, lvl) => Log(msg, lvl);
                NY930Bridge.RegisterHedge();

                if (EnablePartials)
                {
                    bool hayError = false;

                    if (Partial1Ticks <= 0 || Partial1Contracts <= 0)
                    {
                        NY930Log.Separator(SRC);
                        NY930Log.Error(SRC, "Parciales: Partial1Ticks y Partial1Contracts deben ser > 0.");
                        NY930Log.Separator(SRC);
                        hayError = true;
                    }

                    if (!hayError && Partial1Contracts >= Quantity)
                    {
                        NY930Log.Separator(SRC);
                        NY930Log.Error(SRC, "Parciales: Partial1Contracts (" + Partial1Contracts
                              + ") debe ser < Quantity (" + Quantity + ").");
                        NY930Log.Separator(SRC);
                        hayError = true;
                    }

                    bool usaP2 = Partial2Ticks > 0 && Partial2Contracts > 0;
                    if (!hayError && usaP2)
                    {
                        if (Partial1Contracts + Partial2Contracts >= Quantity)
                        {
                            NY930Log.Separator(SRC);
                            NY930Log.Error(SRC, "Parciales: P1+P2 contratos ("
                                  + (Partial1Contracts + Partial2Contracts)
                                  + ") debe ser < Quantity (" + Quantity + ").");
                            NY930Log.Separator(SRC);
                            hayError = true;
                        }

                        if (!hayError && Partial2Ticks <= Partial1Ticks)
                        {
                            NY930Log.Separator(SRC);
                            NY930Log.Error(SRC, "Parciales: Partial2Ticks (" + Partial2Ticks
                                  + ") debe ser mayor que Partial1Ticks (" + Partial1Ticks + ").");
                            NY930Log.Separator(SRC);
                            hayError = true;
                        }
                    }

                    if (hayError)
                    {
                        NY930Log.Error(SRC, "Estrategia DETENIDA por error en configuracion de Parciales.");
                        return;
                    }
                }

                RestaurarEstado();

                DateTime now       = DateTime.Now;
                DateTime entryTime = new DateTime(now.Year, now.Month, now.Day,
                                                  EntryHour, EntryMinute, EntrySecond);

                if (ordersPlaced)
                {
                    if (lastDate == DateTime.MinValue)
                        lastDate = now.Date;

                    NY930Log.Separator(SRC);
                    NY930Log.Info(SRC, "Estado restaurado tras cambio de temporalidad.");
                    if (exitOrdersPlaced)
                        NY930Log.Info(SRC, "  Estado: posicion abierta — SL/TP resubmitidos.");
                    else
                        NY930Log.Info(SRC, "  Estado: orden de entrada pendiente resubmitida.");
                    NY930Log.Separator(SRC);
                }
                else if (now > entryTime)
                {
                    lastDate     = now.Date;
                    ordersPlaced = true;
                    sessionDone  = true;

                    NY930Log.Separator(SRC);
                    NY930Log.Warn(SRC, "Estrategia iniciada DESPUES de la hora de entrada ("
                          + entryTime.ToString("HH:mm:ss") + "). Sesion bloqueada hasta manana.");
                    NY930Log.Separator(SRC);
                }
                else
                {
                    bool usaP2 = EnablePartials && Partial2Ticks > 0 && Partial2Contracts > 0;
                    lastDate = now.Date;

                    NY930Log.Info(SRC, "Estrategia lista. Esperando hora de entrada: "
                          + entryTime.ToString("HH:mm:ss"));
                    NY930Log.Info(SRC, "  Direccion        : " + Direccion);
                    NY930Log.Info(SRC, "  Stop Loss        : " + StopLossTicks   + " ticks");
                    NY930Log.Info(SRC, "  Take Profit      : " + TakeProfitTicks + " ticks");
                    NY930Log.Info(SRC, "  Breakeven        : " + (EnableBreakeven
                        ? "SI  (trigger=" + BreakevenTriggerTicks + " ticks, offset=" + BreakevenOffsetTicks + " ticks)"
                        : "NO"));
                    NY930Log.Info(SRC, "  Trailing Stop    : " + (EnableTrailing
                        ? "SI  (trigger=" + TrailTriggerTicks + " ticks, paso=" + TrailStepTicks + " ticks)"
                        : "NO"));
                    NY930Log.Info(SRC, "  Trailing TP      : " + (EnableTrailingTP
                        ? "SI  (dist=" + TrailingTPDistanceTicks + " ticks, timeout=" + TrailingTPTimeoutSeconds + "s)"
                        : "NO"));
                    NY930Log.Info(SRC, "  Parciales        : " + (EnablePartials
                        ? "SI  P1=" + Partial1Ticks + "t/" + Partial1Contracts + "c"
                          + (usaP2 ? " | P2=" + Partial2Ticks + "t/" + Partial2Contracts + "c" : " | P2=no")
                        : "NO"));
                    NY930Log.Info(SRC, "  Salida Tiempo    : " + (EnableTimeExit
                        ? "SI  (" + MinDurationSeconds + "s, modo=" + ExitMode + ")"
                        : "NO"));
                    NY930Log.Info(SRC, "  TP Gap Guard     : " + (EnableTpGapGuard
                        ? "SI  (" + TpGapGuardTicks + " ticks / " + TpGapGuardSeconds + "s)"
                        : "NO"));
                    NY930Log.Info(SRC, "  SL Gap Guard     : " + (EnableSlGapGuard
                        ? "SI  (" + SlGapGuardTicks + " ticks / " + SlGapGuardSeconds + "s)"
                        : "NO"));

                    ProgramarTimer();
                }
            }

            else if (State == State.Transition)
            {
                GuardarEstado();
            }

            else if (State == State.Terminated)
            {
                if (!_saveOrdersPlaced)
                    GuardarEstado();

                DescartarTimer();
                NY930Bridge.UnregisterHedge();
            }
        }

        // ── GuardarEstado ─────────────────────────────────────────
        private void GuardarEstado()
        {
            bool hayEstado = ordersPlaced
                          || entryFill      > 0
                          || currentSlPrice > 0;

            if (!hayEstado) return;

            _saveOrdersPlaced  = hayEstado;
            _saveExitPlaced    = exitOrdersPlaced;
            _saveSessionDone   = sessionDone;
            _saveFillPrice     = entryFill;
            _saveContratos     = contratosRestantes;
            _savePartial1Done  = partial1Done;
            _savePartial2Done  = partial2Done;
            _savePartial1Filled = partial1FilledQty;
            _savePartial2Filled = partial2FilledQty;
            _saveTradeStart    = tradeStartTime;
            _saveTpRetained    = tpRetained;
            _saveRetainedTP    = retainedTP;
            _saveRetainedQty   = retainedTpQty;
            _saveTimeExitFired = timeExitFired;

            if (currentSlPrice > 0) _saveSlStop  = currentSlPrice;
            if (currentTpPrice > 0) _saveTpLimit = currentTpPrice;

            if (currentP1Price > 0 && !partial1Done) _saveP1Price = currentP1Price;
            if (currentP2Price > 0 && !partial2Done) _saveP2Price = currentP2Price;

            NY930Log.Separator(SRC);
            NY930Log.Info(SRC, "Estado guardado (Transition).");
            if (_saveExitPlaced)
                NY930Log.Info(SRC, "  Fill=" + _saveFillPrice
                      + "  SL=" + _saveSlStop
                      + "  TP=" + _saveTpLimit);
            NY930Log.Separator(SRC);
        }

        // ── RestaurarEstado ────────────────────────────────────────
        private void RestaurarEstado()
        {
            if (!_saveOrdersPlaced) return;

            NY930Log.Separator(SRC);
            NY930Log.Info(SRC, "Restaurando estado...");

            lastDate           = DateTime.Now.Date;
            ordersPlaced       = _saveOrdersPlaced;
            exitOrdersPlaced   = _saveExitPlaced;
            sessionDone        = _saveSessionDone;
            entryFill          = _saveFillPrice;
            contratosRestantes = _saveContratos;
            partial1Done       = _savePartial1Done;
            partial2Done       = _savePartial2Done;
            partial1FilledQty  = _savePartial1Filled;
            partial2FilledQty  = _savePartial2Filled;
            tradeStartTime     = _saveTradeStart;
            tpRetained         = _saveTpRetained;
            retainedTP         = _saveRetainedTP;
            retainedTpQty      = _saveRetainedQty;
            timeExitFired      = _saveTimeExitFired;

            ocoExit = "OCO_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (_saveExitPlaced && _saveFillPrice > 0)
            {
                int qty = _saveContratos > 0 ? _saveContratos : Quantity;
                bool esLong = (Direccion == DireccionEntrada.Long);

                if (_saveSlStop > 0)
                {
                    slOrder = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.StopMarket,
                        qty, 0, _saveSlStop, ocoExit,
                        esLong ? "LONG_SL" : "SHORT_SL");
                    currentSlPrice = _saveSlStop;
                    NY930Log.Info(SRC, "  SL resubmitido : " + _saveSlStop);
                }

                if (_saveTpLimit > 0 && !_saveTpRetained)
                {
                    tpOrder = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Limit,
                        qty, _saveTpLimit, 0, ocoExit,
                        esLong ? "LONG_TP" : "SHORT_TP");
                    currentTpPrice = _saveTpLimit;
                    NY930Log.Info(SRC, "  TP resubmitido : " + _saveTpLimit);
                }
                else if (_saveTpRetained && _saveTpLimit > 0)
                {
                    retainedTP     = _saveTpLimit;
                    retainedTpQty  = _saveRetainedQty > 0 ? _saveRetainedQty : qty;
                    currentTpPrice = _saveTpLimit;
                    NY930Log.Info(SRC, "  TP RETENIDO (SalidaPorTiempo) : " + _saveTpLimit
                          + "  tiempo restante ~"
                          + Math.Max(0, MinDurationSeconds - (DateTime.Now - _saveTradeStart).TotalSeconds).ToString("F0")
                          + "s");
                }

                if (_saveP1Price > 0 && !_savePartial1Done)
                {
                    p1Order = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Limit,
                        Partial1Contracts, _saveP1Price, 0, string.Empty,
                        esLong ? "LONG_P1" : "SHORT_P1");
                    currentP1Price = _saveP1Price;
                    NY930Log.Info(SRC, "  P1 resubmitido : " + _saveP1Price
                          + "  [" + Partial1Contracts + " contratos]");
                }

                if (_saveP2Price > 0 && !_savePartial2Done)
                {
                    p2Order = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Limit,
                        Partial2Contracts, _saveP2Price, 0, string.Empty,
                        esLong ? "LONG_P2" : "SHORT_P2");
                    currentP2Price = _saveP2Price;
                    NY930Log.Info(SRC, "  P2 resubmitido : " + _saveP2Price
                          + "  [" + Partial2Contracts + " contratos]");
                }
            }

            NY930Log.Separator(SRC);
            LimpiarStatics();
        }

        private static void LimpiarStatics()
        {
            _saveOrdersPlaced  = false;
            _saveExitPlaced    = false;
            _saveSessionDone   = false;
            _saveFillPrice     = 0;
            _saveSlStop        = 0;
            _saveTpLimit       = 0;
            _saveP1Price       = 0;
            _saveP2Price       = 0;
            _saveContratos     = 0;
            _savePartial1Done  = false;
            _savePartial2Done  = false;
            _savePartial1Filled = 0;
            _savePartial2Filled = 0;
            _saveTradeStart    = DateTime.MinValue;
            _saveTpRetained    = false;
            _saveRetainedTP    = 0;
            _saveRetainedQty   = 0;
            _saveTimeExitFired = false;
        }

        // ── ProgramarTimer ────────────────────────────────────────
        private void ProgramarTimer()
        {
            DescartarTimer();

            DateTime now       = DateTime.Now;
            DateTime entryTime = new DateTime(now.Year, now.Month, now.Day,
                                              EntryHour, EntryMinute, EntrySecond);

            if (now >= entryTime) return;

            long delayMs = (long)(entryTime - now).TotalMilliseconds;

            _entryTimer = new Timer(
                _ => TriggerCustomEvent(o => ColocarOrden(), null),
                null,
                delayMs,
                Timeout.Infinite
            );

            NY930Log.Info(SRC, "Timer programado — disparo en "
                  + delayMs + " ms a las "
                  + entryTime.ToString("HH:mm:ss.fff"));
        }

        private void DescartarTimer()
        {
            if (_entryTimer != null) { _entryTimer.Dispose(); _entryTimer = null; }
        }

        // ── OnBarUpdate ──────────────────────────────────────────
        protected override void OnBarUpdate()
        {
            if (State != State.Realtime) return;
            if (Close[0] > 0) lastPrice = Close[0];
        }

        // ── OnMarketData ─────────────────────────────────────────
        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (State != State.Realtime) return;

            if (e.Price > 0)
            {
                if (e.MarketDataType == MarketDataType.Last)
                    lastPrice = e.Price;
                else if (e.MarketDataType == MarketDataType.Bid
                      || e.MarketDataType == MarketDataType.Ask)
                    lastPrice = e.Price;
            }

            DateTime now   = DateTime.Now;
            DateTime today = now.Date;

            // ── Reset diario ──────────────────────────────────────
            if (today != lastDate)
            {
                lastDate           = today;
                ordersPlaced       = false;
                exitOrdersPlaced   = false;
                sessionDone        = false;
                entryOrder         = null;
                slOrder            = null;
                tpOrder            = null;
                p1Order            = null;
                p2Order            = null;
                ocoExit            = string.Empty;
                entryFill          = 0;
                currentSlPrice     = 0;
                currentTpPrice     = 0;
                currentP1Price     = 0;
                currentP2Price     = 0;
                breakevenApplied   = false;
                breakevenSent      = false;
                bePricePending     = 0;
                trailActive        = false;
                trailSent          = false;
                trailCurrentSl     = 0;
                trailPreviousSl    = 0;
                tpTrailActive      = false;
                tpTrailExtreme     = 0;
                tpCrossedTime      = DateTime.MinValue;
                tpTimeoutFired     = false;
                partial1Done       = false;
                partial2Done       = false;
                contratosRestantes = 0;
                closingPosition    = false;
                partial1FilledQty  = 0;
                partial2FilledQty  = 0;
                tpRetained         = false;
                retainedTP         = 0;
                retainedTpQty      = 0;
                tradeStartTime     = DateTime.MinValue;
                timeExitFired      = false;
                delayedTpOrder     = null;
                _timeCheckRetry    = DateTime.MinValue;
                slChangePending    = false;
                slTargetQty        = 0;
                _slRetryTime       = DateTime.MinValue;
                tpGapGuardFired    = false;
                slGapGuardFired    = false;
                tpOvershootSince   = DateTime.MinValue;
                slOvershootSince   = DateTime.MinValue;

                ProgramarTimer();
            }

            // ── 1. Drain UI actions ───────────────────────────────
            DrainBridgeActions();

            // ── 2. SALIDA POR TIEMPO ──────────────────────────────
            if (EnableTimeExit && exitOrdersPlaced && tpRetained
                && !timeExitFired && lastPrice > 0
                && tradeStartTime != DateTime.MinValue
                && (now - _timeCheckRetry).TotalMilliseconds >= 200)
            {
                _timeCheckRetry = now;
                if ((now - tradeStartTime).TotalSeconds >= MinDurationSeconds)
                    EjecutarSalidaPorTiempo();
            }

            // ── 3. REINTENTO PERIODICO DE REDUCCION DE SL ─────────
            if (slChangePending && slTargetQty > 0 && exitOrdersPlaced
                && (now - _slRetryTime).TotalMilliseconds >= 300)
            {
                _slRetryTime = now;

                if (slOrder != null)
                {
                    if (slOrder.Quantity == slTargetQty)
                    {
                        slChangePending = false;
                        slTargetQty     = 0;
                        NY930Log.Debug(SRC, "SL cantidad confirmada via polling: "
                              + slOrder.Quantity + " contratos.");
                    }
                    else if (slOrder.OrderState == OrderState.Working
                          || slOrder.OrderState == OrderState.Accepted)
                    {
                        ChangeOrder(slOrder, slTargetQty, 0, slOrder.StopPrice);
                        NY930Log.Info(SRC, "Reintento ReducirSL via polling: "
                              + slOrder.Quantity + " → " + slTargetQty + " contratos.");
                    }
                }
            }

            // ── 4. TP / SL GAP GUARD ──────────────────────────────
            EvaluarGapGuards(now);

            // ── 5. BREAKEVEN ──────────────────────────────────────
            if (EnableBreakeven && !breakevenApplied && !breakevenSent
                && exitOrdersPlaced && lastPrice > 0 && entryFill > 0
                && slOrder != null
                && (slOrder.OrderState == OrderState.Working
                    || slOrder.OrderState == OrderState.Accepted))
            {
                bool esLong   = (Direccion == DireccionEntrada.Long);
                bool trigger  = esLong
                    ? lastPrice >= entryFill + BreakevenTriggerTicks * TickSize
                    : lastPrice <= entryFill - BreakevenTriggerTicks * TickSize;

                if (trigger)
                {
                    double bePrice = esLong
                        ? Math.Round((entryFill + BreakevenOffsetTicks * TickSize) / TickSize) * TickSize
                        : Math.Round((entryFill - BreakevenOffsetTicks * TickSize) / TickSize) * TickSize;

                    bool mejor = esLong
                        ? bePrice > slOrder.StopPrice
                        : bePrice < slOrder.StopPrice;

                    if (mejor)
                    {
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        ChangeOrder(slOrder, qty, 0, bePrice);
                        currentSlPrice = bePrice;
                        breakevenSent  = true;
                        bePricePending = bePrice;

                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "BE enviado al broker (" + Direccion + ")");
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Info(SRC, "  Entrada       : " + entryFill);
                        NY930Log.Info(SRC, "  SL solicitado : " + bePrice
                              + "  (offset=" + BreakevenOffsetTicks + " ticks)");
                        NY930Log.Separator(SRC);
                    }
                }
            }

            // ── 6. TRAILING STOP ──────────────────────────────────
            if (EnableTrailing && exitOrdersPlaced && !trailSent && lastPrice > 0
                && entryFill > 0 && slOrder != null)
            {
                bool esLong = (Direccion == DireccionEntrada.Long);
                bool beOk   = !EnableBreakeven || breakevenApplied;

                if (!trailActive && beOk
                    && (slOrder.OrderState == OrderState.Working
                        || slOrder.OrderState == OrderState.Accepted))
                {
                    trailCurrentSl = (EnableBreakeven && breakevenApplied && bePricePending > 0)
                                     ? bePricePending
                                     : slOrder.StopPrice;
                    trailActive = true;

                    NY930Log.Separator(SRC);
                    NY930Log.Info(SRC, "Trailing activado (" + Direccion + ")");
                    NY930Log.Info(SRC, "  Ancla inicial SL : " + trailCurrentSl);
                    NY930Log.Info(SRC, "  Trigger          : " + TrailTriggerTicks + " ticks");
                    NY930Log.Info(SRC, "  Paso             : " + TrailStepTicks    + " ticks");
                    NY930Log.Separator(SRC);
                }

                if (trailActive
                    && slOrder != null
                    && (slOrder.OrderState == OrderState.Working
                        || slOrder.OrderState == OrderState.Accepted))
                {
                    bool escalonLong  = esLong  && lastPrice >= trailCurrentSl + TrailTriggerTicks * TickSize;
                    bool escalonShort = !esLong && lastPrice <= trailCurrentSl - TrailTriggerTicks * TickSize;

                    if (escalonLong || escalonShort)
                    {
                        double newSl = esLong
                            ? Math.Round((trailCurrentSl + TrailStepTicks * TickSize) / TickSize) * TickSize
                            : Math.Round((trailCurrentSl - TrailStepTicks * TickSize) / TickSize) * TickSize;

                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        trailPreviousSl = trailCurrentSl;
                        trailCurrentSl  = newSl;
                        ChangeOrder(slOrder, qty, 0, newSl);
                        currentSlPrice = newSl;
                        trailSent = true;

                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "Trail escalon enviado (" + Direccion + ")");
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Info(SRC, "  Nuevo SL      : " + newSl);
                        NY930Log.Separator(SRC);
                    }
                }
            }

            // ── 7. TRAILING TP ────────────────────────────────────
            if (EnableTrailingTP && exitOrdersPlaced && !tpTimeoutFired
                && lastPrice > 0 && entryFill > 0
                && tpOrder != null
                && (tpOrder.OrderState == OrderState.Working
                    || tpOrder.OrderState == OrderState.Accepted))
            {
                bool esLong   = (Direccion == DireccionEntrada.Long);
                bool cruzado  = esLong
                    ? lastPrice > tpOrder.LimitPrice
                    : lastPrice < tpOrder.LimitPrice;

                if (cruzado)
                {
                    if (!tpTrailActive)
                    {
                        tpTrailActive  = true;
                        tpTrailExtreme = lastPrice;
                        tpCrossedTime  = now;

                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "Trailing TP activado (" + Direccion + ")");
                        NY930Log.Info(SRC, "  TP original   : " + tpOrder.LimitPrice);
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Info(SRC, "  Distancia     : " + TrailingTPDistanceTicks + " ticks");
                        NY930Log.Info(SRC, "  Timeout       : " + TrailingTPTimeoutSeconds + "s");
                        NY930Log.Separator(SRC);
                    }

                    bool nuevoExtremo = esLong
                        ? lastPrice > tpTrailExtreme
                        : (lastPrice < tpTrailExtreme || tpTrailExtreme == 0);

                    if (nuevoExtremo)
                    {
                        tpTrailExtreme = lastPrice;

                        double nuevoTP = esLong
                            ? Math.Round((tpTrailExtreme - TrailingTPDistanceTicks * TickSize) / TickSize) * TickSize
                            : Math.Round((tpTrailExtreme + TrailingTPDistanceTicks * TickSize) / TickSize) * TickSize;

                        bool mejora = esLong
                            ? nuevoTP > tpOrder.LimitPrice
                            : nuevoTP < tpOrder.LimitPrice;

                        if (mejora)
                        {
                            int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                            ChangeOrder(tpOrder, qty, nuevoTP, 0);
                            currentTpPrice = nuevoTP;
                            NY930Log.Info(SRC, "Trailing TP → " + nuevoTP
                                  + "  (extremo=" + tpTrailExtreme + ")");
                        }
                    }

                    if ((now - tpCrossedTime).TotalSeconds > TrailingTPTimeoutSeconds)
                    {
                        tpTimeoutFired = true;
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;

                        NY930Log.Separator(SRC);
                        NY930Log.Warn(SRC, "Trailing TP TIMEOUT — cerrando a mercado.");
                        NY930Log.Info(SRC, "  Sin fill por  : " + TrailingTPTimeoutSeconds + "s");
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Separator(SRC);

                        if (tpOrder != null
                            && (tpOrder.OrderState == OrderState.Working
                                || tpOrder.OrderState == OrderState.Accepted))
                            CancelOrder(tpOrder);

                        if (slOrder != null
                            && (slOrder.OrderState == OrderState.Working
                                || slOrder.OrderState == OrderState.Accepted))
                            CancelOrder(slOrder);

                        SubmitOrderUnmanaged(
                            0,
                            esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.Market,
                            qty, 0, 0, string.Empty,
                            esLong ? "LONG_TP_TIMEOUT" : "SHORT_TP_TIMEOUT");
                    }
                }
            }

            // ── 8. Publish snapshot to UI (throttled) ─────────────
            if ((now - _lastSnapshotPush).TotalMilliseconds >= 200)
            {
                _lastSnapshotPush = now;
                PublicarSnapshot();
            }
        }

        // ── EvaluarGapGuards ──────────────────────────────────────
        // Tick-based: if lastPrice has crossed TP/SL by N ticks while
        // the order is still working, cancel everything and exit at
        // market.
        // Time-based: if price first crossed the level Y seconds ago
        // and still no fill, exit at market.
        private void EvaluarGapGuards(DateTime now)
        {
            if (!exitOrdersPlaced || lastPrice <= 0 || entryFill <= 0) return;
            if (closingPosition) return;

            bool esLong = (Direccion == DireccionEntrada.Long);

            // ── TP guard ─────────────────────────────────────────
            if (EnableTpGapGuard && !tpGapGuardFired && tpOrder != null
                && (tpOrder.OrderState == OrderState.Working
                    || tpOrder.OrderState == OrderState.Accepted))
            {
                double tp = tpOrder.LimitPrice;
                bool   beyond = esLong ? lastPrice > tp : lastPrice < tp;

                if (beyond)
                {
                    if (tpOvershootSince == DateTime.MinValue) tpOvershootSince = now;

                    double overshoot = esLong ? (lastPrice - tp) : (tp - lastPrice);
                    int    overTicks = (int)Math.Round(overshoot / TickSize);
                    double elapsed   = (now - tpOvershootSince).TotalSeconds;

                    bool tickTrip = TpGapGuardTicks   > 0 && overTicks >= TpGapGuardTicks;
                    bool timeTrip = TpGapGuardSeconds > 0 && elapsed   >= TpGapGuardSeconds;

                    if (tickTrip || timeTrip)
                    {
                        tpGapGuardFired = true;
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;

                        NY930Log.Separator(SRC);
                        NY930Log.Warn(SRC, "TP GAP GUARD disparado (" + Direccion + ")");
                        NY930Log.Warn(SRC, "  TP            : " + tp);
                        NY930Log.Warn(SRC, "  Precio actual : " + lastPrice + " (over " + overTicks + " ticks)");
                        NY930Log.Warn(SRC, "  Tiempo over   : " + elapsed.ToString("F1") + "s");
                        NY930Log.Warn(SRC, "  Motivo        : " + (tickTrip ? "ticks" : "tiempo"));
                        NY930Log.Separator(SRC);

                        ForzarCierreAMercado(esLong, qty,
                            tickTrip ? "TP_GAP_TICKS" : "TP_GAP_TIME");
                        return;
                    }
                }
                else
                {
                    tpOvershootSince = DateTime.MinValue;
                }
            }

            // ── SL guard ─────────────────────────────────────────
            if (EnableSlGapGuard && !slGapGuardFired && slOrder != null
                && (slOrder.OrderState == OrderState.Working
                    || slOrder.OrderState == OrderState.Accepted))
            {
                double sl = slOrder.StopPrice;
                bool   beyond = esLong ? lastPrice < sl : lastPrice > sl;

                if (beyond)
                {
                    if (slOvershootSince == DateTime.MinValue) slOvershootSince = now;

                    double overshoot = esLong ? (sl - lastPrice) : (lastPrice - sl);
                    int    overTicks = (int)Math.Round(overshoot / TickSize);
                    double elapsed   = (now - slOvershootSince).TotalSeconds;

                    bool tickTrip = SlGapGuardTicks   > 0 && overTicks >= SlGapGuardTicks;
                    bool timeTrip = SlGapGuardSeconds > 0 && elapsed   >= SlGapGuardSeconds;

                    if (tickTrip || timeTrip)
                    {
                        slGapGuardFired = true;
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;

                        NY930Log.Separator(SRC);
                        NY930Log.Warn(SRC, "SL GAP GUARD disparado (" + Direccion + ")");
                        NY930Log.Warn(SRC, "  SL            : " + sl);
                        NY930Log.Warn(SRC, "  Precio actual : " + lastPrice + " (over " + overTicks + " ticks)");
                        NY930Log.Warn(SRC, "  Tiempo over   : " + elapsed.ToString("F1") + "s");
                        NY930Log.Warn(SRC, "  Motivo        : " + (tickTrip ? "ticks" : "tiempo"));
                        NY930Log.Separator(SRC);

                        ForzarCierreAMercado(esLong, qty,
                            tickTrip ? "SL_GAP_TICKS" : "SL_GAP_TIME");
                        return;
                    }
                }
                else
                {
                    slOvershootSince = DateTime.MinValue;
                }
            }
        }

        // ── ForzarCierreAMercado (used by gap guards) ─────────────
        private void ForzarCierreAMercado(bool esLong, int qty, string etiqueta)
        {
            closingPosition = true;
            CancelarParciales();

            if (slOrder != null
                && (slOrder.OrderState == OrderState.Working
                    || slOrder.OrderState == OrderState.Accepted))
                CancelOrder(slOrder);

            if (tpOrder != null
                && (tpOrder.OrderState == OrderState.Working
                    || tpOrder.OrderState == OrderState.Accepted))
                CancelOrder(tpOrder);

            SubmitOrderUnmanaged(
                0,
                esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                OrderType.Market,
                qty, 0, 0, string.Empty, etiqueta);
        }

        // ── ColocarOrden: llamado por TriggerCustomEvent ──────────
        private void ColocarOrden()
        {
            if (ordersPlaced || sessionDone) return;

            DateTime ahora     = DateTime.Now;
            DateTime entryTime = new DateTime(ahora.Year, ahora.Month, ahora.Day,
                                              EntryHour, EntryMinute, EntrySecond);

            if (ahora < entryTime.AddSeconds(-2))
            {
                NY930Log.Warn(SRC, "ColocarOrden — disparo espurio ignorado @ "
                      + ahora.ToString("HH:mm:ss.fff"));
                ProgramarTimer();
                return;
            }

            if (Direccion == DireccionEntrada.SinOperacion)
            {
                ordersPlaced = true;
                sessionDone  = true;
                NY930Log.Separator(SRC);
                NY930Log.Warn(SRC, "Tipo de Operacion no seleccionado. No se coloca ninguna orden.");
                NY930Log.Separator(SRC);
                return;
            }

            if (lastPrice <= 0)
            {
                NY930Log.Warn(SRC, "lastPrice no disponible al disparar el timer.");
                return;
            }

            ColocarEntradaMercado();
        }

        // Manual entry (also used by UI Buy Now / Sell Now actions)
        private void ColocarEntradaMercado()
        {
            ocoExit = "OCO_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            NY930Log.Separator(SRC);
            NY930Log.Info(SRC, "Enviando orden a mercado");
            NY930Log.Info(SRC, "  Precio referencia : " + lastPrice);
            NY930Log.Info(SRC, "  Direccion         : " + Direccion);

            if (Direccion == DireccionEntrada.Long)
            {
                entryOrder = SubmitOrderUnmanaged(
                    0, OrderAction.Buy, OrderType.Market,
                    Quantity, 0, 0, string.Empty, "LONG_ENTRY");
                NY930Log.Info(SRC, "  BUY Market        : ENVIADA");
            }
            else
            {
                entryOrder = SubmitOrderUnmanaged(
                    0, OrderAction.SellShort, OrderType.Market,
                    Quantity, 0, 0, string.Empty, "SHORT_ENTRY");
                NY930Log.Info(SRC, "  SELL Market       : ENVIADA");
            }
            NY930Log.Separator(SRC);
            ordersPlaced = true;
        }

        // ── OnExecutionUpdate ────────────────────────────────────
        protected override void OnExecutionUpdate(
            Execution execution, string executionId, double price,
            int quantity, MarketPosition marketPosition,
            string orderId, DateTime time)
        {
            if (execution.Order == null) return;
            if (execution.Quantity <= 0)  return;

            double fillPrice = execution.Price;
            bool   esLong    = (Direccion == DireccionEntrada.Long);

            // ── FILL DE ENTRADA ──────────────────────────────────
            if (!exitOrdersPlaced
                && entryOrder != null
                && execution.Order == entryOrder)
            {
                exitOrdersPlaced   = true;
                sessionDone        = true;
                entryFill          = fillPrice;
                contratosRestantes = Quantity;
                closingPosition    = false;
                tradeStartTime     = DateTime.Now;

                double slPrice = esLong
                    ? Math.Round((fillPrice - StopLossTicks   * TickSize) / TickSize) * TickSize
                    : Math.Round((fillPrice + StopLossTicks   * TickSize) / TickSize) * TickSize;

                double tpPrice = esLong
                    ? Math.Round((fillPrice + TakeProfitTicks * TickSize) / TickSize) * TickSize
                    : Math.Round((fillPrice - TakeProfitTicks * TickSize) / TickSize) * TickSize;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, Direccion + " llenado en " + fillPrice);

                if (EnablePartials && Partial1Ticks > 0 && Partial1Contracts > 0)
                {
                    bool usaP2 = Partial2Ticks > 0 && Partial2Contracts > 0;
                    int  tpQty = Quantity - Partial1Contracts - (usaP2 ? Partial2Contracts : 0);

                    double p1Price = esLong
                        ? Math.Round((fillPrice + Partial1Ticks * TickSize) / TickSize) * TickSize
                        : Math.Round((fillPrice - Partial1Ticks * TickSize) / TickSize) * TickSize;

                    double p2Price = 0;
                    if (usaP2)
                        p2Price = esLong
                            ? Math.Round((fillPrice + Partial2Ticks * TickSize) / TickSize) * TickSize
                            : Math.Round((fillPrice - Partial2Ticks * TickSize) / TickSize) * TickSize;

                    slOrder = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.StopMarket,
                        Quantity, 0, slPrice, ocoExit,
                        esLong ? "LONG_SL" : "SHORT_SL");
                    currentSlPrice = slPrice;

                    p1Order = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Limit,
                        Partial1Contracts, p1Price, 0, string.Empty,
                        esLong ? "LONG_P1" : "SHORT_P1");
                    currentP1Price = p1Price;

                    if (usaP2)
                    {
                        p2Order = SubmitOrderUnmanaged(
                            0,
                            esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.Limit,
                            Partial2Contracts, p2Price, 0, string.Empty,
                            esLong ? "LONG_P2" : "SHORT_P2");
                        currentP2Price = p2Price;
                    }

                    if (tpQty > 0)
                    {
                        if (EnableTimeExit)
                        {
                            tpRetained     = true;
                            retainedTP     = tpPrice;
                            retainedTpQty  = tpQty;
                            currentTpPrice = tpPrice;
                            NY930Log.Info(SRC, "  TP RETENIDO (SalidaPorTiempo): se colocara tras "
                                  + MinDurationSeconds + "s @ " + tpPrice
                                  + "  [" + tpQty + " contratos]");
                        }
                        else
                        {
                            tpOrder = SubmitOrderUnmanaged(
                                0,
                                esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                                OrderType.Limit,
                                tpQty, tpPrice, 0, ocoExit,
                                esLong ? "LONG_TP" : "SHORT_TP");
                            currentTpPrice = tpPrice;
                        }
                    }

                    string dir = esLong ? "+" : "-";
                    NY930Log.Info(SRC, "  SL  : " + slPrice + "  (" + (esLong ? "-" : "+") + StopLossTicks + " ticks)  [" + Quantity + " contratos]");
                    NY930Log.Info(SRC, "  P1  : " + p1Price + "  (" + dir + Partial1Ticks + " ticks)  [" + Partial1Contracts + " contratos]");
                    if (usaP2)
                        NY930Log.Info(SRC, "  P2  : " + p2Price + "  (" + dir + Partial2Ticks + " ticks)  [" + Partial2Contracts + " contratos]");
                    if (tpQty > 0 && !EnableTimeExit)
                        NY930Log.Info(SRC, "  TP  : " + tpPrice + "  (" + dir + TakeProfitTicks + " ticks)  [" + tpQty + " contratos]");
                }
                else
                {
                    slOrder = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.StopMarket,
                        Quantity, 0, slPrice, ocoExit,
                        esLong ? "LONG_SL" : "SHORT_SL");
                    currentSlPrice = slPrice;

                    if (EnableTimeExit)
                    {
                        tpRetained     = true;
                        retainedTP     = tpPrice;
                        retainedTpQty  = Quantity;
                        currentTpPrice = tpPrice;
                        NY930Log.Info(SRC, "  SL : " + slPrice + "  (" + (esLong ? "-" : "+") + StopLossTicks + " ticks)");
                        NY930Log.Info(SRC, "  TP RETENIDO (SalidaPorTiempo): se colocara tras "
                              + MinDurationSeconds + "s @ " + tpPrice
                              + "  [" + Quantity + " contratos]");
                    }
                    else
                    {
                        tpOrder = SubmitOrderUnmanaged(
                            0,
                            esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.Limit,
                            Quantity, tpPrice, 0, ocoExit,
                            esLong ? "LONG_TP" : "SHORT_TP");
                        currentTpPrice = tpPrice;
                        NY930Log.Info(SRC, "  SL : " + slPrice + "  (" + (esLong ? "-" : "+") + StopLossTicks   + " ticks)");
                        NY930Log.Info(SRC, "  TP : " + tpPrice + "  (" + (esLong ? "+" : "-") + TakeProfitTicks + " ticks)");
                    }
                }

                NY930Log.Separator(SRC);
                return;
            }

            // ── FILL DE TP RETARDADO ─────────────────────────────
            if (delayedTpOrder != null && execution.Order == delayedTpOrder)
            {
                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "TP RETARDADO llenado @ " + fillPrice);

                if (slOrder != null
                    && (slOrder.OrderState == OrderState.Working
                        || slOrder.OrderState == OrderState.Accepted))
                {
                    CancelOrder(slOrder);
                    NY930Log.Info(SRC, "  SL cancelado tras fill de TP retardado.");
                }

                delayedTpOrder = null;
                timeExitFired  = true;
                tpRetained     = false;
                CapturarResultado(esLong, fillPrice, "TP_DELAYED");
                NY930Log.Separator(SRC);
                return;
            }

            // ── FILL DE PARCIAL 1 ────────────────────────────────
            if (p1Order != null && execution.Order == p1Order && !partial1Done)
            {
                partial1FilledQty  += execution.Quantity;
                contratosRestantes -= execution.Quantity;

                if (partial1FilledQty >= Partial1Contracts)
                    partial1Done = true;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "PARCIAL 1 fill @ " + fillPrice);
                NY930Log.Info(SRC, "  Esta ejecucion      : " + execution.Quantity + " contratos");
                NY930Log.Info(SRC, "  P1 acumulado        : " + partial1FilledQty + " / " + Partial1Contracts);
                NY930Log.Info(SRC, "  Contratos restantes : " + contratosRestantes);
                if (partial1Done) NY930Log.Info(SRC, "  P1 COMPLETO.");

                ReducirSL(contratosRestantes);
                NY930Log.Separator(SRC);
                return;
            }

            // ── FILL DE PARCIAL 2 ────────────────────────────────
            if (p2Order != null && execution.Order == p2Order && !partial2Done)
            {
                partial2FilledQty  += execution.Quantity;
                contratosRestantes -= execution.Quantity;

                if (partial2FilledQty >= Partial2Contracts)
                    partial2Done = true;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "PARCIAL 2 fill @ " + fillPrice);
                NY930Log.Info(SRC, "  Esta ejecucion      : " + execution.Quantity + " contratos");
                NY930Log.Info(SRC, "  P2 acumulado        : " + partial2FilledQty + " / " + Partial2Contracts);
                NY930Log.Info(SRC, "  Contratos restantes : " + contratosRestantes);
                if (partial2Done) NY930Log.Info(SRC, "  P2 COMPLETO.");

                ReducirSL(contratosRestantes);
                NY930Log.Separator(SRC);
            }
        }

        // ── EjecutarSalidaPorTiempo ────────────────────────────────
        private void EjecutarSalidaPorTiempo()
        {
            timeExitFired = true;
            tpRetained    = false;

            bool   esLong   = (Direccion == DireccionEntrada.Long);
            double tpPrice  = retainedTP;
            int    qty      = contratosRestantes > 0 ? contratosRestantes : Quantity;
            double elapsed  = (DateTime.Now - tradeStartTime).TotalSeconds;

            NY930Log.Separator(SRC);
            NY930Log.Warn(SRC, "SALIDA POR TIEMPO (" + Direccion + ")");
            NY930Log.Info(SRC, "  Tiempo transcurrido  : " + elapsed.ToString("F1") + "s  (min=" + MinDurationSeconds + "s)");
            NY930Log.Info(SRC, "  Precio actual        : " + lastPrice);
            NY930Log.Info(SRC, "  Fill entrada         : " + entryFill);
            NY930Log.Info(SRC, "  TP retenido          : " + tpPrice);
            NY930Log.Info(SRC, "  Contratos a cerrar   : " + qty);

            bool beyondTP = esLong ? lastPrice > tpPrice : lastPrice < tpPrice;
            if (CloseIfBeyondTP && beyondTP)
            {
                NY930Log.Warn(SRC, "  [PASO 1] Precio supero TP → CIERRE A MERCADO");
                NY930Log.Separator(SRC);
                CerrarPosicionAMercado(esLong, qty, "TIME_BEYOND_TP");
                return;
            }

            switch (ExitMode)
            {
                case TimeExitMode.CloseAlways:
                    NY930Log.Info(SRC, "  [MODO CloseAlways] → CIERRE A MERCADO");
                    NY930Log.Separator(SRC);
                    CerrarPosicionAMercado(esLong, qty, "TIME_CLOSE_ALWAYS");
                    break;

                case TimeExitMode.CloseIfPositive:
                    double minProfit  = entryFill + (esLong ? 1 : -1) * MinProfitTicks * TickSize;
                    bool   esPositivo = esLong ? lastPrice >= minProfit : lastPrice <= minProfit;
                    if (esPositivo)
                    {
                        NY930Log.Info(SRC, "  [MODO CloseIfPositive] Precio positivo → CIERRE A MERCADO");
                        NY930Log.Separator(SRC);
                        CerrarPosicionAMercado(esLong, qty, "TIME_CLOSE_POSITIVE");
                    }
                    else
                    {
                        timeExitFired = false;
                        tpRetained    = true;
                        NY930Log.Info(SRC, "  [MODO CloseIfPositive] Precio no positivo — se mantiene con SL.");
                        NY930Log.Separator(SRC);
                    }
                    break;

                case TimeExitMode.PlaceTPAfterTime:
                    NY930Log.Info(SRC, "  [MODO PlaceTPAfterTime] → COLOCANDO ORDEN LIMIT TP @ " + tpPrice);
                    NY930Log.Separator(SRC);
                    CancelarParciales();
                    delayedTpOrder = SubmitOrderUnmanaged(
                        0,
                        esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                        OrderType.Limit,
                        qty, tpPrice, 0, string.Empty,
                        esLong ? "LONG_TP_DELAYED" : "SHORT_TP_DELAYED");
                    tpOrder        = delayedTpOrder;
                    currentTpPrice = tpPrice;
                    break;
            }
        }

        // ── CerrarPosicionAMercado ─────────────────────────────────
        private void CerrarPosicionAMercado(bool esLong, int qty, string etiqueta)
        {
            closingPosition = true;
            CancelarParciales();

            if (slOrder != null
                && (slOrder.OrderState == OrderState.Working
                    || slOrder.OrderState == OrderState.Accepted))
                CancelOrder(slOrder);

            SubmitOrderUnmanaged(
                0,
                esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                OrderType.Market,
                qty, 0, 0, string.Empty, etiqueta);
        }

        // ── ReducirSL ─────────────────────────────────────────────
        private void ReducirSL(int nuevaQty)
        {
            if (slOrder == null) return;

            slTargetQty = nuevaQty;

            if (slChangePending)
            {
                NY930Log.Debug(SRC, "  SL cambio ya en vuelo — objetivo actualizado a "
                      + nuevaQty + " contratos.");
                return;
            }

            OrderState estado = slOrder.OrderState;

            if (estado == OrderState.Working || estado == OrderState.Accepted)
            {
                ChangeOrder(slOrder, nuevaQty, 0, slOrder.StopPrice);
                slChangePending = true;
                NY930Log.Info(SRC, "  SL ChangeOrder enviado → " + nuevaQty + " contratos.");
            }
            else
            {
                slChangePending = true;
                NY930Log.Debug(SRC, "  SL en estado " + estado + " — reintento programado para " + nuevaQty + " contratos.");
            }
        }

        // ── CancelarParciales ──────────────────────────────────────
        private void CancelarParciales()
        {
            if (closingPosition) { /* fall-through ok */ }

            if (p1Order != null
                && (p1Order.OrderState == OrderState.Working
                 || p1Order.OrderState == OrderState.Accepted))
            {
                CancelOrder(p1Order);
                NY930Log.Info(SRC, "P1 cancelado.");
            }
            if (p2Order != null
                && (p2Order.OrderState == OrderState.Working
                 || p2Order.OrderState == OrderState.Accepted))
            {
                CancelOrder(p2Order);
                NY930Log.Info(SRC, "P2 cancelado.");
            }
        }

        // ── OnOrderUpdate ─────────────────────────────────────────
        protected override void OnOrderUpdate(
            Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice,
            OrderState orderState, DateTime time, ErrorCode error,
            string nativeError)
        {
            if (orderState == OrderState.Cancelled)
            {
                if (order == slOrder)
                    NY930Log.Info(SRC, "SL cancelado por OCO (TP alcanzado).");
                else if (order == tpOrder)
                    NY930Log.Info(SRC, "TP cancelado por OCO (SL alcanzado).");
            }

            if (orderState == OrderState.Filled && EnablePartials)
            {
                if (order == slOrder)
                {
                    timeExitFired = false;
                    tpRetained    = false;
                    NY930Log.Separator(SRC);
                    NY930Log.Warn(SRC, "SL ejecutado — cancelando parciales pendientes.");
                    CancelarParciales();
                    CapturarResultado(Direccion == DireccionEntrada.Long, averageFillPrice, "SL");
                    NY930Log.Separator(SRC);
                }
                else if (order == tpOrder)
                {
                    NY930Log.Separator(SRC);
                    NY930Log.Info(SRC, "TP ejecutado — cancelando parciales pendientes.");
                    CancelarParciales();
                    CapturarResultado(Direccion == DireccionEntrada.Long, averageFillPrice, "TP");
                    NY930Log.Separator(SRC);
                }
            }

            if (!EnablePartials && EnableTimeExit
                && orderState == OrderState.Filled
                && order == slOrder)
            {
                timeExitFired = false;
                tpRetained    = false;
                CapturarResultado(Direccion == DireccionEntrada.Long, averageFillPrice, "SL");
            }

            if (slChangePending
                && order == slOrder
                && (orderState == OrderState.Working || orderState == OrderState.Accepted)
                && error == ErrorCode.NoError)
            {
                if (order.Quantity != slTargetQty && slTargetQty > 0)
                {
                    NY930Log.Info(SRC, "Reintento ReducirSL: "
                          + order.Quantity + " → " + slTargetQty + " contratos.");
                    ChangeOrder(order, slTargetQty, 0, order.StopPrice);
                }
                else
                {
                    slChangePending = false;
                    slTargetQty     = 0;
                }
            }

            if (!breakevenSent && !trailSent) return;
            if (order != slOrder)              return;

            if (breakevenSent)
            {
                if (error != ErrorCode.NoError)
                {
                    breakevenSent = false;
                    NY930Log.Separator(SRC);
                    NY930Log.Error(SRC, "BE RECHAZADO por el broker.");
                    NY930Log.Error(SRC, "  Error : " + error
                          + (string.IsNullOrEmpty(nativeError) ? "" : " / " + nativeError));
                    NY930Log.Separator(SRC);
                    return;
                }

                if (orderState == OrderState.Working || orderState == OrderState.Accepted)
                {
                    breakevenApplied = true;
                    breakevenSent    = false;
                    NY930Log.Separator(SRC);
                    NY930Log.Info(SRC, "BE CONFIRMADO por el broker. SL movido a : " + stopPrice);
                    NY930Log.Separator(SRC);
                }
                return;
            }

            if (trailSent)
            {
                if (error != ErrorCode.NoError)
                {
                    trailCurrentSl = trailPreviousSl;
                    trailSent      = false;
                    NY930Log.Separator(SRC);
                    NY930Log.Error(SRC, "Trail RECHAZADO por el broker.");
                    NY930Log.Error(SRC, "  Error : " + error
                          + (string.IsNullOrEmpty(nativeError) ? "" : " / " + nativeError));
                    NY930Log.Info(SRC, "  Ancla revertida a : " + trailCurrentSl);
                    NY930Log.Separator(SRC);
                    return;
                }

                trailSent = false;
                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "Trail CONFIRMADO por el broker.");
                NY930Log.Info(SRC, "  Nuevo SL activo : " + stopPrice);
                NY930Log.Separator(SRC);
            }
        }

        // ── CapturarResultado ──────────────────────────────────────
        private void CapturarResultado(bool esLong, double exitPrice, string reason)
        {
            if (entryFill <= 0 || exitPrice <= 0) return;

            double pnlTicks = ((esLong ? exitPrice - entryFill : entryFill - exitPrice) / TickSize);
            double pnlCcy   = pnlTicks * (Instrument != null ? Instrument.MasterInstrument.PointValue * TickSize : 0);

            _lastResult = new NY930TradeResult
            {
                Strategy    = "Hedge",
                Instrument  = Instrument != null ? Instrument.FullName : null,
                Side        = esLong ? "Long" : "Short",
                EntryTime   = tradeStartTime,
                ExitTime    = DateTime.Now,
                EntryPrice  = entryFill,
                ExitPrice   = exitPrice,
                Contracts   = Quantity,
                PnLTicks    = pnlTicks,
                PnLCurrency = pnlCcy,
                P1Hit       = partial1Done,
                P2Hit       = partial2Done,
                TpHit       = reason.StartsWith("TP"),
                SlHit       = reason.StartsWith("SL"),
                ExitReason  = reason
            };
        }

        // ── DrainBridgeActions ─────────────────────────────────────
        private void DrainBridgeActions()
        {
            foreach (var a in NY930Bridge.DrainHedgeActions())
            {
                try { ExecuteAction(a); }
                catch (Exception ex)
                {
                    NY930Log.Error(SRC, "Error ejecutando accion " + a.Type + ": " + ex.Message);
                }
            }
        }

        private void ExecuteAction(NY930Action a)
        {
            switch (a.Type)
            {
                case NY930ActionType.HedgeBuyNow:
                    if (!exitOrdersPlaced && !ordersPlaced && lastPrice > 0)
                    {
                        Direccion = DireccionEntrada.Long;
                        ColocarEntradaMercado();
                    }
                    break;

                case NY930ActionType.HedgeSellNow:
                    if (!exitOrdersPlaced && !ordersPlaced && lastPrice > 0)
                    {
                        Direccion = DireccionEntrada.Short;
                        ColocarEntradaMercado();
                    }
                    break;

                case NY930ActionType.HedgeFlatten:
                    if (exitOrdersPlaced && !closingPosition)
                    {
                        bool esLong = (Direccion == DireccionEntrada.Long);
                        int  qty    = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        NY930Log.Warn(SRC, "Flatten manual desde UI.");
                        ForzarCierreAMercado(esLong, qty, "MANUAL_FLATTEN");
                    }
                    break;

                case NY930ActionType.HedgeBreakeven:
                    if (exitOrdersPlaced && slOrder != null && entryFill > 0)
                    {
                        bool esLong = (Direccion == DireccionEntrada.Long);
                        double bePrice = esLong
                            ? Math.Round((entryFill + Math.Max(1, BreakevenOffsetTicks) * TickSize) / TickSize) * TickSize
                            : Math.Round((entryFill - Math.Max(1, BreakevenOffsetTicks) * TickSize) / TickSize) * TickSize;
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        ChangeOrder(slOrder, qty, 0, bePrice);
                        currentSlPrice = bePrice;
                        breakevenApplied = true;
                        NY930Log.Info(SRC, "BE manual aplicado @ " + bePrice);
                    }
                    break;

                case NY930ActionType.HedgePartialClose:
                    if (exitOrdersPlaced && a.IntArg > 0 && contratosRestantes > 0)
                    {
                        bool esLong = (Direccion == DireccionEntrada.Long);
                        int qty = Math.Min(a.IntArg, contratosRestantes);
                        SubmitOrderUnmanaged(
                            0,
                            esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                            OrderType.Market,
                            qty, 0, 0, string.Empty, "MANUAL_PARTIAL");
                        NY930Log.Info(SRC, "Cierre parcial manual: " + qty + " contratos.");
                    }
                    break;

                case NY930ActionType.HedgeCancelEntry:
                    if (entryOrder != null
                        && (entryOrder.OrderState == OrderState.Working
                         || entryOrder.OrderState == OrderState.Accepted))
                    {
                        CancelOrder(entryOrder);
                        NY930Log.Info(SRC, "Orden de entrada cancelada manualmente.");
                    }
                    break;
            }
        }

        // ── PublicarSnapshot ───────────────────────────────────────
        private void PublicarSnapshot()
        {
            try
            {
                bool esLong   = (Direccion == DireccionEntrada.Long);
                double upTicks = 0;
                if (entryFill > 0 && lastPrice > 0)
                    upTicks = ((esLong ? lastPrice - entryFill : entryFill - lastPrice) / TickSize);

                var snap = new NY930HedgeSnapshot
                {
                    Instrument         = Instrument != null ? Instrument.FullName : "(none)",
                    Timestamp          = DateTime.Now,
                    TickSize           = TickSize,
                    Direction          = Direccion.ToString(),
                    Quantity           = Quantity,
                    ContractsRemaining = contratosRestantes,
                    InPosition         = exitOrdersPlaced,
                    EntryFill          = entryFill,
                    SlPrice            = currentSlPrice,
                    TpPrice            = currentTpPrice,
                    P1Price            = currentP1Price,
                    P2Price            = currentP2Price,
                    Partial1Done       = partial1Done,
                    Partial2Done       = partial2Done,
                    LastPrice          = lastPrice,
                    UnrealizedTicks    = upTicks,
                    TradeStartTime     = tradeStartTime,
                    SessionDone        = sessionDone,
                    LastResult         = _lastResult
                };

                NY930Bridge.PublishHedge(snap);
            }
            catch (Exception ex)
            {
                NY930Log.Error(SRC, "PublicarSnapshot error: " + ex.Message);
            }
        }

        // ────────────────────────────────────────────────────────
        #region Properties

        // ── Grupo 1: Horario ─────────────────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Hora (HH)", GroupName = "1. Horario", Order = 0)]
        public int EntryHour { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Minuto (MM)", GroupName = "1. Horario", Order = 1)]
        public int EntryMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Segundo (SS)", GroupName = "1. Horario", Order = 2)]
        public int EntrySecond { get; set; }

        // ── Grupo 2: General ─────────────────────────────────────

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Contratos", GroupName = "2. General", Order = 0)]
        public int Quantity { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Stop Loss (ticks)", GroupName = "2. General", Order = 1)]
        public int StopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Take Profit (ticks)", GroupName = "2. General", Order = 2)]
        public int TakeProfitTicks { get; set; }

        // ── Grupo 3: Operacion ───────────────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Tipo de Operacion", GroupName = "3. Operacion", Order = 0)]
        public DireccionEntrada Direccion { get; set; }

        // ── Grupo 4: Breakeven ───────────────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Breakeven", GroupName = "4. Breakeven", Order = 0)]
        public bool EnableBreakeven { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks para activar BE", GroupName = "4. Breakeven", Order = 1)]
        public int BreakevenTriggerTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Ticks SL sobre/bajo entrada", GroupName = "4. Breakeven", Order = 2)]
        public int BreakevenOffsetTicks { get; set; }

        // ── Grupo 5: Trailing Stop ───────────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Trailing Stop", GroupName = "5. Trailing Stop", Order = 0)]
        public bool EnableTrailing { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks para activar escalon", GroupName = "5. Trailing Stop", Order = 1)]
        public int TrailTriggerTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks por escalon", GroupName = "5. Trailing Stop", Order = 2)]
        public int TrailStepTicks { get; set; }

        // ── Grupo 6: Trailing TP ─────────────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Trailing TP", GroupName = "6. Trailing TP", Order = 0)]
        public bool EnableTrailingTP { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Distancia al maximo (ticks)", GroupName = "6. Trailing TP", Order = 1)]
        public int TrailingTPDistanceTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Timeout sin fill (segundos)", GroupName = "6. Trailing TP", Order = 2)]
        public int TrailingTPTimeoutSeconds { get; set; }

        // ── Grupo 7: Parciales ───────────────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Parciales", GroupName = "7. Parciales", Order = 0)]
        public bool EnablePartials { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "P1 — Ticks de beneficio", GroupName = "7. Parciales", Order = 1)]
        public int Partial1Ticks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "P1 — Contratos a cerrar", GroupName = "7. Parciales", Order = 2)]
        public int Partial1Contracts { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "P2 — Ticks de beneficio (0=desactivado)", GroupName = "7. Parciales", Order = 3)]
        public int Partial2Ticks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "P2 — Contratos a cerrar (0=desactivado)", GroupName = "7. Parciales", Order = 4)]
        public int Partial2Contracts { get; set; }

        // ── Grupo 8: Salida por Tiempo ───────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Salida por Tiempo", GroupName = "8. Salida por Tiempo", Order = 0)]
        public bool EnableTimeExit { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Duracion minima (segundos)", GroupName = "8. Salida por Tiempo", Order = 1)]
        public int MinDurationSeconds { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo de salida", GroupName = "8. Salida por Tiempo", Order = 2)]
        public TimeExitMode ExitMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Cerrar si precio supero TP (recomendado ON)", GroupName = "8. Salida por Tiempo", Order = 3)]
        public bool CloseIfBeyondTP { get; set; }

        public int MinProfitTicks { get; set; }

        // ── Grupo 9: NY930 Gap Guards ────────────────────────────
        // Tick guard fires when price crosses TP/SL by N ticks while
        // the working order has not filled. Time guard fires after Y
        // seconds of staying beyond the level. Both close the open
        // position at market and cancel the residual SL/TP/parciales.

        [NinjaScriptProperty]
        [Display(Name = "Habilitar TP Gap Guard", GroupName = "9. NY930 Gap Guards", Order = 0)]
        public bool EnableTpGapGuard { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "TP Gap Guard — Ticks (0=off)", GroupName = "9. NY930 Gap Guards", Order = 1)]
        public int TpGapGuardTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "TP Gap Guard — Segundos (0=off)", GroupName = "9. NY930 Gap Guards", Order = 2)]
        public int TpGapGuardSeconds { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Habilitar SL Gap Guard", GroupName = "9. NY930 Gap Guards", Order = 3)]
        public bool EnableSlGapGuard { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "SL Gap Guard — Ticks (0=off)", GroupName = "9. NY930 Gap Guards", Order = 4)]
        public int SlGapGuardTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "SL Gap Guard — Segundos (0=off)", GroupName = "9. NY930 Gap Guards", Order = 5)]
        public int SlGapGuardSeconds { get; set; }

        #endregion
    }
}
