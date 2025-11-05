using Microsoft.Extensions.Logging;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Filters;
using Tableau.Migration.Resources;

namespace MigrationSDK.Hooks.Filters
{
    /// <summary>
    /// Filters out ALL users from migration.
    /// Users have already been migrated through a separate process.
    /// The SDK will match content ownership based on user display name and email address.
    /// </summary>
    public class SkipAllUsersFilter : ContentFilterBase<IUser>
    {
        public SkipAllUsersFilter(
            ISharedResourcesLocalizer localizer,
            ILogger<IContentFilter<IUser>> logger)
                : base(localizer, logger) { }

        public override bool ShouldMigrate(ContentMigrationItem<IUser> item)
        {
            // Return false to skip all users
            return false;
        }
    }
}
