namespace EtwSuite.Core;

public interface IEtwProviderCatalog
{
    Task<IReadOnlyList<EtwProviderInfo>> EnumerateProvidersAsync(CancellationToken cancellationToken);
}

