// ============================================================
//  AperturaBreakout — NY930 reinforced edition
//  ------------------------------------------------------------
//  Drop-in replacement for the original openrange.cs (class name
//  and namespace preserved for backwards compatibility with
//  existing workspaces).
//
//  Original feature set (UNCHANGED — see PROGRESS_LOG.md §2):
//    - SubmitOrderUnmanaged + OCO on entry, OCO on Long/Short exits
//    - Buy/Sell Stop placed at fixed time via System.Threading.Timer
//    - Independent SL/TP per side, Breakeven, Trailing, Trailing TP
//    - Partials P1/P2 (cumulative fill, SL contract reduction)
//    - Time Exit (CloseAlways / CloseIfPositive / PlaceTPAfterTime)
//    - State persistence across timeframe changes (statics)
//    - SL contract reduction with 300 ms polling retry
//
//  NY930 additions:
//    1. Structured logger (NY930Log)
//    2. TP Gap Guard (ticks + seconds) — close at market when
//       lastPrice crosses the working TP by N ticks or stays
//       beyond it for Y seconds without filling.
//    3. SL Gap Guard (ticks + seconds) — same protection on SL.
//    4. Single-Stop Reverse-Tick Protection — when only Buy Stop
//       OR only Sell Stop is enabled, cancel the pending entry if
//       price moves against the entry offset by N ticks
//       (default N = stop offset itself, per the client's
//       screenshot in chat.md). Anchored at the price recorded
//       when the entry order was placed.
//    5. NY930Bridge integration — publishes a snapshot every tick
//       and consumes Move / Spread / Cancel / Buy Now / Sell Now
//       / Flatten actions sent from the AddOn UI.
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
    public class AperturaBreakout : Strategy
    {
        private const string SRC = "AperturaBreakout";

        // ── Referencias a ordenes ────────────────────────────────
        private Order longEntryOrder  = null;
        private Order shortEntryOrder = null;
        private Order longSlOrder     = null;
        private Order longTpOrder     = null;
        private Order longP1Order     = null;
        private Order longP2Order     = null;
        private Order shortSlOrder    = null;
        private Order shortTpOrder    = null;
        private Order shortP1Order    = null;
        private Order shortP2Order    = null;

        // ── OCO IDs ─────────────────────────────────────────────
        private string ocoEntry = string.Empty;
        private string ocoLong  = string.Empty;
        private string ocoShort = string.Empty;

        // ── Control de estado ────────────────────────────────────
        private bool     ordersPlaced     = false;
        private bool     exitOrdersPlaced = false;
        private bool     sessionDone      = false;
        private DateTime lastDate         = DateTime.MinValue;

        // ── Precios y fills ──────────────────────────────────────
        private double longStopPrice  = 0;
        private double shortStopPrice = 0;
        private double lastPrice      = 0;
        private double longFillPrice  = 0;
        private double shortFillPrice = 0;

        // Anchor price at entry-order placement time (used by the
        // Single-Stop Reverse-Tick guard).
        private double singleStopAnchorPrice = 0;

        // ── Precios activos de SL y TP ────────────────────────────
        private double longCurrentSlPrice  = 0;
        private double longCurrentTpPrice  = 0;
        private double shortCurrentSlPrice = 0;
        private double shortCurrentTpPrice = 0;
        private double longCurrentP1Price  = 0;
        private double longCurrentP2Price  = 0;
        private double shortCurrentP1Price = 0;
        private double shortCurrentP2Price = 0;

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
        private double   tpTrailMaxPrice = 0;
        private double   tpTrailMinPrice = 0;
        private DateTime tpCrossedTime   = DateTime.MinValue;
        private bool     tpTimeoutFired  = false;

        // ── Salida por Tiempo ─────────────────────────────────────
        private bool     tpRetained       = false;
        private double   retainedLongTP   = 0;
        private double   retainedShortTP  = 0;
        private int      retainedTpQty    = 0;
        private DateTime tradeStartTime   = DateTime.MinValue;
        private bool     timeExitFired    = false;
        private Order    delayedTpOrder   = null;
        private DateTime _timeCheckRetry  = DateTime.MinValue;

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
        private bool     tpGapGuardFired   = false;
        private bool     slGapGuardFired   = false;
        private DateTime tpOvershootSince  = DateTime.MinValue;
        private DateTime slOvershootSince  = DateTime.MinValue;
        private bool     singleStopCancelFired = false;
        private DateTime _lastSnapshotPush = DateTime.MinValue;

        private NY930TradeResult _lastResult;

        // ── Estado persistente entre reinicios (static) ───────────
        private static double _saveLongEntryStop  = 0;
        private static double _saveShortEntryStop = 0;
        private static double _saveLongSlStop     = 0;
        private static double _saveLongTpLimit    = 0;
        private static double _saveShortSlStop    = 0;
        private static double _saveShortTpLimit   = 0;
        private static double _saveLongFill       = 0;
        private static double _saveShortFill      = 0;
        private static int    _saveContratos      = 0;
        private static bool   _saveOrdersPlaced   = false;
        private static bool   _saveExitPlaced     = false;
        private static bool   _saveSessionDone    = false;
        private static bool   _savePartial1Done   = false;
        private static bool   _savePartial2Done   = false;
        private static int    _savePartial1Filled = 0;
        private static int    _savePartial2Filled = 0;
        private static DateTime _saveTradeStart    = DateTime.MinValue;
        private static bool     _saveTpRetained    = false;
        private static double   _saveRetainedLTP   = 0;
        private static double   _saveRetainedSTP   = 0;
        private static int      _saveRetainedQty   = 0;
        private static bool     _saveTimeExitFired = false;
        private static double   _saveSingleAnchor  = 0;
        private static double _saveLongP1Price    = 0;
        private static double _saveLongP2Price    = 0;
        private static double _saveShortP1Price   = 0;
        private static double _saveShortP2Price   = 0;

        // ────────────────────────────────────────────────────────
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "BuyStop + SellStop a hora fija. SL/TP independientes. NY930 Gap Guards + Single-Stop Reverse Cancel.";
                Name        = "AperturaBreakout";

                EntryHour   = 9;
                EntryMinute = 29;
                EntrySecond = 58;

                EnableLong  = true;
                EnableShort = true;

                TicksLong   = 40;
                TicksShort  = 40;

                StopLossLongTicks    = 90;
                TakeProfitLongTicks  = 61;
                StopLossShortTicks   = 90;
                TakeProfitShortTicks = 61;

                Quantity = 10;

                EnableBreakeven       = false;
                BreakevenTriggerTicks = 30;
                BreakevenOffsetTicks  = 2;

                EnableTrailing    = false;
                TrailTriggerTicks = 35;
                TrailStepTicks    = 2;

                EnableTrailingTP          = false;
                TrailingTPDistanceTicks   = 4;
                TrailingTPTimeoutSeconds  = 2;

                EnablePartials     = false;
                Partial1Ticks      = 30;
                Partial1Contracts  = 3;
                Partial2Ticks      = 50;
                Partial2Contracts  = 3;

                EnableTimeExit      = false;
                MinDurationSeconds  = 10;
                ExitMode            = TimeExitMode.PlaceTPAfterTime;
                CloseIfBeyondTP     = true;
                MinProfitTicks      = 1;

                // ── NY930 Gap Guards ─────────────────────────────
                EnableTpGapGuard   = true;
                TpGapGuardTicks    = 3;
                TpGapGuardSeconds  = 2;
                EnableSlGapGuard   = true;
                SlGapGuardTicks    = 3;
                SlGapGuardSeconds  = 2;

                // ── Single-Stop Reverse-Tick Protection ──────────
                EnableSingleStopReverseProtection = true;
                SingleStopReverseTicks            = 0; // 0 = use the order's stop offset

                Calculate                    = Calculate.OnEachTick;
                IsUnmanaged                  = true;
                IsExitOnSessionCloseStrategy = true;
                BarsRequiredToTrade          = 0;
            }
            else if (State == State.Realtime)
            {
                NY930Log.PrintSink = msg => Print(msg);
                NY930Log.LogSink   = (msg, lvl) => Log(msg, lvl);
                NY930Bridge.RegisterOpenRange();

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

                    if (!hayError && Partial1Contracts > Quantity)
                    {
                        NY930Log.Separator(SRC);
                        NY930Log.Error(SRC, "Parciales: Partial1Contracts (" + Partial1Contracts
                              + ") > Quantity (" + Quantity + ").");
                        NY930Log.Separator(SRC);
                        hayError = true;
                    }

                    bool usaPartial2 = Partial2Ticks > 0 && Partial2Contracts > 0;
                    if (!hayError && usaPartial2)
                    {
                        if (Partial1Contracts + Partial2Contracts > Quantity)
                        {
                            NY930Log.Separator(SRC);
                            NY930Log.Error(SRC, "Parciales: P1+P2 contratos ("
                                  + (Partial1Contracts + Partial2Contracts)
                                  + ") > Quantity (" + Quantity + ").");
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
                        NY930Log.Info(SRC, "  Estado: ordenes STOP pendientes resubmitidas.");
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
                    bool usaPartial2 = EnablePartials && Partial2Ticks > 0 && Partial2Contracts > 0;
                    lastDate = now.Date;

                    NY930Log.Info(SRC, "Estrategia lista. Esperando hora de entrada: "
                          + entryTime.ToString("HH:mm:ss"));
                    NY930Log.Info(SRC, "  Long  habilitado : " + EnableLong);
                    NY930Log.Info(SRC, "  Short habilitado : " + EnableShort);
                    NY930Log.Info(SRC, "  Breakeven        : " + (EnableBreakeven
                        ? "SI  (trigger=" + BreakevenTriggerTicks + " ticks, offset=" + BreakevenOffsetTicks + " ticks)"
                        : "NO"));
                    NY930Log.Info(SRC, "  Parciales        : " + (EnablePartials
                        ? "SI  P1=" + Partial1Ticks + " ticks / " + Partial1Contracts + " contratos"
                          + (usaPartial2 ? " | P2=" + Partial2Ticks + " ticks / " + Partial2Contracts + " contratos" : " | P2=no")
                        : "NO"));
                    NY930Log.Info(SRC, "  TP Gap Guard     : " + (EnableTpGapGuard
                        ? "SI  (" + TpGapGuardTicks + " ticks / " + TpGapGuardSeconds + "s)"
                        : "NO"));
                    NY930Log.Info(SRC, "  SL Gap Guard     : " + (EnableSlGapGuard
                        ? "SI  (" + SlGapGuardTicks + " ticks / " + SlGapGuardSeconds + "s)"
                        : "NO"));
                    NY930Log.Info(SRC, "  Single-Stop Rev. : " + (EnableSingleStopReverseProtection
                        ? "SI  (ticks=" + (SingleStopReverseTicks <= 0 ? "auto (offset)" : SingleStopReverseTicks.ToString()) + ")"
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
                NY930Bridge.UnregisterOpenRange();
            }
        }

        // ── GuardarEstado ─────────────────────────────────────────
        private void GuardarEstado()
        {
            bool hayEstado = ordersPlaced
                          || longStopPrice    > 0
                          || shortStopPrice   > 0
                          || longFillPrice    > 0
                          || shortFillPrice   > 0
                          || longCurrentSlPrice  > 0
                          || shortCurrentSlPrice > 0;

            if (!hayEstado) return;

            _saveOrdersPlaced = hayEstado;
            _saveExitPlaced   = exitOrdersPlaced;
            _saveSessionDone  = sessionDone;
            _saveLongFill     = longFillPrice;
            _saveShortFill    = shortFillPrice;
            _saveContratos    = contratosRestantes;
            _savePartial1Done = partial1Done;
            _savePartial2Done = partial2Done;
            _savePartial1Filled = partial1FilledQty;
            _savePartial2Filled = partial2FilledQty;
            _saveTradeStart    = tradeStartTime;
            _saveTpRetained    = tpRetained;
            _saveRetainedLTP   = retainedLongTP;
            _saveRetainedSTP   = retainedShortTP;
            _saveRetainedQty   = retainedTpQty;
            _saveTimeExitFired = timeExitFired;
            _saveSingleAnchor  = singleStopAnchorPrice;

            if (longStopPrice  > 0) _saveLongEntryStop  = longStopPrice;
            if (shortStopPrice > 0) _saveShortEntryStop = shortStopPrice;

            if (longCurrentSlPrice  > 0) _saveLongSlStop  = longCurrentSlPrice;
            if (longCurrentTpPrice  > 0) _saveLongTpLimit = longCurrentTpPrice;
            if (shortCurrentSlPrice > 0) _saveShortSlStop = shortCurrentSlPrice;
            if (shortCurrentTpPrice > 0) _saveShortTpLimit = shortCurrentTpPrice;

            if (longCurrentP1Price  > 0 && !partial1Done) _saveLongP1Price  = longCurrentP1Price;
            if (longCurrentP2Price  > 0 && !partial2Done) _saveLongP2Price  = longCurrentP2Price;
            if (shortCurrentP1Price > 0 && !partial1Done) _saveShortP1Price = shortCurrentP1Price;
            if (shortCurrentP2Price > 0 && !partial2Done) _saveShortP2Price = shortCurrentP2Price;

            NY930Log.Separator(SRC);
            NY930Log.Info(SRC, "Estado guardado (Transition).");
            if (_saveExitPlaced)
            {
                if (_saveLongFill > 0)
                    NY930Log.Info(SRC, "  LONG fill=" + _saveLongFill
                          + "  SL=" + _saveLongSlStop
                          + "  TP=" + _saveLongTpLimit);
                else
                    NY930Log.Info(SRC, "  SHORT fill=" + _saveShortFill
                          + "  SL=" + _saveShortSlStop
                          + "  TP=" + _saveShortTpLimit);
            }
            else
            {
                if (_saveLongEntryStop  > 0) NY930Log.Info(SRC, "  BuyStop  pendiente : " + _saveLongEntryStop);
                if (_saveShortEntryStop > 0) NY930Log.Info(SRC, "  SellStop pendiente : " + _saveShortEntryStop);
            }
            NY930Log.Separator(SRC);
        }

        // ── RestaurarEstado ───────────────────────────────────────
        private void RestaurarEstado()
        {
            if (!_saveOrdersPlaced) return;

            NY930Log.Separator(SRC);
            NY930Log.Info(SRC, "Restaurando estado...");

            lastDate              = DateTime.Now.Date;
            ordersPlaced          = _saveOrdersPlaced;
            exitOrdersPlaced      = _saveExitPlaced;
            sessionDone           = _saveSessionDone;
            longFillPrice         = _saveLongFill;
            shortFillPrice        = _saveShortFill;
            contratosRestantes    = _saveContratos;
            partial1Done          = _savePartial1Done;
            partial2Done          = _savePartial2Done;
            partial1FilledQty     = _savePartial1Filled;
            partial2FilledQty     = _savePartial2Filled;
            tradeStartTime        = _saveTradeStart;
            tpRetained            = _saveTpRetained;
            retainedLongTP        = _saveRetainedLTP;
            retainedShortTP       = _saveRetainedSTP;
            retainedTpQty         = _saveRetainedQty;
            timeExitFired         = _saveTimeExitFired;
            singleStopAnchorPrice = _saveSingleAnchor;

            ocoLong  = "OCO_L_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            ocoShort = "OCO_S_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (!_saveExitPlaced)
            {
                string ocoEntryNuevo = "ENTRY_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                if (_saveLongEntryStop > 0 && EnableLong)
                {
                    longEntryOrder = SubmitOrderUnmanaged(
                        0, OrderAction.Buy, OrderType.StopMarket,
                        Quantity, 0, _saveLongEntryStop, ocoEntryNuevo, "LONG_ENTRY");
                    longStopPrice = _saveLongEntryStop;
                    NY930Log.Info(SRC, "  BuyStop  resubmitido : " + _saveLongEntryStop);
                }

                if (_saveShortEntryStop > 0 && EnableShort)
                {
                    shortEntryOrder = SubmitOrderUnmanaged(
                        0, OrderAction.SellShort, OrderType.StopMarket,
                        Quantity, 0, _saveShortEntryStop, ocoEntryNuevo, "SHORT_ENTRY");
                    shortStopPrice = _saveShortEntryStop;
                    NY930Log.Info(SRC, "  SellStop resubmitido : " + _saveShortEntryStop);
                }
            }
            else
            {
                int qty = _saveContratos > 0 ? _saveContratos : Quantity;

                if (_saveLongFill > 0)
                {
                    if (_saveLongSlStop > 0)
                    {
                        longSlOrder = SubmitOrderUnmanaged(
                            0, OrderAction.Sell, OrderType.StopMarket,
                            qty, 0, _saveLongSlStop, ocoLong, "LONG_SL");
                        longCurrentSlPrice = _saveLongSlStop;
                        NY930Log.Info(SRC, "  LONG_SL  resubmitido : " + _saveLongSlStop);
                    }
                    if (_saveLongTpLimit > 0 && !_saveTpRetained)
                    {
                        longTpOrder = SubmitOrderUnmanaged(
                            0, OrderAction.Sell, OrderType.Limit,
                            qty, _saveLongTpLimit, 0, ocoLong, "LONG_TP");
                        longCurrentTpPrice = _saveLongTpLimit;
                        NY930Log.Info(SRC, "  LONG_TP  resubmitido : " + _saveLongTpLimit);
                    }
                    else if (_saveTpRetained && _saveLongTpLimit > 0)
                    {
                        retainedLongTP     = _saveLongTpLimit;
                        retainedTpQty      = _saveRetainedQty > 0 ? _saveRetainedQty : qty;
                        longCurrentTpPrice = _saveLongTpLimit;
                        NY930Log.Info(SRC, "  LONG_TP  RETENIDO (SalidaPorTiempo) : " + _saveLongTpLimit);
                    }
                    if (_saveLongP1Price > 0 && !_savePartial1Done)
                    {
                        longP1Order = SubmitOrderUnmanaged(
                            0, OrderAction.Sell, OrderType.Limit,
                            Partial1Contracts, _saveLongP1Price, 0, string.Empty, "LONG_P1");
                        longCurrentP1Price = _saveLongP1Price;
                        NY930Log.Info(SRC, "  LONG_P1  resubmitido : " + _saveLongP1Price
                              + "  [" + Partial1Contracts + " contratos]");
                    }
                    if (_saveLongP2Price > 0 && !_savePartial2Done)
                    {
                        longP2Order = SubmitOrderUnmanaged(
                            0, OrderAction.Sell, OrderType.Limit,
                            Partial2Contracts, _saveLongP2Price, 0, string.Empty, "LONG_P2");
                        longCurrentP2Price = _saveLongP2Price;
                        NY930Log.Info(SRC, "  LONG_P2  resubmitido : " + _saveLongP2Price
                              + "  [" + Partial2Contracts + " contratos]");
                    }
                }
                else if (_saveShortFill > 0)
                {
                    if (_saveShortSlStop > 0)
                    {
                        shortSlOrder = SubmitOrderUnmanaged(
                            0, OrderAction.BuyToCover, OrderType.StopMarket,
                            qty, 0, _saveShortSlStop, ocoShort, "SHORT_SL");
                        shortCurrentSlPrice = _saveShortSlStop;
                        NY930Log.Info(SRC, "  SHORT_SL resubmitido : " + _saveShortSlStop);
                    }
                    if (_saveShortTpLimit > 0 && !_saveTpRetained)
                    {
                        shortTpOrder = SubmitOrderUnmanaged(
                            0, OrderAction.BuyToCover, OrderType.Limit,
                            qty, _saveShortTpLimit, 0, ocoShort, "SHORT_TP");
                        shortCurrentTpPrice = _saveShortTpLimit;
                        NY930Log.Info(SRC, "  SHORT_TP resubmitido : " + _saveShortTpLimit);
                    }
                    else if (_saveTpRetained && _saveShortTpLimit > 0)
                    {
                        retainedShortTP     = _saveShortTpLimit;
                        retainedTpQty       = _saveRetainedQty > 0 ? _saveRetainedQty : qty;
                        shortCurrentTpPrice = _saveShortTpLimit;
                        NY930Log.Info(SRC, "  SHORT_TP RETENIDO (SalidaPorTiempo) : " + _saveShortTpLimit);
                    }
                    if (_saveShortP1Price > 0 && !_savePartial1Done)
                    {
                        shortP1Order = SubmitOrderUnmanaged(
                            0, OrderAction.BuyToCover, OrderType.Limit,
                            Partial1Contracts, _saveShortP1Price, 0, string.Empty, "SHORT_P1");
                        shortCurrentP1Price = _saveShortP1Price;
                        NY930Log.Info(SRC, "  SHORT_P1 resubmitido : " + _saveShortP1Price);
                    }
                    if (_saveShortP2Price > 0 && !_savePartial2Done)
                    {
                        shortP2Order = SubmitOrderUnmanaged(
                            0, OrderAction.BuyToCover, OrderType.Limit,
                            Partial2Contracts, _saveShortP2Price, 0, string.Empty, "SHORT_P2");
                        shortCurrentP2Price = _saveShortP2Price;
                        NY930Log.Info(SRC, "  SHORT_P2 resubmitido : " + _saveShortP2Price);
                    }
                }
            }

            NY930Log.Separator(SRC);
            LimpiarStatics();
        }

        private static void LimpiarStatics()
        {
            _saveOrdersPlaced   = false;
            _saveExitPlaced     = false;
            _saveSessionDone    = false;
            _saveLongEntryStop  = 0;
            _saveShortEntryStop = 0;
            _saveLongSlStop     = 0;
            _saveLongTpLimit    = 0;
            _saveShortSlStop    = 0;
            _saveShortTpLimit   = 0;
            _saveLongFill       = 0;
            _saveShortFill      = 0;
            _saveContratos      = 0;
            _savePartial1Done   = false;
            _savePartial2Done   = false;
            _savePartial1Filled = 0;
            _savePartial2Filled = 0;
            _saveTradeStart    = DateTime.MinValue;
            _saveTpRetained    = false;
            _saveRetainedLTP   = 0;
            _saveRetainedSTP   = 0;
            _saveRetainedQty   = 0;
            _saveTimeExitFired = false;
            _saveSingleAnchor  = 0;
            _saveLongP1Price    = 0;
            _saveLongP2Price    = 0;
            _saveShortP1Price   = 0;
            _saveShortP2Price   = 0;
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
                _ => TriggerCustomEvent(o => ColocarOrdenes(), null),
                null,
                delayMs,
                Timeout.Infinite
            );

            NY930Log.Info(SRC, "Timer programado — disparo en "
                  + delayMs + " ms exactamente a las "
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

        // ── OnMarketData: timing de alta precision ───────────────
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

            // ── Reset diario ─────────────────────────────────────
            if (today != lastDate)
            {
                lastDate         = today;
                ordersPlaced     = false;
                exitOrdersPlaced = false;
                sessionDone      = false;
                longEntryOrder   = null;
                shortEntryOrder  = null;
                longSlOrder      = null;
                longTpOrder      = null;
                shortSlOrder     = null;
                shortTpOrder     = null;
                longStopPrice    = 0;
                shortStopPrice   = 0;
                singleStopAnchorPrice = 0;
                ocoEntry         = string.Empty;
                ocoLong          = string.Empty;
                ocoShort         = string.Empty;
                breakevenApplied = false;
                breakevenSent    = false;
                bePricePending   = 0;
                longFillPrice    = 0;
                shortFillPrice   = 0;
                longCurrentSlPrice  = 0;
                longCurrentTpPrice  = 0;
                shortCurrentSlPrice = 0;
                shortCurrentTpPrice = 0;
                longCurrentP1Price  = 0;
                longCurrentP2Price  = 0;
                shortCurrentP1Price = 0;
                shortCurrentP2Price = 0;
                trailActive      = false;
                trailSent        = false;
                trailCurrentSl   = 0;
                trailPreviousSl  = 0;
                tpTrailActive   = false;
                tpTrailMaxPrice = 0;
                tpTrailMinPrice = 0;
                tpCrossedTime   = DateTime.MinValue;
                tpTimeoutFired  = false;
                partial1Done       = false;
                partial2Done       = false;
                contratosRestantes = 0;
                closingPosition    = false;
                partial1FilledQty  = 0;
                partial2FilledQty  = 0;
                tpRetained        = false;
                retainedLongTP    = 0;
                retainedShortTP   = 0;
                retainedTpQty     = 0;
                tradeStartTime    = DateTime.MinValue;
                timeExitFired     = false;
                delayedTpOrder    = null;
                _timeCheckRetry   = DateTime.MinValue;
                slChangePending    = false;
                slTargetQty        = 0;
                _slRetryTime       = DateTime.MinValue;
                longP1Order        = null;
                longP2Order        = null;
                shortP1Order       = null;
                shortP2Order       = null;
                tpGapGuardFired   = false;
                slGapGuardFired   = false;
                tpOvershootSince  = DateTime.MinValue;
                slOvershootSince  = DateTime.MinValue;
                singleStopCancelFired = false;

                ProgramarTimer();
            }

            // Drain UI actions
            DrainBridgeActions();

            // ── 2. SALIDA POR TIEMPO ──────────────────────────────
            if (EnableTimeExit && exitOrdersPlaced && tpRetained
                && !timeExitFired && lastPrice > 0
                && tradeStartTime != DateTime.MinValue
                && (now - _timeCheckRetry).TotalMilliseconds >= 200)
            {
                _timeCheckRetry = now;
                double elapsed = (now - tradeStartTime).TotalSeconds;

                if (elapsed >= MinDurationSeconds)
                    EjecutarSalidaPorTiempo();
            }

            // ── 3. REINTENTO PERIODICO DE REDUCCION DE SL ────────
            if (slChangePending && slTargetQty > 0 && exitOrdersPlaced
                && (now - _slRetryTime).TotalMilliseconds >= 300)
            {
                _slRetryTime = now;

                Order slOrd  = (longFillPrice  > 0) ? longSlOrder  : shortSlOrder;
                string ladoR = (longFillPrice  > 0) ? "LONG"       : "SHORT";

                if (slOrd != null)
                {
                    if (slOrd.Quantity == slTargetQty)
                    {
                        slChangePending = false;
                        slTargetQty     = 0;
                        NY930Log.Debug(SRC, "SL " + ladoR + " cantidad confirmada via polling: "
                              + slOrd.Quantity + " contratos.");
                    }
                    else if (slOrd.OrderState == OrderState.Working
                          || slOrd.OrderState == OrderState.Accepted)
                    {
                        ChangeOrder(slOrd, slTargetQty, 0, slOrd.StopPrice);
                        NY930Log.Info(SRC, "SL " + ladoR + " reintento ReducirSL via polling: "
                              + slOrd.Quantity + " → " + slTargetQty + " contratos.");
                    }
                }
            }

            // ── 4. SINGLE-STOP REVERSE-TICK PROTECTION ───────────
            EvaluarSingleStopReverse(now);

            // ── 5. TP / SL GAP GUARDS ────────────────────────────
            EvaluarGapGuards(now);

            // ── 6. BREAKEVEN ──────────────────────────────────────
            if (EnableBreakeven && !breakevenApplied && !breakevenSent
                && exitOrdersPlaced && lastPrice > 0)
            {
                if (longFillPrice > 0
                    && longSlOrder  != null
                    && (longSlOrder.OrderState == OrderState.Working
                        || longSlOrder.OrderState == OrderState.Accepted)
                    && lastPrice >= longFillPrice + BreakevenTriggerTicks * TickSize)
                {
                    double bePrice = Math.Round(
                        (longFillPrice + BreakevenOffsetTicks * TickSize) / TickSize) * TickSize;

                    if (bePrice > longSlOrder.StopPrice)
                    {
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        ChangeOrder(longSlOrder, qty, 0, bePrice);
                        longCurrentSlPrice = bePrice;
                        breakevenSent  = true;
                        bePricePending = bePrice;
                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "BE enviado al broker (LONG)");
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Info(SRC, "  Entrada Long  : " + longFillPrice);
                        NY930Log.Info(SRC, "  SL solicitado : " + bePrice);
                        NY930Log.Separator(SRC);
                    }
                }
                else if (shortFillPrice > 0
                         && shortSlOrder  != null
                         && (shortSlOrder.OrderState == OrderState.Working
                             || shortSlOrder.OrderState == OrderState.Accepted)
                         && lastPrice <= shortFillPrice - BreakevenTriggerTicks * TickSize)
                {
                    double bePrice = Math.Round(
                        (shortFillPrice - BreakevenOffsetTicks * TickSize) / TickSize) * TickSize;

                    if (bePrice < shortSlOrder.StopPrice)
                    {
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        ChangeOrder(shortSlOrder, qty, 0, bePrice);
                        shortCurrentSlPrice = bePrice;
                        breakevenSent  = true;
                        bePricePending = bePrice;
                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "BE enviado al broker (SHORT)");
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Info(SRC, "  Entrada Short : " + shortFillPrice);
                        NY930Log.Info(SRC, "  SL solicitado : " + bePrice);
                        NY930Log.Separator(SRC);
                    }
                }
            }

            // ── 7. TRAILING STOP ──────────────────────────────────
            if (EnableTrailing && exitOrdersPlaced && !trailSent && lastPrice > 0)
            {
                if (!trailActive)
                {
                    bool beOk = !EnableBreakeven || breakevenApplied;

                    if (beOk)
                    {
                        if (longFillPrice > 0 && longSlOrder != null
                            && (longSlOrder.OrderState == OrderState.Working
                                || longSlOrder.OrderState == OrderState.Accepted))
                        {
                            trailCurrentSl = (EnableBreakeven && breakevenApplied && bePricePending > 0)
                                             ? bePricePending
                                             : longSlOrder.StopPrice;
                            trailActive    = true;
                            NY930Log.Separator(SRC);
                            NY930Log.Info(SRC, "Trailing LONG activado");
                            NY930Log.Info(SRC, "  Ancla inicial SL : " + trailCurrentSl);
                            NY930Log.Separator(SRC);
                        }
                        else if (shortFillPrice > 0 && shortSlOrder != null
                                 && (shortSlOrder.OrderState == OrderState.Working
                                     || shortSlOrder.OrderState == OrderState.Accepted))
                        {
                            trailCurrentSl = (EnableBreakeven && breakevenApplied && bePricePending > 0)
                                             ? bePricePending
                                             : shortSlOrder.StopPrice;
                            trailActive    = true;
                            NY930Log.Separator(SRC);
                            NY930Log.Info(SRC, "Trailing SHORT activado");
                            NY930Log.Info(SRC, "  Ancla inicial SL : " + trailCurrentSl);
                            NY930Log.Separator(SRC);
                        }
                    }
                }

                if (trailActive)
                {
                    if (longFillPrice > 0
                        && longSlOrder != null
                        && (longSlOrder.OrderState == OrderState.Working
                            || longSlOrder.OrderState == OrderState.Accepted)
                        && lastPrice >= trailCurrentSl + TrailTriggerTicks * TickSize)
                    {
                        double newSl = Math.Round(
                            (trailCurrentSl + TrailStepTicks * TickSize) / TickSize) * TickSize;

                        int qty         = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        trailPreviousSl = trailCurrentSl;
                        trailCurrentSl  = newSl;
                        ChangeOrder(longSlOrder, qty, 0, newSl);
                        longCurrentSlPrice = newSl;
                        trailSent = true;

                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "Trail LONG — escalon enviado");
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Info(SRC, "  Nuevo SL      : " + newSl);
                        NY930Log.Separator(SRC);
                    }
                    else if (shortFillPrice > 0
                             && shortSlOrder != null
                             && (shortSlOrder.OrderState == OrderState.Working
                                 || shortSlOrder.OrderState == OrderState.Accepted)
                             && lastPrice <= trailCurrentSl - TrailTriggerTicks * TickSize)
                    {
                        double newSl = Math.Round(
                            (trailCurrentSl - TrailStepTicks * TickSize) / TickSize) * TickSize;

                        int qty         = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        trailPreviousSl = trailCurrentSl;
                        trailCurrentSl  = newSl;
                        ChangeOrder(shortSlOrder, qty, 0, newSl);
                        shortCurrentSlPrice = newSl;
                        trailSent = true;

                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "Trail SHORT — escalon enviado");
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Info(SRC, "  Nuevo SL      : " + newSl);
                        NY930Log.Separator(SRC);
                    }
                }
            }

            // ── 8. TRAILING TP ────────────────────────────────────
            if (EnableTrailingTP && exitOrdersPlaced && !tpTimeoutFired && lastPrice > 0)
            {
                if (longFillPrice > 0
                    && longTpOrder != null
                    && (longTpOrder.OrderState == OrderState.Working
                        || longTpOrder.OrderState == OrderState.Accepted)
                    && lastPrice > longTpOrder.LimitPrice)
                {
                    if (!tpTrailActive)
                    {
                        tpTrailActive   = true;
                        tpTrailMaxPrice = lastPrice;
                        tpCrossedTime   = DateTime.Now;
                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "Trailing TP LONG activado");
                        NY930Log.Info(SRC, "  TP original   : " + longTpOrder.LimitPrice);
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Separator(SRC);
                    }

                    if (lastPrice > tpTrailMaxPrice)
                    {
                        tpTrailMaxPrice = lastPrice;

                        double nuevoTP = Math.Round(
                            (tpTrailMaxPrice - TrailingTPDistanceTicks * TickSize)
                            / TickSize) * TickSize;

                        if (nuevoTP > longTpOrder.LimitPrice)
                        {
                            int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                            ChangeOrder(longTpOrder, qty, nuevoTP, 0);
                            longCurrentTpPrice = nuevoTP;
                            NY930Log.Info(SRC, "Trailing TP LONG → " + nuevoTP
                                  + "  (max=" + tpTrailMaxPrice + ")");
                        }
                    }

                    if ((DateTime.Now - tpCrossedTime).TotalSeconds > TrailingTPTimeoutSeconds)
                    {
                        tpTimeoutFired  = true;
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;

                        NY930Log.Separator(SRC);
                        NY930Log.Warn(SRC, "Trailing TP LONG TIMEOUT — cerrando a mercado.");
                        NY930Log.Separator(SRC);

                        if (longTpOrder != null
                            && (longTpOrder.OrderState == OrderState.Working
                                || longTpOrder.OrderState == OrderState.Accepted))
                            CancelOrder(longTpOrder);

                        if (longSlOrder != null
                            && (longSlOrder.OrderState == OrderState.Working
                                || longSlOrder.OrderState == OrderState.Accepted))
                            CancelOrder(longSlOrder);

                        SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market,
                            qty, 0, 0, string.Empty, "LONG_TP_TIMEOUT");
                    }
                }
                else if (shortFillPrice > 0
                         && shortTpOrder != null
                         && (shortTpOrder.OrderState == OrderState.Working
                             || shortTpOrder.OrderState == OrderState.Accepted)
                         && lastPrice < shortTpOrder.LimitPrice)
                {
                    if (!tpTrailActive)
                    {
                        tpTrailActive   = true;
                        tpTrailMinPrice = lastPrice;
                        tpCrossedTime   = DateTime.Now;
                        NY930Log.Separator(SRC);
                        NY930Log.Info(SRC, "Trailing TP SHORT activado");
                        NY930Log.Info(SRC, "  TP original   : " + shortTpOrder.LimitPrice);
                        NY930Log.Info(SRC, "  Precio actual : " + lastPrice);
                        NY930Log.Separator(SRC);
                    }

                    if (lastPrice < tpTrailMinPrice || tpTrailMinPrice == 0)
                    {
                        tpTrailMinPrice = lastPrice;

                        double nuevoTP = Math.Round(
                            (tpTrailMinPrice + TrailingTPDistanceTicks * TickSize)
                            / TickSize) * TickSize;

                        if (nuevoTP < shortTpOrder.LimitPrice)
                        {
                            int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                            ChangeOrder(shortTpOrder, qty, nuevoTP, 0);
                            shortCurrentTpPrice = nuevoTP;
                            NY930Log.Info(SRC, "Trailing TP SHORT → " + nuevoTP
                                  + "  (min=" + tpTrailMinPrice + ")");
                        }
                    }

                    if ((DateTime.Now - tpCrossedTime).TotalSeconds > TrailingTPTimeoutSeconds)
                    {
                        tpTimeoutFired  = true;
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;

                        NY930Log.Separator(SRC);
                        NY930Log.Warn(SRC, "Trailing TP SHORT TIMEOUT — cerrando a mercado.");
                        NY930Log.Separator(SRC);

                        if (shortTpOrder != null
                            && (shortTpOrder.OrderState == OrderState.Working
                                || shortTpOrder.OrderState == OrderState.Accepted))
                            CancelOrder(shortTpOrder);

                        if (shortSlOrder != null
                            && (shortSlOrder.OrderState == OrderState.Working
                                || shortSlOrder.OrderState == OrderState.Accepted))
                            CancelOrder(shortSlOrder);

                        SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market,
                            qty, 0, 0, string.Empty, "SHORT_TP_TIMEOUT");
                    }
                }
            }

            // ── 9. Publish snapshot to UI (throttled) ─────────────
            if ((now - _lastSnapshotPush).TotalMilliseconds >= 200)
            {
                _lastSnapshotPush = now;
                PublicarSnapshot();
            }
        }

        // ── EvaluarGapGuards ──────────────────────────────────────
        // Mirrors the hedge implementation but evaluated on whichever
        // side actually filled (longFillPrice > 0 or shortFillPrice > 0).
        private void EvaluarGapGuards(DateTime now)
        {
            if (!exitOrdersPlaced || lastPrice <= 0) return;
            if (closingPosition) return;

            bool   esLong   = longFillPrice > 0;
            double fillRef  = esLong ? longFillPrice : shortFillPrice;
            if (fillRef <= 0) return;

            Order  tpOrd    = esLong ? longTpOrder    : shortTpOrder;
            Order  slOrd    = esLong ? longSlOrder    : shortSlOrder;

            // ── TP guard ─────────────────────────────────────────
            if (EnableTpGapGuard && !tpGapGuardFired && tpOrd != null
                && (tpOrd.OrderState == OrderState.Working
                    || tpOrd.OrderState == OrderState.Accepted))
            {
                double tp = tpOrd.LimitPrice;
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
                        NY930Log.Warn(SRC, "TP GAP GUARD disparado (" + (esLong ? "LONG" : "SHORT") + ")");
                        NY930Log.Warn(SRC, "  TP            : " + tp);
                        NY930Log.Warn(SRC, "  Precio actual : " + lastPrice + " (over " + overTicks + " ticks)");
                        NY930Log.Warn(SRC, "  Tiempo over   : " + elapsed.ToString("F1") + "s");
                        NY930Log.Warn(SRC, "  Motivo        : " + (tickTrip ? "ticks" : "tiempo"));
                        NY930Log.Separator(SRC);

                        ForzarCierreAMercado(esLong, qty, slOrd,
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
            if (EnableSlGapGuard && !slGapGuardFired && slOrd != null
                && (slOrd.OrderState == OrderState.Working
                    || slOrd.OrderState == OrderState.Accepted))
            {
                double sl = slOrd.StopPrice;
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
                        NY930Log.Warn(SRC, "SL GAP GUARD disparado (" + (esLong ? "LONG" : "SHORT") + ")");
                        NY930Log.Warn(SRC, "  SL            : " + sl);
                        NY930Log.Warn(SRC, "  Precio actual : " + lastPrice + " (over " + overTicks + " ticks)");
                        NY930Log.Warn(SRC, "  Tiempo over   : " + elapsed.ToString("F1") + "s");
                        NY930Log.Warn(SRC, "  Motivo        : " + (tickTrip ? "ticks" : "tiempo"));
                        NY930Log.Separator(SRC);

                        ForzarCierreAMercado(esLong, qty, slOrd,
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

        // ── EvaluarSingleStopReverse ──────────────────────────────
        // Active only while exactly one of EnableLong/EnableShort is true,
        // entry order is still working (no fill yet) and price has moved
        // against the entry by N ticks (default = stop offset).
        private void EvaluarSingleStopReverse(DateTime now)
        {
            if (!EnableSingleStopReverseProtection) return;
            if (singleStopCancelFired) return;
            if (exitOrdersPlaced || !ordersPlaced || lastPrice <= 0) return;
            if (singleStopAnchorPrice <= 0) return;

            bool onlyLong  =  EnableLong && !EnableShort
                           && longEntryOrder != null
                           && (longEntryOrder.OrderState == OrderState.Working
                            || longEntryOrder.OrderState == OrderState.Accepted);

            bool onlyShort = !EnableLong &&  EnableShort
                           && shortEntryOrder != null
                           && (shortEntryOrder.OrderState == OrderState.Working
                            || shortEntryOrder.OrderState == OrderState.Accepted);

            if (!onlyLong && !onlyShort) return;

            // For a Buy Stop placed +TicksLong above the anchor, the
            // protective check is "anchor - lastPrice >= N". For a Sell
            // Stop placed -TicksShort below the anchor, "lastPrice - anchor".
            int    threshold = SingleStopReverseTicks > 0
                ? SingleStopReverseTicks
                : (onlyLong ? TicksLong : TicksShort);

            double against;
            string lado;
            if (onlyLong)
            {
                against = singleStopAnchorPrice - lastPrice;
                lado    = "BUY STOP";
            }
            else
            {
                against = lastPrice - singleStopAnchorPrice;
                lado    = "SELL STOP";
            }

            int againstTicks = (int)Math.Round(against / TickSize);
            if (againstTicks < threshold) return;

            singleStopCancelFired = true;

            NY930Log.Separator(SRC);
            NY930Log.Warn(SRC, "Single-Stop Reverse — cancelando " + lado);
            NY930Log.Warn(SRC, "  Anchor        : " + singleStopAnchorPrice);
            NY930Log.Warn(SRC, "  Precio actual : " + lastPrice + " (" + againstTicks + " ticks en contra)");
            NY930Log.Warn(SRC, "  Threshold     : " + threshold + " ticks");
            NY930Log.Separator(SRC);

            CancelarOrdenesEntrada();
            sessionDone = true;
        }

        private void CancelarOrdenesEntrada()
        {
            if (longEntryOrder != null
                && (longEntryOrder.OrderState == OrderState.Working
                 || longEntryOrder.OrderState == OrderState.Accepted))
                CancelOrder(longEntryOrder);

            if (shortEntryOrder != null
                && (shortEntryOrder.OrderState == OrderState.Working
                 || shortEntryOrder.OrderState == OrderState.Accepted))
                CancelOrder(shortEntryOrder);
        }

        // ── ForzarCierreAMercado (used by gap guards) ─────────────
        private void ForzarCierreAMercado(bool esLong, int qty, Order slOrd, string etiqueta)
        {
            closingPosition = true;
            CancelarParciales();

            if (slOrd != null
                && (slOrd.OrderState == OrderState.Working
                    || slOrd.OrderState == OrderState.Accepted))
                CancelOrder(slOrd);

            Order tpOrd = esLong ? longTpOrder : shortTpOrder;
            if (tpOrd != null
                && (tpOrd.OrderState == OrderState.Working
                    || tpOrd.OrderState == OrderState.Accepted))
                CancelOrder(tpOrd);

            if (esLong)
                SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market,
                    qty, 0, 0, string.Empty, etiqueta);
            else
                SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market,
                    qty, 0, 0, string.Empty, etiqueta);
        }

        // ── ColocarOrdenes: llamado por TriggerCustomEvent desde Timer ─
        private void ColocarOrdenes()
        {
            if (ordersPlaced || sessionDone) return;

            DateTime ahora     = DateTime.Now;
            DateTime entryTime = new DateTime(ahora.Year, ahora.Month, ahora.Day,
                                              EntryHour, EntryMinute, EntrySecond);
            if (ahora < entryTime.AddSeconds(-2))
            {
                NY930Log.Warn(SRC, "ColocarOrdenes — disparo espurio ignorado @ "
                      + ahora.ToString("HH:mm:ss.fff"));
                ProgramarTimer();
                return;
            }
            if (lastPrice <= 0)
            {
                NY930Log.Warn(SRC, "lastPrice no disponible al disparar el timer.");
                return;
            }
            if (!EnableLong && !EnableShort)
            {
                ordersPlaced = true;
                sessionDone  = true;
                NY930Log.Separator(SRC);
                NY930Log.Warn(SRC, "Long y Short deshabilitados. No se colocan ordenes.");
                NY930Log.Separator(SRC);
                return;
            }

            ColocarOrdenesEntradaEnPrecio(lastPrice);
        }

        private void ColocarOrdenesEntradaEnPrecio(double refPrice)
        {
            longStopPrice  = Math.Round((refPrice + TicksLong  * TickSize) / TickSize) * TickSize;
            shortStopPrice = Math.Round((refPrice - TicksShort * TickSize) / TickSize) * TickSize;
            singleStopAnchorPrice = refPrice;

            bool usarOcoCompartido = EnableLong && EnableShort;

            ocoEntry = "ENTRY_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            ocoLong  = "OCO_L_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            ocoShort = "OCO_S_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            NY930Log.Separator(SRC);
            NY930Log.Info(SRC, "Ordenes colocadas");
            NY930Log.Info(SRC, "  Precio referencia : " + refPrice);

            if (EnableLong)
            {
                longEntryOrder = SubmitOrderUnmanaged(
                    0, OrderAction.Buy, OrderType.StopMarket,
                    Quantity, 0, longStopPrice, ocoEntry, "LONG_ENTRY");
                NY930Log.Info(SRC, "  BuyStop  (Long)   : " + longStopPrice + "  (+" + TicksLong + " ticks)");
            }
            else NY930Log.Info(SRC, "  BuyStop  (Long)   : DESHABILITADO");

            if (EnableShort)
            {
                shortEntryOrder = SubmitOrderUnmanaged(
                    0, OrderAction.SellShort, OrderType.StopMarket,
                    Quantity, 0, shortStopPrice, ocoEntry, "SHORT_ENTRY");
                NY930Log.Info(SRC, "  SellStop (Short)  : " + shortStopPrice + "  (-" + TicksShort + " ticks)");
            }
            else NY930Log.Info(SRC, "  SellStop (Short)  : DESHABILITADO");

            NY930Log.Info(SRC, "  OCO Entrada       : " + ocoEntry
                  + (usarOcoCompartido ? " (compartido)" : " (un lado)"));
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
            if (execution.Quantity <= 0) return;

            double fillPrice = execution.Price;

            // ── FILL DE ENTRADA LONG ─────────────────────────────
            if (!exitOrdersPlaced
                && longEntryOrder != null
                && execution.Order == longEntryOrder)
            {
                exitOrdersPlaced   = true;
                sessionDone        = true;
                longFillPrice      = fillPrice;
                contratosRestantes = Quantity;
                closingPosition    = false;
                tradeStartTime     = DateTime.Now;

                double slPrice = Math.Round((fillPrice - StopLossLongTicks   * TickSize) / TickSize) * TickSize;
                double tpPrice = Math.Round((fillPrice + TakeProfitLongTicks * TickSize) / TickSize) * TickSize;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "LONG llenado en " + fillPrice);
                if (EnableShort)
                    NY930Log.Info(SRC, "  SHORT cancelado por OCO");

                if (EnablePartials && Partial1Ticks > 0 && Partial1Contracts > 0)
                {
                    bool usaP2 = Partial2Ticks > 0 && Partial2Contracts > 0;
                    int  tpQty = Quantity - Partial1Contracts - (usaP2 ? Partial2Contracts : 0);

                    double p1Price = Math.Round((fillPrice + Partial1Ticks * TickSize) / TickSize) * TickSize;
                    double p2Price = usaP2
                                  ? Math.Round((fillPrice + Partial2Ticks * TickSize) / TickSize) * TickSize
                                  : 0;

                    longSlOrder = SubmitOrderUnmanaged(
                        0, OrderAction.Sell, OrderType.StopMarket,
                        Quantity, 0, slPrice, ocoLong, "LONG_SL");
                    longCurrentSlPrice = slPrice;

                    longP1Order = SubmitOrderUnmanaged(
                        0, OrderAction.Sell, OrderType.Limit,
                        Partial1Contracts, p1Price, 0, string.Empty, "LONG_P1");
                    longCurrentP1Price = p1Price;

                    if (usaP2)
                    {
                        longP2Order = SubmitOrderUnmanaged(
                            0, OrderAction.Sell, OrderType.Limit,
                            Partial2Contracts, p2Price, 0, string.Empty, "LONG_P2");
                        longCurrentP2Price = p2Price;
                    }

                    if (tpQty > 0)
                    {
                        if (EnableTimeExit)
                        {
                            tpRetained         = true;
                            retainedLongTP     = tpPrice;
                            retainedTpQty      = tpQty;
                            longCurrentTpPrice = tpPrice;
                            NY930Log.Info(SRC, "  TP RETENIDO (SalidaPorTiempo): se colocara tras "
                                  + MinDurationSeconds + "s @ " + tpPrice);
                        }
                        else
                        {
                            longTpOrder = SubmitOrderUnmanaged(
                                0, OrderAction.Sell, OrderType.Limit,
                                tpQty, tpPrice, 0, ocoLong, "LONG_TP");
                            longCurrentTpPrice = tpPrice;
                        }
                    }

                    NY930Log.Info(SRC, "  SL : " + slPrice  + "  [" + Quantity + " contratos]");
                    NY930Log.Info(SRC, "  P1 : " + p1Price  + "  [" + Partial1Contracts + " contratos]");
                    if (usaP2)
                        NY930Log.Info(SRC, "  P2 : " + p2Price + "  [" + Partial2Contracts + " contratos]");
                    if (tpQty > 0 && !EnableTimeExit)
                        NY930Log.Info(SRC, "  TP : " + tpPrice + "  [" + tpQty + " contratos]");
                }
                else
                {
                    longSlOrder = SubmitOrderUnmanaged(
                        0, OrderAction.Sell, OrderType.StopMarket,
                        Quantity, 0, slPrice, ocoLong, "LONG_SL");
                    longCurrentSlPrice = slPrice;

                    if (EnableTimeExit)
                    {
                        tpRetained         = true;
                        retainedLongTP     = tpPrice;
                        retainedTpQty      = Quantity;
                        longCurrentTpPrice = tpPrice;
                        NY930Log.Info(SRC, "  SL Long : " + slPrice);
                        NY930Log.Info(SRC, "  TP RETENIDO (SalidaPorTiempo)");
                    }
                    else
                    {
                        longTpOrder = SubmitOrderUnmanaged(
                            0, OrderAction.Sell, OrderType.Limit,
                            Quantity, tpPrice, 0, ocoLong, "LONG_TP");
                        longCurrentTpPrice = tpPrice;
                        NY930Log.Info(SRC, "  SL Long : " + slPrice);
                        NY930Log.Info(SRC, "  TP Long : " + tpPrice);
                    }
                }
                NY930Log.Separator(SRC);
            }

            // ── FILL DE ENTRADA SHORT ────────────────────────────
            else if (!exitOrdersPlaced
                     && shortEntryOrder != null
                     && execution.Order == shortEntryOrder)
            {
                exitOrdersPlaced   = true;
                sessionDone        = true;
                shortFillPrice     = fillPrice;
                contratosRestantes = Quantity;
                closingPosition    = false;
                tradeStartTime     = DateTime.Now;

                double slPrice = Math.Round((fillPrice + StopLossShortTicks   * TickSize) / TickSize) * TickSize;
                double tpPrice = Math.Round((fillPrice - TakeProfitShortTicks * TickSize) / TickSize) * TickSize;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "SHORT llenado en " + fillPrice);
                if (EnableLong)
                    NY930Log.Info(SRC, "  LONG cancelado por OCO");

                if (EnablePartials && Partial1Ticks > 0 && Partial1Contracts > 0)
                {
                    bool usaP2 = Partial2Ticks > 0 && Partial2Contracts > 0;
                    int  tpQty = Quantity - Partial1Contracts - (usaP2 ? Partial2Contracts : 0);

                    double p1Price = Math.Round((fillPrice - Partial1Ticks * TickSize) / TickSize) * TickSize;
                    double p2Price = usaP2
                                  ? Math.Round((fillPrice - Partial2Ticks * TickSize) / TickSize) * TickSize
                                  : 0;

                    shortSlOrder = SubmitOrderUnmanaged(
                        0, OrderAction.BuyToCover, OrderType.StopMarket,
                        Quantity, 0, slPrice, ocoShort, "SHORT_SL");
                    shortCurrentSlPrice = slPrice;

                    shortP1Order = SubmitOrderUnmanaged(
                        0, OrderAction.BuyToCover, OrderType.Limit,
                        Partial1Contracts, p1Price, 0, string.Empty, "SHORT_P1");
                    shortCurrentP1Price = p1Price;

                    if (usaP2)
                    {
                        shortP2Order = SubmitOrderUnmanaged(
                            0, OrderAction.BuyToCover, OrderType.Limit,
                            Partial2Contracts, p2Price, 0, string.Empty, "SHORT_P2");
                        shortCurrentP2Price = p2Price;
                    }

                    if (tpQty > 0)
                    {
                        if (EnableTimeExit)
                        {
                            tpRetained          = true;
                            retainedShortTP     = tpPrice;
                            retainedTpQty       = tpQty;
                            shortCurrentTpPrice = tpPrice;
                            NY930Log.Info(SRC, "  TP RETENIDO (SalidaPorTiempo)");
                        }
                        else
                        {
                            shortTpOrder = SubmitOrderUnmanaged(
                                0, OrderAction.BuyToCover, OrderType.Limit,
                                tpQty, tpPrice, 0, ocoShort, "SHORT_TP");
                            shortCurrentTpPrice = tpPrice;
                        }
                    }

                    NY930Log.Info(SRC, "  SL : " + slPrice  + "  [" + Quantity + " contratos]");
                    NY930Log.Info(SRC, "  P1 : " + p1Price  + "  [" + Partial1Contracts + " contratos]");
                    if (usaP2)
                        NY930Log.Info(SRC, "  P2 : " + p2Price + "  [" + Partial2Contracts + " contratos]");
                    if (tpQty > 0 && !EnableTimeExit)
                        NY930Log.Info(SRC, "  TP : " + tpPrice + "  [" + tpQty + " contratos]");
                }
                else
                {
                    shortSlOrder = SubmitOrderUnmanaged(
                        0, OrderAction.BuyToCover, OrderType.StopMarket,
                        Quantity, 0, slPrice, ocoShort, "SHORT_SL");
                    shortCurrentSlPrice = slPrice;

                    if (EnableTimeExit)
                    {
                        tpRetained          = true;
                        retainedShortTP     = tpPrice;
                        retainedTpQty       = Quantity;
                        shortCurrentTpPrice = tpPrice;
                        NY930Log.Info(SRC, "  SL Short : " + slPrice);
                        NY930Log.Info(SRC, "  TP RETENIDO (SalidaPorTiempo)");
                    }
                    else
                    {
                        shortTpOrder = SubmitOrderUnmanaged(
                            0, OrderAction.BuyToCover, OrderType.Limit,
                            Quantity, tpPrice, 0, ocoShort, "SHORT_TP");
                        shortCurrentTpPrice = tpPrice;
                        NY930Log.Info(SRC, "  SL Short : " + slPrice);
                        NY930Log.Info(SRC, "  TP Short : " + tpPrice);
                    }
                }
                NY930Log.Separator(SRC);
            }

            // ── FILL DE TP RETARDADO ─────────────────────────────
            else if (delayedTpOrder != null && execution.Order == delayedTpOrder)
            {
                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "TP RETARDADO llenado @ " + fillPrice);

                Order slOrd = longFillPrice > 0 ? longSlOrder : shortSlOrder;
                if (slOrd != null
                    && (slOrd.OrderState == OrderState.Working
                        || slOrd.OrderState == OrderState.Accepted))
                {
                    CancelOrder(slOrd);
                    NY930Log.Info(SRC, "  SL cancelado tras fill de TP retardado.");
                }

                delayedTpOrder = null;
                timeExitFired  = true;
                tpRetained     = false;
                CapturarResultado(longFillPrice > 0, fillPrice, "TP_DELAYED");
                NY930Log.Separator(SRC);
            }

            // ── FILL DE PARCIAL 1 LONG ───────────────────────────
            else if (longP1Order != null && execution.Order == longP1Order && !partial1Done)
            {
                partial1FilledQty  += execution.Quantity;
                contratosRestantes -= execution.Quantity;

                if (partial1FilledQty >= Partial1Contracts)
                    partial1Done = true;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "PARCIAL 1 LONG fill @ " + fillPrice);
                NY930Log.Info(SRC, "  Esta ejecucion      : " + execution.Quantity + " contratos");
                NY930Log.Info(SRC, "  P1 acumulado        : " + partial1FilledQty + " / " + Partial1Contracts);
                NY930Log.Info(SRC, "  Contratos restantes : " + contratosRestantes);
                if (partial1Done) NY930Log.Info(SRC, "  P1 COMPLETO.");

                ReducirSL(longSlOrder, contratosRestantes, "LONG");
                NY930Log.Separator(SRC);
            }

            // ── FILL DE PARCIAL 2 LONG ───────────────────────────
            else if (longP2Order != null && execution.Order == longP2Order && !partial2Done)
            {
                partial2FilledQty  += execution.Quantity;
                contratosRestantes -= execution.Quantity;

                if (partial2FilledQty >= Partial2Contracts)
                    partial2Done = true;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "PARCIAL 2 LONG fill @ " + fillPrice);
                NY930Log.Info(SRC, "  Esta ejecucion      : " + execution.Quantity + " contratos");
                NY930Log.Info(SRC, "  P2 acumulado        : " + partial2FilledQty + " / " + Partial2Contracts);
                NY930Log.Info(SRC, "  Contratos restantes : " + contratosRestantes);
                if (partial2Done) NY930Log.Info(SRC, "  P2 COMPLETO.");

                ReducirSL(longSlOrder, contratosRestantes, "LONG");
                NY930Log.Separator(SRC);
            }

            // ── FILL DE PARCIAL 1 SHORT ──────────────────────────
            else if (shortP1Order != null && execution.Order == shortP1Order && !partial1Done)
            {
                partial1FilledQty  += execution.Quantity;
                contratosRestantes -= execution.Quantity;

                if (partial1FilledQty >= Partial1Contracts)
                    partial1Done = true;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "PARCIAL 1 SHORT fill @ " + fillPrice);
                NY930Log.Info(SRC, "  Esta ejecucion      : " + execution.Quantity + " contratos");
                NY930Log.Info(SRC, "  P1 acumulado        : " + partial1FilledQty + " / " + Partial1Contracts);
                NY930Log.Info(SRC, "  Contratos restantes : " + contratosRestantes);
                if (partial1Done) NY930Log.Info(SRC, "  P1 COMPLETO.");

                ReducirSL(shortSlOrder, contratosRestantes, "SHORT");
                NY930Log.Separator(SRC);
            }

            // ── FILL DE PARCIAL 2 SHORT ──────────────────────────
            else if (shortP2Order != null && execution.Order == shortP2Order && !partial2Done)
            {
                partial2FilledQty  += execution.Quantity;
                contratosRestantes -= execution.Quantity;

                if (partial2FilledQty >= Partial2Contracts)
                    partial2Done = true;

                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "PARCIAL 2 SHORT fill @ " + fillPrice);
                NY930Log.Info(SRC, "  Esta ejecucion      : " + execution.Quantity + " contratos");
                NY930Log.Info(SRC, "  P2 acumulado        : " + partial2FilledQty + " / " + Partial2Contracts);
                NY930Log.Info(SRC, "  Contratos restantes : " + contratosRestantes);
                if (partial2Done) NY930Log.Info(SRC, "  P2 COMPLETO.");

                ReducirSL(shortSlOrder, contratosRestantes, "SHORT");
                NY930Log.Separator(SRC);
            }
        }

        // ── EjecutarSalidaPorTiempo ────────────────────────────────
        private void EjecutarSalidaPorTiempo()
        {
            timeExitFired = true;
            tpRetained    = false;

            bool   esLong    = longFillPrice  > 0;
            double fillRef   = esLong ? longFillPrice  : shortFillPrice;
            double tpPrice   = esLong ? retainedLongTP : retainedShortTP;
            Order  slOrd     = esLong ? longSlOrder    : shortSlOrder;
            string lado      = esLong ? "LONG"         : "SHORT";
            int    qty       = contratosRestantes > 0 ? contratosRestantes : Quantity;

            double elapsed   = (DateTime.Now - tradeStartTime).TotalSeconds;

            NY930Log.Separator(SRC);
            NY930Log.Warn(SRC, "SALIDA POR TIEMPO (" + lado + ")");
            NY930Log.Info(SRC, "  Tiempo transcurrido  : " + elapsed.ToString("F1") + "s");
            NY930Log.Info(SRC, "  Precio actual        : " + lastPrice);
            NY930Log.Info(SRC, "  Fill entrada         : " + fillRef);
            NY930Log.Info(SRC, "  TP retenido          : " + tpPrice);
            NY930Log.Info(SRC, "  Contratos a cerrar   : " + qty);

            bool precioBeyondTP = esLong  ? (lastPrice > tpPrice) : (lastPrice < tpPrice);
            if (CloseIfBeyondTP && precioBeyondTP)
            {
                NY930Log.Warn(SRC, "  [PASO 1] Precio supero TP → CIERRE A MERCADO");
                NY930Log.Separator(SRC);
                CerrarPosicionAMercado(esLong, qty, slOrd, "TIME_BEYOND_TP");
                return;
            }

            switch (ExitMode)
            {
                case TimeExitMode.CloseAlways:
                    NY930Log.Info(SRC, "  [MODO CloseAlways] → CIERRE A MERCADO");
                    NY930Log.Separator(SRC);
                    CerrarPosicionAMercado(esLong, qty, slOrd, "TIME_CLOSE_ALWAYS");
                    break;

                case TimeExitMode.CloseIfPositive:
                    double minProfit = fillRef + (esLong ? 1 : -1) * MinProfitTicks * TickSize;
                    bool   esPositivo = esLong ? (lastPrice >= minProfit) : (lastPrice <= minProfit);
                    if (esPositivo)
                    {
                        NY930Log.Info(SRC, "  [MODO CloseIfPositive] → CIERRE A MERCADO");
                        NY930Log.Separator(SRC);
                        CerrarPosicionAMercado(esLong, qty, slOrd, "TIME_CLOSE_POSITIVE");
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
                    if (esLong)
                    {
                        delayedTpOrder = SubmitOrderUnmanaged(
                            0, OrderAction.Sell, OrderType.Limit,
                            qty, tpPrice, 0, string.Empty, "LONG_TP_DELAYED");
                        longTpOrder        = delayedTpOrder;
                        longCurrentTpPrice = tpPrice;
                    }
                    else
                    {
                        delayedTpOrder = SubmitOrderUnmanaged(
                            0, OrderAction.BuyToCover, OrderType.Limit,
                            qty, tpPrice, 0, string.Empty, "SHORT_TP_DELAYED");
                        shortTpOrder        = delayedTpOrder;
                        shortCurrentTpPrice = tpPrice;
                    }
                    break;
            }
        }

        // ── CerrarPosicionAMercado ──────────────────────────────────
        private void CerrarPosicionAMercado(bool esLong, int qty, Order slOrd, string etiqueta)
        {
            closingPosition = true;
            CancelarParciales();

            if (slOrd != null
                && (slOrd.OrderState == OrderState.Working
                    || slOrd.OrderState == OrderState.Accepted))
            {
                CancelOrder(slOrd);
            }

            if (esLong)
                SubmitOrderUnmanaged(0, OrderAction.Sell,
                    OrderType.Market, qty, 0, 0, string.Empty, etiqueta);
            else
                SubmitOrderUnmanaged(0, OrderAction.BuyToCover,
                    OrderType.Market, qty, 0, 0, string.Empty, etiqueta);
        }

        // ── ReducirSL ─────────────────────────────────────────────
        private void ReducirSL(Order slOrder, int nuevaQty, string lado)
        {
            if (slOrder == null) return;
            slTargetQty = nuevaQty;

            if (slChangePending)
            {
                NY930Log.Debug(SRC, "  SL " + lado + " cambio ya en vuelo — objetivo actualizado a "
                      + nuevaQty + " contratos.");
                return;
            }

            OrderState estado = slOrder.OrderState;

            if (estado == OrderState.Working || estado == OrderState.Accepted)
            {
                ChangeOrder(slOrder, nuevaQty, 0, slOrder.StopPrice);
                slChangePending = true;
                NY930Log.Info(SRC, "  SL " + lado + " ChangeOrder enviado → " + nuevaQty + " contratos.");
            }
            else
            {
                slChangePending = true;
                NY930Log.Debug(SRC, "  SL " + lado + " en estado " + estado
                      + " — reintento programado para " + nuevaQty + " contratos.");
            }
        }

        // ── CancelarParciales ──────────────────────────────────────
        private void CancelarParciales()
        {
            if (longP1Order != null
                && (longP1Order.OrderState == OrderState.Working
                 || longP1Order.OrderState == OrderState.Accepted))
            {
                CancelOrder(longP1Order);
                NY930Log.Info(SRC, "LONG_P1 cancelado.");
            }
            if (longP2Order != null
                && (longP2Order.OrderState == OrderState.Working
                 || longP2Order.OrderState == OrderState.Accepted))
            {
                CancelOrder(longP2Order);
                NY930Log.Info(SRC, "LONG_P2 cancelado.");
            }
            if (shortP1Order != null
                && (shortP1Order.OrderState == OrderState.Working
                 || shortP1Order.OrderState == OrderState.Accepted))
            {
                CancelOrder(shortP1Order);
                NY930Log.Info(SRC, "SHORT_P1 cancelado.");
            }
            if (shortP2Order != null
                && (shortP2Order.OrderState == OrderState.Working
                 || shortP2Order.OrderState == OrderState.Accepted))
            {
                CancelOrder(shortP2Order);
                NY930Log.Info(SRC, "SHORT_P2 cancelado.");
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
                if (order == longEntryOrder)
                    NY930Log.Info(SRC, "BuyStop cancelado por OCO.");
                else if (order == shortEntryOrder)
                    NY930Log.Info(SRC, "SellStop cancelado por OCO.");
            }

            if (orderState == OrderState.Filled && EnablePartials)
            {
                if (order == longSlOrder || order == shortSlOrder)
                {
                    timeExitFired   = false;
                    tpRetained      = false;
                    NY930Log.Separator(SRC);
                    NY930Log.Warn(SRC, "SL ejecutado — cancelando parciales pendientes.");
                    CancelarParciales();
                    CapturarResultado(order == longSlOrder, averageFillPrice, "SL");
                    NY930Log.Separator(SRC);
                }
                else if (order == longTpOrder || order == shortTpOrder)
                {
                    NY930Log.Separator(SRC);
                    NY930Log.Info(SRC, "TP ejecutado — cancelando parciales pendientes.");
                    CancelarParciales();
                    CapturarResultado(order == longTpOrder, averageFillPrice, "TP");
                    NY930Log.Separator(SRC);
                }
            }

            if (!EnablePartials && EnableTimeExit
                && orderState == OrderState.Filled
                && (order == longSlOrder || order == shortSlOrder))
            {
                timeExitFired = false;
                tpRetained    = false;
                CapturarResultado(order == longSlOrder, averageFillPrice, "SL");
            }

            if (slChangePending
                && (order == longSlOrder || order == shortSlOrder)
                && (orderState == OrderState.Working || orderState == OrderState.Accepted)
                && error == ErrorCode.NoError)
            {
                if (order.Quantity != slTargetQty && slTargetQty > 0)
                {
                    string ladoSl = (order == longSlOrder) ? "LONG" : "SHORT";
                    NY930Log.Info(SRC, "Reintento ReducirSL (" + ladoSl + "): "
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
            if (order != longSlOrder && order != shortSlOrder) return;

            string lado = (order == longSlOrder) ? "LONG" : "SHORT";

            if (breakevenSent)
            {
                if (error != ErrorCode.NoError)
                {
                    breakevenSent = false;
                    NY930Log.Separator(SRC);
                    NY930Log.Error(SRC, "BE RECHAZADO por el broker (" + lado + ").");
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
                    NY930Log.Info(SRC, "BE CONFIRMADO por el broker (" + lado + ")");
                    NY930Log.Info(SRC, "  SL movido a : " + stopPrice);
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
                    NY930Log.Error(SRC, "Trail RECHAZADO por el broker (" + lado + ").");
                    NY930Log.Error(SRC, "  Error : " + error
                          + (string.IsNullOrEmpty(nativeError) ? "" : " / " + nativeError));
                    NY930Log.Info(SRC, "  Ancla revertida a : " + trailCurrentSl);
                    NY930Log.Separator(SRC);
                    return;
                }

                trailSent = false;
                NY930Log.Separator(SRC);
                NY930Log.Info(SRC, "Trail CONFIRMADO por el broker (" + lado + ")");
                NY930Log.Info(SRC, "  Nuevo SL activo : " + stopPrice);
                NY930Log.Separator(SRC);
            }
        }

        // ── CapturarResultado ──────────────────────────────────────
        private void CapturarResultado(bool esLong, double exitPrice, string reason)
        {
            double fillRef = esLong ? longFillPrice : shortFillPrice;
            if (fillRef <= 0 || exitPrice <= 0) return;

            double pnlTicks = ((esLong ? exitPrice - fillRef : fillRef - exitPrice) / TickSize);
            double pnlCcy   = pnlTicks * (Instrument != null ? Instrument.MasterInstrument.PointValue * TickSize : 0);

            _lastResult = new NY930TradeResult
            {
                Strategy    = "OpenRange",
                Instrument  = Instrument != null ? Instrument.FullName : null,
                Side        = esLong ? "Long" : "Short",
                EntryTime   = tradeStartTime,
                ExitTime    = DateTime.Now,
                EntryPrice  = fillRef,
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
            foreach (var a in NY930Bridge.DrainOpenRangeActions())
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
                case NY930ActionType.OpenRangeMoveBoth:
                    DoMove(a.IntArg);
                    break;

                case NY930ActionType.OpenRangeAdjustSpread:
                    DoDist(a.IntArg);
                    break;

                case NY930ActionType.OpenRangeCancelAll:
                    DoCancelAll();
                    break;

                case NY930ActionType.OpenRangeBuyNow:
                    if (!exitOrdersPlaced && !ordersPlaced && lastPrice > 0)
                    {
                        ColocarOrdenesEntradaEnPrecio(lastPrice);
                    }
                    break;

                case NY930ActionType.OpenRangeSellNow:
                    if (!exitOrdersPlaced && !ordersPlaced && lastPrice > 0)
                    {
                        ColocarOrdenesEntradaEnPrecio(lastPrice);
                    }
                    break;

                case NY930ActionType.OpenRangeFlatten:
                    if (exitOrdersPlaced && !closingPosition)
                    {
                        bool esLong = longFillPrice > 0;
                        Order slOrd = esLong ? longSlOrder : shortSlOrder;
                        int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
                        NY930Log.Warn(SRC, "Flatten manual desde UI.");
                        ForzarCierreAMercado(esLong, qty, slOrd, "MANUAL_FLATTEN");
                    }
                    break;

                case NY930ActionType.OpenRangeBreakeven:
                    AplicarBreakevenManual();
                    break;

                case NY930ActionType.OpenRangePartialClose:
                    AplicarParcialManual(a.IntArg);
                    break;
            }
        }

        // Manual move/distance (mirrors the OpenRangeControl logic but
        // re-uses the strategy's own state).
        private void DoMove(int ticks)
        {
            if (!BothEntryWorking()) return;

            double delta   = ticks * TickSize;
            longStopPrice  = Math.Round((longStopPrice  + delta) / TickSize) * TickSize;
            shortStopPrice = Math.Round((shortStopPrice + delta) / TickSize) * TickSize;
            singleStopAnchorPrice = (longStopPrice + shortStopPrice) / 2.0;

            ChangeOrder(longEntryOrder,  Quantity, 0, longStopPrice);
            ChangeOrder(shortEntryOrder, Quantity, 0, shortStopPrice);

            NY930Log.Info(SRC, "Ordenes movidas " + (ticks > 0 ? "▲ +" : "▼ ") + ticks
                  + " ticks  Buy=" + longStopPrice + "  Sell=" + shortStopPrice);
        }

        private void DoDist(int ticks)
        {
            if (!BothEntryWorking()) return;

            double mid  = (longStopPrice + shortStopPrice) / 2.0;
            double half = (longStopPrice - shortStopPrice) / 2.0;
            half        = Math.Max(TickSize, half + ticks * TickSize);

            longStopPrice  = Math.Round((mid + half) / TickSize) * TickSize;
            shortStopPrice = Math.Round((mid - half) / TickSize) * TickSize;

            ChangeOrder(longEntryOrder,  Quantity, 0, longStopPrice);
            ChangeOrder(shortEntryOrder, Quantity, 0, shortStopPrice);

            int sp = (int)Math.Round((longStopPrice - shortStopPrice) / TickSize);
            NY930Log.Info(SRC, "Spread " + (ticks > 0 ? "ampliado" : "reducido")
                  + " → " + sp + " ticks  Buy=" + longStopPrice + "  Sell=" + shortStopPrice);
        }

        private void DoCancelAll()
        {
            bool did = false;
            if (longEntryOrder != null
                && (longEntryOrder.OrderState == OrderState.Working
                 || longEntryOrder.OrderState == OrderState.Accepted))
            { CancelOrder(longEntryOrder); did = true; NY930Log.Info(SRC, "BuyStop cancelado por UI."); }

            if (shortEntryOrder != null
                && (shortEntryOrder.OrderState == OrderState.Working
                 || shortEntryOrder.OrderState == OrderState.Accepted))
            { CancelOrder(shortEntryOrder); did = true; NY930Log.Info(SRC, "SellStop cancelado por UI."); }

            if (did) sessionDone = true;
        }

        private bool BothEntryWorking()
        {
            bool longOk  = longEntryOrder  != null
                && (longEntryOrder.OrderState  == OrderState.Working
                 || longEntryOrder.OrderState  == OrderState.Accepted);
            bool shortOk = shortEntryOrder != null
                && (shortEntryOrder.OrderState == OrderState.Working
                 || shortEntryOrder.OrderState == OrderState.Accepted);

            if (longOk && shortOk) return true;
            NY930Log.Warn(SRC, "Accion ignorada — ordenes no activas en ambos lados.");
            return false;
        }

        private void AplicarBreakevenManual()
        {
            bool esLong = longFillPrice > 0;
            Order slOrd = esLong ? longSlOrder : shortSlOrder;
            double fillRef = esLong ? longFillPrice : shortFillPrice;

            if (slOrd == null || fillRef <= 0) return;

            double bePrice = esLong
                ? Math.Round((fillRef + Math.Max(1, BreakevenOffsetTicks) * TickSize) / TickSize) * TickSize
                : Math.Round((fillRef - Math.Max(1, BreakevenOffsetTicks) * TickSize) / TickSize) * TickSize;

            int qty = contratosRestantes > 0 ? contratosRestantes : Quantity;
            ChangeOrder(slOrd, qty, 0, bePrice);
            if (esLong) longCurrentSlPrice  = bePrice;
            else        shortCurrentSlPrice = bePrice;
            breakevenApplied = true;
            NY930Log.Info(SRC, "BE manual aplicado @ " + bePrice);
        }

        private void AplicarParcialManual(int qtyRequested)
        {
            if (qtyRequested <= 0 || contratosRestantes <= 0) return;
            bool esLong = longFillPrice > 0;
            int qty = Math.Min(qtyRequested, contratosRestantes);
            SubmitOrderUnmanaged(
                0,
                esLong ? OrderAction.Sell : OrderAction.BuyToCover,
                OrderType.Market,
                qty, 0, 0, string.Empty, "MANUAL_PARTIAL");
            NY930Log.Info(SRC, "Cierre parcial manual: " + qty + " contratos.");
        }

        // ── PublicarSnapshot ───────────────────────────────────────
        private void PublicarSnapshot()
        {
            try
            {
                bool inLong  = longFillPrice  > 0 && exitOrdersPlaced;
                bool inShort = shortFillPrice > 0 && exitOrdersPlaced;
                double fill  = inLong ? longFillPrice : (inShort ? shortFillPrice : 0);

                double upTicks = 0;
                if (fill > 0 && lastPrice > 0)
                    upTicks = ((inLong ? lastPrice - fill : fill - lastPrice) / TickSize);

                var snap = new NY930OpenRangeSnapshot
                {
                    Instrument          = Instrument != null ? Instrument.FullName : "(none)",
                    Timestamp           = DateTime.Now,
                    TickSize            = TickSize,
                    EnableLong          = EnableLong,
                    EnableShort         = EnableShort,

                    LongEntryStopPrice  = longStopPrice,
                    ShortEntryStopPrice = shortStopPrice,
                    LongEntryWorking    = longEntryOrder != null
                                          && (longEntryOrder.OrderState == OrderState.Working
                                           || longEntryOrder.OrderState == OrderState.Accepted),
                    ShortEntryWorking   = shortEntryOrder != null
                                          && (shortEntryOrder.OrderState == OrderState.Working
                                           || shortEntryOrder.OrderState == OrderState.Accepted),

                    InLong              = inLong,
                    InShort             = inShort,
                    EntryFill           = fill,
                    ContractsRemaining  = contratosRestantes,
                    Quantity            = Quantity,

                    SlPrice  = inLong ? longCurrentSlPrice : shortCurrentSlPrice,
                    TpPrice  = inLong ? longCurrentTpPrice : shortCurrentTpPrice,
                    P1Price  = inLong ? longCurrentP1Price : shortCurrentP1Price,
                    P2Price  = inLong ? longCurrentP2Price : shortCurrentP2Price,
                    Partial1Done = partial1Done,
                    Partial2Done = partial2Done,

                    LastPrice       = lastPrice,
                    UnrealizedTicks = upTicks,
                    TradeStartTime  = tradeStartTime,
                    SessionDone     = sessionDone,
                    LastResult      = _lastResult
                };

                NY930Bridge.PublishOpenRange(snap);
            }
            catch (Exception ex)
            {
                NY930Log.Error(SRC, "PublicarSnapshot error: " + ex.Message);
            }
        }

        // ────────────────────────────────────────────────────────
        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Hora (HH)", GroupName = "1. Horario", Order = 0)]
        public int EntryHour { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Minuto (MM)", GroupName = "1. Horario", Order = 1)]
        public int EntryMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Segundo (SS)", GroupName = "1. Horario", Order = 2)]
        public int EntrySecond { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Contratos", GroupName = "2. General", Order = 0)]
        public int Quantity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Long", GroupName = "3. Long", Order = 0)]
        public bool EnableLong { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks BuyStop sobre precio", GroupName = "3. Long", Order = 1)]
        public int TicksLong { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Stop Loss (ticks)", GroupName = "3. Long", Order = 2)]
        public int StopLossLongTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Take Profit (ticks)", GroupName = "3. Long", Order = 3)]
        public int TakeProfitLongTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Short", GroupName = "4. Short", Order = 0)]
        public bool EnableShort { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks SellStop bajo precio", GroupName = "4. Short", Order = 1)]
        public int TicksShort { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Stop Loss (ticks)", GroupName = "4. Short", Order = 2)]
        public int StopLossShortTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Take Profit (ticks)", GroupName = "4. Short", Order = 3)]
        public int TakeProfitShortTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Breakeven", GroupName = "5. Breakeven", Order = 0)]
        public bool EnableBreakeven { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks para activar BE", GroupName = "5. Breakeven", Order = 1)]
        public int BreakevenTriggerTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Ticks SL sobre/bajo entrada", GroupName = "5. Breakeven", Order = 2)]
        public int BreakevenOffsetTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Trailing Stop", GroupName = "6. Trailing Stop", Order = 0)]
        public bool EnableTrailing { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks para activar escalon", GroupName = "6. Trailing Stop", Order = 1)]
        public int TrailTriggerTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Ticks por escalon", GroupName = "6. Trailing Stop", Order = 2)]
        public int TrailStepTicks { get; set; }

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

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Trailing TP", GroupName = "8. Trailing TP", Order = 0)]
        public bool EnableTrailingTP { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Distancia al maximo (ticks)", GroupName = "8. Trailing TP", Order = 1)]
        public int TrailingTPDistanceTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Timeout sin fill (segundos)", GroupName = "8. Trailing TP", Order = 2)]
        public int TrailingTPTimeoutSeconds { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Salida por Tiempo", GroupName = "9. Salida por Tiempo", Order = 0)]
        public bool EnableTimeExit { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Duracion minima (segundos)", GroupName = "9. Salida por Tiempo", Order = 1)]
        public int MinDurationSeconds { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo de salida", GroupName = "9. Salida por Tiempo", Order = 2)]
        public TimeExitMode ExitMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Cerrar si precio supero TP (recomendado ON)", GroupName = "9. Salida por Tiempo", Order = 3)]
        public bool CloseIfBeyondTP { get; set; }

        public int MinProfitTicks { get; set; }

        // ── Grupo 10: NY930 Gap Guards ───────────────────────────

        [NinjaScriptProperty]
        [Display(Name = "Habilitar TP Gap Guard", GroupName = "10. NY930 Gap Guards", Order = 0)]
        public bool EnableTpGapGuard { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "TP Gap Guard — Ticks (0=off)", GroupName = "10. NY930 Gap Guards", Order = 1)]
        public int TpGapGuardTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "TP Gap Guard — Segundos (0=off)", GroupName = "10. NY930 Gap Guards", Order = 2)]
        public int TpGapGuardSeconds { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Habilitar SL Gap Guard", GroupName = "10. NY930 Gap Guards", Order = 3)]
        public bool EnableSlGapGuard { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "SL Gap Guard — Ticks (0=off)", GroupName = "10. NY930 Gap Guards", Order = 4)]
        public int SlGapGuardTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "SL Gap Guard — Segundos (0=off)", GroupName = "10. NY930 Gap Guards", Order = 5)]
        public int SlGapGuardSeconds { get; set; }

        // ── Grupo 11: Single-Stop Reverse-Tick Protection ────────
        // Active only when exactly one of EnableLong / EnableShort is
        // true. If price moves against the entry by N ticks before the
        // stop triggers, the pending entry is cancelled.
        // Default 0 → use the entry's own stop offset (TicksLong /
        // TicksShort), as agreed with the client.

        [NinjaScriptProperty]
        [Display(Name = "Habilitar Single-Stop Reverse Cancel", GroupName = "11. Single-Stop Reverse", Order = 0)]
        public bool EnableSingleStopReverseProtection { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Ticks en contra (0 = usar offset)", GroupName = "11. Single-Stop Reverse", Order = 1)]
        public int SingleStopReverseTicks { get; set; }

        #endregion
    }
}
