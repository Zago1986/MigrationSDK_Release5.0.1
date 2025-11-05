using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MigrationSDK.Config;
using MigrationSDK.Hooks.BatchMigrationCompleted;
using MigrationSDK.Hooks.Filters;
using MigrationSDK.Hooks.InitializeMigration;
using MigrationSDK.Hooks.Mappings;
using MigrationSDK.Hooks.MigrationActionCompleted;
using MigrationSDK.Hooks.PostPublish;
using MigrationSDK.Hooks.Transformers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tableau.Migration;
using Tableau.Migration.Content;
using Tableau.Migration.Engine.Pipelines;
using Tableau.Migration.Engine.Manifest; // Add back the missing namespace for MigrationManifestSerializer
using Tableau.Migration.Engine; // Add missing namespace for ContentMigrationItem<>

// New usings for CSV & culture
using System.Collections.Generic;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Tableau.Migration.Content.Schedules.Cloud; // New: required for ICloudExtractRefreshTask
using Tableau.Migration.Engine.Hooks; // New: for hook base interfaces
using Tableau.Migration.Engine.Hooks.Filters; // ensures ContentMigrationItem<T> resolves

#region namespace

namespace MigrationSDK
{
    internal sealed class MyMigrationApplication : IHostedService
    {
        private readonly Stopwatch _timer;
        private readonly IHostApplicationLifetime _appLifetime;
        private IMigrationPlanBuilder _planBuilder;
        private readonly IMigrator _migrator;
        private readonly MyMigrationApplicationOptions _options;
        private readonly ILogger<MyMigrationApplication> _logger;
        private readonly MigrationManifestSerializer _manifestSerializer;
        private readonly ProjectMappingStore _projectMap;

        public MyMigrationApplication(
            IHostApplicationLifetime appLifetime,
            IMigrationPlanBuilder planBuilder,
            IMigrator migrator,
            IOptions<MyMigrationApplicationOptions> options,
            ILogger<MyMigrationApplication> logger,
            MigrationManifestSerializer manifestSerializer,
            ProjectMappingStore projectMap)
        {
            _timer = new Stopwatch();
            _appLifetime = appLifetime;
            _planBuilder = planBuilder;
            _migrator = migrator;
            _options = options.Value;
            _logger = logger;
            _manifestSerializer = manifestSerializer;
            _projectMap = projectMap;
        }

        public async Task StartAsync(CancellationToken cancel)
        {
            var executablePath = Assembly.GetExecutingAssembly().Location;
            var currentFolder = Path.GetDirectoryName(executablePath);
            if (currentFolder is null)
            {
                throw new Exception("Could not get the current folder path.");
            }
            var manifestPath = $"{currentFolder}/manifest.json";

            // 1) Load CSV mapping and preflight report
            var csvPath = ResolveCsvPath(_options.Csv.ProjectMapping, currentFolder);
            await _projectMap.LoadAsync(csvPath, _logger, cancel);

            if (_options.Csv.DryRun)
            {
                PrintCsvPreflight(_projectMap, dryRun: true);
                Console.WriteLine("Dry-run complete. Press any key to exit");
                Console.ReadKey();
                _appLifetime.StopApplication();
                return;
            }

            PrintCsvPreflight(_projectMap, dryRun: false);

            var startTime = DateTime.UtcNow;
            _timer.Start();

            #region EmailDomainMapping-Registration
            _planBuilder = _planBuilder
                .FromSourceTableauServer(_options.Source.ServerUrl, _options.Source.SiteContentUrl, _options.Source.AccessTokenName, _options.Source.AccessToken)
                .ToDestinationTableauCloud(_options.Destination.ServerUrl, _options.Destination.SiteContentUrl, _options.Destination.AccessTokenName, _options.Destination.AccessToken)
                .ForServerToCloud()
                .WithTableauIdAuthenticationType()
                .WithTableauCloudUsernames<EmailDomainMapping>();
            #endregion

            var validationResult = _planBuilder.Validate();
            if (!validationResult.Success)
            {
                _logger.LogError("Migration plan validation failed. {Errors}", validationResult.Errors);
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                _appLifetime.StopApplication();
                return;
            }

            // Do NOT migrate projects; rely on config (projects are not in contentTypes).
            // Add filters for only CSV-mapped content:
            // _planBuilder.Filters.Add<SkipAllProjectsFilter, IProject>(); // removed; redundant and causing compile issues
            _planBuilder.Filters.Add<WorkbookCsvFilter, IWorkbook>();
            _planBuilder.Filters.Add<DataSourceCsvFilter, IDataSource>();

            // Remap destination ProjectId for publishable content to existing destination LUIDs from CSV
            _planBuilder.Transformers.Add<CsvProjectRemapTransformer<IPublishableWorkbook>, IPublishableWorkbook>();
            _planBuilder.Transformers.Add<CsvProjectRemapTransformer<IPublishableDataSource>, IPublishableDataSource>();

            // Add other necessary hooks/transformers for workbooks only
            _planBuilder.Transformers.Add<MigratedTagTransformer<IPublishableWorkbook>, IPublishableWorkbook>();
            _planBuilder.Transformers.Add<EncryptExtractsTransformer<IPublishableWorkbook>, IPublishableWorkbook>();
            _planBuilder.Hooks.Add<UpdatePermissionsHook<IPublishableWorkbook, IWorkbookDetails>>();
            _planBuilder.Hooks.Add<BulkLoggingHook<IWorkbook>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IWorkbook>>();

            // Initialize migration hooks
            _planBuilder.Hooks.Add<SetMigrationContextHook>();

            // Action completed hooks
            _planBuilder.Hooks.Add<LogMigrationActionsHook>();

            // Batch completed hooks
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IUser>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IProject>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IDataSource>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IWorkbook>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<ICloudExtractRefreshTask>>();

            // NOTE: We intentionally remove these to avoid creating new LUID-named projects:
            // _planBuilder.Filters.Add<ProjectLuidFilter, IProject>();
            // _planBuilder.Filters.Add<UserEmailFilter, IUser>();
            // _planBuilder.Mappings.Add<ProjectDestinationLuidMapping, IProject>();

            // Load the previous manifest if possible
            var prevManifest = await LoadManifest(manifestPath, cancel);

            // Build the plan
            var plan = _planBuilder.Build();

            // Execute the migration
            var result = await _migrator.ExecuteAsync(plan, prevManifest, cancel);

            _timer.Stop();

            // Save the manifest
            await _manifestSerializer.SaveAsync(result.Manifest, manifestPath);

            PrintResult(result);
            PrintCsvPostReport(_projectMap, result);

            _logger.LogInformation($"Migration Started: {startTime}");
            _logger.LogInformation($"Migration Finished: {DateTime.UtcNow}");
            _logger.LogInformation($"Elapsed: {_timer.Elapsed}");

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
            _appLifetime.StopApplication();
        }

