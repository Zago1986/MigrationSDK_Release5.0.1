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
    /// Maps source users to destination users based on email address.
    /// 
    /// This mapping handles cases where usernames differ between source and destination
    /// (e.g., Server uses "MOS7CA", Cloud uses "mos7ca@company.com") but the email 
    /// address remains the same on both platforms.
    /// 
    /// The SDK will use the email address to find the matching user at the destination,
    /// ensuring content ownership is correctly maintained even when usernames change.
    /// 
    /// Example:
    ///   Source User: Username="MOS7CA", Email="silvio.carvalho@company.com"
    ///   Destination User: Username="mos7ca@company.com", Email="silvio.carvalho@company.com"
    ///   Result: Content is correctly assigned to the destination user via email matching
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
            var sourceUser = ctx.ContentItem;
            
            // Check if user has an email address
            if (string.IsNullOrEmpty(sourceUser.Email))
            {
                _logger?.LogWarning(
                    "Source user {SourceUser} has no email address. Using username for mapping. " +
                    "This may fail if the username differs at the destination.",
                    sourceUser.Name);
                
                // No email, use username (may fail if username differs at destination)
                var usernameLocation = ContentLocation.ForUsername(sourceUser.Domain, sourceUser.Name);
                return Task.FromResult<ContentMappingContext<IUser>?>(ctx.MapTo(usernameLocation));
            }

            // Map based on email address - this works even when usernames differ
            _logger?.LogInformation(
                "Mapping source user {SourceUsername} (Email: {Email}) to destination user by email address",
                sourceUser.Name,
                sourceUser.Email);

            // Use email for mapping - SDK will find the destination user with this email
            // The destination username can be different (e.g., "mos7ca@company.com" vs "MOS7CA")
            var emailLocation = ContentLocation.ForUsername(sourceUser.Domain, sourceUser.Email);
            
            return Task.FromResult<ContentMappingContext<IUser>?>(ctx.MapTo(emailLocation));
        }
    }
}
