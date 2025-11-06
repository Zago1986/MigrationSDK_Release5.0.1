using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tableau.Migration;
using Tableau.Migration.Content;
using Tableau.Migration.Engine.Hooks.Mappings;
using Tableau.Migration.Engine.Hooks.Mappings.Default;
using Tableau.Migration.Resources;

namespace MigrationSDK.Hooks.Mappings
{
    /// <summary>
    /// Maps source users to destination users based on email address for Tableau Cloud.
    /// 
    /// This mapping implements ITableauCloudUsernameMapping which tells the SDK to use
    /// email addresses as usernames when assigning content ownership in Tableau Cloud.
    /// 
    /// Users are NOT migrated - they already exist at the destination (via Azure AD).
    /// This mapping only affects how content ownership is assigned.
    /// 
    /// Example:
    ///   Source User: Username="MOS7CA", Email="silvio.carvalho@company.com"
    ///   Result: Content owned by user with email="silvio.carvalho@company.com" at destination
    /// </summary>
    public class DestinationUserMapping : ContentMappingBase<IUser>, ITableauCloudUsernameMapping
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
            var domain = ctx.MappedLocation.Parent();
            
            // Use email as the username for Tableau Cloud
            // This is the key: we're telling the SDK "when you need to reference this user,
            // use their email address as the identifier"
            if (!string.IsNullOrEmpty(sourceUser.Email))
            {
                _logger?.LogInformation(
                    "Mapping source user {SourceUsername} to Tableau Cloud using email {Email}",
                    sourceUser.Name,
                    sourceUser.Email);
                
                // Map to email-based location
                return Task.FromResult<ContentMappingContext<IUser>?>(
                    ctx.MapTo(domain.Append(sourceUser.Email)));
            }

            // Fallback to username if no email
            _logger?.LogWarning(
                "Source user {SourceUser} has no email address. Using username for mapping.",
                sourceUser.Name);
            
            return Task.FromResult<ContentMappingContext<IUser>?>(
                ctx.MapTo(domain.Append(sourceUser.Name)));
        }
    }
}
