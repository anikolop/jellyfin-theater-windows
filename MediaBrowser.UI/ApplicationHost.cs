﻿using MediaBrowser.ClickOnce;
using MediaBrowser.Common.Implementations;
using MediaBrowser.Common.Implementations.HttpServer;
using MediaBrowser.Common.Implementations.Logging;
using MediaBrowser.Common.Implementations.NetworkManagement;
using MediaBrowser.Common.Implementations.ScheduledTasks;
using MediaBrowser.Common.Implementations.Serialization;
using MediaBrowser.Common.Implementations.ServerManager;
using MediaBrowser.Common.IO;
using MediaBrowser.Common.Kernel;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.IsoMounter;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Updates;
using MediaBrowser.UI.Configuration;
using MediaBrowser.UI.Controller;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.UI
{
    /// <summary>
    /// Class CompositionRoot
    /// </summary>
    public class ApplicationHost : BaseApplicationHost, IApplicationHost
    {
        /// <summary>
        /// Gets or sets the kernel.
        /// </summary>
        /// <value>The kernel.</value>
        internal UIKernel Kernel { get; private set; }

        /// <summary>
        /// The json serializer
        /// </summary>
        private readonly IJsonSerializer _jsonSerializer = new JsonSerializer();

        /// <summary>
        /// The _XML serializer
        /// </summary>
        private readonly IXmlSerializer _xmlSerializer = new XmlSerializer();

        /// <summary>
        /// Gets the server application paths.
        /// </summary>
        /// <value>The server application paths.</value>
        protected UIApplicationPaths UIApplicationPaths
        {
            get { return (UIApplicationPaths)ApplicationPaths; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationHost" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public ApplicationHost()
            : base()
        {
            Kernel = new UIKernel(this, UIApplicationPaths, _xmlSerializer, Logger);

            var networkManager = new NetworkManager();

            var serverManager = new ServerManager(this, Kernel, networkManager, _jsonSerializer, Logger);

            var taskManager = new TaskManager(ApplicationPaths, _jsonSerializer, Logger, serverManager);

            LogManager.ReloadLogger(Kernel.Configuration.EnableDebugLevelLogging ? LogSeverity.Debug : LogSeverity.Info);

            Logger.Info("Version {0} initializing", ApplicationVersion);

            RegisterResources(taskManager, networkManager, serverManager);

            FindParts();
        }

        /// <summary>
        /// Gets the application paths.
        /// </summary>
        /// <returns>IApplicationPaths.</returns>
        protected override IApplicationPaths GetApplicationPaths()
        {
            return new UIApplicationPaths();
        }

        /// <summary>
        /// Gets the log manager.
        /// </summary>
        /// <returns>ILogManager.</returns>
        protected override ILogManager GetLogManager()
        {
            return new NlogManager(ApplicationPaths.LogDirectoryPath, "MBT");
        }

        /// <summary>
        /// Registers resources that classes will depend on
        /// </summary>
        protected override void RegisterResources(ITaskManager taskManager, INetworkManager networkManager, IServerManager serverManager)
        {
            base.RegisterResources(taskManager, networkManager, serverManager);

            RegisterSingleInstance<IKernel>(Kernel);
            RegisterSingleInstance(Kernel);

            RegisterSingleInstance<IApplicationHost>(this);

            RegisterSingleInstance(UIApplicationPaths);
            RegisterSingleInstance<IIsoManager>(new PismoIsoManager(Logger));
            RegisterSingleInstance(_jsonSerializer);
            RegisterSingleInstance(_xmlSerializer);
            RegisterSingleInstance(ServerFactory.CreateServer(this, ProtobufSerializer, Logger, "Media Browser", "index.html"), false);
        }

        /// <summary>
        /// Restarts this instance.
        /// </summary>
        public void Restart()
        {
            App.Instance.Restart();
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance can self update.
        /// </summary>
        /// <value><c>true</c> if this instance can self update; otherwise, <c>false</c>.</value>
        public bool CanSelfUpdate
        {
            get { return ClickOnceHelper.IsNetworkDeployed; }
        }

        /// <summary>
        /// Checks for update.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task{CheckForUpdateResult}.</returns>
        public Task<CheckForUpdateResult> CheckForApplicationUpdate(CancellationToken cancellationToken, IProgress<double> progress)
        {
            return new ApplicationUpdateCheck().CheckForApplicationUpdate(cancellationToken, progress);
        }

        /// <summary>
        /// Updates the application.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task.</returns>
        public Task UpdateApplication(CancellationToken cancellationToken, IProgress<double> progress)
        {
            return new ApplicationUpdater().UpdateApplication(cancellationToken, progress);
        }

        /// <summary>
        /// Gets the composable part assemblies.
        /// </summary>
        /// <returns>IEnumerable{Assembly}.</returns>
        protected override IEnumerable<Assembly> GetComposablePartAssemblies()
        {
            // Gets all plugin assemblies by first reading all bytes of the .dll and calling Assembly.Load against that
            // This will prevent the .dll file from getting locked, and allow us to replace it when needed
            foreach (var pluginAssembly in Directory
                .EnumerateFiles(ApplicationPaths.PluginsPath, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(LoadAssembly).Where(a => a != null))
            {
                yield return pluginAssembly;
            }

            var runningDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            var corePluginDirectory = Path.Combine(runningDirectory, "CorePlugins");

            // This will prevent the .dll file from getting locked, and allow us to replace it when needed
            foreach (var pluginAssembly in Directory
                .EnumerateFiles(corePluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(LoadAssembly).Where(a => a != null))
            {
                yield return pluginAssembly;
            }

            // Include composable parts in the Model assembly 
            yield return typeof(SystemInfo).Assembly;

            // Include composable parts in the Common assembly 
            yield return typeof(IKernel).Assembly;

            // Common implementations
            yield return typeof(TaskManager).Assembly;

            // Include composable parts in the running assembly
            yield return GetType().Assembly;
        }

        /// <summary>
        /// Shuts down.
        /// </summary>
        public void Shutdown()
        {
            App.Instance.Shutdown();
        }
    }
}