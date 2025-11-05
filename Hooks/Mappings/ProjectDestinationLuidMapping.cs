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
    /// Maps source projects to destination project locations based on the CSV mapping.
    /// NOTE: This mapping assumes destination projects already exist at the destination.
    /// Source projects listed in CSV will be skipped (not created), and their content
    /// will be published into the existing destination projects.
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
                _logger?.LogWarning("No ProjectDestinationLUID mapping found for source ProjectLUID: {SourceId}. Project will be skipped.", sourceId);
                return Task.FromResult<ContentMappingContext<IProject>?>(null); // Skip this project
            }

            // Map to a location with the destination LUID as the project name
            // This works because we're assuming the destination project already exists
            // and the SDK will match by project name/path
            var destLocation = ctx.ContentItem.Location.Rename(destLuid);
            var mappedCtx = ctx.MapTo(destLocation);
            
            _logger?.LogInformation("Source project {SourceId} ({SourceName}) mapped to destination project LUID {DestLuid}", 
                sourceId, ctx.ContentItem.Name, destLuid);
            
            return Task.FromResult<ContentMappingContext<IProject>?>(mappedCtx);
        }
    }
}
