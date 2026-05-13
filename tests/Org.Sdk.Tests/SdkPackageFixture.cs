using Org.Sdk.Testing;

[assembly: AssemblyFixture(typeof(Org.Sdk.Tests.SdkPackageFixture))]

namespace Org.Sdk.Tests;

/// <summary>
/// Package fixture for Org.Sdk tests.
/// Defines the solution file and SDK names used in this test project.
/// </summary>
public sealed class SdkPackageFixture()
    : PackageFixture(SolutionFile, BuildSdkName, CodeStyleSdkName, AnalyzersSdkName, ConsoleSdkName, PackageSdkName, DemoSdkName, ObservabilitySdkName, ObservabilityRuntimeName)
{
    public const string SolutionFile = "Org.Sdk.slnx";
    public const string BuildSdkName = "Org.BuildSdk";
    public const string CodeStyleSdkName = "Org.CodeStyleSdk";
    public const string AnalyzersSdkName = "Org.AnalyzersSdk";
    public const string ConsoleSdkName = "Org.ConsoleSdk";
    public const string PackageSdkName = "Org.PackageSdk";
    public const string DemoSdkName = "Org.DemoSdk";
    public const string ObservabilitySdkName = "Org.ObservabilitySdk";
    public const string ObservabilityRuntimeName = "Org.Observability";
}
