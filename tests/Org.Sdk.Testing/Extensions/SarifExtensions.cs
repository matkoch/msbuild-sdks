namespace Org.Sdk.Testing.Extensions;

/// <summary>
/// Extension methods for inspecting SARIF diagnostics in <see cref="ExecutionResult"/>.
/// </summary>
public static class SarifExtensions
{
    /// <summary>
    /// Returns true if any error diagnostic was reported.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <returns>True if any error was reported.</returns>
    public static bool HasError(this ExecutionResult result) =>
        result.Sarif?.AllResults().Any(r => r.Level == "error") ?? false;

    /// <summary>
    /// Returns true if an error with the specified rule ID was reported.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="ruleId">The rule ID to search for (e.g., "CS8600").</param>
    /// <returns>True if an error with the rule ID was reported.</returns>
    public static bool HasError(this ExecutionResult result, string ruleId) =>
        result.Sarif?.AllResults().Any(r => r.Level == "error" && r.RuleId == ruleId) ?? false;

    /// <summary>
    /// Returns true if any warning diagnostic was reported.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <returns>True if any warning was reported.</returns>
    public static bool HasWarning(this ExecutionResult result) =>
        result.Sarif?.AllResults().Any(r => r.Level == "warning") ?? false;

    /// <summary>
    /// Returns true if a warning with the specified rule ID was reported.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="ruleId">The rule ID to search for (e.g., "CS8600").</param>
    /// <returns>True if a warning with the rule ID was reported.</returns>
    public static bool HasWarning(this ExecutionResult result, string ruleId) =>
        result.Sarif?.AllResults().Any(r => r.Level == "warning" && r.RuleId == ruleId) ?? false;

    /// <summary>
    /// Returns true if a note with the specified rule ID was reported.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="ruleId">The rule ID to search for.</param>
    /// <returns>True if a note with the rule ID was reported.</returns>
    public static bool HasNote(this ExecutionResult result, string ruleId) =>
        result.Sarif?.AllResults().Any(r => r.Level == "note" && r.RuleId == ruleId) ?? false;

    /// <summary>
    /// Gets all diagnostic results from the SARIF file.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <returns>All diagnostic results, or an empty enumerable if no SARIF file exists.</returns>
    public static IEnumerable<SarifResult> GetDiagnostics(this ExecutionResult result) =>
        result.Sarif?.AllResults() ?? [];
}
