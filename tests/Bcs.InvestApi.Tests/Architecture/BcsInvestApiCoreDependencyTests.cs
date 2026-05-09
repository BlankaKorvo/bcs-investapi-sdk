namespace Bcs.InvestApi.Tests.Architecture;

using Xunit;

public sealed class BcsInvestApiCoreDependencyTests
{
    private static readonly string[] ForbiddenApplicationLayerTerms =
    [
        "Domain",
        "Strategy",
        "Mapping",
        "Mapper",
        "Store",
        "Repository",
        "Backtest",
        "TradingSystem",
    ];

    private static readonly string[] ForbiddenStoreFileNameTerms =
    [
        "token-store",
        "tokenstore",
        "disk-store",
        "diskstore",
        "file-store",
        "filestore",
    ];

    private static readonly string[] ForbiddenRefreshRetryTerms =
    [
        "Polly",
        "Retry",
        "RetryPolicy",
        "WaitAndRetry",
        "AddPolicyHandler",
        "ResiliencePipeline",
        "RetryStrategyOptions",
        "AddResilienceHandler",
    ];

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

    [Fact]
    public void CoreAssembly_DoesNotContainApplicationLayerTypeOrNamespaceNames()
    {
        var offenders = typeof(BcsInvestApiClient)
            .Assembly
            .GetTypes()
            .Select(type => type.FullName ?? type.Name)
            .Where(name => ForbiddenApplicationLayerTerms.Any(term =>
                name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Core SDK must not contain application-layer type or namespace names: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void CoreSource_DoesNotContainTokenOrDiskStoreFiles()
    {
        var sourceRoot = GetCoreSourceRoot();

        var offenders = Directory
            .EnumerateFiles(sourceRoot.FullName, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(sourceRoot.FullName, path).Replace('\\', '/'))
            .Where(path => ForbiddenStoreFileNameTerms.Any(term =>
                path.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Core SDK must not contain token/disk/file store files: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void CoreRefreshAuth_DoesNotUseRetryPolicyOrPolly()
    {
        var referencedAssemblyNames = typeof(BcsInvestApiClient)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToArray();
        var retryAssemblyReferences = referencedAssemblyNames
            .Where(name => name?.Contains("Polly", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
        var sourceRoot = GetCoreSourceRoot();
        var refreshAuthSources = Directory
            .EnumerateFiles(sourceRoot.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relativePath = Path.GetRelativePath(sourceRoot.FullName, path).Replace('\\', '/');
                return relativePath.StartsWith("Auth/", StringComparison.Ordinal) ||
                    relativePath.StartsWith("Tokens/", StringComparison.Ordinal) ||
                    relativePath.StartsWith("Infrastructure/", StringComparison.Ordinal);
            })
            .Select(path => new
            {
                Path = Path.GetRelativePath(sourceRoot.FullName, path).Replace('\\', '/'),
                Text = File.ReadAllText(path),
            });
        var retrySourceReferences = refreshAuthSources
            .SelectMany(file => ForbiddenRefreshRetryTerms
                .Where(term => file.Text.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(term => $"{file.Path}: {term}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            retryAssemblyReferences.Length == 0,
            $"Core SDK must not reference Polly assemblies: {string.Join(", ", retryAssemblyReferences)}");
        Assert.True(
            retrySourceReferences.Length == 0,
            $"Refresh auth must not use retry policy or Polly terms: {string.Join(", ", retrySourceReferences)}");
    }

    private static DirectoryInfo GetCoreSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Bcs.InvestApi");
            if (File.Exists(Path.Combine(candidate, "Bcs.InvestApi.csproj")))
            {
                return new DirectoryInfo(candidate);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate src/Bcs.InvestApi from the test output directory.");
    }
}
