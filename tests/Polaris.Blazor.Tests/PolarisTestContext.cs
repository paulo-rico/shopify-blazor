using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Polaris.Blazor.Interop;

namespace Polaris.Blazor.Tests;

/// <summary>
/// A bUnit test context with the Polaris interop layer replaced by a recorder.
/// </summary>
/// <remarks>
/// The default bUnit JSInterop is strict: any interop call fails the test. Every component here
/// performs interop after render, so a strict runtime would fail for the wrong reason and prove
/// nothing about the markup. Recording instead lets a test assert both.
/// </remarks>
public sealed class PolarisTestContext : BunitContext
{
    public PolarisTestContext()
    {
        Interop = new RecordingPolarisInterop();
        Services.AddSingleton<IPolarisInterop>(Interop);
    }

    /// <summary>The recorded interop traffic.</summary>
    public RecordingPolarisInterop Interop { get; }
}
