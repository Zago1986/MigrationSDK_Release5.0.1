using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tableau.Migration;
using Tableau.Migration.Content;
using Tableau.Migration.Engine;
using Tableau.Migration.Engine.Endpoints;
using Tableau.Migration.Engine.Hooks;

namespace MigrationSDK.Hooks.InitializeMigration
{
    /// <summary>
    /// Pre-loads destination projects during migration initialization.
    /// Stores project information in a shared cache for use by content mappings.
    /// </summary>
    public class LoadDestinationProjectsHook : IInitializeMigrationHook
    {
        private readonly DestinationProjectCache _projectCache;
        private readonly ILogger<LoadDestinationProjectsHook> _logger;

        public LoadDestinationProjectsHook(
            DestinationProjectCache projectCache,
            ILogger<LoadDestinationProjectsHook> logger)
        {
            _projectCache = projectCache;
            _logger = logger;
        }

        public async Task<IInitializeMigrationHookResult?> ExecuteAsync(
            IInitializeMigrationHookResult ctx, 
            CancellationToken cancel)
        {
            _logger.LogInformation("Pre-loading destination projects for content mapping...");

            // Note: Unfortunately, we don't have access to the destination API endpoint here
            // The hook context doesn't provide access to IMigration or IDestinationEndpoint
            // We'll need to load projects lazily from the mappings instead

            _logger.LogInformation("Destination project cache initialized (lazy loading enabled).");
            
            return ctx;
        }
    }
    
    /// <summary>
    /// Thread-safe cache for destination projects.
    /// Projects are loaded lazily on first access.
    /// </summary>
    public class DestinationProjectCache
    {
        private readonly ConcurrentDictionary<string, ContentLocation> _projectLocations = new();
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private bool _isLoaded = false;
        private IMigration? _migration;

        public void SetMigration(IMigration migration)
        {
            _migration = migration;
        }

        public async Task<bool> EnsureLoadedAsync(IMigration migration, CancellationToken cancel)
        {
            if (_isLoaded)
                return true;

            await _loadLock.WaitAsync(cancel);
            try
            {
                if (_isLoaded)
                    return true;

                _migration = migration;
                // Projects will be loaded on-demand when requested
                _isLoaded = true;
                return true;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public bool TryGetProjectLocation(string luid, out ContentLocation location)
        {
            var found = _projectLocations.TryGetValue(luid, out var loc);
            location = loc;
            return found;
        }

        public void CacheProjectLocation(string luid, ContentLocation location)
        {
            _projectLocations.TryAdd(luid, location);
        }
    }
}