        public Task StopAsync(CancellationToken cancel) => Task.CompletedTask;

        /// <summary>
        /// Prints the result to console. 
        /// You can replace this with a logging based method of your choice.
        /// </summary>
        /// <param name="result">The migration result.</param>
        private void PrintResult(MigrationResult result)
        {
            _logger.LogInformation($"Result: {result.Status}");

            // Logging any errors from the manifest.
            if (result.Manifest.Errors.Any())
            {
                _logger.LogError("## Errors detected! ##");
                foreach (var error in result.Manifest.Errors)
                {
                    _logger.LogError(error, "Processing Error.");
                }
            }

            foreach (var type in ServerToCloudMigrationPipeline.ContentTypes)
            {
                var contentType = type.ContentType;

                _logger.LogInformation($"## {contentType.Name} ##");

                // Manifest entries can be grouped based on content type.
                foreach (var entry in result.Manifest.Entries.ForContentType(contentType))
                {
                    _logger.LogInformation($"{contentType.Name} {entry.Source.Location} Migration Status: {entry.Status}");

                    if (entry.Errors.Any())
                    {
                        _logger.LogError($"## {contentType.Name} Errors detected! ##");
                        foreach (var error in entry.Errors)
                        {
                            _logger.LogError(error, "Processing Error.");
                        }
                    }

                    if (entry.Destination is not null)
                    {
                        _logger.LogInformation($"{contentType.Name} {entry.Source.Location} migrated to {entry.Destination.Location}");
                    }
                }
            }
        }

        private async Task<MigrationManifest?> LoadManifest(string manifestFilepath, CancellationToken cancel)
        {
            var manifest = await _manifestSerializer.LoadAsync(manifestFilepath, cancel);
            if (manifest is not null)
            {
                ConsoleKey key;
                do
                {
                    Console.Write($"Existing Manifest found at {manifestFilepath}. Should it be used? [Y/n] ");
                    key = Console.ReadKey().Key;
                    Console.WriteLine(); // make Console logs prettier
                } while (key is not ConsoleKey.Enter && key is not ConsoleKey.Y && key is not ConsoleKey.N);

                if (key is ConsoleKey.N)
                {
                    return null;
                }

                _logger.LogInformation($"Using previous manifest from {manifestFilepath}");
                return manifest;
            }

            return null;
        }

