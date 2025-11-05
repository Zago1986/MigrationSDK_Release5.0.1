using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tableau.Migration;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Mappings;
using Tableau.Migration.Resources;
using MigrationSDK;

namespace MigrationSDK.Hooks.Mappings
{
    /// <summary>
    /// Maps workbooks to the correct destination project location based on CSV mapping.
    /// This ensures workbooks are published into the correct destination projects.
    /// </summary>
    public class WorkbookProjectMapping : ContentMappingBase<IPublishableWorkbook>
    {
        private readonly ProjectMappingStore _mappingStore;
        private readonly IMigration _migration;
        private readonly ILogger<IContentMapping<IPublishableWorkbook>>? _logger;

        public WorkbookProjectMapping(
            ProjectMappingStore mappingStore,
            IMigration migration,
            ISharedResourcesLocalizer? localizer = null, 
            ILogger<IContentMapping<IPublishableWorkbook>>? logger = null)
            : base(localizer, logger)
        {
            _mappingStore = mappingStore;
            _migration = migration;
            _logger = logger;
        }

        public override Task<ContentMappingContext<IPublishableWorkbook>?> MapAsync(
            ContentMappingContext<IPublishableWorkbook> ctx, 
            CancellationToken cancel)
        {
            // Get the parent (project) location
            var sourceProjectLocation = ctx.ContentItem.Location.Parent();
            var sourceProjectId = sourceProjectLocation.Name;
            
            if (string.IsNullOrEmpty(sourceProjectId))
            {
                _logger?.LogWarning("Could not determine source project ID for workbook {WorkbookName}", 
                    ctx.ContentItem.Name);
                return Task.FromResult<ContentMappingContext<IPublishableWorkbook>?>(ctx);
            }

            if (!_mappingStore.TryGetDestination(sourceProjectId, out var destProjectLuid))
            {
                _logger?.LogWarning("No destination project mapping found for workbook {WorkbookName} in source project {SourceProjectId}. Content will not be migrated.", 
                    ctx.ContentItem.Name, sourceProjectId);
                return Task.FromResult<ContentMappingContext<IPublishableWorkbook>?>(null);
            }

            // Build new location using the destination project ID
            // Create a location with just the project ID and the workbook name
            var sourceLocation = ctx.ContentItem.Location;
            var destProjectLocation = sourceLocation.Parent().Rename(destProjectLuid);
            var newLocation = destProjectLocation.Append(ctx.ContentItem.Name);
            var mappedCtx = ctx.MapTo(newLocation);
            
            _logger?.LogInformation("Workbook '{WorkbookName}' from source project {SourceProjectId} remapped to destination project {DestProjectLuid} at location {NewLocation}", 
                ctx.ContentItem.Name, sourceProjectId, destProjectLuid, newLocation);
            
            return Task.FromResult<ContentMappingContext<IPublishableWorkbook>?>(mappedCtx);
        }
    }
}
