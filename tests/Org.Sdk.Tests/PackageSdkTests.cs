using Org.Sdk.Testing;
using Org.Sdk.Testing.Extensions;

namespace Org.Sdk.Tests;

/// <summary>
/// Tests for Org.PackageSdk - verifies NuGet packaging defaults.
/// Uses library projects (no entry point needed).
/// </summary>
public class PackageSdkTests(SdkPackageFixture fixture, ITestOutputHelper output)
    : SdkTestBase([SdkPackageFixture.BuildSdkName, SdkPackageFixture.PackageSdkName], fixture, output)
{
    /// <summary>
    /// Verifies that IsPackable and GenerateDocumentationFile are enabled by default.
    /// </summary>
    [Fact]
    public async Task PackagingDefaultsEnabled()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);
        project.AddCSharpFile("Class1.cs", "/// <summary>A class.</summary>\npublic class Class1 { }");

        var result = await DotNetBuild(project);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("true", result.GetMSBuildPropertyValue("IsPackable"));
        Assert.Equal("true", result.GetMSBuildPropertyValue("GenerateDocumentationFile"));
    }

    /// <summary>
    /// Verifies README.md and LICENSE auto-detection from both project and solution directories.
    /// Project-level files take precedence over solution-level files.
    /// </summary>
    [Fact]
    public async Task AutoDetectsReadmeAndLicenseWithProjectPrecedence()
    {
        await using var solution = CreateSolution();
        solution.AddFile("README.md", "# Solution README");
        solution.AddFile("LICENSE", "Solution MIT License");

        // ProjectA has its own README/LICENSE - should use project-level
        var projectA = solution.AddProject(properties: [("OutputType", "Library")], projectName: "ProjectA");
        projectA.AddCSharpFile("Class1.cs", "namespace ProjectA;\n\n/// <summary>A class.</summary>\npublic class Class1 { }");
        projectA.AddTextFile("README.md", "# ProjectA README");
        projectA.AddTextFile("LICENSE", "ProjectA Apache License");

        // ProjectB has no README/LICENSE - should inherit from solution
        var projectB = solution.AddProject(properties: [("OutputType", "Library")], projectName: "ProjectB");
        projectB.AddCSharpFile("Class1.cs", "namespace ProjectB;\n\n/// <summary>A class.</summary>\npublic class Class1 { }");

        var resultA = await DotNetPack(projectA);
        var resultB = await DotNetPack(projectB);

        // ProjectA package contains project-level files
        Assert.Equal(0, resultA.ExitCode);
        Assert.True(resultA.PackageHasFile("README.md"));
        Assert.True(resultA.PackageHasFile("LICENSE"));
        Assert.Equal("# ProjectA README", resultA.GetPackageFileContent("README.md"));
        Assert.Equal("ProjectA Apache License", resultA.GetPackageFileContent("LICENSE"));

        // ProjectB package contains solution-level files
        Assert.Equal(0, resultB.ExitCode);
        Assert.True(resultB.PackageHasFile("README.md"));
        Assert.True(resultB.PackageHasFile("LICENSE"));
        Assert.Equal("# Solution README", resultB.GetPackageFileContent("README.md"));
        Assert.Equal("Solution MIT License", resultB.GetPackageFileContent("LICENSE"));
    }

    /// <summary>
    /// Verifies that SourceLink properties are set correctly.
    /// </summary>
    [Fact]
    public async Task SourceLinkPropertiesSet()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);
        project.AddCSharpFile("Class1.cs", "namespace MyLib;\n\n/// <summary>A class.</summary>\npublic class Class1 { }");

        var result = await DotNetBuild(project);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("true", result.GetMSBuildPropertyValue("PublishRepositoryUrl"));
        Assert.Equal("embedded", result.GetMSBuildPropertyValue("DebugType"));
        Assert.Equal("true", result.GetMSBuildPropertyValue("EmbedUntrackedSources"));
    }

    /// <summary>
    /// Verifies that package validation is enabled.
    /// </summary>
    [Fact]
    public async Task PackageValidationEnabled()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);
        project.AddCSharpFile("Class1.cs", "namespace MyLib;\n\n/// <summary>A class.</summary>\npublic class Class1 { }");

        var result = await DotNetBuild(project);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("true", result.GetMSBuildPropertyValue("EnablePackageValidation"));
    }

    /// <summary>
    /// Verifies that NuGet audit is enabled with expected settings.
    /// </summary>
    [Fact]
    public async Task NuGetAuditEnabled()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")]);
        project.AddCSharpFile("Class1.cs", "namespace MyLib;\n\n/// <summary>A class.</summary>\npublic class Class1 { }");

        var result = await DotNetBuild(project);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("true", result.GetMSBuildPropertyValue("NuGetAudit"));
        Assert.Equal("all", result.GetMSBuildPropertyValue("NuGetAuditMode"));
        Assert.Equal("low", result.GetMSBuildPropertyValue("NuGetAuditLevel"));
    }

    /// <summary>
    /// Verifies that .props and .targets files are auto-included in package.
    /// Files are renamed to match the PackageId.
    /// </summary>
    [Fact]
    public async Task AutoIncludesBuildPropsAndTargets()
    {
        await using var solution = CreateSolution();
        var project = solution.AddProject(properties: [("OutputType", "Library")], projectName: "MyLib");
        project.AddCSharpFile("Class1.cs", "namespace MyLib;\n\n/// <summary>A class.</summary>\npublic class Class1 { }");
        project.AddXmlFile("MyLib.props", "<Project><PropertyGroup><MyCustomProp>true</MyCustomProp></PropertyGroup></Project>");
        project.AddXmlFile("MyLib.targets", "<Project><Target Name=\"MyTarget\" /></Project>");

        var result = await DotNetPack(project);

        Assert.Equal(0, result.ExitCode);

        // Files are renamed from MyLib.* to TestFixture.* (the PackageId)
        Assert.True(result.PackageHasFile("build/TestFixture.props"));
        Assert.True(result.PackageHasFile("build/TestFixture.targets"));
    }
}
