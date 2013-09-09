﻿using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Plugins.DefaultTheme.Controls;
using MediaBrowser.Plugins.DefaultTheme.ListPage;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.ViewModels;
using MediaBrowser.Theater.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.DefaultTheme.Home
{
    public class TvViewModel : BaseHomePageSectionViewModel, IDisposable
    {
        private readonly ISessionManager _sessionManager;
        private readonly IPlaybackManager _playbackManager;
        private readonly IImageManager _imageManager;
        private readonly INavigationService _navService;
        private readonly ILogger _logger;

        public ItemListViewModel NextUpViewModel { get; private set; }
        public ItemListViewModel ResumeViewModel { get; private set; }

        public GalleryViewModel AllShowsViewModel { get; private set; }
        public GalleryViewModel ActorsViewModel { get; private set; }

        public ImageViewerViewModel SpotlightViewModel { get; private set; }

        public TvViewModel(IPresentationManager presentation, IImageManager imageManager, IApiClient apiClient, ISessionManager session, INavigationService nav, IPlaybackManager playback, ILogger logger, double tileWidth, double tileHeight)
            : base(presentation, apiClient)
        {
            _sessionManager = session;
            _playbackManager = playback;
            _imageManager = imageManager;
            _navService = nav;
            _logger = logger;

            TileWidth = tileWidth;
            TileHeight = tileHeight;

            NextUpViewModel = new ItemListViewModel(GetNextUpAsync, presentation, imageManager, apiClient, session, nav, playback, logger)
            {
                ImageDisplayWidth = TileWidth,
                ImageDisplayHeightGenerator = v => TileHeight,
                DisplayNameGenerator = MultiItemTile.GetDisplayName,
                EnableBackdropsForCurrentItem = false
            };
            NextUpViewModel.PropertyChanged += NextUpViewModel_PropertyChanged;

            ResumeViewModel = new ItemListViewModel(GetResumeablesAsync, presentation, imageManager, apiClient, session, nav, playback, logger)
            {
                ImageDisplayWidth = TileWidth,
                ImageDisplayHeightGenerator = v => TileHeight,
                DisplayNameGenerator = MultiItemTile.GetDisplayName,
                EnableBackdropsForCurrentItem = false
            };
            ResumeViewModel.PropertyChanged += ResumeViewModel_PropertyChanged;

            LoadSpotlightViewModel();
            LoadAllShowsViewModel();
            LoadActorsViewModel();
        }

        void ResumeViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ShowResume = ResumeViewModel.ItemCount > 0;
        }

        void NextUpViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ShowNextUp = NextUpViewModel.ItemCount > 0;
        }

        private bool _showNextUp;
        public bool ShowNextUp
        {
            get { return _showNextUp; }

            set
            {
                var changed = _showNextUp != value;

                _showNextUp = value;

                if (changed)
                {
                    OnPropertyChanged("ShowNextUp");
                }
            }
        }

        private bool _showResume;
        public bool ShowResume
        {
            get { return _showResume; }

            set
            {
                var changed = _showResume != value;

                _showResume = value;

                if (changed)
                {
                    OnPropertyChanged("ShowResume");
                }
            }
        }

        private async void LoadSpotlightViewModel()
        {
            const ImageType imageType = ImageType.Backdrop;

            var tileWidth = TileWidth * 2 + TilePadding;
            var tileHeight = tileWidth * 9 / 16;

            SpotlightViewModel = new ImageViewerViewModel(_imageManager, new List<ImageViewerImage>())
            {
                Height = tileHeight,
                Width = tileWidth
            };

            var itemsResult = await ApiClient.GetItemsAsync(new ItemQuery
            {
                UserId = _sessionManager.CurrentUser.Id,

                SortBy = new[] { ItemSortBy.Random },

                IncludeItemTypes = new[] { "Series" },

                ImageTypes = new[] { imageType },

                Limit = 30,

                Recursive = true
            });

            BackdropItems = itemsResult.Items;

            var images = itemsResult.Items.OrderBy(i => Guid.NewGuid()).Select(i => new ImageViewerImage
            {
                Url = ApiClient.GetImageUrl(i, new ImageOptions
                {
                    Height = Convert.ToInt32(tileHeight),
                    ImageType = imageType

                }),

                Caption = i.Name,
                Item = i

            }).ToList();

            SpotlightViewModel.CustomCommandAction = i => _navService.NavigateToItem(i.Item, ViewType.Tv);

            SpotlightViewModel.Images.AddRange(images);
            SpotlightViewModel.StartRotating(8000);
        }

        private async void LoadActorsViewModel()
        {
            ActorsViewModel = new GalleryViewModel(ApiClient, _imageManager, _navService)
            {
                GalleryHeight = TileHeight,
                GalleryWidth = TileWidth
            };

            ActorsViewModel.CustomCommandAction = NavigateToActors;

            var actorsResult = await ApiClient.GetPeopleAsync(new PersonsQuery
            {
                IncludeItemTypes = new[] { "Series" },
                SortBy = new[] { ItemSortBy.Random },
                Recursive = true,
                Limit = 3,
                PersonTypes = new[] { PersonType.Actor },
                ImageTypes = new[] { ImageType.Primary }
            });

            var images = actorsResult.Items.Select(i => ApiClient.GetImageUrl(i, new ImageOptions
            {
                Height = Convert.ToInt32(TileHeight)

            }));

            ActorsViewModel.AddImages(images);
        }

        private async void NavigateToActors()
        {
            PresentationManager.ShowLoadingAnimation();

            try
            {
                await NavigateToActorsInternal();
            }
            finally
            {
                PresentationManager.HideLoadingAnimation();
            }
        }
        
        private async Task NavigateToActorsInternal()
        {
            var item = await ApiClient.GetRootFolderAsync(_sessionManager.CurrentUser.Id);

            var displayPreferences = await PresentationManager.GetDisplayPreferences("TVActors", CancellationToken.None);

            var page = new FolderPage(item, displayPreferences, ApiClient, _imageManager, _sessionManager,
                                      PresentationManager, _navService, _playbackManager, _logger);

            page.CustomPageTitle = "TV | Actors";

            page.ViewType = ViewType.Tv;
            page.CustomItemQuery = GetAllActors;

            await _navService.Navigate(page);
        }

        private async void LoadAllShowsViewModel()
        {
            AllShowsViewModel = new GalleryViewModel(ApiClient, _imageManager, _navService)
            {
                GalleryHeight = TileHeight,
                GalleryWidth = TileWidth
            };

            AllShowsViewModel.CustomCommandAction = NavigateToAllShows;

            const ImageType imageType = ImageType.Primary;

            var allSeriesResult = await ApiClient.GetItemsAsync(new ItemQuery
            {
                UserId = _sessionManager.CurrentUser.Id,

                SortBy = new[] { ItemSortBy.Random },

                IncludeItemTypes = new[] { "Series" },

                ImageTypes = new[] { imageType },

                Limit = 3,

                Recursive = true
            });

            var images = allSeriesResult.Items.Select(i => ApiClient.GetImageUrl(i, new ImageOptions
            {
                Height = Convert.ToInt32(TileHeight),
                ImageType = imageType

            }));

            AllShowsViewModel.AddImages(images);
        }

        private async void NavigateToAllShows()
        {
            PresentationManager.ShowLoadingAnimation();

            try
            {
                await NavigateToAllShowsInternal();
            }
            finally
            {
                PresentationManager.HideLoadingAnimation();
            }
        }

        private async Task NavigateToAllShowsInternal()
        {
            var item = await ApiClient.GetRootFolderAsync(_sessionManager.CurrentUser.Id);

            var displayPreferences = await PresentationManager.GetDisplayPreferences("AllShows", CancellationToken.None);

            var page = new FolderPage(item, displayPreferences, ApiClient, _imageManager, _sessionManager,
                                      PresentationManager, _navService, _playbackManager, _logger);

            page.CustomPageTitle = "TV Shows";

            page.ViewType = ViewType.Tv;
            page.CustomItemQuery = GetAllShows;

            await _navService.Navigate(page);
        }

        private Task<ItemsResult> GetAllShows(DisplayPreferences displayPreferences)
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

            return ApiClient.GetItemsAsync(query);
        }

        private Task<ItemsResult> GetAllActors(DisplayPreferences displayPreferences)
        {
            var fields = FolderPage.QueryFields.ToList();
            fields.Remove(ItemFields.ItemCounts);
            fields.Remove(ItemFields.Overview);
            fields.Remove(ItemFields.DisplayPreferencesId);
            fields.Remove(ItemFields.DateCreated);

            var query = new PersonsQuery
            {
                Fields = fields.ToArray(),

                IncludeItemTypes = new[] { "Series", "Episode" },

                SortBy = !String.IsNullOrEmpty(displayPreferences.SortBy)
                             ? new[] { displayPreferences.SortBy }
                             : new[] { ItemSortBy.SortName },

                SortOrder = displayPreferences.SortOrder,

                Recursive = true,

                PersonTypes = new[] { PersonType.Actor, PersonType.GuestStar }
            };

            return ApiClient.GetPeopleAsync(query);
        }

        private Task<ItemsResult> GetNextUpAsync()
        {
            var query = new NextUpQuery
            {
                Fields = new[]
                        {
                            ItemFields.PrimaryImageAspectRatio,
                            ItemFields.DateCreated,
                            ItemFields.DisplayPreferencesId
                        },

                UserId = _sessionManager.CurrentUser.Id,

                Limit = 30
            };

            return ApiClient.GetNextUpAsync(query);
        }

        private Task<ItemsResult> GetResumeablesAsync()
        {
            var query = new ItemQuery
            {
                Fields = new[]
                        {
                            ItemFields.PrimaryImageAspectRatio,
                            ItemFields.DateCreated,
                            ItemFields.DisplayPreferencesId
                        },

                UserId = _sessionManager.CurrentUser.Id,

                SortBy = new[] { ItemSortBy.DatePlayed },

                SortOrder = SortOrder.Descending,

                IncludeItemTypes = new[] { "Episode" },

                Filters = new[] { ItemFilter.IsResumable },

                Limit = 4,

                Recursive = true
            };

            return ApiClient.GetItemsAsync(query);
        }

        public void Dispose()
        {
            if (SpotlightViewModel != null)
            {
                SpotlightViewModel.Dispose();
            }
            if (ActorsViewModel != null)
            {
                ActorsViewModel.Dispose();
            }
            if (AllShowsViewModel != null)
            {
                AllShowsViewModel.Dispose();
            }
            if (ResumeViewModel != null)
            {
                ResumeViewModel.Dispose();
            }
            if (NextUpViewModel != null)
            {
                NextUpViewModel.Dispose();
            }
        }
    }
}
