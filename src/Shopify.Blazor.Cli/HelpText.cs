namespace Shopify.Blazor.Cli;

/// <summary>
/// The text <c>--help</c> prints.
/// </summary>
/// <remarks>
/// Kept as one literal rather than assembled from attributes, because this is a contract a person
/// reads: every command, every option and the exit codes appear here, and docs/cli-reference.md
/// documents the same surface. A test asserts that each documented option name appears, so the two
/// cannot drift silently.
/// </remarks>
public static class HelpText
{
    /// <summary>The full help.</summary>
    public static string Full => """
        shopify-blazor — a convenience wrapper for the Shopify Blazor Toolkit.

        Usage:
          shopify-blazor new <Name> [options]
          shopify-blazor add page <Name> [--path <dir>] [--project <path>] [--nav] [--force] [--dry-run]
          shopify-blazor add component <Name> [--path <file-or-dir>] [--project <path>] [--force] [--dry-run]
          shopify-blazor configure [--check] [--project <path>]
          shopify-blazor doctor [--project <path>] [--json]
          shopify-blazor --help | --version

        Commands:
          new <Name>        Create a project from the 'shopify-blazor' template and print the setup
                            steps. Refuses a target directory that exists and is not empty.
          add page <Name>   Write Components/Pages/<Name>.razor with a kebab-case route, an
                            interactive render mode and loading, empty and error states.
          add component     Write a usage snippet for a component in the toolkit catalogue.
          configure         Print the user-secrets commands and the Partner Dashboard settings.
          doctor            Check the project and report PASS / WARN / FAIL per check.

        Options:
          --include-sample-pages <bool>       new: sample dashboard, products and gallery (default true)
          --include-admin-api-example <bool>  new: the /api/products endpoint and its service (default true)
          --output <dir>                      new: parent directory for the generated project
          --path <dir>                        add page: directory for the new file
                                              (default: the project's Components/Pages)
          --path <file-or-dir>                add component: where to write the snippet
                                              (default: print it rather than write it)
          --project <path>                    the project to act on (default: search upwards)
          --nav                               add page: also insert a navigation entry
          --force                             overwrite an existing file (never the default)
          --dry-run                           print exactly what would be written and write nothing
          --json                              doctor: emit the report as JSON for CI
          --check                             configure: validate the settings resolve
          -h, --help                          show this help
          --version                           show the version

        Options may be written as '--option value' or '--option=value'.

        Exit codes:
          0  success (doctor: nothing FAILed)
          1  a check failed, or configure --check found something missing
          2  the command line was wrong (unknown option, missing value, bad name)
          3  a file already exists and --force was not given
          4  an external process failed (dotnet new)
          5  a piece of the CLI's own data is missing (for example the component catalogue)

        The CLI is never the only way to do anything: 'new' runs 'dotnet new shopify-blazor', 'add page'
        writes one ordinary .razor file, 'configure' sets user secrets, and 'doctor' only reads. Every
        action can be taken by hand; see docs/cli-reference.md.
        """;

    /// <summary>Help for <c>add</c>, which is not a command on its own.</summary>
    public static string AddUsage => """
        shopify-blazor add <page|component> <Name> [options]

          add page <Name>       Write Components/Pages/<Name>.razor.
                                Options: --path, --project, --nav, --force, --dry-run
          add component <Name>  Write a usage snippet.
                                Options: --path, --project, --force, --dry-run

        'add' on its own does nothing; name what to add. See 'shopify-blazor --help'.
        """;
}
