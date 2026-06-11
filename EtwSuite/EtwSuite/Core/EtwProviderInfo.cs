namespace EtwSuite.Core;

public sealed record EtwProviderInfo(
    string Name,
    Guid Id,
    EtwProviderSchemaSource SchemaSource);