        private static string ResolveCsvPath(string configuredPath, string currentFolder)
        {
            // Prefer configured path; otherwise use default relative location
            var relative = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine("CSV_Files", "Project_Migration.csv")
                : configuredPath;

            if (Path.IsPathRooted(relative))
                return relative;

            // 1) Try assembly folder (bin/...)
            var fromAssembly = Path.GetFullPath(Path.Combine(currentFolder, relative));
            if (File.Exists(fromAssembly))
                return fromAssembly;

            // 2) Try current working directory (project root when running via dotnet run)
            var cwd = Directory.GetCurrentDirectory();
            var fromCwd = Path.GetFullPath(Path.Combine(cwd, relative));
            if (File.Exists(fromCwd))
                return fromCwd;

            // Fall back to assembly-based path (will produce a clear error with this path)
            return fromAssembly;
        }

        private void PrintCsvPreflight(ProjectMappingStore map, bool dryRun)
        {
            _logger.LogInformation("CSV Preflight: rows processed={Processed}, ready={Ready}, skipped={Skipped}",
                map.TotalRows, map.ReadyRows, map.SkippedRows.Count);

            foreach (var s in map.SkippedRows.Take(10)) // keep concise
                _logger.LogWarning("CSV row skipped: {Reason}", s);

            if (dryRun)
                _logger.LogInformation("Dry-run: no migration executed.");
        }

        private void PrintCsvPostReport(ProjectMappingStore map, MigrationResult result)
        {
            var successes = result.Manifest.Entries.Count(e => !e.Errors.Any() && e.Destination is not null);
            var failures = result.Manifest.Entries.Count(e => e.Errors.Any());
            _logger.LogInformation("CSV Report: rows processed={Processed}, migration successes (items)={ItemSuccess}, failures (items)={ItemFailures}, csv-skipped-rows={Skipped}",
                map.TotalRows, successes, failures, map.SkippedRows.Count);
        }
    }

    // CSV record definition
    internal sealed class ProjectMappingCsvRow
    {
        public string ProjectLUID { get; set; } = string.Empty;
        public string ProjectDestinationLUID { get; set; } = string.Empty;
    }

    // Stores validated mappings and preflight stats
    internal sealed class ProjectMappingStore
    {
        private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
        public int TotalRows { get; private set; }
        public int ReadyRows => _map.Count;
        public List<string> SkippedRows { get; } = new();

        public bool TryGetDestination(string sourceProjectLuid, out string destProjectLuid)
            => _map.TryGetValue(sourceProjectLuid ?? string.Empty, out destProjectLuid!);

