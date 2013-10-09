﻿using System.ComponentModel;
using System.Net;
using System.Text;
using MediaBrowser.ApiInteraction;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Constants;
using MediaBrowser.Common.Implementations.Logging;
using MediaBrowser.Common.Implementations.Updates;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.System;
using MediaBrowser.Theater.Implementations.Configuration;
using MediaBrowser.Theater.Interfaces.System;
using MediaBrowser.UI.StartupWizard;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MediaBrowser.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// The single instance mutex
        /// </summary>
        private static Mutex _singleInstanceMutex;

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        private ILogger _logger;
        private ILogManager _logManager;

        /// <summary>
        /// Gets or sets the composition root.
        /// </summary>
        /// <value>The composition root.</value>
        private ApplicationHost _appHost;

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>The instance.</value>
        public static App Instance
        {
            get
            {
                return Current as App;
            }
        }

        /// <summary>
        /// Gets the application window.
        /// </summary>
        /// <value>The application window.</value>
        public MainWindow ApplicationWindow { get; private set; }

        /// <summary>
        /// Gets the hidden window.
        /// </summary>
        /// <value>The hidden window.</value>
        internal HiddenWindow HiddenWindow { get; set; }

        /// <summary>
        /// The _app paths
        /// </summary>
        private readonly ApplicationPaths _appPaths;

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            bool createdNew;

            _singleInstanceMutex = new Mutex(true, @"Local\" + typeof(App).Assembly.GetName().Name, out createdNew);

            if (!createdNew)
            {
                _singleInstanceMutex = null;
                return;
            }

            // Look for the existence of an update archive
            var appPaths = new ApplicationPaths();
            var logManager = new NlogManager(appPaths.LogDirectoryPath, "theater");
            logManager.ReloadLogger(LogSeverity.Debug);

            var updateArchive = Path.Combine(appPaths.TempUpdatePath, Constants.MbTheaterPkgName + ".zip");

            if (File.Exists(updateArchive))
            {
                // Update is there - execute update
                try
                {
                    new ApplicationUpdater().UpdateApplication(MBApplication.MBTheater, appPaths, updateArchive, logManager.GetLogger("ApplicationUpdater"), string.Empty);

                    // And just let the app exit so it can update
                    return;
                }
                catch (Exception e)
                {
                    MessageBox.Show(string.Format("Error attempting to update application.\n\n{0}\n\n{1}", e.GetType().Name, e.Message));
                }
            }

            var application = new App(appPaths, logManager);

            application.Run();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="App" /> class.
        /// </summary>
        public App(ApplicationPaths appPaths, ILogManager logManager)
        {
            _appPaths = appPaths;
            _logManager = logManager;

            InitializeComponent();
        }

        /// <summary>
        /// Shows the application window.
        /// </summary>
        private void ShowApplicationWindow()
        {
            var win = new MainWindow(_logger, _appHost.PlaybackManager, _appHost.ApiClient, _appHost.ImageManager, _appHost, _appHost.PresentationManager, _appHost.UserInputManager, _appHost.TheaterConfigurationManager, _appHost.NavigationService);

            var config = _appHost.TheaterConfigurationManager.Configuration;

            // Restore window position/size
            if (config.WindowState.HasValue)
            {
                // Set window state
                win.WindowState = config.WindowState.Value;

                // Set position if not maximized
                if (config.WindowState.Value != WindowState.Maximized)
                {
                    double left = 0;
                    double top = 0;

                    // Set left
                    if (config.WindowLeft.HasValue)
                    {
                        win.WindowStartupLocation = WindowStartupLocation.Manual;
                        win.Left = left = Math.Max(config.WindowLeft.Value, 0);
                    }

                    // Set top
                    if (config.WindowTop.HasValue)
                    {
                        win.WindowStartupLocation = WindowStartupLocation.Manual;
                        win.Top = top = Math.Max(config.WindowTop.Value, 0);
                    }

                    // Set width
                    if (config.WindowWidth.HasValue)
                    {
                        win.Width = Math.Min(config.WindowWidth.Value, SystemParameters.VirtualScreenWidth - left);
                    }

                    // Set height
                    if (config.WindowHeight.HasValue)
                    {
                        win.Height = Math.Min(config.WindowHeight.Value, SystemParameters.VirtualScreenHeight - top);
                    }
                }
            }

            win.LocationChanged += ApplicationWindow_LocationChanged;
            win.StateChanged += ApplicationWindow_LocationChanged;
            win.SizeChanged += ApplicationWindow_LocationChanged;
            win.Closing += win_Closing;

            HiddenWindow.Activated += HiddenWindow_Activated;
            HiddenWindow.IsVisibleChanged += HiddenWindow_IsVisibleChanged;

            ApplicationWindow = win;

            ApplicationWindow.Show();

            ApplicationWindow.Owner = HiddenWindow;

            SyncHiddenWindowLocation();

            ApplicationWindow.Activate();
        }

        void win_Closing(object sender, CancelEventArgs e)
        {
            HiddenWindow.Close();
        }

        void HiddenWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _logger.Debug("HiddenWindow_IsVisibleChanged.");
            ApplicationWindow.Activate();
        }

        /// <summary>
        /// Handles the LocationChanged event of the ApplicationWindow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        void ApplicationWindow_LocationChanged(object sender, EventArgs e)
        {
            SyncHiddenWindowLocation();
        }

        /// <summary>
        /// Syncs the hidden window location.
        /// </summary>
        public void SyncHiddenWindowLocation()
        {
            HiddenWindow.Width = ApplicationWindow.Width;
            HiddenWindow.Height = ApplicationWindow.Height;
            HiddenWindow.Top = ApplicationWindow.Top;
            HiddenWindow.Left = ApplicationWindow.Left;
            HiddenWindow.WindowState = ApplicationWindow.WindowState;

            ApplicationWindow.Activate();
        }

        void HiddenWindow_Activated(object sender, EventArgs e)
        {
            _logger.Debug("Hidden window activated.");
            ApplicationWindow.Activate();
        }

        /// <summary>
        /// Loads the kernel.
        /// </summary>
        protected async void LoadKernel()
        {
            try
            {
                _appHost = new ApplicationHost(_appPaths, _logManager);

                _logger = _appHost.LogManager.GetLogger("App");

                await _appHost.Init();

                // Load default theme
                await _appHost.ThemeManager.LoadDefaultTheme();

                HiddenWindow = new HiddenWindow();
                HiddenWindow.Show();

                ShowApplicationWindow();

                _appHost.StartEntryPoints();

                await LoadInitialPresentation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error launching application", ex);

                MessageBox.Show("There was an error launching Media Browser: " + ex.Message);

                // Shutdown the app with an error code
                Shutdown(1);
            }
        }

        /// <summary>
        /// Loads the initial presentation.
        /// </summary>
        /// <returns>Task.</returns>
        private async Task LoadInitialPresentation()
        {
            var foundServer = false;

            try
            {
                var systemInfo = await _appHost.ApiClient.GetSystemInfoAsync().ConfigureAwait(false);

                foundServer = true;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error connecting to server using saved connection information. Host: {0}, Port {1}", ex, _appHost.ApiClient.ServerHostName, _appHost.ApiClient.ServerApiPort);
            }

            if (!foundServer)
            {
                try
                {
                    var address = await new ServerLocator().FindServer(500, CancellationToken.None).ConfigureAwait(false);

                    var parts = address.ToString().Split(':');

                    _appHost.ApiClient.ChangeServerLocation(parts[0], address.Port);

                    foundServer = true;
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error attempting to locate server.", ex);
                }
            }

            var mediaFilters = _appHost.MediaFilters;

            if (!foundServer || !AreRequiredMediaFiltersInstalled(mediaFilters))
            {
                // Show connection wizard
                await Dispatcher.InvokeAsync(async () => await _appHost.NavigationService.Navigate(new StartupWizardPage(_appHost.NavigationService, _appHost.TheaterConfigurationManager, _appHost.ApiClient, _appHost.PresentationManager, _logger, mediaFilters)));
            }
            else
            {
                // TODO: Open web socket using systemInfo

                await _appHost.NavigationService.NavigateToLoginPage();
            }
        }

        private bool AreRequiredMediaFiltersInstalled(IMediaFilters mediaFilters)
        {
            try
            {
                return mediaFilters.IsLavFiltersInstalled() && mediaFilters.IsXyVsFilterInstalled();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Startup" /> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.StartupEventArgs" /> that contains the event data.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            LoadKernel();

            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
        }

        /// <summary>
        /// Handles the UnhandledException event of the CurrentDomain control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="UnhandledExceptionEventArgs" /> instance containing the event data.</param>
        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;

            LogUnhandledException(exception);

            MessageBox.Show("Unhandled exception: " + exception.Message);
        }

        private void LogUnhandledException(Exception ex)
        {
            _logger.ErrorException("UnhandledException", ex);

            var path = Path.Combine(_appPaths.LogDirectoryPath, "crash_" + Guid.NewGuid() + ".txt");

            var builder = LogHelper.GetLogMessage(ex);

            File.WriteAllText(path, builder.ToString());
        }

        /// <summary>
        /// Handles the SessionEnding event of the SystemEvents control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="SessionEndingEventArgs" /> instance containing the event data.</param>
        void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            // Try to shut down gracefully
            Shutdown();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Exit" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.Windows.ExitEventArgs" /> that contains the event data.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            var win = ApplicationWindow;

            if (win != null)
            {
                // Save window position
                var config = _appHost.TheaterConfigurationManager.Configuration;
                config.WindowState = win.WindowState;
                config.WindowTop = win.Top;
                config.WindowLeft = win.Left;
                config.WindowWidth = win.Width;
                config.WindowHeight = win.Height;
                _appHost.TheaterConfigurationManager.SaveConfiguration();
            }

            ReleaseMutex();

            base.OnExit(e);

            _appHost.Dispose();
        }

        /// <summary>
        /// Releases the mutex.
        /// </summary>
        private void ReleaseMutex()
        {
            if (_singleInstanceMutex == null)
            {
                return;
            }

            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Close();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        /// <summary>
        /// Restarts this instance.
        /// </summary>
        public void Restart()
        {
            Dispatcher.Invoke(ReleaseMutex);

            _appHost.Dispose();

            System.Windows.Forms.Application.Restart();

            Dispatcher.Invoke(Shutdown);
        }

    }
}
