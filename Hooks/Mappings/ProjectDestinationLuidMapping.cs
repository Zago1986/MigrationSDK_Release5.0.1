using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tableau.Migration;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Mappings;
using Tableau.Migration.Resources;

namespace MigrationSDK.Hooks.Mappings
{
    /// <summary>
    /// Skips project migration - destination projects already exist.
    /// Returns null to prevent creating new project folders.
    /// Content will be mapped directly to existing destination projects.
    /// </summary>
    public class ProjectDestinationLuidMapping : ContentMappingBase<IProject>
    {
        private readonly ProjectMappingStore _mappingStore;
        private readonly ILogger<IContentMapping<IProject>>? _logger;

        public ProjectDestinationLuidMapping(
            ProjectMappingStore mappingStore,
            ISharedResourcesLocalizer? localizer = null, 
            ILogger<IContentMapping<IProject>>? logger = null)
            : base(localizer, logger)
        {
            _mappingStore = mappingStore;
            _logger = logger;
        }

        public override Task<ContentMappingContext<IProject>?> MapAsync(ContentMappingContext<IProject> ctx, CancellationToken cancel)
        {
            var sourceId = ctx.ContentItem.Id.ToString();
            
            if (!_mappingStore.TryGetDestination(sourceId, out var destLuid))
            {
                _logger?.LogInformation("Source project {SourceId} ({SourceName}) not in CSV - will be skipped.", 
                    sourceId, ctx.ContentItem.Name);
                return Task.FromResult<ContentMappingContext<IProject>?>(null); // Skip this project
            }

            // Skip project migration - destination projects already exist
            // Return null so the SDK doesn't create new project folders
            _logger?.LogInformation("Source project {SourceId} ({SourceName}) mapped to destination LUID {DestLuid} - skipping project migration (dest already exists)", 
                sourceId, ctx.ContentItem.Name, destLuid);
            
            return Task.FromResult<ContentMappingContext<IProject>?>(null);
        }
    }
}
