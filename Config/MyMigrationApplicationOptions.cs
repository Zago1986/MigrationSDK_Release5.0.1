#region namespace
namespace MigrationSDK.Config
{
    public sealed class MyMigrationApplicationOptions
    {
        public EndpointOptions Source { get; set; } = new();

        public EndpointOptions Destination { get; set; } = new();

        // New: CSV settings for project LUID mapping and dry-run
        public CsvOptions Csv { get; set; } = new();
    }

    public sealed class CsvOptions
    {
        public string ProjectMapping { get; set; } = string.Empty;
        public bool DryRun { get; set; } = false;
    }
}
#endregion