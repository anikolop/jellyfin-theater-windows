﻿using MediaBrowser.Common.Events;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.Theming;
using System;
using System.Threading.Tasks;

namespace MediaBrowser.Theater.Implementations.Session
{
    public class SessionManager : ISessionManager
    {
        public event EventHandler<EventArgs> UserLoggedIn;

        public event EventHandler<EventArgs> UserLoggedOut;

        private readonly INavigationService _navService;
        private readonly ILogger _logger;
        private readonly IThemeManager _themeManager;
        private readonly ITheaterConfigurationManager _config;
        private readonly IPlaybackManager _playback;
        private readonly IConnectionManager _connectionManager;

        public SessionManager(INavigationService navService, ILogger logger, IThemeManager themeManager, ITheaterConfigurationManager config, IPlaybackManager playback, IConnectionManager connectionManager)
        {
            _navService = navService;
            _logger = logger;
            _themeManager = themeManager;
            _config = config;
            _playback = playback;
            _connectionManager = connectionManager;

            _connectionManager.RemoteLoggedOut += _connectionManager_RemoteLoggedOut;
        }

        async void _connectionManager_RemoteLoggedOut(object sender, EventArgs e)
        {
            if (CurrentUser != null)
            {
                await Logout();
            }
        }

        public UserDto CurrentUser { get; private set; }

        public IApiClient ActiveApiClient
        {
            get
            {
                return _connectionManager.GetApiClient(new BaseItemDto());
            }
        }

        public async Task Logout()
        {
            _playback.StopAllPlayback();

            await _connectionManager.Logout();

            var previous = CurrentUser;

            CurrentUser = null;

            if (previous != null)
            {
                EventHelper.FireEventIfNotNull(UserLoggedOut, this, EventArgs.Empty, _logger);
            }

            await _navService.NavigateToLoginPage();

            _navService.ClearHistory();
        }

        public async Task LoginToServer(string username, string password, bool rememberCredentials)
        {
            var apiClient = ActiveApiClient;

            //Check just in case
            if (password == null)
            {
                password = string.Empty;
            }

            try
            {
                var result = await apiClient.AuthenticateUserAsync(username, password);

                CurrentUser = result.User;

                _config.Configuration.RememberLogin = rememberCredentials;
                _config.SaveConfiguration();
            }
            catch (HttpException ex)
            {
                throw new UnauthorizedAccessException("Invalid username or password. Please try again.");
            }

            await AfterLogin();
        }

        public async Task ValidateSavedLogin(ConnectionResult result)
        {
            CurrentUser = await result.ApiClient.GetUserAsync(result.ApiClient.CurrentUserId);
            
            await AfterLogin();
        }

        private async Task AfterLogin()
        {
            EventHelper.FireEventIfNotNull(UserLoggedIn, this, EventArgs.Empty, _logger);

            await _navService.NavigateToHomePage();

            _navService.ClearHistory();
        }
    }
}
