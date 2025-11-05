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
using MigrationSDK;

namespace MigrationSDK.Hooks.Filters
{
    /// <summary>
    /// Filters workbooks to only include those whose parent project is listed in the CSV.
    /// This ensures only workbooks from CSV-listed projects are migrated.
    /// </summary>
    public class WorkbookCsvFilter : ContentFilterBase<IWorkbook>
    {
        private readonly ProjectMappingStore _mappingStore;
        private readonly ILogger<ContentFilterBase<IWorkbook>> _logger;

        public WorkbookCsvFilter(
            ProjectMappingStore mappingStore,
            ISharedResourcesLocalizer localizer, 
            ILogger<ContentFilterBase<IWorkbook>> logger)
            : base(localizer, logger)
        {
            _mappingStore = mappingStore;
            _logger = logger;
        }

        public override bool ShouldMigrate(ContentMigrationItem<IWorkbook> item)
        {
            // Get the parent project ID from the workbook's container
            var mappableContent = item.SourceItem as IMappableContainerContent;
            var projectId = mappableContent?.Container?.Id.ToString();

            if (string.IsNullOrEmpty(projectId))
            {
                _logger.LogWarning("Could not determine project ID for workbook {WorkbookName}. Skipping.", 
                    item.SourceItem.Name);
                return false;
            }

            // Check if the project is in the CSV mapping
            var shouldMigrate = _mappingStore.TryGetDestination(projectId, out _);

            if (shouldMigrate)
            {
                _logger.LogDebug("Workbook {WorkbookName} will be migrated (project {ProjectId} is in CSV)", 
                    item.SourceItem.Name, projectId);
            }
            else
            {
                _logger.LogDebug("Workbook {WorkbookName} will be skipped (project {ProjectId} is not in CSV)", 
                    item.SourceItem.Name, projectId);
            }

            return shouldMigrate;
        }
    }
}