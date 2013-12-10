﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.Theming;
using MediaBrowser.Common;

namespace MediaBrowser.Theater.Interfaces.ViewModels
{
    /// <summary>
    /// Represents a set of commands which can be executed from anywhere in the theme. Bind to this class to implement functionality and inherit to add functionality.
    /// </summary>
    public class MasterCommandsViewModel : BaseViewModel, IDisposable
    {
        private bool _serverCanSelfRestart;
        private bool _serverHasPendingRestart;

        protected readonly INavigationService NavigationService;
        protected readonly ISessionManager SessionManager;
        protected readonly IPresentationManager PresentationManager;
        protected readonly IApiClient ApiClient;
        protected readonly ILogger Logger;
        protected readonly IApplicationHost AppHost;
        protected readonly IServerEvents ServerEvents;
        protected readonly Dispatcher Dispatcher;

        public ICommand HomeCommand { get; private set; }
        public ICommand FullscreenVideoCommand { get; private set; }
        public ICommand SettingsCommand { get; private set; }
        public ICommand GoBackCommand { get; private set; }
        public ICommand RestartServerCommand { get; private set; }
        public ICommand RestartApplicationCommand { get; private set; }

        private bool _showRestartServerNotification;
        public bool ShowRestartServerNotification
        {
            get { return _showRestartServerNotification; }

            set
            {
                var changed = _showRestartServerNotification != value;

                _showRestartServerNotification = value;
                if (changed)
                {
                    OnPropertyChanged("ShowRestartServerNotification");
                }
            }
        }

        private bool _showRestartApplicationNotification;
        public bool ShowRestartApplicationNotification
        {
            get { return _showRestartApplicationNotification; }

            set
            {
                var changed = _showRestartApplicationNotification != value;

                _showRestartApplicationNotification = value;
                if (changed)
                {
                    OnPropertyChanged("ShowRestartApplicationNotification");
                }
            }
        }

        public MasterCommandsViewModel(INavigationService navigationService, ISessionManager sessionManager, IPresentationManager presentationManager, IApiClient apiClient, ILogger logger, IApplicationHost appHost, IServerEvents serverEvents)
        {
            Dispatcher = Dispatcher.CurrentDispatcher;

            NavigationService = navigationService;
            SessionManager = sessionManager;
            PresentationManager = presentationManager;
            ApiClient = apiClient;
            Logger = logger;
            AppHost = appHost;
            ServerEvents = serverEvents;

            ServerEvents.RestartRequired += ServerEvents_RestartRequired;
            ServerEvents.ServerRestarting += ServerEvents_ServerRestarting;
            ServerEvents.ServerShuttingDown += ServerEvents_ServerShuttingDown;
            ServerEvents.Connected += ServerEvents_Connected;
            AppHost.HasPendingRestartChanged += AppHostHasPendingRestartChanged;

            HomeCommand = new RelayCommand(i => GoHome());
            FullscreenVideoCommand = new RelayCommand(i => NavigationService.NavigateToInternalPlayerPage());
            SettingsCommand = new RelayCommand(i => NavigationService.NavigateToSettingsPage());
            GoBackCommand = new RelayCommand(i => GoBack());
            RestartServerCommand = new RelayCommand(i => RestartServer());
            RestartApplicationCommand = new RelayCommand(i => RestartApplication());

            RefreshRestartServerNotification();
        }

        public void RefreshRestartApplicationNotification()
        {
            Dispatcher.InvokeAsync(() => ShowRestartApplicationNotification = AppHost.HasPendingRestart && SessionManager.CurrentUser != null && SessionManager.CurrentUser.Configuration.IsAdministrator);
        }

        public void RefreshRestartServerNotification()
        {
            Dispatcher.InvokeAsync(() => ShowRestartServerNotification = _serverHasPendingRestart && SessionManager.CurrentUser != null && SessionManager.CurrentUser.Configuration.IsAdministrator);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool dispose)
        {
            ServerEvents.Connected -= ServerEvents_Connected;
            ServerEvents.RestartRequired -= ServerEvents_RestartRequired;
            ServerEvents.ServerRestarting -= ServerEvents_ServerRestarting;
            ServerEvents.ServerShuttingDown -= ServerEvents_ServerShuttingDown;
            AppHost.HasPendingRestartChanged -= AppHostHasPendingRestartChanged;
        }

