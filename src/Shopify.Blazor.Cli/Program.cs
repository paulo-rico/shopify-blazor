using System.Reflection;
using Shopify.Blazor.Cli;
using Shopify.Blazor.Cli.Abstractions;
using Shopify.Blazor.Cli.Catalogue;
using Shopify.Blazor.Cli.Commands;

// The CLI's entry point. It does as little as possible: it owns the dispatch, the top-level option
// handling and the exit code, and every decision lives in a command class that a test can drive
// without a process, a disk or a network.
//
// The tool is a convenience wrapper. Each command maps onto something a developer can do by hand —
// 'dotnet new shopify-blazor', one .razor file, user secrets, a few reads — and none of them
// requires this CLI to exist. See docs/cli-reference.md.

var console = new SystemConsole();
var fileSystem = new PhysicalFileSystem();
var environment = new SystemEnvironment();

int Run(string[] argv)
{
    if (argv.Length == 0)
    {
        console.WriteLine(HelpText.Full);
        return ExitCodes.Success;
    }

    // The first non-option token is the command. Everything else belongs to that command.
    var command = argv[0];

    if (command is "-h" or "--help" or "help")
    {
        console.WriteLine(HelpText.Full);
        return ExitCodes.Success;
    }

    if (command is "--version" or "-v")
    {
        console.WriteLine(VersionText());
        return ExitCodes.Success;
    }

    if (command.StartsWith('-'))
    {
        console.WriteLine($"error: '{command}' is not a command. Run 'shopify-blazor --help' for the list.");
        return ExitCodes.Usage;
    }

    var rest = argv[1..];

    return command switch
    {
        "new" => Dispatch(NewCommand.AcceptedOptions, rest, arguments => new NewCommand(
            fileSystem, new ProcessRunner(Console.Out), environment, console).Run(arguments)),

        "add" => DispatchAdd(rest),

        "configure" => Dispatch(ConfigureCommand.AcceptedOptions, rest, arguments =>
            new ConfigureCommand(fileSystem, environment, console).Run(arguments, environment.CurrentDirectory)),

        "doctor" => Dispatch(DoctorCommand.AcceptedOptions, rest, arguments =>
            new DoctorCommand(fileSystem, environment, console).Run(arguments)),

        _ => UnknownCommand(command),
    };
}

int DispatchAdd(string[] rest)
{
    if (rest.Length == 0)
    {
        console.WriteLine(HelpText.AddUsage);
        return ExitCodes.Usage;
    }

    if (rest[0] is "-h" or "--help")
    {
        console.WriteLine(HelpText.AddUsage);
        return ExitCodes.Success;
    }

    var subject = rest[0];
    var subjectArguments = rest[1..];

    return subject switch
    {
        "page" => Dispatch(AddPageCommand.AcceptedOptions, subjectArguments, arguments =>
            new AddPageCommand(fileSystem, environment, console).Run(arguments, environment.CurrentDirectory)),

        "component" => Dispatch(AddComponentCommand.AcceptedOptions, subjectArguments, arguments =>
            new AddComponentCommand(fileSystem, environment, console, PolarisCatalogue.LoadEmbedded)
                .Run(arguments, environment.CurrentDirectory)),

        _ => UnknownAddSubject(subject),
    };
}

int Dispatch(
    IReadOnlyList<OptionSpec> acceptedOptions,
    string[] commandArguments,
    Func<ParsedArguments, int> run)
{
    // A problem with the command line itself is caught before the command runs, so no command
    // needs its own option validation — and a mistyped flag can never be silently ignored.
    var parsed = ArgumentParser.Parse(commandArguments, acceptedOptions);

    if (!parsed.Succeeded)
    {
        console.WriteLine("error: " + parsed.Error);
        return ExitCodes.Usage;
    }

    var arguments = parsed.Arguments!;

    if (arguments.Has(ArgumentParser.HelpOption))
    {
        console.WriteLine(HelpText.Full);
        return ExitCodes.Success;
    }

    return run(arguments);
}

int UnknownCommand(string command)
{
    console.WriteLine($"error: '{command}' is not a command. Run 'shopify-blazor --help' for the list.");
    return ExitCodes.Usage;
}

int UnknownAddSubject(string subject)
{
    console.WriteLine($"error: 'add {subject}' is not something this CLI can add. " +
        "The supported subjects are 'page' and 'component'.");
    console.WriteLine(HelpText.AddUsage);
    return ExitCodes.Usage;
}

string VersionText()
{
    var assembly = Assembly.GetExecutingAssembly();

    // InformationalVersion carries the package version and, from SourceLink, a '+<commit>' suffix
    // that belongs in a package feed rather than in a terminal.
    var informational = assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion;

    if (informational is { Length: > 0 })
    {
        var plus = informational.IndexOf('+');

        return plus > 0 ? informational[..plus] : informational;
    }

    return assembly.GetName().Version?.ToString() ?? "0.0.0";
}

return Run(args);
