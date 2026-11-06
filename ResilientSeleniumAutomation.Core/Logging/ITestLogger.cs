using System;

namespace ResilientSeleniumAutomation.Core.Logging
{
    /// <summary>
    /// Contract for structured test execution logging and resilient locator diagnostics.
    /// </summary>
    public interface ITestLogger
    {
        void Info(string message);
        void Debug(string message);
        void Warn(string message);
        void Error(string message, Exception? ex = null);
        void LogCandidateEvaluation(string targetSemantic, string candidateTag, string identifier, double score, bool accepted, string rationale);
        void LogAmbiguityWarning(string targetSemantic, double score1, string candidate1, double score2, string candidate2, double delta);
        void LogSection(string title);
    }
}

