namespace RustIQ.Application.ServerQuery;

public sealed class RustServerCatalogImportOptions
{
    public const string SectionName = "RustServerCatalogImport";

    public bool Enabled { get; init; }

    public string? JsonFilePath { get; init; }
}