        public async Task LoadAsync(string csvPath, ILogger logger, CancellationToken cancel)
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"CSV not found at {csvPath}");

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim
            });

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in csv.GetRecords<ProjectMappingCsvRow>())
            {
                TotalRows++;
                var src = (row.ProjectLUID ?? string.Empty).Trim();
                var dst = (row.ProjectDestinationLUID ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst))
                {
                    SkippedRows.Add($"Row {TotalRows}: missing ProjectLUID or ProjectDestinationLUID");
                    continue;
                }
                if (!seen.Add(src))
                {
                    SkippedRows.Add($"Row {TotalRows}: duplicate ProjectLUID '{src}'");
                    continue;
                }

                // Accept the row
                _map[src] = dst;
            }

            logger.LogInformation("Loaded {Ready} project mappings from CSV at {Path}", ReadyRows, csvPath);
            await Task.CompletedTask; // maintain async signature
        }
    }

    // Filter: only migrate workbooks whose source project LUID is in CSV
    internal sealed class WorkbookCsvFilter : Tableau.Migration.Engine.Hooks.Filters.IContentFilter<IWorkbook>
    {
        private readonly ProjectMappingStore _map;
        public WorkbookCsvFilter(ProjectMappingStore map) => _map = map;

        public Task<IEnumerable<ContentMigrationItem<IWorkbook>>?> ExecuteAsync(
            IEnumerable<ContentMigrationItem<IWorkbook>> ctx,
            CancellationToken cancel)
        {
            var filtered = ctx.Where(i =>
            {
                var srcProjectId = ((IContainerContent?)i.SourceItem)?.Container.Id.ToString() ?? string.Empty;
                return _map.TryGetDestination(srcProjectId, out _);
            });

            return Task.FromResult<IEnumerable<ContentMigrationItem<IWorkbook>>?>(filtered);
        }
    }

    // Filter: only migrate data sources whose source project LUID is in CSV
    internal sealed class DataSourceCsvFilter : Tableau.Migration.Engine.Hooks.Filters.IContentFilter<IDataSource>
    {
        private readonly ProjectMappingStore _map;
        public DataSourceCsvFilter(ProjectMappingStore map) => _map = map;

        public Task<IEnumerable<ContentMigrationItem<IDataSource>>?> ExecuteAsync(
            IEnumerable<ContentMigrationItem<IDataSource>> ctx,
            CancellationToken cancel)
        {
            var filtered = ctx.Where(i =>
            {
                var srcProjectId = ((IContainerContent?)i.SourceItem)?.Container.Id.ToString() ?? string.Empty;
                return _map.TryGetDestination(srcProjectId, out _);
            });

            return Task.FromResult<IEnumerable<ContentMigrationItem<IDataSource>>?>(filtered);
        }
    }

    // Transformer: direct items to the existing destination project by LUID
    internal sealed class CsvProjectRemapTransformer<TPublishable> : Tableau.Migration.Engine.Hooks.Transformers.IContentTransformer<TPublishable>
        where TPublishable : class
    {
        private readonly ProjectMappingStore _map;
        private readonly ILogger<CsvProjectRemapTransformer<TPublishable>> _logger;

        public CsvProjectRemapTransformer(ProjectMappingStore map, ILogger<CsvProjectRemapTransformer<TPublishable>> logger)
        {
            _map = map;
            _logger = logger;
        }

        // Implement the SDK-required method
        public Task<TPublishable> ExecuteAsync(TPublishable publishable, CancellationToken cancel)
        {
            if (publishable is IPublishableWorkbook wb)
            {
                RemapProject(wb);
            }
            else if (publishable is IPublishableDataSource ds)
            {
                RemapProject(ds);
            }

            return Task.FromResult(publishable);
        }

        private void RemapProject(IPublishableWorkbook wb)
        {
            var srcProjectId = GetProjectIdViaReflection(wb);
            if (string.IsNullOrWhiteSpace(srcProjectId))
            {
                _logger.LogError("Could not determine source project id for workbook. Item will fail to publish.");
                return;
            }

            if (_map.TryGetDestination(srcProjectId, out var dst))
            {
                if (!TrySetProjectIdViaReflection(wb, dst))
                {
                    _logger.LogError("Failed to set destination project id for workbook. Item will fail to publish.");
                }
            }
            else
            {
                _logger.LogError("No destination mapping found for workbook in source project {ProjectId}. Item will fail to publish.", srcProjectId);
            }
        }

        private void RemapProject(IPublishableDataSource ds)
        {
            var srcProjectId = GetProjectIdViaReflection(ds);
            if (string.IsNullOrWhiteSpace(srcProjectId))
            {
                _logger.LogError("Could not determine source project id for data source. Item will fail to publish.");
                return;
            }

            if (_map.TryGetDestination(srcProjectId, out var dst))
            {
                if (!TrySetProjectIdViaReflection(ds, dst))
                {
                    _logger.LogError("Failed to set destination project id for data source. Item will fail to publish.");
                }
            }
            else
            {
                _logger.LogError("No destination mapping found for data source in source project {ProjectId}. Item will fail to publish.", srcProjectId);
            }
        }

        // Helpers: handle multiple SDK shapes (ProjectId, ParentProjectId, or Project.Id)
        private static string GetProjectIdViaReflection(object obj)
        {
            var t = obj.GetType();

            // Try ProjectId
            var pi = t.GetProperty("ProjectId");
            if (pi is not null)
            {
                var val = pi.GetValue(obj);
                if (val is string strVal)
                {
                    return strVal;
                }
            }

            // Try ParentProjectId (for DataSources)
            pi = t.GetProperty("ParentProjectId");
            if (pi is not null)
            {
                var val = pi.GetValue(obj);
                if (val is string strVal)
                {
                    return strVal;
                }
            }

            // Try Project.Id (for newer SDK shapes)
            pi = t.GetProperty("Id");
            if (pi is not null)
            {
                var val = pi.GetValue(obj);
                if (val is string strVal)
                {
                    return strVal;
                }
            }

            return string.Empty;
        }

        private static bool TrySetProjectIdViaReflection(object obj, string projectId)
        {
            var t = obj.GetType();

            // Prefer ProjectId if writable
            var pi = t.GetProperty("ProjectId");
            if (pi is not null && pi.CanWrite)
            {
                pi.SetValue(obj, projectId);
                return true;
            }

            // Try ParentProjectId if writable
            var ppi = t.GetProperty("ParentProjectId");
            if (ppi is not null && ppi.CanWrite)
            {
                ppi.SetValue(obj, projectId);
                return true;
            }

            // Try Project.Id if inner Id is writable
            var projProp = t.GetProperty("Project");
            if (projProp is not null)
            {
                var projObj = projProp.GetValue(obj);
                if (projObj is not null)
                {
                    var idProp = projObj.GetType().GetProperty("Id");
                    if (idProp is not null && idProp.CanWrite)
                    {
                        idProp.SetValue(projObj, projectId);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
#endregion
