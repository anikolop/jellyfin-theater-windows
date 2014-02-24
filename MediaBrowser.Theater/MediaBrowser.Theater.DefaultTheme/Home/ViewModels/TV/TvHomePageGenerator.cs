﻿using System;
using System.Collections.Generic;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Api.Navigation;
using MediaBrowser.Theater.Api.Session;
using MediaBrowser.Theater.Api.UserInterface;
using MediaBrowser.Theater.Presentation.ViewModels;

namespace MediaBrowser.Theater.DefaultTheme.Home.ViewModels.TV
{
    public class TvHomePageGenerator
        : IHomePageGenerator
    {
        private readonly IImageManager _imageManager;
        private readonly INavigator _navigator;
        private readonly IApiClient _apiClient;
        private readonly ISessionManager _sessionManager;
        private readonly ILogManager _logManager;

        public TvHomePageGenerator(IImageManager imageManager, INavigator navigator, IApiClient apiClient, ISessionManager sessionManager, ILogManager logManager)
        {
            _imageManager = imageManager;
            _navigator = navigator;
            _apiClient = apiClient;
            _sessionManager = sessionManager;
            _logManager = logManager;
        }

        public IEnumerable<IPanoramaPage> GetHomePages()
        {
            yield return new TvSpotlightViewModel(_imageManager, _navigator, _apiClient, _sessionManager, _logManager);
            yield return new TvSpotlightViewModel(_imageManager, _navigator, _apiClient, _sessionManager, _logManager);
            yield return new TvSpotlightViewModel(_imageManager, _navigator, _apiClient, _sessionManager, _logManager);
            yield return new TvSpotlightViewModel(_imageManager, _navigator, _apiClient, _sessionManager, _logManager);
        }
    }
}