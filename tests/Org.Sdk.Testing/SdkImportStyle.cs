namespace Org.Sdk.Testing;

/// <summary>
/// Specifies how the SDK is imported in the project file.
/// </summary>
public enum SdkImportStyle
{
    /// <summary>
    /// Use the default import style for the context.
    /// </summary>
    Default,

    /// <summary>
    /// Import SDK via Project element's Sdk attribute: &lt;Project Sdk="Org.BuildSdk/1.0.0"&gt;
    /// </summary>
    ProjectElement,

    /// <summary>
    /// Import SDK via inner Sdk element: &lt;Sdk Name="Org.BuildSdk" Version="1.0.0" /&gt;
    /// </summary>
    SdkElement,
}
