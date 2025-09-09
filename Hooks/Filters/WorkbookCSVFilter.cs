using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Filters;
using Tableau.Migration.Resources;

namespace MigrationSDK.Hooks.Filters
{
    public class WorkbookCsvFilter : ContentFilterBase<IWorkbook>
    {
        private readonly HashSet<string> _workbookLuids;

        public WorkbookCsvFilter(ISharedResourcesLocalizer localizer, ILogger<ContentFilterBase<IWorkbook>> logger)
            : base(localizer, logger)
        {
            var csvPath = Path.Combine(AppContext.BaseDirectory, "CSV_Files", "workbooks.csv");
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));
            var records = csv.GetRecords<WorkbookCsvRow>().ToList();
            _workbookLuids = records.Select(r => r.WorkbookLUID).ToHashSet();
        }

        public override bool ShouldMigrate(ContentMigrationItem<IWorkbook> item)
        {
            return _workbookLuids.Contains(item.SourceItem.ContentUrl);
        }
    }

    public class WorkbookCsvRow
    {
        public string WorkbookLUID { get; set; }
        public string ProjectLUID { get; set; }
        public string ProjectDestinationLuid { get; set; }
    }
}