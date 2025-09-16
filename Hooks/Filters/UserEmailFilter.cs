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
        private readonly ILogger<UserEmailFilter>? _logger;

        public UserEmailFilter(ILogger<UserEmailFilter>? logger = null)
            : base(null, logger)
        {
            _logger = logger;
            _allowedEmails = new HashSet<string>();
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "CSV_Files", "users.csv");
            try
            {
                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                csv.Read();
                csv.ReadHeader();
                var header = csv.HeaderRecord;
                if (System.Array.IndexOf(header, "UserEmail") < 0)
                {
                    throw new System.Exception("users.csv must contain UserEmail column.");
                }
                while (csv.Read())
                {
                    var email = csv.GetField("UserEmail").Replace("\"", "").Trim();
                    if (!string.IsNullOrWhiteSpace(email))
                        _allowedEmails.Add(email);
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Error reading users.csv for user filtering.");
                throw;
            }
        }

        public override bool ShouldMigrate(ContentMigrationItem<IUser> item)
        {
            return _allowedEmails.Contains(item.SourceItem.Email);
        }
    }
}
