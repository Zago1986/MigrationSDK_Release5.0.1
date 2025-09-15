using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Hooks.Mappings;
using Tableau.Migration.Resources;
using Microsoft.Extensions.Logging;

namespace MigrationSDK.Hooks.Mappings
{
    public class ProjectDestinationLuidMapping : ContentMappingBase<IProject>
    {
        private readonly Dictionary<string, string> _destinationLookup;
        private readonly ILogger<IContentMapping<IProject>>? _logger;

        public ProjectDestinationLuidMapping(ISharedResourcesLocalizer? localizer = null, ILogger<IContentMapping<IProject>>? logger = null)
            : base(localizer, logger)
        {
            _logger = logger;
            _destinationLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "CSV_Files", "workbooks.csv");
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            var header = csv.HeaderRecord;
            if (Array.IndexOf(header, "ProjectLUID") < 0 || Array.IndexOf(header, "ProjectDestinationLUID") < 0)
            {
                throw new Exception("workbooks.csv must contain ProjectLUID and ProjectDestinationLUID columns.");
            }
            while (csv.Read())
            {
                var srcLuid = csv.GetField("ProjectLUID").Replace("\"", "").Trim();
                var destLuid = csv.GetField("ProjectDestinationLUID").Replace("\"", "").Trim();
                if (!string.IsNullOrWhiteSpace(srcLuid) && !string.IsNullOrWhiteSpace(destLuid))
                    _destinationLookup[srcLuid] = destLuid;
            }
        }

        public override Task<ContentMappingContext<IProject>?> MapAsync(ContentMappingContext<IProject> ctx, CancellationToken cancel)
        {
            var sourceId = ctx.ContentItem.Id.ToString();
            if (_destinationLookup.TryGetValue(sourceId, out var destLuid))
            {
                var mappedCtx = ctx.MapTo(ctx.ContentItem.Location.Rename(destLuid));
                return Task.FromResult(mappedCtx);
            }
            _logger?.LogWarning($"No ProjectDestinationLUID mapping found for ProjectLUID: {sourceId}");
            return Task.FromResult<ContentMappingContext<IProject>?>(null);
        }
    }
}
