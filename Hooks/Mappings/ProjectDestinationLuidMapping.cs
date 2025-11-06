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
    /// Maps source projects to existing destination projects.
    /// 
    /// IMPORTANT: The Tableau Migration SDK's location system uses project names/paths, not LUIDs.
    /// This mapping uses the destination project PATH from the CSV to tell the SDK where to place
    /// child content. The SDK will then match these paths to existing projects at the destination
    /// by name. Once matched, the SDK automatically uses the correct project LUID.
    /// 
    /// CSV Format: ProjectLUID,ProjectDestinationLUID,DestinationProjectPath
    /// - ProjectLUID: Source project LUID
    /// - ProjectDestinationLUID: Destination project LUID (for reference/validation)
    /// - DestinationProjectPath: The NAME/PATH of the destination project in Tableau Cloud
    /// 
    /// The destination projects must already exist with the exact names specified in the CSV.
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

        public override Task<ContentMappingContext<IProject>?> MapAsync(
            ContentMappingContext<IProject> ctx, 
            CancellationToken cancel)
        {
            var sourceId = ctx.ContentItem.Id.ToString();
            
            // Check if this project is in the CSV mapping
            if (!_mappingStore.TryGetDestinationInfo(sourceId, out var destLuid, out var destPath))
            {
                // This shouldn't happen because ProjectLuidFilter should have filtered it out
                _logger?.LogWarning("Source project {SourceId} ({SourceName}) not in CSV - will be skipped.", 
                    sourceId, ctx.ContentItem.Name);
                return Task.FromResult<ContentMappingContext<IProject>?>(null);
            }

            _logger?.LogInformation("Mapping source project {SourceId} ({SourceName}) to destination project '{DestPath}' (LUID: {DestLuid})", 
                sourceId, ctx.ContentItem.Name, destPath, destLuid);

            // Map the project to the destination project's location
            // This ensures the SDK knows where to place child content
            var destLocation = ContentLocation.FromPath(destPath);
            var mappedCtx = ctx.MapTo(destLocation);
            
            return Task.FromResult<ContentMappingContext<IProject>?>(mappedCtx);
        }
    }
}
