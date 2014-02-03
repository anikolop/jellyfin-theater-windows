﻿using System;
using System.Windows.Input;
using MediaBrowser.Common;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Plugins.DefaultTheme.ListPage;
using MediaBrowser.Plugins.DefaultTheme.UserProfileMenu;
using MediaBrowser.Theater.Interfaces;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.ViewModels;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Plugins.DefaultTheme.SystemOptionsMenu;

namespace MediaBrowser.Plugins.DefaultTheme
{
    public class DefaultThemePageMasterCommandsViewModel : MasterCommandsViewModel
    {
        protected readonly IImageManager ImageManager;

        public ICommand UserCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }
        public ICommand TestPowerCommand { get; private set; }

        private bool _displayPreferencesEnabled;
        public bool DisplayPreferencesEnabled
        {
            get { return _displayPreferencesEnabled; }

            set
            {
                var changed = _displayPreferencesEnabled != value;

                _displayPreferencesEnabled = value;
                if (changed)
                {
                    OnPropertyChanged("DisplayPreferencesEnabled");
                }
            }
        }

        private bool _sortEnabled;
        public bool SortEnabled
        {
            get { return _sortEnabled; }

            set
            {
                var changed = _sortEnabled != value;

                _sortEnabled = value;
                if (changed)
                {
                    OnPropertyChanged("SortEnabled");
                }
            }
        }

        private bool _powerOptionsEnabled;
        public bool PowerOptionsEnabled
        {
            get { return _powerOptionsEnabled; }

            set
            {
                var changed = _powerOptionsEnabled != value;

                _powerOptionsEnabled = value;
                if (changed)
                {
                    OnPropertyChanged("PowerOptionsEnabled");
                }
            }
        }

        public DefaultThemePageMasterCommandsViewModel(INavigationService navigationService, ISessionManager sessionManager, IPresentationManager presentationManager, IApiClient apiClient, ILogger logger, ITheaterApplicationHost appHost, IServerEvents serverEvents, IImageManager imageManager) 
            : base(navigationService, sessionManager, presentationManager, apiClient, logger, appHost, serverEvents)
        {
            ImageManager = imageManager;

            UserCommand = new RelayCommand(i => ShowUserMenu());
            LogoutCommand = new RelayCommand(i => Logout());
            TestPowerCommand = new RelayCommand(i=> ShowTestPower());

            PowerOptionsEnabled = true;
        }

        protected virtual void ShowUserMenu()
        {
            var page = NavigationService.CurrentPage as IHasDisplayPreferences;
            DisplayPreferences displayPreferences = null;
            ListPageConfig options = null;
            if (page != null)
            {
                displayPreferences = page.GetDisplayPreferences();
                options = page.GetListPageConfig();
            }

            new UserProfileWindow(this, SessionManager, PresentationManager, ImageManager, ApiClient, displayPreferences, options).ShowModal(PresentationManager.Window);
        }

        protected async void Logout()
        {
            if (SessionManager.CurrentUser == null)
            {
                throw new InvalidOperationException("The user is not logged in.");
            }

            await SessionManager.Logout();
        }

        private void ShowTestPower()
        {
            new SystemOptionsWindow().ShowModal(PresentationManager.Window);
        }

        protected override void Dispose(bool dispose)
        {
            if (dispose)
            {
                
            }

            base.Dispose(dispose);
        }
    }
}
