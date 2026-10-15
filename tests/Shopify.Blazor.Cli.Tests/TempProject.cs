using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli.Tests;

/// <summary>
/// A throwaway project directory under the system temp path.
/// </summary>
/// <remarks>
/// Fixtures are built at runtime and never committed: a checked-in fixture directory drifts from
/// the shape the template actually produces, and the bits that matter here — the references, the
/// render-mode directive, the committed secret — are exactly the bits that would be "fixed" by
/// someone tidying the repository.
/// </remarks>
public sealed class TempProject : IDisposable
{
    private readonly string _root;
    private readonly string _repositoryRoot;

    /// <summary>
    /// The relative path the fixture's project file uses to reach the toolkit's libraries.
    /// </summary>
    /// <remarks>
    /// The fixture lives at <c>&lt;temp&gt;/tests/Fixture/</c>, which is the depth of the
    /// repository's own test projects, so <c>../../src/…</c> is a real two-level climb and the
    /// resolution is exercised rather than stubbed.
    /// </remarks>
    public const string RepositoryRootMarker = "shopify-blazor-cli-fixture";

    /// <summary>Creates an empty fixture tree shaped like the repository.</summary>
    public TempProject()
    {
        _repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            RepositoryRootMarker,
            Guid.NewGuid().ToString("n"));

        _root = Path.Combine(_repositoryRoot, "tests", "Fixture");
        Directory.CreateDirectory(_root);

        // A solution file at the top of the tree is the boundary the locator stops at, so a
        // fixture cannot accidentally resolve a project outside itself.
        File.WriteAllText(Path.Combine(_repositoryRoot, "Fixture.slnx"), "<Solution />");

        FileSystem = new PhysicalFileSystem();
        Environment = new FakeEnvironment(_root);
        Console = new RecordingConsole();
    }

    /// <summary>The real filesystem, so a test exercises the file paths it ships.</summary>
    public IFileSystem FileSystem { get; }

    /// <summary>The fake environment, rooted at this project.</summary>
    public FakeEnvironment Environment { get; }

    /// <summary>The recorded console output.</summary>
    public RecordingConsole Console { get; }

    /// <summary>The project directory.</summary>
    public string Root => _root;

    /// <summary>The tree above the project, where a sibling <c>src/</c> directory can live.</summary>
    public string RepositoryRoot => _repositoryRoot;

    /// <summary>The default project file path for a project named <paramref name="name"/>.</summary>
    public string ProjectFile(string name = "Fixture") => Path.Combine(_root, name + ".csproj");

    /// <summary>A path inside the project directory.</summary>
    public string At(params string[] parts) => System.IO.Path.Combine([_root, .. parts]);

    /// <summary>Writes a text file inside the project directory, creating parents.</summary>
    public TempProject Write(string relativePath, string contents)
    {
        var full = System.IO.Path.Combine(_root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
        return this;
    }

    /// <summary>Reads a text file inside the project directory.</summary>
    public string Read(string relativePath) =>
        File.ReadAllText(System.IO.Path.Combine(_root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    /// <summary>Whether a file exists inside the project directory.</summary>
    public bool Exists(string relativePath) =>
        File.Exists(System.IO.Path.Combine(_root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    /// <summary>
    /// Writes a project file that references both libraries as project references, which is the
    /// shape the template and the sample app use.
    /// </summary>
    public TempProject WithHealthyProjectFile(
        string name = "Fixture",
        bool referencePolaris = true,
        bool referenceShopify = true)
    {
        var references = new List<string>();

        if (referenceShopify)
        {
            references.Add("""    <ProjectReference Include="../../src/Shopify.Blazor/Shopify.Blazor.csproj" />""");
        }

        if (referencePolaris)
        {
            references.Add("""    <ProjectReference Include="../../src/Polaris.Blazor/Polaris.Blazor.csproj" />""");
        }

        Write($"{name}.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>{name}</RootNamespace>
                <UserSecretsId>fixture-secrets-id</UserSecretsId>
              </PropertyGroup>

              <ItemGroup>
            {string.Join("\n", references)}
              </ItemGroup>

            </Project>
            """);

        return this;
    }

    /// <summary>
    /// Writes a <c>global.json</c> at the project root, matching the repository's own pin.
    /// </summary>
    public TempProject WithGlobalJson(string version = "10.0.100", string rollForward = "latestFeature") =>
        Write("global.json", $$"""
            {
              "sdk": {
                "version": "{{version}}",
                "rollForward": "{{rollForward}}"
              }
            }
            """);

    /// <summary>Writes an <c>appsettings.json</c> with the given Shopify section.</summary>
    public TempProject WithAppSettings(
        string? apiKey = "test-key",
        string? apiSecret = "",
        string? appUrl = "https://example.ngrok.app",
        string? adminApiVersion = null,
        string fileName = "appsettings.json")
    {
        var adminApiLine = adminApiVersion is null
            ? string.Empty
            : $",\n    \"AdminApiVersion\": \"{adminApiVersion}\"";

        return Write(fileName, $$"""
            {
              "Logging": {
                "LogLevel": {
                  "Default": "Information"
                }
              },
              "Shopify": {
                "ApiKey": "{{apiKey}}",
                "ApiSecret": "{{apiSecret}}",
                "AppUrl": "{{appUrl}}"{{adminApiLine}},
                "Scopes": "read_products"
              }
            }
            """);
    }

    /// <summary>Writes the render-mode directive into a Razor file at the conventional location.</summary>
    public TempProject WithRenderMode(string relativePath = "Components/Routes.razor") =>
        Write(relativePath, """
            @* Interactive Server, on purpose. *@
            @rendermode InteractiveServer
            <Router AppAssembly="@typeof(Program).Assembly">
                <Found Context="routeData">
                    <RouteView RouteData="@routeData" />
                </Found>
            </Router>
            """);

    /// <summary>Writes a page shell with a divider, the shape the sample app's layout has.</summary>
    public TempProject WithLayout(string relativePath = "Components/Layout/MainLayout.razor") =>
        Write(relativePath, """
            @inherits LayoutComponentBase

            <PolarisPage Heading="Fixture">
                <PolarisStack Gap="PolarisGap.Large">
                    <PolarisStack Direction="PolarisStackDirection.Inline" Gap="PolarisGap.Base">
                        <PolarisButton Href="/" Target="PolarisLinkTarget.Self" Variant="PolarisButtonVariant.Tertiary">
                            Dashboard
                        </PolarisButton>
                    </PolarisStack>

                    <PolarisDivider />

                    @Body
                </PolarisStack>
            </PolarisPage>
            """);

    /// <summary>Points the fake environment's framework description at a version.</summary>
    public TempProject WithFramework(string frameworkDescription)
    {
        Environment.FrameworkDescription = frameworkDescription;
        return this;
    }

    /// <summary>Removes the temp directory, and everything a test put in it.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_repositoryRoot))
            {
                Directory.Delete(_repositoryRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }
}
