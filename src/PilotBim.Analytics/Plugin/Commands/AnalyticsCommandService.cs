using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using Ascon.Pilot.Bim.SDK.ModelStorage;
using Ascon.Pilot.Bim.SDK.Search;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Export;
using PilotBim.Analytics.Properties;
using PilotBim.Analytics.Services;
using PilotBim.Analytics.Views;

namespace PilotBim.Analytics.Plugin.Commands
{
    [Export]
    public sealed class AnalyticsCommandService
    {
        private readonly IObjectsRepository _repository;
        private readonly IPilotServiceProvider _services;
        private readonly ISearchService _importedSearch;
        private InventoryWindow _catalogWindow;
        private AnalyticsWindow _analyticsWindow;

        [ImportingConstructor]
        public AnalyticsCommandService(
            IObjectsRepository repository,
            IPilotServiceProvider services,
            [Import(AllowDefault = true)] ISearchService search)
        {
            _repository = repository;
            _services = services;
            _importedSearch = search;
            AnalyticsLogger.Info("command-service-ctor",
                "importedSearch=" + (search != null) + " services=" + (services != null));
        }

        public void OpenCatalog()
        {
            try
            {
                var inventory = CreateInventoryService();

                if (_catalogWindow != null && _catalogWindow.IsVisible)
                {
                    _catalogWindow.Activate();
                    return;
                }

                _catalogWindow = new InventoryWindow(inventory);
                _catalogWindow.Closed += (s, e) => _catalogWindow = null;
                _catalogWindow.Show();
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("open-catalog", ex);
                MessageBox.Show(
                    Resources.Catalog_OpenFailedPrefix + ex.Message,
                    "PilotBim.Analytics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void OpenAnalytics()
        {
            try
            {
                var inventory = CreateInventoryService();

                if (_analyticsWindow != null && _analyticsWindow.IsVisible)
                {
                    _analyticsWindow.Activate();
                    return;
                }

                _analyticsWindow = new AnalyticsWindow(
                    inventory,
                    new ProjectAnalyticsService(),
                    new AnalyticsCsvExporter());
                _analyticsWindow.Closed += (s, e) => _analyticsWindow = null;
                _analyticsWindow.Show();
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("open-analytics", ex);
                MessageBox.Show(
                    Resources.Analytics_OpenFailedPrefix + ex.Message,
                    "PilotBim.Analytics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private InventoryService CreateInventoryService()
        {
            ISearchService search = _importedSearch;
            IModelStorageProvider storage = null;
            IModelSearchManager modelSearch = null;

            if (_services != null)
            {
                if (search == null)
                    search = TryGetService<ISearchService>(_services, "ISearchService");
                storage = TryGetService<IModelStorageProvider>(_services, "IModelStorageProvider");
                modelSearch = TryGetService<IModelSearchManager>(_services, "IModelSearchManager");
                LogKnownServices(_services);
            }

            AnalyticsLogger.Info("inventory-service",
                "search=" + (search != null)
                + " storage=" + (storage != null)
                + " modelSearch=" + (modelSearch != null)
                + " importedSearch=" + (_importedSearch != null));

            return new InventoryService(_repository, search, storage, modelSearch);
        }

        private static T TryGetService<T>(IPilotServiceProvider services, string label) where T : class
        {
            try
            {
                var found = services.GetServices<T>().FirstOrDefault();
                AnalyticsLogger.Info("resolve-service", label + "=" + (found != null));
                return found;
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("resolve-service-" + label, ex);
                return null;
            }
        }

        private static void LogKnownServices(IPilotServiceProvider services)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("probe:");
                sb.Append(" Search=").Append(CountServices<ISearchService>(services));
                sb.Append(" Messages=").Append(CountServices<IMessagesRepository>(services));
                sb.Append(" Storage=").Append(CountServices<IModelStorageProvider>(services));
                sb.Append(" ModelSearch=").Append(CountServices<IModelSearchManager>(services));
                sb.Append(" Transition=").Append(CountServices<ITransitionManager>(services));
                AnalyticsLogger.Info("service-probe", sb.ToString());
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("service-probe", ex);
            }
        }

        private static int CountServices<T>(IPilotServiceProvider services)
        {
            try
            {
                return services.GetServices<T>().Count();
            }
            catch
            {
                return -1;
            }
        }
    }
}
