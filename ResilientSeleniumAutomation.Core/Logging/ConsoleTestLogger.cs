using System;

namespace ResilientSeleniumAutomation.Core.Logging
{
    /// <summary>
    /// Thread-safe structured console test logger with ANSI-friendly formatting and timestamps.
    /// Writes directly to Console and integrates with NUnit test output context.
    /// </summary>
    public class ConsoleTestLogger : ITestLogger
    {
        private static readonly object _lock = new();

        public void Info(string message)
        {
            Log("INFO", message, ConsoleColor.Cyan);
        }

        public void Debug(string message)
        {
            Log("DEBUG", message, ConsoleColor.DarkGray);
        }

        public void Warn(string message)
        {
            Log("WARN", message, ConsoleColor.Yellow);
        }

        public void Error(string message, Exception? ex = null)
        {
            var formatted = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
            Log("ERROR", formatted, ConsoleColor.Red);
        }

        public void LogCandidateEvaluation(string targetSemantic, string candidateTag, string identifier, double score, bool accepted, string rationale)
        {
            var status = accepted ? "[MATCH]" : "[REJECT]";
            var color = accepted ? ConsoleColor.Green : ConsoleColor.DarkYellow;
            var msg = $"{status} Target '{targetSemantic}' => Candidate <{candidateTag} {identifier}> | Score: {score:P1} | Details: {rationale}";
            Log("RESILE", msg, color);
        }

        public void LogAmbiguityWarning(string targetSemantic, double score1, string candidate1, double score2, string candidate2, double delta)
        {
            var msg = $"Ambiguity detected for '{targetSemantic}'! Top Candidate #1 ({score1:P1}): {candidate1} vs Candidate #2 ({score2:P1}): {candidate2}. Delta: {delta:P2} <= Threshold. Failing safely to prevent false interaction.";
            Log("AMBIGUITY", msg, ConsoleColor.Magenta);
        }

        public void LogSection(string title)
        {
            lock (_lock)
            {
                Console.WriteLine();
                Console.WriteLine(new string('=', 80));
                Console.WriteLine($"  {title.ToUpperInvariant()}");
                Console.WriteLine(new string('=', 80));
            }
        }

        private static void Log(string level, string message, ConsoleColor color)
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var previousColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"[{timestamp}] ");
                    Console.ForegroundColor = color;
                    Console.Write($"[{level,-9}] ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(message);
                }
                finally
                {
                    Console.ForegroundColor = previousColor;
                }
            }
        }
    }
}

