namespace Org.Sdk.Testing.Extensions;

/// <summary>
/// Extension methods for inspecting command output in <see cref="ExecutionResult"/>.
/// </summary>
public static class OutputExtensions
{
    /// <summary>
    /// Returns true if the output contains the specified text.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="value">The text to search for.</param>
    /// <param name="comparison">The string comparison type. Defaults to ordinal.</param>
    /// <returns>True if the output contains the text.</returns>
    public static bool OutputContains(this ExecutionResult result, string value, StringComparison comparison = StringComparison.Ordinal) =>
        result.Output.Contains(value, comparison);

    /// <summary>
    /// Returns true if the output does not contain the specified text.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="value">The text to search for.</param>
    /// <param name="comparison">The string comparison type. Defaults to ordinal.</param>
    /// <returns>True if the output does not contain the text.</returns>
    public static bool OutputDoesNotContain(this ExecutionResult result, string value, StringComparison comparison = StringComparison.Ordinal) =>
        !result.Output.Contains(value, comparison);
}
