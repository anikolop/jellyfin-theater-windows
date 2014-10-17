﻿using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Plugins.DefaultTheme.Details;
using MediaBrowser.Plugins.DefaultTheme.Home;
using MediaBrowser.Plugins.DefaultTheme.ListPage;
using MediaBrowser.Plugins.DefaultTheme.Search;
using MediaBrowser.Theater.Interfaces;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.Theming;
using MediaBrowser.Theater.Interfaces.ViewModels;
using MediaBrowser.Theater.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MediaBrowser.Plugins.DefaultTheme
{
    /// <summary>
    /// Class Theme
    /// </summary>
    public class DefaultTheme : ITheme
    {
        /// <summary>
        /// The _playback manager
        /// </summary>
        private readonly IPlaybackManager _playbackManager;
        /// <summary>
        /// The _image manager
        /// </summary>
        private readonly IImageManager _imageManager;
        /// <summary>
        /// The _nav service
        /// </summary>
        private readonly INavigationService _navService;
        /// <summary>
        /// The _session manager
        /// </summary>
        private readonly ISessionManager _sessionManager;
        /// <summary>
        /// The _app window
        /// </summary>
        private readonly IPresentationManager _presentationManager;
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;
        private readonly ITheaterApplicationHost _appHost;
        private readonly ITheaterConfigurationManager _config;
        private readonly IConnectionManager _connectionManager;

        public static DefaultTheme Current;

        public DefaultTheme(IPlaybackManager playbackManager, IImageManager imageManager, INavigationService navService, ISessionManager sessionManager, IPresentationManager presentationManager, ILogManager logManager, ITheaterApplicationHost appHost, ITheaterConfigurationManager config, IConnectionManager connectionManager)
        {
            Current = this;

            _playbackManager = playbackManager;
            _imageManager = imageManager;
            _navService = navService;
            _sessionManager = sessionManager;
            _presentationManager = presentationManager;
            _appHost = appHost;
            _config = config;
            _connectionManager = connectionManager;
            _logger = logManager.GetLogger(GetType().Name);
        }

        /// <summary>
        /// Gets the global resources.
        /// </summary>
        /// <returns>IEnumerable{ResourceDictionary}.</returns>
        public IEnumerable<ResourceDictionary> GetResources()
        {
            var namespaceName = GetType().Namespace;

            return new[] { "Merged" }.Select(i => new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/" + namespaceName + ";component/Resources/" + i + ".xaml", UriKind.Absolute)

            });
        }

        public Page GetSearchPage(BaseItemDto item)
        {
            var apiClient = _connectionManager.GetApiClient(item);

            return new SearchPage(item, apiClient, _sessionManager, _imageManager, _presentationManager, _navService, _playbackManager, _logger);
        }

        /// <summary>
        /// Gets the item page.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="context">The context.</param>
        /// <returns>Page.</returns>
        public Page GetItemPage(BaseItemDto item, ViewType context)
        {
            var apiClient = _connectionManager.GetApiClient(item);

            var itemViewModel = new ItemViewModel(apiClient, _imageManager, _playbackManager, _presentationManager, _logger)
            {
                Item = item,
                ImageWidth = 550,
                PreferredImageTypes = new[] { ImageType.Primary, ImageType.Thumb }
            };

            return new DetailPage(itemViewModel, _presentationManager)
            {
                DataContext = new DetailPageViewModel(itemViewModel, apiClient, _sessionManager, _imageManager, _presentationManager, _playbackManager, _navService, _logger, context)
            };
        }

        private readonly string[] _folderTypesWithDetailPages = new[] { "series", "musicalbum", "musicartist" };

        /// <summary>
        /// Gets the folder page.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="context">The context.</param>
        /// <param name="displayPreferences">The display preferences.</param>
        /// <returns>Page.</returns>
        public Page GetFolderPage(BaseItemDto item, ViewType context, DisplayPreferences displayPreferences)
        {
            var apiClient = _connectionManager.GetApiClient(item);

            if (item.IsType("channel") || item.IsType("channelfolderitem"))
            {
                var options = GetChannelPageConfig(item, context);

                return new FolderPage(item, displayPreferences, apiClient, _imageManager, _presentationManager, _navService, _playbackManager, _logger, options);
            }

            if (context == ViewType.Folders || !_folderTypesWithDetailPages.Contains(item.Type, StringComparer.OrdinalIgnoreCase))
            {
                var options = GetListPageConfig(item, context);

                return new FolderPage(item, displayPreferences, apiClient, _imageManager, _presentationManager,
                    _navService, _playbackManager, _logger, options);
            }

            return GetItemPage(item, context);
        }

        private ListPageConfig GetChannelPageConfig(BaseItemDto item, ViewType context)
        {
            var apiClient = _connectionManager.GetApiClient(item);

            var config = new ListPageConfig();

            config.CustomItemQuery = (vm, displayPreferences) =>
            {
                return GetChannelItems(apiClient, new ChannelItemQuery
                {
                    UserId = _sessionManager.LocalUserId,
                    ChannelId = item.IsType("channel") ? item.Id : item.ChannelId,
                    FolderId = item.IsType("channel") ? null : item.Id,
                    Fields = FolderPage.QueryFields

                }, CancellationToken.None);
            };

            return config;
        }

        private async Task<ItemsResult> GetChannelItems(IApiClient apiClient, ChannelItemQuery query, CancellationToken cancellationToken)
        {
            var startIndex = 0;
            var callLimit = 3;
            var currentCall = 1;

            var result = await GetChannelItems(apiClient, query, startIndex, null, CancellationToken.None);

            var queryLimit = result.Items.Length;
            
            while (result.Items.Length < result.TotalRecordCount && currentCall <= callLimit)
            {
                startIndex += queryLimit;

                var innerResult = await GetChannelItems(apiClient, query, startIndex, queryLimit, CancellationToken.None);

                var list = result.Items.ToList();
                list.AddRange(innerResult.Items);
                result.Items = list.ToArray();

                currentCall++;
            }

            return new ItemsResult
            {
                TotalRecordCount = result.TotalRecordCount,
                Items = result.Items
            };
        }

        private async Task<ItemsResult> GetChannelItems(IApiClient apiClient, ChannelItemQuery query, int start, int? limit, CancellationToken cancellationToken)
        {
            query.StartIndex = start;
            query.Limit = limit;

            var result = await apiClient.GetChannelItems(query, CancellationToken.None);

            return new ItemsResult
            {
                TotalRecordCount = result.TotalRecordCount,
                Items = result.Items
            };
        }

        private ListPageConfig GetListPageConfig(BaseItemDto item, ViewType context)
        {
            var apiClient = _connectionManager.GetApiClient(item);

            var config = new ListPageConfig();

            if (item.IsType("playlist"))
            {
                config.DefaultViewType = ListViewTypes.List;
            }
            if (context == ViewType.Tv || item.IsType("season"))
            {
                TvViewModel.SetDefaults(config);

                if (item.IsType("season"))
                {
                    config.DefaultViewType = ListViewTypes.List;

                    config.PosterImageWidth = 480;
                    config.PosterStripImageWidth = 592;
                    config.ThumbImageWidth = 592;
                }
            }
            else if (context == ViewType.Movies)
            {
                MoviesViewModel.SetDefaults(config);
            }
            else if (context == ViewType.Games)
            {
                GamesViewModel.SetDefaults(config, item.GameSystem);
            }

            if (item.IsFolder)
            {
                config.CustomItemQuery = (vm, displayPreferences) =>
                {

                    if (item.IsType("series"))
                    {
                        return apiClient.GetSeasonsAsync(new SeasonQuery
                        {
                            UserId = _sessionManager.LocalUserId,
                            SeriesId = item.Id,
                            Fields = FolderPage.QueryFields
                        }, CancellationToken.None);
                    }


                    if (item.IsType("season"))
                    {
                        return apiClient.GetEpisodesAsync(new EpisodeQuery
                        {
                            UserId = _sessionManager.LocalUserId,
                            SeriesId = item.SeriesId,
                            SeasonId = item.Id,
                            Fields = FolderPage.QueryFields
                        }, CancellationToken.None);
                    }

                    var query = new ItemQuery
                    {
                        UserId = _sessionManager.LocalUserId,

                        ParentId = item.Id,

                        Fields = FolderPage.QueryFields
                    };

                    // Server will sort boxset titles based on server settings
                    if (!item.IsType("boxset"))
                    {
                        query.SortBy = new[] { ItemSortBy.SortName };
                        query.SortOrder = displayPreferences.SortOrder;
                    }

                    return apiClient.GetItemsAsync(query, CancellationToken.None);
                };

                if (item.IsType("season") && item.IndexNumber.HasValue && item.IndexNumber.Value > 0)
                {
                    config.DisplayNameGenerator = FolderPage.GetDisplayNameWithAiredSpecial;
                }
            }

            return config;
        }

        /// <summary>
        /// Gets the person page.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="context">The context.</param>
        /// <param name="mediaItemId">The media item id.</param>
        /// <returns>Page.</returns>
        public Page GetPersonPage(BaseItemDto item, ViewType context, string mediaItemId = null)
        {
            var page = (DetailPage)GetItemPage(item, context);

            page.ItemByNameMediaItemId = mediaItemId;

            return page;
        }

        public virtual string Name
        {
            get { return "Default"; }
        }

        public void Load()
        {
        }

        public void Unload()
        {
        }

        public string DefaultHomePageName
        {
            get { return "Default"; }
        }

        internal DefaultThemePageContentViewModel PageContentDataContext { get; private set; }

        public PageContentViewModel CreatePageContentDataContext()
        {
            PageContentDataContext = new DefaultThemePageContentViewModel(_navService, _sessionManager, _connectionManager, _imageManager, _presentationManager, _playbackManager, _logger, _appHost, _config);

            return PageContentDataContext;
        }

        public void CallBackModal()
        {
            PageContentDataContext.CallBackModal();
        }
    }
}
