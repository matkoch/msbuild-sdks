using System.Text.Json;
using System.Text.Json.Serialization;

namespace Org.Sdk.Testing;

/// <summary>
/// Root model for SARIF (Static Analysis Results Interchange Format) files.
/// Used to parse compiler diagnostics from build output.
/// </summary>
public sealed class SarifFile
{
    [JsonPropertyName("runs")]
    public SarifRun[] Runs { get; set; } = [];

    /// <summary>
    /// Flattens all results from all runs.
    /// </summary>
    public IEnumerable<SarifResult> AllResults() => Runs.SelectMany(r => r.Results);

    /// <summary>
    /// Loads a SARIF file from disk.
    /// </summary>
    public static SarifFile? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize<SarifFile>(json);
    }
}

/// <summary>
/// A single run within a SARIF file (typically one per tool/compiler).
/// </summary>
public sealed class SarifRun
{
    [JsonPropertyName("results")]
    public SarifResult[] Results { get; set; } = [];
}

/// <summary>
/// A single diagnostic result (error, warning, or note).
/// </summary>
public sealed class SarifResult
{
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = "";

    [JsonPropertyName("level")]
    public string Level { get; set; } = "";

    [JsonPropertyName("message")]
    public SarifMessage? Message { get; set; }

    public override string ToString() => $"{Level}:{RuleId} {Message}";
}

/// <summary>
/// The message associated with a diagnostic.
/// </summary>
public sealed class SarifMessage
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    public override string ToString() => Text;
}
