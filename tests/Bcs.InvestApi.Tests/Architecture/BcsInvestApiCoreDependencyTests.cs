namespace Bcs.InvestApi.Tests.Architecture;

using Xunit;

public sealed class BcsInvestApiCoreDependencyTests
{
    [Fact]
    public void CoreAssembly_DoesNotReferenceMicrosoftExtensions()
    {
        var referencedAssemblyNames = typeof(BcsInvestApiClient)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToArray();

        Assert.DoesNotContain(
            referencedAssemblyNames,
            name => name?.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) == true);
    }
}
