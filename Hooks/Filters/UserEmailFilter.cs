using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Filters;
using Tableau.Migration.Resources;
using Microsoft.Extensions.Logging;

namespace MigrationSDK.Hooks.Filters
{
    public class UserEmailFilter : ContentFilterBase<IUser>
    {
        private readonly HashSet<string> _allowedEmails;

        public UserEmailFilter(ISharedResourcesLocalizer? localizer = null, ILogger<IContentFilter<IUser>>? logger = null)
            : base(localizer, logger)
        {
            _allowedEmails = new HashSet<string>();
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "CSV_Files", "users.csv");
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var email = csv.GetField("UserEmail").Replace("\"", "").Trim();
                if (!string.IsNullOrWhiteSpace(email))
                    _allowedEmails.Add(email);
            }
        }

        public override bool ShouldMigrate(ContentMigrationItem<IUser> item)
        {
            return _allowedEmails.Contains(item.SourceItem.Email);
        }
    }
}
