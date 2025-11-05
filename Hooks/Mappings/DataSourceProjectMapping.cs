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
    /// Maps data sources to the correct destination project location based on CSV mapping.
    /// This ensures data sources are published into the correct destination projects.
    /// </summary>
    public class DataSourceProjectMapping : ContentMappingBase<IPublishableDataSource>
    {
        private readonly ProjectMappingStore _mappingStore;
        private readonly IMigration _migration;
        private readonly ILogger<IContentMapping<IPublishableDataSource>>? _logger;

        public DataSourceProjectMapping(
            ProjectMappingStore mappingStore,
            IMigration migration,
            ISharedResourcesLocalizer? localizer = null, 
            ILogger<IContentMapping<IPublishableDataSource>>? logger = null)
            : base(localizer, logger)
        {
            _mappingStore = mappingStore;
            _migration = migration;
            _logger = logger;
        }

        public override Task<ContentMappingContext<IPublishableDataSource>?> MapAsync(
            ContentMappingContext<IPublishableDataSource> ctx, 
            CancellationToken cancel)
        {
            // Get the parent (project) location
            var sourceProjectLocation = ctx.ContentItem.Location.Parent();
            var sourceProjectId = sourceProjectLocation.Name;
            
            if (string.IsNullOrEmpty(sourceProjectId))
            {
                _logger?.LogWarning("Could not determine source project ID for data source {DataSourceName}", 
                    ctx.ContentItem.Name);
                return Task.FromResult<ContentMappingContext<IPublishableDataSource>?>(ctx);
            }

            if (!_mappingStore.TryGetDestination(sourceProjectId, out var destProjectLuid))
            {
                _logger?.LogWarning("No destination project mapping found for data source {DataSourceName} in source project {SourceProjectId}. Content will not be migrated.", 
                    ctx.ContentItem.Name, sourceProjectId);
                return Task.FromResult<ContentMappingContext<IPublishableDataSource>?>(null);
            }

            // Build new location using the destination project ID
            // Create a location with just the project ID and the data source name
            var sourceLocation = ctx.ContentItem.Location;
            var destProjectLocation = sourceLocation.Parent().Rename(destProjectLuid);
            var newLocation = destProjectLocation.Append(ctx.ContentItem.Name);
            var mappedCtx = ctx.MapTo(newLocation);
            
            _logger?.LogInformation("Data source '{DataSourceName}' from source project {SourceProjectId} remapped to destination project {DestProjectLuid} at location {NewLocation}", 
                ctx.ContentItem.Name, sourceProjectId, destProjectLuid, newLocation);
            
            return Task.FromResult<ContentMappingContext<IPublishableDataSource>?>(mappedCtx);
        }
    }
}
