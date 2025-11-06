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
    /// Maps source projects to be created as subfolders inside existing destination projects.
    /// 
    /// IMPORTANT: This mapping preserves the source project structure by creating source projects
    /// as child folders inside the destination projects specified in the CSV.
    /// 
    /// Example:
    /// - CSV: ProjectLUID=ABC, DestinationProjectPath=MigrationSDK
    /// - Source Project: DATABRICKS_TEST (name from Tableau Server)
    /// - Result: Creates MigrationSDK/DATABRICKS_TEST in Tableau Cloud
    /// - Content: Workbooks/data sources placed in MigrationSDK/DATABRICKS_TEST
    /// 
    /// Structure:
    ///   Destination Project (existing in Cloud)
    ///   └── Source Project (created during migration)
    ///       └── Workbooks and Data Sources
    /// 
    /// CSV Format: ProjectLUID,ProjectDestinationLUID,DestinationProjectPath
    /// - ProjectLUID: Source project LUID
    /// - ProjectDestinationLUID: Destination project LUID (for reference/validation)
    /// - DestinationProjectPath: The NAME/PATH of the parent destination project in Tableau Cloud
    /// 
    /// The destination parent projects must already exist with the exact names specified in the CSV.
    /// The source project folders will be created automatically during migration.
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

            // Create the full destination path: DestinationProject/SourceProjectName
            // This preserves the source project structure inside the destination project
            var sourceProjectName = ctx.ContentItem.Name;
            var fullDestPath = $"{destPath}/{sourceProjectName}";
            
            _logger?.LogInformation("Mapping source project {SourceId} ({SourceName}) to destination path '{FullDestPath}' (parent: {DestPath})", 
                sourceId, sourceProjectName, fullDestPath, destPath);

            // Map the project to be created as a subfolder inside the destination project
            var destLocation = ContentLocation.FromPath(fullDestPath);
            var mappedCtx = ctx.MapTo(destLocation);
            
            return Task.FromResult<ContentMappingContext<IProject>?>(mappedCtx);
        }
    }
}
