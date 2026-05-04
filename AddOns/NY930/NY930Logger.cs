// ============================================================
//  NY930Logger — structured logging for the NY930 platform
// ------------------------------------------------------------
//  - Levels: Debug / Info / Warn / Error
//  - Single entry point (NY930Log.Info / Warn / Error / Debug)
//  - Uses NinjaTrader's Output window via NinjaScript.Print and
//    mirrors Warn/Error to the NT Log so production issues surface
//    in the same place as broker errors.
//  - The strategies fall back to System.Diagnostics.Debug if the
//    NT runtime is not available (defensive only — never thrown
//    in production).
// ============================================================

#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public enum NY930LogLevel
    {
        Debug = 0,
        Info  = 1,
        Warn  = 2,
        Error = 3
    }

    public static class NY930Log
    {
        // Strategies set this to a delegate that calls NinjaScript.Print
        // (because Print is an instance method on NinjaScriptBase).
        public static Action<string> PrintSink;

        // Optional NT Log mirror. Set by the strategy to its NinjaScript
        // instance so Warn/Error also appear in the NT Log window.
        public static Action<string, LogLevel> LogSink;

        // Minimum level to emit. Can be raised in production.
        public static NY930LogLevel MinimumLevel = NY930LogLevel.Info;

        public static void Debug(string source, string message)
            => Emit(NY930LogLevel.Debug, source, message);

        public static void Info(string source, string message)
            => Emit(NY930LogLevel.Info, source, message);

        public static void Warn(string source, string message)
            => Emit(NY930LogLevel.Warn, source, message);

        public static void Error(string source, string message)
            => Emit(NY930LogLevel.Error, source, message);

        public static void Separator(string source)
            => Emit(NY930LogLevel.Info, source,
                "─────────────────────────────────────────────");

        private static void Emit(NY930LogLevel level, string source, string message)
        {
            if (level < MinimumLevel) return;

            string line = string.Format(
                "[{0}][{1}][{2}] {3}",
                DateTime.Now.ToString("HH:mm:ss.fff"),
                LevelTag(level),
                source ?? "NY930",
                message ?? string.Empty);

            try
            {
                if (PrintSink != null) PrintSink(line);
                else                   System.Diagnostics.Debug.WriteLine(line);
            }
            catch { /* never let logging break trading */ }

            if (level >= NY930LogLevel.Warn && LogSink != null)
            {
                try
                {
                    LogSink(line,
                        level == NY930LogLevel.Error ? LogLevel.Error : LogLevel.Warning);
                }
                catch { }
            }
        }

        private static string LevelTag(NY930LogLevel level)
        {
            switch (level)
            {
                case NY930LogLevel.Debug: return "DEBUG";
                case NY930LogLevel.Info:  return "INFO ";
                case NY930LogLevel.Warn:  return "WARN ";
                case NY930LogLevel.Error: return "ERROR";
                default:                  return "INFO ";
            }
        }
    }
}
