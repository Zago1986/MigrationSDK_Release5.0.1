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
using MigrationSDK.Hooks.InitializeMigration;

namespace MigrationSDK.Hooks.Mappings
{
    /// <summary>
    /// Maps workbooks to existing destination projects based on CSV mappings.
    /// Queries destination projects and uses their actual locations.
    /// </summary>
    public class WorkbookProjectMapping : ContentMappingBase<IPublishableWorkbook>
    {
        private readonly ProjectMappingStore _mappingStore;
        private readonly DestinationProjectCache _projectCache;
        private readonly IMigration _migration;
        private readonly ILogger<IContentMapping<IPublishableWorkbook>>? _logger;
        private bool _projectsQueried = false;
        private readonly SemaphoreSlim _queryLock = new SemaphoreSlim(1, 1);

        public WorkbookProjectMapping(
            ProjectMappingStore mappingStore,
            DestinationProjectCache projectCache,
            IMigration migration,
            ISharedResourcesLocalizer? localizer = null, 
            ILogger<IContentMapping<IPublishableWorkbook>>? logger = null)
            : base(localizer, logger)
        {
            _mappingStore = mappingStore;
            _projectCache = projectCache;
            _migration = migration;
            _logger = logger;
        }

        private async Task EnsureProjectsLoadedAsync(CancellationToken cancel)
        {
            if (_projectsQueried)
                return;

            await _queryLock.WaitAsync(cancel);
            try
            {
                if (_projectsQueried)
                    return;

                _logger?.LogInformation("Querying destination projects for workbook mapping...");
                await _projectCache.EnsureLoadedAsync(_migration, cancel);
                _projectsQueried = true;
            }
            finally
            {
                _queryLock.Release();
            }
        }

        public override async Task<ContentMappingContext<IPublishableWorkbook>?> MapAsync(
            ContentMappingContext<IPublishableWorkbook> ctx, 
            CancellationToken cancel)
        {
            // Get the source project ID from the workbook's container (project) reference
            // Cast to IMappableContainerContent to access the Container property
            var mappableContent = ctx.ContentItem as IMappableContainerContent;
            var sourceProjectId = mappableContent?.Container?.Id.ToString();
            
            if (string.IsNullOrEmpty(sourceProjectId))
            {
                _logger?.LogWarning("Could not determine source project ID for workbook {WorkbookName}", 
                    ctx.ContentItem.Name);
                return ctx;
            }

            // Look up the destination project info from CSV
            if (!_mappingStore.TryGetDestinationInfo(sourceProjectId, out var destProjectLuid, out var destProjectPath))
            {
                _logger?.LogWarning("No destination project mapping found for workbook {WorkbookName} in source project {SourceProjectId}. Workbook will not be migrated.", 
                    ctx.ContentItem.Name, sourceProjectId);
                return null; // Skip this workbook
            }

            // Validate the path is not empty
            if (string.IsNullOrEmpty(destProjectPath))
            {
                _logger?.LogError("Empty destination project path for workbook {WorkbookName} (dest LUID: {DestLuid})", 
                    ctx.ContentItem.Name, destProjectLuid);
                return null;
            }

            // Build the destination location using the project path from CSV
            // The SDK expects locations without a leading slash for top-level projects
            // Format: destProjectPath/workbookName
            var destLocation = ContentLocation.FromPath($"{destProjectPath}/{ctx.ContentItem.Name}");
            var mappedCtx = ctx.MapTo(destLocation);
            
            _logger?.LogInformation("Workbook '{WorkbookName}' (source project {SourceProjectId}) mapped to destination location '{DestLocation}' (project: '{DestPath}', LUID: {DestLuid})", 
                ctx.ContentItem.Name, sourceProjectId, destLocation, destProjectPath, destProjectLuid);
            
            return mappedCtx;
        }
    }
}
