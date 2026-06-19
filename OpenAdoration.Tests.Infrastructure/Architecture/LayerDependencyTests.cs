using NetArchTest.Rules;
using OpenAdoration.Application.Common;
using OpenAdoration.Domain.Entities;
using OpenAdoration.Infrastructure.Backup;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Architecture;

/// <summary>
/// Enforces the Clean Architecture boundaries that CLAUDE.md documents (G28). These are the
/// teeth behind the rule "no invariant in prose without a test": the layering claim is now
/// verified, so it can't silently rot the way G26/G27 did. Domain → nothing; Application →
/// Domain only; Infrastructure → never WPF.
/// </summary>
public sealed class LayerDependencyTests
{
    private const string Application    = "OpenAdoration.Application";
    private const string Infrastructure = "OpenAdoration.Infrastructure";
    private const string Wpf            = "OpenAdoration.WPF";

    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        var result = Types.InAssembly(typeof(Song).Assembly)
            .ShouldNot().HaveDependencyOnAny(Application, Infrastructure, Wpf)
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_wpf()
    {
        var result = Types.InAssembly(typeof(AppPaths).Assembly)
            .ShouldNot().HaveDependencyOnAny(Infrastructure, Wpf)
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_wpf()
    {
        var result = Types.InAssembly(typeof(ZipBackupService).Assembly)
            .ShouldNot().HaveDependencyOn(Wpf)
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult r) =>
        r.IsSuccessful ? "" : "Layer violation in: " + string.Join(", ", r.FailingTypeNames);
}
