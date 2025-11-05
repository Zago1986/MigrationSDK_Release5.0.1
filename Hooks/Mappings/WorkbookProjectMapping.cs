using System;
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
        private readonly ILogger<IContentMapping<IPublishableWorkbook>>? _logger;

        public WorkbookProjectMapping(
            ProjectMappingStore mappingStore,
            ISharedResourcesLocalizer? localizer = null, 
            ILogger<IContentMapping<IPublishableWorkbook>>? logger = null)
            : base(localizer, logger)
        {
            _mappingStore = mappingStore;
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

            if (_mappingStore.TryGetDestination(sourceProjectId, out var destProjectLuid))
            {
                // Build new location: replace the project segment with the destination project LUID
                // The destination project must already exist with this LUID/ID
                var newProjectLocation = sourceProjectLocation.Rename(destProjectLuid);
                var newLocation = newProjectLocation.Append(ctx.ContentItem.Name);
                
                var mappedCtx = ctx.MapTo(newLocation);
                
                _logger?.LogInformation("Workbook '{WorkbookName}' from source project {SourceProjectId} remapped to destination project {DestProjectLuid}", 
                    ctx.ContentItem.Name, sourceProjectId, destProjectLuid);
                
                return Task.FromResult<ContentMappingContext<IPublishableWorkbook>?>(mappedCtx);
            }
            
            _logger?.LogWarning("No destination project mapping found for workbook {WorkbookName} in source project {SourceProjectId}. Content will not be migrated.", 
                ctx.ContentItem.Name, sourceProjectId);
            
            // Return null to skip this workbook (it shouldn't reach here because of the filter, but just in case)
            return Task.FromResult<ContentMappingContext<IPublishableWorkbook>?>(null);
        }
    }
}
