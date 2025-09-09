using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tableau.Migration.Content;
using Tableau.Migration.Engine.Hooks.Mappings;
using Tableau.Migration.Resources;

namespace MigrationSDK.Hooks.Mappings
{
    public class WorkbookCsvProjectMapping : ContentMappingBase<IWorkbook>
    {
        private readonly Dictionary<string, string> _workbookProjectMap;
        private readonly ILogger<IContentMapping<IWorkbook>> _logger;

        public WorkbookCsvProjectMapping(ISharedResourcesLocalizer localizer, ILogger<IContentMapping<IWorkbook>> logger)
            : base(localizer, logger)
        {
            _logger = logger;
            var csvPath = Path.Combine(AppContext.BaseDirectory, "CSV_Files", "workbooks.csv");
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvHelper.CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
            var records = csv.GetRecords<WorkbookCsvRow>().ToList();
            _workbookProjectMap = records.ToDictionary(r => r.WorkbookLUID, r => r.ProjectDestinationLUID);
        }

        public override Task<ContentMappingContext<IWorkbook>> MapAsync(ContentMappingContext<IWorkbook> ctx, CancellationToken cancel)
        {
            //Find the destination project LUID for this workbook
            if (_workbookProjectMap.TryGetValue(ctx.ContentItem.ContentUrl, out var destProjectLuid))
            {
                //Build the new location for the destination project
                var parentLocation = ctx.ContentItem.Location.Parent();
                var newProjectLocation = parentLocation.Rename(destProjectLuid);
                var newLocation = newProjectLocation.Append(ctx.ContentItem.Location.Name);
                ctx = ctx.MapTo(newLocation);
                _logger?.LogInformation("workbook {ContentUrl} mapped to destination project {ProjectLuid}.", ctx.ContentItem.ContentUrl, destProjectLuid);
            }
            return ctx.ToTask();
        }

        public class WorkbookCsvRow
        {
            public string WorkbookLUID { get; set; }
            public string ProjectLUID { get; set; }
            public string ProjectDestinationLUID { get; set; }
        }
    }
}