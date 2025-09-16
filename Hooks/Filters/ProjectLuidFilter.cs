using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Filters;
using Tableau.Migration.Resources;
using Microsoft.Extensions.Logging;

namespace MigrationSDK.Hooks.Filters
{
    public class ProjectLuidFilter : ContentFilterBase<IProject>
    {
        private readonly HashSet<string> _allowedLuids;
        private readonly ILogger<ProjectLuidFilter>? _logger;

        public ProjectLuidFilter(ILogger<ProjectLuidFilter>? logger = null)
            : base(null, logger)
        {
            _logger = logger;
            _allowedLuids = new HashSet<string>();
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "CSV_Files", "workbooks.csv");
            try
            {
                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                csv.Read();
                csv.ReadHeader();
                var header = csv.HeaderRecord;
                if (System.Array.IndexOf(header, "ProjectLUID") < 0)
                {
                    throw new System.Exception("workbooks.csv must contain ProjectLUID column.");
                }
                while (csv.Read())
                {
                    var luid = csv.GetField("ProjectLUID");
                    if (!string.IsNullOrWhiteSpace(luid))
                        _allowedLuids.Add(luid.Replace("\"", "").Trim());
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Error reading workbooks.csv for project filtering.");
                throw;
            }
        }

        public override bool ShouldMigrate(ContentMigrationItem<IProject> item)
        {
            return _allowedLuids.Contains(item.SourceItem.Id.ToString());
        }
    }
}
