using System.Threading.Tasks;
using MigrationSDK.Config;
using MigrationSDK.Hooks.BatchMigrationCompleted;
using MigrationSDK.Hooks.Filters;
using MigrationSDK.Hooks.InitializeMigration;
using MigrationSDK.Hooks.Mappings;
using MigrationSDK.Hooks.MigrationActionCompleted;
using MigrationSDK.Hooks.PostPublish;
using MigrationSDK.Hooks.Transformers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tableau.Migration;
using Tableau.Migration.Content;

#region namespace
namespace MigrationSDK
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // Set the DOTNET_ENVIRONMENT environment variable to the name of the environment.
            // This loads the appsettings.<DOTNET_ENVIRONMENT>.json config file.
            // If no DOTNET_ENVIRONMENT is set, appsettings.json will be used
            using var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx, services) =>
                {
                    services
                        .Configure<MyMigrationApplicationOptions>(ctx.Configuration)
                        .Configure<EmailDomainMappingOptions>(ctx.Configuration.GetSection("tableau:emailDomainMapping"))
                        .Configure<UnlicensedUsersMappingOptions>(ctx.Configuration.GetSection("tableau:unlicensedUsersMapping"))
                        .AddTableauMigrationSdk(ctx.Configuration.GetSection("tableau:migrationSdk"))
                        .AddCustomizations()
                        .AddHostedService<MyMigrationApplication>();
                })
                .Build();

            await host.RunAsync();
        }

        /// <summary>
        /// Registers services required for using the Tableau Migration SDK customizations.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>        
        /// <returns>The same service collection as the <paramref name="services"/> parameter.</returns>
        public static IServiceCollection AddCustomizations(this IServiceCollection services)
        {
            #region SetCustomContext-Service-DI
            services.AddScoped<CustomContext>();
            #endregion

            #region SetCustomContext-Hook-DI
            services.AddScoped<SetMigrationContextHook>();
            #endregion

            #region EmailDomainMapping-DI
            services.AddScoped<EmailDomainMapping>();
            #endregion

            #region UnlicensedUsersMapping-DI
            services.AddScoped<UnlicensedUsersMapping>();
            #endregion

            #region ProjectRenameMapping-DI
            services.AddScoped<ProjectRenameMapping>();
            #endregion

            #region ChangeProjectMapping-DI
            services.AddScoped<ChangeProjectMapping<IWorkbook>>();
            services.AddScoped<ChangeProjectMapping<IDataSource>>();
            #endregion

            #region DefaultProjectsFilter-DI
            services.AddScoped<DefaultProjectsFilter>();
            #endregion

            #region UnlicensedUsersFilter-DI
            services.AddScoped<UnlicensedUsersFilter>();
            #endregion
            
            #region SharedCustomViewFilter-DI
            services.AddScoped<SharedCustomViewFilter>();
            #endregion

            #region UpdatePermissionsHook-DI
            services.AddScoped(typeof(UpdatePermissionsHook<,>));
            #endregion

            #region BulkLoggingHook-DI
            services.AddScoped(typeof(BulkLoggingHook<>));
            #endregion

            #region MigratedTagTransformer-DI
            services.AddScoped<MigratedTagTransformer<IPublishableDataSource>>();
            services.AddScoped<MigratedTagTransformer<IPublishableWorkbook>>();
            #endregion

            #region EncryptExtractTransformer-DI
            services.AddScoped<EncryptExtractsTransformer<IPublishableDataSource>>();
            services.AddScoped<EncryptExtractsTransformer<IPublishableWorkbook>>();
            #endregion

            #region StartAtTransformer-DI
            services.AddScoped(typeof(SimpleScheduleStartAtTransformer<>));
            #endregion
            
            #region CustomViewDefaultUsersTransformer-DI
            services.AddScoped<CustomViewExcludeDefaultUserTransformer>();
            #endregion

            #region LogMigrationActionsHook-DI
            services.AddScoped<LogMigrationActionsHook>();
            #endregion

            #region LogMigrationBatchesHook-DI
            services.AddScoped(typeof(LogMigrationBatchesHook<>));
            #endregion

            #region ProjectLuidFilter-DI
            services.AddScoped<ProjectLuidFilter>();
            #endregion

            #region ProjectDestinationLuidMapping-DI
            services.AddScoped<ProjectDestinationLuidMapping>();
            #endregion

            #region UserEmailFilter-DI
            services.AddScoped<UserEmailFilter>();
            #endregion

            #region CSV project mapping store + filters/transformers
            services.AddSingleton<ProjectMappingStore>();
            services.AddScoped<CsvProjectRemapTransformer<IPublishableWorkbook>>();
            services.AddScoped<CsvProjectRemapTransformer<IPublishableDataSource>>();
            services.AddScoped<WorkbookCsvFilter>();
            services.AddScoped<DataSourceCsvFilter>();
            #endregion

            return services;
        }
    }
}
#endregion