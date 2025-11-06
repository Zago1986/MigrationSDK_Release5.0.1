using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tableau.Migration;
using Tableau.Migration.Content;
using Tableau.Migration.Engine.Hooks.Mappings;
using Tableau.Migration.Resources;

namespace MigrationSDK.Hooks.Mappings
{
    /// <summary>
    /// Maps source users to existing destination users by display name and email.
    /// This mapping is used when users have already been migrated to the destination
    /// (e.g., via Azure AD import) and we need to match content ownership.
    /// </summary>
    public class DestinationUserMapping : ContentMappingBase<IUser>
    {
        private readonly ILogger<IContentMapping<IUser>>? _logger;

        public DestinationUserMapping(
            ISharedResourcesLocalizer? localizer = null,
            ILogger<IContentMapping<IUser>>? logger = null)
            : base(localizer, logger)
        {
            _logger = logger;
        }

        public override Task<ContentMappingContext<IUser>?> MapAsync(
            ContentMappingContext<IUser> ctx,
            CancellationToken cancel)
        {
            // Since users are already at the destination (migrated via Azure AD),
            // we map to the same username/email so the SDK can find them
            // The SDK will match by email address at the destination

            var sourceUser = ctx.ContentItem;
            
            _logger?.LogInformation(
                "Mapping source user {SourceUser} (Name: {Name}, Email: {Email}) to destination using email matching",
                sourceUser.Name,
                sourceUser.Name,
                sourceUser.Email ?? "N/A");

            // Use the email as the mapping key - SDK will match destination users by email
            // If no email, use the display name
            var mappingKey = !string.IsNullOrEmpty(sourceUser.Email) 
                ? sourceUser.Email 
                : sourceUser.Name;

            // Create a location based on the email/username for matching
            var mappedLocation = ContentLocation.ForUsername(sourceUser.Domain, mappingKey);
            
            return Task.FromResult<ContentMappingContext<IUser>?>(ctx.MapTo(mappedLocation));
        }
    }
}
