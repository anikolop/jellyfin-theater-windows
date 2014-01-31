﻿using System.Windows;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Plugins.DefaultTheme.ListPage;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.ViewModels;
using MediaBrowser.Theater.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MediaBrowser.Plugins.DefaultTheme.Home
{
    public class TvViewModel : BaseHomePageSectionViewModel, IDisposable, IHasActivePresentation
    {
        private readonly ISessionManager _sessionManager;
        private readonly IPlaybackManager _playbackManager;
        private readonly IImageManager _imageManager;
        private readonly INavigationService _navService;
        private readonly ILogger _logger;
        private readonly IServerEvents _serverEvents;

        public ItemListViewModel LatestEpisodesViewModel { get; private set; }
        public ItemListViewModel NextUpViewModel { get; private set; }
        public ItemListViewModel ResumeViewModel { get; private set; }
        public ItemListViewModel MiniSpotlightsViewModel { get; private set; }

        public GalleryViewModel AllShowsViewModel { get; private set; }
        public GalleryViewModel GenresViewModel { get; private set; }
        public GalleryViewModel UpcomingViewModel { get; private set; }

        public ImageViewerViewModel SpotlightViewModel { get; private set; }

        private TvView _tvView;

        public TvViewModel(IPresentationManager presentation, IImageManager imageManager, IApiClient apiClient, ISessionManager session, INavigationService nav, IPlaybackManager playback, ILogger logger, double tileWidth, double tileHeight, IServerEvents serverEvents)
            : base(presentation, apiClient)
        {
            _sessionManager = session;
            _playbackManager = playback;
            _imageManager = imageManager;
            _navService = nav;
            _logger = logger;
            _serverEvents = serverEvents;

            TileWidth = tileWidth;
            TileHeight = tileHeight;

            var spotlightTileHeight = TileHeight * 2 + TileMargin * 2;
            var spotlightTileWidth = 16 * (spotlightTileHeight / 9) + 100;

            var lowerSpotlightWidth = spotlightTileWidth / 3 - (TileMargin * 1.5);

            SpotlightViewModel = new ImageViewerViewModel(_imageManager, new List<ImageViewerImage>())
            {
                Height = spotlightTileHeight,
                Width = spotlightTileWidth,
                CustomCommandAction = i => _navService.NavigateToItem(i.Item, ViewType.Tv),
                ImageStretch = Stretch.UniformToFill
            };

            GenresViewModel = new GalleryViewModel(ApiClient, _imageManager, _navService)
            {
                GalleryHeight = TileHeight,
                GalleryWidth = lowerSpotlightWidth,
                CustomCommandAction = () => NavigateWithLoading(NavigateToGenresInternal)
            };

            AllShowsViewModel = new GalleryViewModel(ApiClient, _imageManager, _navService)
            {
                GalleryHeight = TileHeight,
                GalleryWidth = lowerSpotlightWidth,
                CustomCommandAction = () => NavigateWithLoading(() => NavigateToAllShowsInternal("AllShows"))
            };

            UpcomingViewModel = new GalleryViewModel(ApiClient, _imageManager, _navService)
            {
                GalleryHeight = TileHeight,
                GalleryWidth = lowerSpotlightWidth,
                CustomCommandAction = () => NavigateWithLoading(() => NavigateToAllShowsInternal("Upcoming"))
            };

            LoadViewModels();
        }

        private CancellationTokenSource _mainViewCancellationTokenSource;
        private void DisposeMainViewCancellationTokenSource(bool cancel)
        {
            if (_mainViewCancellationTokenSource != null)
            {
                if (cancel)
                {
                    try
                    {
                        _mainViewCancellationTokenSource.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {

                    }
                }
                _mainViewCancellationTokenSource.Dispose();
                _mainViewCancellationTokenSource = null;
            }
        }

        public const int PosterWidth = 214;
        public const int ThumbstripWidth = 500;
        public const int ListImageWidth = 160;
        public const int PosterStripWidth = 290;

        public static void SetDefaults(ListPageConfig config)
        {
            config.DefaultViewType = ListViewTypes.PosterStrip;
            config.PosterImageWidth = PosterWidth;
            config.ThumbImageWidth = ThumbstripWidth;
            config.ListImageWidth = ListImageWidth;
            config.PosterStripImageWidth = PosterStripWidth;
        }

        private async void LoadViewModels()
        {
            var cancellationSource = _mainViewCancellationTokenSource = new CancellationTokenSource();

            try
            {
                var view = await ApiClient.GetTvView(_sessionManager.CurrentUser.Id, cancellationSource.Token);

                _tvView = view;

                cancellationSource.Token.ThrowIfCancellationRequested();

                LoadSpotlightViewModel(view);
                LoadAllShowsViewModel(view);
                LoadMiniSpotlightsViewModel(view);
                LoadNextUpViewModel(view);
                LoadLatestEpisodesViewModel(view);
                LoadResumableViewModel(view);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting tv view", ex);
                PresentationManager.ShowDefaultErrorMessage();
            }
            finally
            {
                DisposeMainViewCancellationTokenSource(false);
            }
        }

        private void LoadResumableViewModel(TvView view)
        {
            ResumeViewModel = new ItemListViewModel(GetResumeablesAsync, PresentationManager, _imageManager, ApiClient, _navService, _playbackManager, _logger, _serverEvents)
            {
                ImageDisplayWidth = TileWidth,
                ImageDisplayHeightGenerator = v => TileHeight,
                DisplayNameGenerator = i => i.SeriesName,
                EnableBackdropsForCurrentItem = false,
                EnableServerImageEnhancers = false,

                OnItemCreated = vm =>
                {
                    vm.DisplayNameVisibility = Visibility.Visible;
                }
            };

            OnPropertyChanged("ResumeViewModel");
        }

        private void LoadLatestEpisodesViewModel(TvView view)
        {
            LatestEpisodesViewModel = new ItemListViewModel(GetLatestEpisodes, PresentationManager, _imageManager, ApiClient, _navService, _playbackManager, _logger, _serverEvents)
            {
                ImageDisplayWidth = TileWidth,
                ImageDisplayHeightGenerator = v => TileHeight,
                DisplayNameGenerator = HomePageViewModel.GetDisplayName,
                EnableBackdropsForCurrentItem = false,
                EnableServerImageEnhancers = false,

                OnItemCreated = vm =>
                {
                    vm.DisplayNameVisibility = Visibility.Visible;
                }
            };

            OnPropertyChanged("LatestEpisodesViewModel");
        }

        private void LoadNextUpViewModel(TvView view)
        {
            NextUpViewModel = new ItemListViewModel(GetNextUpAsync, PresentationManager, _imageManager, ApiClient, _navService, _playbackManager, _logger, _serverEvents)
            {
                ImageDisplayWidth = TileWidth,
                ImageDisplayHeightGenerator = v => TileHeight,
                DisplayNameGenerator = i => i.SeriesName,
                EnableBackdropsForCurrentItem = false,
                EnableServerImageEnhancers = false,

                OnItemCreated = vm =>
                {
                    vm.DisplayNameVisibility = Visibility.Visible;
                }
            };

            OnPropertyChanged("NextUpViewModel");
        }

        private void LoadMiniSpotlightsViewModel(TvView view)
        {
            Func<ItemListViewModel, Task<ItemsResult>> getItems = vm =>
            {
                var items = view.MiniSpotlights.Take(3).ToArray();

                return Task.FromResult(new ItemsResult
                {
                    TotalRecordCount = items.Length,
                    Items = items
                });
            };

            MiniSpotlightsViewModel = new ItemListViewModel(getItems, PresentationManager, _imageManager, ApiClient, _navService, _playbackManager, _logger, _serverEvents)
            {
                ImageDisplayWidth = TileWidth + (TileMargin / 4) - 1,
                ImageDisplayHeightGenerator = v => TileHeight,
                DisplayNameGenerator = HomePageViewModel.GetDisplayName,
                EnableBackdropsForCurrentItem = false,
                ImageStretch = Stretch.UniformToFill,
                PreferredImageTypesGenerator = vm => new[] { ImageType.Backdrop },
                DownloadImageAtExactSize = true,

                OnItemCreated = vm =>
                {
                    vm.DisplayNameVisibility = Visibility.Visible;
                }
            };

            OnPropertyChanged("MiniSpotlightsViewModel");
        }

        private Visibility _latestEpisodesVisibility = Visibility.Collapsed;
        public Visibility LatestEpisodesVisibility
        {
            get { return _latestEpisodesVisibility; }

            set
            {
                var changed = _latestEpisodesVisibility != value;

                _latestEpisodesVisibility = value;

                if (changed)
                {
                    OnPropertyChanged("LatestEpisodesVisibility");
                }
            }
        }

        private Visibility _nextUpVisibility = Visibility.Collapsed;
        public Visibility NextUpVisibility
        {
            get { return _nextUpVisibility; }

            set
            {
                var changed = _nextUpVisibility != value;

                _nextUpVisibility = value;

                if (changed)
                {
                    OnPropertyChanged("NextUpVisibility");
                }
            }
        }

        private Visibility _resumeVisibility = Visibility.Collapsed;
        public Visibility ResumeVisibility
        {
            get { return _resumeVisibility; }

            set
            {
                var changed = _resumeVisibility != value;

                _resumeVisibility = value;

                if (changed)
                {
                    OnPropertyChanged("ResumeVisibility");
                }
            }
        }

        private void LoadSpotlightViewModel(TvView view)
        {
            const ImageType imageType = ImageType.Backdrop;

            var tileWidth = TileWidth * 2 + TileMargin;
            var tileHeight = tileWidth * 9 / 16;

            BackdropItems = view.BackdropItems.ToArray();

            var images = view.SpotlightItems.Select(i => new ImageViewerImage
            {
                Url = ApiClient.GetImageUrl(i, new ImageOptions
                {
                    Height = Convert.ToInt32(tileHeight),
                    Width = Convert.ToInt32(tileWidth),
                    ImageType = imageType

                }),

                Caption = i.Name,
                Item = i

            }).ToList();

            SpotlightViewModel.Images.AddRange(images);
            SpotlightViewModel.StartRotating(10000);
        }

        public void EnableActivePresentation()
        {
            SpotlightViewModel.StartRotating(10000);
        }
        public void DisableActivePresentation()
        {
            SpotlightViewModel.StopRotating();
        }

        private async Task NavigateToGenresInternal()
        {
            var item = await GetRootFolder();

            var displayPreferences = await PresentationManager.GetDisplayPreferences("TVGenres", CancellationToken.None);

            var genres = await ApiClient.GetGenresAsync(new ItemsByNameQuery
            {
                IncludeItemTypes = new[] { "Series" },
                SortBy = new[] { ItemSortBy.SortName },
                Recursive = true,
                UserId = _sessionManager.CurrentUser.Id
            });

            var indexOptions = genres.Items.Select(i => new TabItem
            {
                Name = i.Name,
                DisplayName = i.Name
            });

            var options = new ListPageConfig
            {
                IndexOptions = indexOptions.ToList(),
                PageTitle = "TV",
                CustomItemQuery = GetSeriesByGenre
            };

            SetDefaults(options);

            var page = new FolderPage(item, displayPreferences, ApiClient, _imageManager, PresentationManager, _navService, _playbackManager, _logger, _serverEvents, options)
            {
                ViewType = ViewType.Tv
            };

            await _navService.Navigate(page);
        }

        private void LoadAllShowsViewModel(TvView view)
        {
            var images = view.ShowsItems.Take(1).Select(i => ApiClient.GetImageUrl(i.Id, new ImageOptions
            {
                ImageType = i.ImageType,
                Tag = i.ImageTag,
                Height = Convert.ToInt32(TileWidth * 2),
                EnableImageEnhancers = false
            }));

            AllShowsViewModel.AddImages(images);
        }

        public Task NavigateToAllShows()
        {
            return NavigateToAllShowsInternal("AllShows");
        }

        private async Task NavigateToAllShowsInternal(string indexValue)
        {
            var item = await GetRootFolder();

            var displayPreferences = await PresentationManager.GetDisplayPreferences("Shows", CancellationToken.None);

            var tvView = _tvView ?? await ApiClient.GetTvView(_sessionManager.CurrentUser.Id, CancellationToken.None);

            var tabs = new List<TabItem>();

            tabs.Add(new TabItem
            {
                DisplayName = "All Shows",
                Name = "AllShows"
            });

            if (tvView.SeriesIdsInProgress.Count > 0)
            {
                tabs.Add(new TabItem
                {
                    DisplayName = "In Progress",
                    Name = "ShowsInProgress",
                    TabType = string.Join(",", tvView.SeriesIdsInProgress.ToArray())
                });
            }

            //tabs.Add(new TabItem
            //{
            //    DisplayName = "Latest",
            //    Name = "Latest"
            //});

            //tabs.Add(new TabItem
            //{
            //    DisplayName = "Next Up",
            //    Name = "NextUp"
            //});

            tabs.Add(new TabItem
            {
                DisplayName = "Favorites",
                Name = "FavoriteShows"
            });

            tabs.Add(new TabItem
            {
                DisplayName = "Top Rated",
                Name = "TopRated",
            });

            tabs.Add(new TabItem
            {
                DisplayName = "Upcoming",
                Name = "Upcoming",
            });

            var options = new ListPageConfig
            {
                PageTitle = " ",
                CustomItemQuery = GetAllShows,
                SortOptions = GetSeriesSortOptions(),
                IndexOptions = tabs,
                IndexValue = indexValue
            };

            SetDefaults(options);

            var page = new FolderPage(item, displayPreferences, ApiClient, _imageManager, PresentationManager, _navService, _playbackManager, _logger, _serverEvents, options)
            {
                ViewType = ViewType.Tv
            };

            await _navService.Navigate(page);
        }

        private Task<ItemsResult> GetAllShows(ItemListViewModel viewModel, DisplayPreferences displayPreferences)
        {
            var query = new ItemQuery
            {
                Fields = FolderPage.QueryFields,

                UserId = _sessionManager.CurrentUser.Id,

                IncludeItemTypes = new[] { "Series" },

                SortBy = !String.IsNullOrEmpty(displayPreferences.SortBy)
                             ? new[] { displayPreferences.SortBy }
                             : new[] { ItemSortBy.SortName },

                SortOrder = displayPreferences.SortOrder,

                Recursive = true
            };

            var indexOption = viewModel.CurrentIndexOption == null ? string.Empty : viewModel.CurrentIndexOption.Name;

            if (string.Equals(indexOption, "TopRated"))
            {
                query.MinCommunityRating = ApiClientExtensions.TopTvCommunityRating;

                query.SortBy = new[] { ItemSortBy.SortName };
                query.SortOrder = SortOrder.Ascending;
            }
            else if (string.Equals(indexOption, "FavoriteShows"))
            {
                query.Filters = new[] { ItemFilter.IsFavorite };

                query.SortBy = new[] { ItemSortBy.SortName };
                query.SortOrder = SortOrder.Ascending;
            }
            else if (string.Equals(indexOption, "ShowsInProgress"))
            {
                query.Ids = viewModel.CurrentIndexOption.TabType.Split(',');

                query.SortBy = new[] { ItemSortBy.SortName };
                query.SortOrder = SortOrder.Ascending;
            }
            else if (indexOption.StartsWith("Genre:"))
            {
                query.Genres = new[] { indexOption.Split(':').Last() };

                query.SortBy = new[] { ItemSortBy.SortName };
                query.SortOrder = SortOrder.Ascending;
            }
            else if (indexOption.StartsWith("Studio:"))
            {
                query.Studios = new[] { indexOption.Split(':').Last() };

                query.SortBy = new[] { ItemSortBy.SortName };
                query.SortOrder = SortOrder.Ascending;
            }

            return ApiClient.GetItemsAsync(query);
        }

        internal static Dictionary<string, string> GetSeriesSortOptions()
        {
            var sortOptions = new Dictionary<string, string>();
            sortOptions["Name"] = ItemSortBy.SortName;

            sortOptions["Date Added"] = ItemSortBy.DateCreated;
            sortOptions["IMDb Rating"] = ItemSortBy.CommunityRating;
            sortOptions["Parental Rating"] = ItemSortBy.OfficialRating;
            sortOptions["Premiere Date"] = ItemSortBy.PremiereDate;
            sortOptions["Runtime"] = ItemSortBy.Runtime;

            return sortOptions;
        }

        private Task<ItemsResult> GetSeriesByGenre(ItemListViewModel viewModel, DisplayPreferences displayPreferences)
        {
            var query = new ItemQuery
            {
                Fields = FolderPage.QueryFields,

                UserId = _sessionManager.CurrentUser.Id,

                IncludeItemTypes = new[] { "Series" },

                SortBy = !String.IsNullOrEmpty(displayPreferences.SortBy)
                             ? new[] { displayPreferences.SortBy }
                             : new[] { ItemSortBy.SortName },

                SortOrder = displayPreferences.SortOrder,

                Recursive = true
            };

            var indexOption = viewModel.CurrentIndexOption;

            if (indexOption != null)
            {
                query.Genres = new[] { indexOption.Name };
            }

            return ApiClient.GetItemsAsync(query);
        }

        private Task<ItemsResult> GetNextUpAsync(ItemListViewModel viewModel)
        {
            var result = new ItemsResult
            {
                Items = _tvView.NextUpEpisodes.ToArray(),
                TotalRecordCount = _tvView.NextUpEpisodes.Count
            };

            NextUpVisibility = result.TotalRecordCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            return Task.FromResult(result);
        }

        private Task<ItemsResult> GetLatestEpisodes(ItemListViewModel viewModel)
        {
            var result = new ItemsResult
            {
                Items = _tvView.LatestEpisodes.ToArray(),
                TotalRecordCount = _tvView.LatestEpisodes.Count
            };

            LatestEpisodesVisibility = result.TotalRecordCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            return Task.FromResult(result);
        }

        private Task<ItemsResult> GetResumeablesAsync(ItemListViewModel viewModel)
        {
            var result = new ItemsResult
            {
                Items = _tvView.ResumableEpisodes.ToArray(),
                TotalRecordCount = _tvView.ResumableEpisodes.Count
            };

            ResumeVisibility = result.TotalRecordCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            return Task.FromResult(result);
        }

        public void Dispose()
        {
            if (LatestEpisodesViewModel != null)
            {
                LatestEpisodesViewModel.Dispose();
            }
            if (NextUpViewModel != null)
            {
                NextUpViewModel.Dispose();
            }
            if (ResumeViewModel != null)
            {
                ResumeViewModel.Dispose();
            }
            if (MiniSpotlightsViewModel != null)
            {
                MiniSpotlightsViewModel.Dispose();
            }
            if (AllShowsViewModel != null)
            {
                AllShowsViewModel.Dispose();
            }
            if (GenresViewModel != null)
            {
                GenresViewModel.Dispose();
            }
            if (UpcomingViewModel != null)
            {
                UpcomingViewModel.Dispose();
            }
            if (SpotlightViewModel != null)
            {
                SpotlightViewModel.Dispose();
            }
            DisposeMainViewCancellationTokenSource(true);
        }
    }
}