        /// <summary>
        /// Navigates to the home page
        /// </summary>
        protected virtual void GoHome()
        {
            if (SessionManager.CurrentUser != null)
            {
                NavigationService.NavigateToHomePage();
            }
            else
            {
                NavigationService.NavigateToLoginPage();
            }
        }

        /// <summary>
        /// Navigates to the previous page
        /// </summary>
        protected virtual async void GoBack()
        {
            await NavigationService.NavigateBack();
        }

        protected void FocusElement()
        {
            var win = PresentationManager.Window;

            win.Activate();
            win.Focus();

            win.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }

        /// <summary>
        /// Restarts the server.
        /// </summary>
        private async void RestartServer()
        {
            if (!_serverCanSelfRestart)
            {
                PresentationManager.ShowMessage(new MessageBoxInfo
                {
                    Button = MessageBoxButton.OK,
                    Caption = "Restart Media Browser Server",
                    Icon = MessageBoxIcon.Information,
                    Text = "Please logon to Media Browser Server and restart in order to finish applying updates."
                });

                return;
            }

            var result = PresentationManager.ShowMessage(new MessageBoxInfo
            {
                Button = MessageBoxButton.OKCancel,
                Caption = "Restart Media Browser Server",
                Icon = MessageBoxIcon.Information,
                Text = "Please restart Media Browser Server in order to finish applying updates."
            });

            if (result != MessageBoxResult.OK) return;
            try
            {
                var systemInfo = await ApiClient.GetSystemInfoAsync();

                if (systemInfo.HasPendingRestart)
                {
                    await ApiClient.RestartServerAsync();

                    WaitForServerToRestart();
                }

                _serverHasPendingRestart = false;
                RefreshRestartServerNotification();
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error restarting server.", ex);

                PresentationManager.ShowDefaultErrorMessage();
            }
        }

        /// <summary>
        /// Restarts the application.
        /// </summary>
        private async void RestartApplication()
        {
            var result = PresentationManager.ShowMessage(new MessageBoxInfo
            {
                Button = MessageBoxButton.OKCancel,
                Caption = "Restart Media Browser Theater",
                Icon = MessageBoxIcon.Information,
                Text = "Please restart to finish updating Media Browser Theater."
            });

            if (result != MessageBoxResult.OK) return;
            try
            {
                await AppHost.Restart();
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error restarting application.", ex);

                PresentationManager.ShowDefaultErrorMessage();
            }
        }

        private async void WaitForServerToRestart()
        {
            PresentationManager.ShowModalLoadingAnimation();

            try
            {
                await WaitForServerToRestartInternal();
            }
            finally
            {
                PresentationManager.HideModalLoadingAnimation();

                FocusElement();
            }
        }

        private async Task WaitForServerToRestartInternal()
        {
            var count = 0;

            while (count < 10)
            {
                await Task.Delay(3000);

                try
                {
                    await ApiClient.GetSystemInfoAsync();
                    break;
                }
                catch (Exception)
                {

                }

                count++;
            }
        }

        async void ServerEvents_Connected(object sender, EventArgs e)
        {
            try
            {
                var systemInfo = await ApiClient.GetSystemInfoAsync();
                _serverHasPendingRestart = systemInfo.HasPendingRestart;
                _serverCanSelfRestart = systemInfo.CanSelfRestart;
                RefreshRestartServerNotification();
            }
            catch (Exception)
            {

            }
        }

        void ServerEvents_ServerShuttingDown(object sender, EventArgs e)
        {
            _serverHasPendingRestart = false;
            RefreshRestartServerNotification();
        }

        void ServerEvents_ServerRestarting(object sender, EventArgs e)
        {
            _serverHasPendingRestart = false;
            RefreshRestartServerNotification();
        }

        void ServerEvents_RestartRequired(object sender, EventArgs e)
        {
            _serverHasPendingRestart = true;
            RefreshRestartServerNotification();
        }

        void AppHostHasPendingRestartChanged(object sender, EventArgs e)
        {
            RefreshRestartApplicationNotification();
        }
    }
}
