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

        public ProjectLuidFilter(ISharedResourcesLocalizer? localizer = null, ILogger<IContentFilter<IProject>>? logger = null)
            : base(localizer, logger)
        {
            _allowedLuids = new HashSet<string>();
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "CSV_Files", "workbooks.csv");
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var luid = csv.GetField("ProjectLUID");
                if (!string.IsNullOrWhiteSpace(luid))
                    _allowedLuids.Add(luid.Replace("\"", "").Trim());
            }
        }

        public override bool ShouldMigrate(ContentMigrationItem<IProject> item)
        {
            return _allowedLuids.Contains(item.SourceItem.Id.ToString());
        }
    }
}
