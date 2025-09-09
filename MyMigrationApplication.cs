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
using Tableau.Migration.Content.Schedules.Cloud;
using Tableau.Migration.Engine.Manifest;
using Tableau.Migration.Engine.Pipelines;

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

        public MyMigrationApplication(
            IHostApplicationLifetime appLifetime,
            IMigrationPlanBuilder planBuilder,
            IMigrator migrator,
            IOptions<MyMigrationApplicationOptions> options,
            ILogger<MyMigrationApplication> logger,
            MigrationManifestSerializer manifestSerializer)
        {
            _timer = new Stopwatch();

            _appLifetime = appLifetime;

            // You can choose to assign an instance of the ServerToCloudMigrationPlanBuilder to help you 
            // add your own filters, mappings, transformers or hooks.
            // Refer to the Articles section of this documentation for more details.
            _planBuilder = planBuilder;
            _migrator = migrator;
            _options = options.Value;
            _logger = logger;
            _manifestSerializer = manifestSerializer;
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

            var startTime = DateTime.UtcNow;
            _timer.Start();

            #region EmailDomainMapping-Registration
            // Use the methods on your plan builder to add configuration and make customizations.
            _planBuilder = _planBuilder
                .FromSourceTableauServer(_options.Source.ServerUrl, _options.Source.SiteContentUrl, _options.Source.AccessTokenName, _options.Source.AccessToken)
                .ToDestinationTableauCloud(_options.Destination.ServerUrl, _options.Destination.SiteContentUrl, _options.Destination.AccessTokenName, _options.Destination.AccessToken)
                .ForServerToCloud()
                .WithTableauIdAuthenticationType()
                // You can add authentication type mappings here            
                .WithTableauCloudUsernames<EmailDomainMapping>();
            #endregion

            var validationResult = _planBuilder.Validate();

            if (!validationResult.Success)
            {
                _logger.LogError("Migration plan validation failed. {Errors}", validationResult.Errors);
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                _appLifetime.StopApplication();
            }

            // Add mappings
            #region UnlicensedUsersMapping-Registration
            _planBuilder.Mappings.Add<UnlicensedUsersMapping, IUser>();
            #endregion

            #region ProjectRenameMapping-Registration
            _planBuilder.Mappings.Add<ProjectRenameMapping, IProject>();
            #endregion

            #region ChangeProjectMapping-Registration
            _planBuilder.Mappings.Add<ChangeProjectMapping<IDataSource>, IDataSource>();
            _planBuilder.Mappings.Add<ChangeProjectMapping<IWorkbook>, IWorkbook>();
            #endregion

            // Add filters
            #region DefaultProjectsFilter-Registration
            _planBuilder.Filters.Add<DefaultProjectsFilter, IProject>();
            #endregion

            #region UnlicensedUsersFilter-Registration
            _planBuilder.Filters.Add<UnlicensedUsersFilter, IUser>();
            #endregion
            
            #region SharedCustomViewFilter-Registration
            _planBuilder.Filters.Add<SharedCustomViewFilter, ICustomView>();
            #endregion

            // Only migrate workbooks defined in CSV
            _planBuilder.Filters.Add<WorkbookCsvFilter, IWorkbook>();
            _planBuilder.Mappings.Add<WorkbookCsvProjectMapping, IWorkbook>();

            // Do not migrate users or groups
            // (Do not add filters/mappings for IUser or IGroup)

            // Add other necessary hooks/transformers for workbooks only
            _planBuilder.Transformers.Add<MigratedTagTransformer<IPublishableWorkbook>, IPublishableWorkbook>();
            _planBuilder.Transformers.Add<EncryptExtractsTransformer<IPublishableWorkbook>, IPublishableWorkbook>();
            _planBuilder.Hooks.Add<UpdatePermissionsHook<IPublishableWorkbook, IWorkbookDetails>>();
            _planBuilder.Hooks.Add<BulkLoggingHook<IWorkbook>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IWorkbook>>();

            // Add initialize migration hooks
            #region SetCustomContext-Registration
            _planBuilder.Hooks.Add<SetMigrationContextHook>();
            #endregion

            // Add migration action completed hooks
            #region LogMigrationActionsHook-Registration
            _planBuilder.Hooks.Add<LogMigrationActionsHook>();
            #endregion

            // Add batch migration completed hooks
            #region LogMigrationBatchesHook-Registration
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IUser>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IProject>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IDataSource>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<IWorkbook>>();
            _planBuilder.Hooks.Add<LogMigrationBatchesHook<ICloudExtractRefreshTask>>();
            #endregion

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
    }
}

#endregion