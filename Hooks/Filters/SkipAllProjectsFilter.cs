using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Filters;
using Microsoft.Extensions.Logging;

namespace MigrationSDK.Hooks.Filters
{
    /// <summary>
    /// Filters out all projects from migration.
    /// This ensures that no projects are created at the destination.
    /// Only content (workbooks, data sources) will be migrated.
    /// </summary>
    public class SkipAllProjectsFilter : ContentFilterBase<IProject>
    {
        private readonly ILogger<SkipAllProjectsFilter>? _logger;

        public SkipAllProjectsFilter(ILogger<SkipAllProjectsFilter>? logger = null)
            : base(null, logger)
        {
            _logger = logger;
        }

        public override bool ShouldMigrate(ContentMigrationItem<IProject> item)
        {
            // Skip all projects - we don't want to migrate/create any projects
            _logger?.LogDebug("Skipping project {ProjectName} ({ProjectId}) - projects are not migrated", 
                item.SourceItem.Name, item.SourceItem.Id);
            return false;
        }
    }
}
