// ============================================================
//  NY930Localization — bilingual EN/ES with live switching
// ------------------------------------------------------------
//  - Two flat dictionaries.
//  - NY930Localization.T("key") returns the localised string for
//    the active language (falls back to the key itself).
//  - LanguageChanged event lets every visible TextBlock refresh
//    in place when the user toggles language from Settings.
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public enum NY930Language { English, Spanish }

    public static class NY930Localization
    {
        public static event Action LanguageChanged;

        private static NY930Language _current = NY930Language.English;

        public static NY930Language Current
        {
            get { return _current; }
            set
            {
                if (_current == value) return;
                _current = value;
                try { LanguageChanged?.Invoke(); } catch { }
            }
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var dict = _current == NY930Language.Spanish ? _es : _en;
            string val;
            return dict.TryGetValue(key, out val) ? val : key;
        }

        // ─────────────────────────────────────────────────────
        //  Strings
        //  Keys are intentionally human-readable so unrecognised
        //  keys still render acceptably as fallback.
        // ─────────────────────────────────────────────────────

        private static readonly Dictionary<string, string> _en = new Dictionary<string, string>
        {
            // Brand
            { "brand.tagline",            "MAKE MONEY EASY" },
            { "brand.subtitle",           "NY930 — 9:30 NY Open trading platform" },

            // Navigation
            { "nav.home",                 "Home" },
            { "nav.openrange",            "Open Range" },
            { "nav.hedge",                "Hedge" },
            { "nav.control",              "Open Range Control" },
            { "nav.progress",             "Trade Progress" },
            { "nav.result",               "Trade Result" },
            { "nav.settings",             "Settings" },
            { "nav.about",                "About" },
            { "nav.back",                 "Back" },
            { "nav.close",                "Close" },
            { "nav.locked",               "Cancel orders or close the trade first." },

            // Home
            { "home.openrange.title",     "OPEN RANGE" },
            { "home.openrange.desc",      "Buy Stop + Sell Stop placed automatically at the open." },
            { "home.hedge.title",         "HEDGE" },
            { "home.hedge.desc",          "Direct market entry with full position management." },
            { "home.hint",                "Click a strategy to start." },

            // Status
            { "status.waiting",           "Waiting for {0}" },
            { "status.active",            "Orders working" },
            { "status.in_long",           "In Long position" },
            { "status.in_short",          "In Short position" },
            { "status.cancelled",         "Orders cancelled" },
            { "status.session_done",      "Session done" },
            { "status.no_strategy",       "No strategy attached to a chart" },

            // Open Range card
            { "or.buystop",               "BUY STOP" },
            { "or.sellstop",              "SELL STOP" },
            { "or.spread",                "Spread" },
            { "or.move.title",            "MOVE BOTH" },
            { "or.move.sub",              "Keeps the distance" },
            { "or.spread.title",          "SPREAD" },
            { "or.spread.sub",            "Keeps the midpoint" },
            { "or.cancel",                "CANCEL ORDERS" },
            { "or.buy_now",               "BUY NOW" },
            { "or.sell_now",              "SELL NOW" },
            { "or.flatten",               "CLOSE POSITION" },

            // Hedge fields
            { "hedge.time",               "Entry time" },
            { "hedge.qty",                "Contracts" },
            { "hedge.direction",          "Direction" },
            { "hedge.long",               "Long" },
            { "hedge.short",              "Short" },
            { "hedge.none",               "(none)" },
            { "hedge.sl",                 "Stop Loss (ticks)" },
            { "hedge.tp",                 "Take Profit (ticks)" },
            { "hedge.be",                 "Breakeven" },
            { "hedge.be.trigger",         "BE trigger (ticks)" },
            { "hedge.be.offset",          "BE offset (ticks)" },
            { "hedge.trail",              "Trailing Stop" },
            { "hedge.trail.trigger",      "Trail trigger (ticks)" },
            { "hedge.trail.step",         "Trail step (ticks)" },
            { "hedge.trailtp",            "Trailing TP" },
            { "hedge.trailtp.dist",       "Distance from extreme (ticks)" },
            { "hedge.trailtp.timeout",    "Timeout if no fill (s)" },
            { "hedge.partials",           "Partials" },
            { "hedge.partials.p1t",       "P1 ticks" },
            { "hedge.partials.p1c",       "P1 contracts" },
            { "hedge.partials.p2t",       "P2 ticks (0 = off)" },
            { "hedge.partials.p2c",       "P2 contracts (0 = off)" },
            { "hedge.timeexit",           "Time Exit" },
            { "hedge.timeexit.dur",       "Min duration (s)" },
            { "hedge.timeexit.mode",      "Exit mode" },
            { "hedge.timeexit.beyondtp",  "Close if price beyond TP" },

            // Gap guard / single-stop reverse
            { "guard.tp",                 "TP Gap Guard" },
            { "guard.sl",                 "SL Gap Guard" },
            { "guard.ticks",              "Trigger ticks" },
            { "guard.seconds",            "Trigger seconds" },
            { "guard.singlestop",         "Single-stop reverse cancel" },
            { "guard.singlestop.ticks",   "Reverse ticks (0 = use stop offset)" },

            // Progress
            { "progress.title",           "Trade progress" },
            { "progress.tp1",             "TP1" },
            { "progress.tp2",             "TP2" },
            { "progress.tp3",             "TP" },
            { "progress.sl",              "SL" },
            { "progress.pnl",             "PnL" },
            { "progress.duration",        "Duration" },
            { "progress.contracts",       "Contracts" },
            { "progress.entry",           "Entry" },
            { "progress.last",            "Last" },
            { "progress.ticks",           "ticks" },

            // Result
            { "result.title",             "Trade result" },
            { "result.profit",            "Profit" },
            { "result.loss",              "Loss" },
            { "result.entry",             "Entry" },
            { "result.exit",              "Exit" },
            { "result.tp_hits",           "TP hits" },
            { "result.reason",            "Reason" },

            // Settings
            { "settings.language",        "Language" },
            { "settings.lang.en",         "English" },
            { "settings.lang.es",         "Spanish" },
            { "settings.about.title",     "About NY930" },
            { "settings.about.body",      "NY930 unifies Open Range and Hedge under a single, themed control plane for the 9:30 NY open. Phase 1." },

            // Common
            { "common.enabled",           "Enabled" },
            { "common.disabled",          "Disabled" },
            { "common.yes",               "Yes" },
            { "common.no",                "No" },
            { "common.ticks",             "ticks" },

            // Countdown / status
            { "status.countdown",         "Entry in {0}" },
            { "status.countdown.now",     "Entry now" },
            { "status.locked",            "Session locked - resumes tomorrow" },
            { "status.no_chart",          "Open a chart and add the strategy to start" },

            // Trade view sections
            { "trade.section.management", "POSITION MANAGEMENT" },
            { "trade.section.actions",    "ACTIONS" },
            { "trade.section.targets",    "TARGETS" },
            { "trade.section.stops",      "STOP" },
            { "trade.action.breakeven",   "BREAKEVEN" },
            { "trade.action.close_now",   "CLOSE NOW" },
            { "trade.action.partial",     "PARTIAL CLOSE" },
            { "trade.action.trailing",    "TRAILING STOP" },
            { "trade.tp1.label",          "TP1" },
            { "trade.tp2.label",          "TP2" },
            { "trade.tp.label",           "TP" },
            { "trade.sl.label",           "SL" },
            { "trade.tp.reached",         "{0} reached" },
            { "trade.tp.in_progress",     "{0} in progress" },
            { "trade.tp.pending",         "{0} pending" },
            { "trade.sl.danger",          "SL in danger" },
            { "trade.sl.hit",             "SL hit" },
            { "trade.tp.distance",        "{0} of {1} ticks" },

            // Result screen
            { "result.win.title",         "WINNING TRADE" },
            { "result.loss.title",        "STOP LOSS HIT" },
            { "result.locked",            "STRATEGY LOCKED" },
            { "result.back_home",         "BACK TO HOME" },
            { "result.duration_label",    "Duration" },
            { "result.contracts_label",   "Contracts" },

            // Parameters view
            { "params.title",             "Parameters" },
            { "params.section.schedule",  "SCHEDULE" },
            { "params.section.range",     "RANGE & TARGETS" },
            { "params.section.management","MANAGEMENT" },
            { "params.section.guards",    "SAFETY GUARDS" },
            { "params.entry_time",        "Entry time" },
            { "params.quantity",          "Contracts" },
            { "params.long_offset",       "Long offset (ticks)" },
            { "params.short_offset",      "Short offset (ticks)" },
            { "params.long_sl",           "Long SL (ticks)" },
            { "params.long_tp",           "Long TP (ticks)" },
            { "params.short_sl",          "Short SL (ticks)" },
            { "params.short_tp",          "Short TP (ticks)" },
            { "params.sl_ticks",          "Stop loss (ticks)" },
            { "params.tp_ticks",          "Take profit (ticks)" },
            { "params.enable_long",       "Enable Long" },
            { "params.enable_short",      "Enable Short" },
            { "params.enable_be",         "Breakeven" },
            { "params.enable_trail",      "Trailing stop" },
            { "params.enable_traiTP",     "Trailing TP" },
            { "params.enable_partials",   "Partials" },
            { "params.enable_time_exit",  "Time exit" },
            { "params.enable_tp_guard",   "TP gap guard" },
            { "params.enable_sl_guard",   "SL gap guard" },
            { "params.tp_guard_ticks",    "TP guard (ticks)" },
            { "params.sl_guard_ticks",    "SL guard (ticks)" },
            { "params.enable_single_rev", "Single-stop reverse cancel" },
            { "params.single_rev_ticks",  "Reverse ticks (0 = use offset)" },
            { "params.apply",             "APPLY CHANGES" },
            { "params.apply.note",        "Some fields can only be edited before the trade starts." },
        };

        private static readonly Dictionary<string, string> _es = new Dictionary<string, string>
        {
            { "brand.tagline",            "MAKE MONEY EASY" },
            { "brand.subtitle",           "NY930 — Plataforma para la apertura 9:30 NY" },

            { "nav.home",                 "Inicio" },
            { "nav.openrange",            "Open Range" },
            { "nav.hedge",                "Hedge" },
            { "nav.control",              "Control Open Range" },
            { "nav.progress",             "Progreso" },
            { "nav.result",               "Resultado" },
            { "nav.settings",             "Ajustes" },
            { "nav.about",                "Acerca de" },
            { "nav.back",                 "Volver" },
            { "nav.close",                "Cerrar" },
            { "nav.locked",               "Cancela las órdenes o cierra la operación primero." },

            { "home.openrange.title",     "OPEN RANGE" },
            { "home.openrange.desc",      "Buy Stop + Sell Stop colocados automáticamente en la apertura." },
            { "home.hedge.title",         "HEDGE" },
            { "home.hedge.desc",          "Entrada directa a mercado con gestión completa de la posición." },
            { "home.hint",                "Toca una estrategia para empezar." },

            { "status.waiting",           "Esperando {0}" },
            { "status.active",            "Órdenes activas" },
            { "status.in_long",           "Posición Long" },
            { "status.in_short",          "Posición Short" },
            { "status.cancelled",         "Órdenes canceladas" },
            { "status.session_done",      "Sesión terminada" },
            { "status.no_strategy",       "Ninguna estrategia adjunta a un gráfico" },

            { "or.buystop",               "BUY STOP" },
            { "or.sellstop",              "SELL STOP" },
            { "or.spread",                "Spread" },
            { "or.move.title",            "MOVER AMBAS" },
            { "or.move.sub",              "Mantiene la distancia" },
            { "or.spread.title",          "SPREAD" },
            { "or.spread.sub",            "Mantiene el punto medio" },
            { "or.cancel",                "CANCELAR ÓRDENES" },
            { "or.buy_now",               "COMPRAR YA" },
            { "or.sell_now",              "VENDER YA" },
            { "or.flatten",               "CERRAR POSICIÓN" },

            { "hedge.time",               "Hora de entrada" },
            { "hedge.qty",                "Contratos" },
            { "hedge.direction",          "Dirección" },
            { "hedge.long",               "Long" },
            { "hedge.short",              "Short" },
            { "hedge.none",               "(ninguno)" },
            { "hedge.sl",                 "Stop Loss (ticks)" },
            { "hedge.tp",                 "Take Profit (ticks)" },
            { "hedge.be",                 "Breakeven" },
            { "hedge.be.trigger",         "Trigger BE (ticks)" },
            { "hedge.be.offset",          "Offset BE (ticks)" },
            { "hedge.trail",              "Trailing Stop" },
            { "hedge.trail.trigger",      "Trigger trail (ticks)" },
            { "hedge.trail.step",         "Paso del trail (ticks)" },
            { "hedge.trailtp",            "Trailing TP" },
            { "hedge.trailtp.dist",       "Distancia al extremo (ticks)" },
            { "hedge.trailtp.timeout",    "Timeout sin fill (s)" },
            { "hedge.partials",           "Parciales" },
            { "hedge.partials.p1t",       "Ticks P1" },
            { "hedge.partials.p1c",       "Contratos P1" },
            { "hedge.partials.p2t",       "Ticks P2 (0 = off)" },
            { "hedge.partials.p2c",       "Contratos P2 (0 = off)" },
            { "hedge.timeexit",           "Salida por Tiempo" },
            { "hedge.timeexit.dur",       "Duración mínima (s)" },
            { "hedge.timeexit.mode",      "Modo de salida" },
            { "hedge.timeexit.beyondtp",  "Cerrar si el precio supera el TP" },

            { "guard.tp",                 "TP Gap Guard" },
            { "guard.sl",                 "SL Gap Guard" },
            { "guard.ticks",              "Ticks de disparo" },
            { "guard.seconds",            "Segundos de disparo" },
            { "guard.singlestop",         "Cancelar Stop único en reversa" },
            { "guard.singlestop.ticks",   "Ticks en contra (0 = usar offset)" },

            { "progress.title",           "Progreso de la operación" },
            { "progress.tp1",             "TP1" },
            { "progress.tp2",             "TP2" },
            { "progress.tp3",             "TP" },
            { "progress.sl",              "SL" },
            { "progress.pnl",             "PnL" },
            { "progress.duration",        "Duración" },
            { "progress.contracts",       "Contratos" },
            { "progress.entry",           "Entrada" },
            { "progress.last",            "Último" },
            { "progress.ticks",           "ticks" },

            { "result.title",             "Resultado de la operación" },
            { "result.profit",            "Ganancia" },
            { "result.loss",              "Pérdida" },
            { "result.entry",             "Entrada" },
            { "result.exit",              "Salida" },
            { "result.tp_hits",           "TPs alcanzados" },
            { "result.reason",            "Motivo" },

            { "settings.language",        "Idioma" },
            { "settings.lang.en",         "Inglés" },
            { "settings.lang.es",         "Español" },
            { "settings.about.title",     "Acerca de NY930" },
            { "settings.about.body",      "NY930 unifica Open Range y Hedge bajo un único panel temático para la apertura 9:30 NY. Fase 1." },

            { "common.enabled",           "Activado" },
            { "common.disabled",          "Desactivado" },
            { "common.yes",               "Sí" },
            { "common.no",                "No" },
            { "common.ticks",             "ticks" },

            { "status.countdown",         "Entrada en {0}" },
            { "status.countdown.now",     "Entrada ahora" },
            { "status.locked",            "Sesión bloqueada - se reanuda mañana" },
            { "status.no_chart",          "Abra un gráfico y adjunte la estrategia para comenzar" },

            { "trade.section.management", "GESTIÓN DE POSICIÓN" },
            { "trade.section.actions",    "ACCIONES" },
            { "trade.section.targets",    "OBJETIVOS" },
            { "trade.section.stops",      "STOP" },
            { "trade.action.breakeven",   "BREAKEVEN" },
            { "trade.action.close_now",   "CERRAR YA" },
            { "trade.action.partial",     "CIERRE PARCIAL" },
            { "trade.action.trailing",    "TRAILING STOP" },
            { "trade.tp1.label",          "TP1" },
            { "trade.tp2.label",          "TP2" },
            { "trade.tp.label",           "TP" },
            { "trade.sl.label",           "SL" },
            { "trade.tp.reached",         "{0} alcanzado" },
            { "trade.tp.in_progress",     "{0} en progreso" },
            { "trade.tp.pending",         "{0} pendiente" },
            { "trade.sl.danger",          "SL en peligro" },
            { "trade.sl.hit",             "SL tocado" },
            { "trade.tp.distance",        "{0} de {1} ticks" },

            { "result.win.title",         "OPERACIÓN GANADORA" },
            { "result.loss.title",        "STOP LOSS TOCADO" },
            { "result.locked",            "ESTRATEGIA BLOQUEADA" },
            { "result.back_home",         "VOLVER AL INICIO" },
            { "result.duration_label",    "Duración" },
            { "result.contracts_label",   "Contratos" },

            { "params.title",             "Parámetros" },
            { "params.section.schedule",  "HORARIO" },
            { "params.section.range",     "RANGO Y OBJETIVOS" },
            { "params.section.management","GESTIÓN" },
            { "params.section.guards",    "PROTECCIONES" },
            { "params.entry_time",        "Hora de entrada" },
            { "params.quantity",          "Contratos" },
            { "params.long_offset",       "Offset Long (ticks)" },
            { "params.short_offset",      "Offset Short (ticks)" },
            { "params.long_sl",           "SL Long (ticks)" },
            { "params.long_tp",           "TP Long (ticks)" },
            { "params.short_sl",          "SL Short (ticks)" },
            { "params.short_tp",          "TP Short (ticks)" },
            { "params.sl_ticks",          "Stop loss (ticks)" },
            { "params.tp_ticks",          "Take profit (ticks)" },
            { "params.enable_long",       "Habilitar Long" },
            { "params.enable_short",      "Habilitar Short" },
            { "params.enable_be",         "Breakeven" },
            { "params.enable_trail",      "Trailing stop" },
            { "params.enable_traiTP",     "Trailing TP" },
            { "params.enable_partials",   "Parciales" },
            { "params.enable_time_exit",  "Salida por tiempo" },
            { "params.enable_tp_guard",   "TP gap guard" },
            { "params.enable_sl_guard",   "SL gap guard" },
            { "params.tp_guard_ticks",    "TP guard (ticks)" },
            { "params.sl_guard_ticks",    "SL guard (ticks)" },
            { "params.enable_single_rev", "Cancel single-stop reverse" },
            { "params.single_rev_ticks",  "Ticks en contra (0 = usar offset)" },
            { "params.apply",             "APLICAR CAMBIOS" },
            { "params.apply.note",        "Algunos campos solo pueden editarse antes de que inicie la operación." },
        };
    }
}
