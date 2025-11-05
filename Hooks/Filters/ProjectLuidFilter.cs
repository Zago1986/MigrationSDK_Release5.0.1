using System.Collections.Generic;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Filters;
using Microsoft.Extensions.Logging;
using MigrationSDK;

namespace MigrationSDK.Hooks.Filters
{
    /// <summary>
    /// Filters projects to only include those that are listed in the CSV mapping file.
    /// This ensures only the projects specified in the CSV are migrated.
    /// </summary>
    public class ProjectLuidFilter : ContentFilterBase<IProject>
    {
        private readonly ProjectMappingStore _mappingStore;
        private readonly ILogger<ProjectLuidFilter>? _logger;

        public ProjectLuidFilter(ProjectMappingStore mappingStore, ILogger<ProjectLuidFilter>? logger = null)
            : base(null, logger)
        {
            _mappingStore = mappingStore;
            _logger = logger;
        }

        public override bool ShouldMigrate(ContentMigrationItem<IProject> item)
        {
            var projectId = item.SourceItem.Id.ToString();
            var shouldMigrate = _mappingStore.TryGetDestination(projectId, out _);
            
            if (shouldMigrate)
            {
                _logger?.LogInformation("Project {ProjectId} ({ProjectName}) will be migrated (found in CSV)", 
                    projectId, item.SourceItem.Name);
            }
            else
            {
                _logger?.LogDebug("Project {ProjectId} ({ProjectName}) will be skipped (not in CSV)", 
                    projectId, item.SourceItem.Name);
            }
            
            return shouldMigrate;
        }
    }
}
