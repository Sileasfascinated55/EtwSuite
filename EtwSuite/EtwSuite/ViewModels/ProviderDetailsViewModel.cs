using EtwSuite.Core;

namespace EtwSuite.ViewModels;

public sealed class ProviderDetailsViewModel
{
    public ProviderDetailsViewModel(EtwProviderInfo provider)
    {
        Name = provider.Name;
        Id = provider.Id;
        SchemaSource = FormatSchemaSource(provider.SchemaSource);
    }

    public string Name { get; }

    public Guid Id { get; }

    public string SchemaSource { get; }

    public string IdText => Id.ToString("D");

    private static string FormatSchemaSource(EtwProviderSchemaSource schemaSource)
    {
        return schemaSource switch
        {
            EtwProviderSchemaSource.XmlManifest => "XML manifest",
            EtwProviderSchemaSource.Wbem => "WMI/MOF",
            EtwProviderSchemaSource.Wpp => "WPP",
            EtwProviderSchemaSource.TraceLogging => "TraceLogging",
            _ => "Unknown"
        };
    }
}

