using System.Diagnostics;
using System.Text;

namespace Shopify.Blazor.Cli.Abstractions;

/// <summary>The result of running an external process.</summary>
/// <param name="ExitCode">The process exit code. <c>-1</c> when the process could not be started.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Whether the process ran and exited with code 0.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Runs an external process. The CLI shells out to <c>dotnet new</c> exactly once; this seam is
/// what keeps the tests from running a template engine (or needing one installed).
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="arguments"/>, returning the captured
    /// output. It does not throw on a non-zero exit code.
    /// </summary>
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory);
}

/// <summary>Runs processes for real, forwarding their output to the console as it arrives.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    private readonly TextWriter _output;

    /// <summary>Creates a runner that echoes child-process output to <paramref name="output"/>.</summary>
    public ProcessRunner(TextWriter output) => _output = output;

    /// <inheritdoc />
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return new ProcessResult(-1, string.Empty, $"Could not start '{fileName}'.");
            }

            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                standardOutput.AppendLine(e.Data);
                _output.WriteLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                standardError.AppendLine(e.Data);
                _output.WriteLine(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
        }
        catch (Exception ex)
        {
            // A missing executable is a result, not a crash: the caller decides what to print.
            return new ProcessResult(-1, string.Empty, ex.Message);
        }
    }
}
