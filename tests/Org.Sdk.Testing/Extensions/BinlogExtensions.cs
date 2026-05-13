using Microsoft.Build.Logging.StructuredLogger;

namespace Org.Sdk.Testing.Extensions;

/// <summary>
/// Extension methods for inspecting MSBuild binary logs in <see cref="ExecutionResult"/>.
/// </summary>
public static class BinlogExtensions
{
    /// <summary>
    /// Gets the value of an MSBuild property from the binary log.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property value, or null if not found or no binlog exists.</returns>
    public static string? GetMSBuildPropertyValue(this ExecutionResult result, string name) =>
        result.Binlog?.FindLastDescendant<Property>(p => p.Name == name)?.Value;

    /// <summary>
    /// Gets the value of an MSBuild property from the binary log, converted to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to convert to (bool, int, or string).</typeparam>
    /// <param name="result">The execution result.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The converted property value, or default if not found or conversion fails.</returns>
    public static T? GetMSBuildPropertyValue<T>(this ExecutionResult result, string name)
    {
        var value = result.GetMSBuildPropertyValue(name);
        if (value == null) return default;

        return typeof(T) switch
        {
            var t when t == typeof(bool) => (T)(object)value.Equals("true", StringComparison.OrdinalIgnoreCase),
            var t when t == typeof(int) => int.TryParse(value, out var i) ? (T)(object)i : default,
            var t when t == typeof(string) => (T)(object)value,
            _ => default
        };
    }

    /// <summary>
    /// Returns true if the specified MSBuild target was executed (not skipped).
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="name">The target name.</param>
    /// <returns>True if the target was executed.</returns>
    public static bool IsMSBuildTargetExecuted(this ExecutionResult result, string name)
    {
        var target = result.Binlog?.FindLastDescendant<Target>(t => t.Name == name);
        return target is { Skipped: false };
    }

    /// <summary>
    /// Gets all source files referenced in the binary log.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <returns>Collection of source file paths, or empty if no binlog exists.</returns>
    public static IReadOnlyCollection<string> GetSourceFiles(this ExecutionResult result) =>
        result.Binlog?.SourceFiles.Select(f => f.FullPath).ToList() ?? [];

    /// <summary>
    /// Gets all items with the specified name from the binary log.
    /// </summary>
    /// <param name="result">The execution result.</param>
    /// <param name="name">The item name (e.g., "Compile", "Reference").</param>
    /// <returns>List of item values, or empty if no binlog exists.</returns>
    public static List<string> GetMSBuildItems(this ExecutionResult result, string name)
    {
        if (result.Binlog == null) return [];

        var items = new List<string>();
        result.Binlog.VisitAllChildren<Item>(item =>
        {
            if (item.Parent is AddItem parent && parent.Name == name)
            {
                items.Add(item.Name);
            }
        });
        return items;
    }
}
