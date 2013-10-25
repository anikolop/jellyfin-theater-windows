﻿using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Plugins.DefaultTheme.Home;
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

namespace MediaBrowser.Plugins.DefaultTheme.Details
{
    public class DetailPageViewModel : TabbedViewModel, IAcceptsPlayCommand
    {
        private readonly IApiClient _apiClient;
        private readonly ISessionManager _sessionManager;
        private readonly IImageManager _imageManager;
        private readonly IPresentationManager _presentationManager;
        private readonly IPlaybackManager _playback;
        private readonly INavigationService _navigation;
        private readonly ILogger _logger;
        private readonly IServerEvents _serverEvents;

        private ItemViewModel _itemViewModel;
        public ItemViewModel ItemViewModel
        {
            get
            {
                return _itemViewModel;
            }
            set
            {
                _itemViewModel = value;
                OnPropertyChanged("ItemViewModel");
            }
        }

        public ViewType Context { get; private set; }

        public DetailPageViewModel(ItemViewModel item, IApiClient apiClient, ISessionManager sessionManager, IImageManager imageManager, IPresentationManager presentationManager, IPlaybackManager playback, INavigationService navigation, ILogger logger, IServerEvents serverEvents, ViewType context)
        {
            _apiClient = apiClient;
            _sessionManager = sessionManager;
            _imageManager = imageManager;
            _presentationManager = presentationManager;
            _playback = playback;
            _navigation = navigation;
            _logger = logger;
            _serverEvents = serverEvents;
            Context = context;
            ItemViewModel = item;
        }

        private bool _enableScrolling = true;
        public bool EnableScrolling
        {
            get
            {
                return _enableScrolling;
            }
            set
            {
                _enableScrolling = value;
                OnPropertyChanged("EnableScrolling");
            }
        }

        public override void OnPropertyChanged(string name)
        {
            base.OnPropertyChanged(name);

            if (string.Equals(name, "CurrentSection"))
            {
                var section = CurrentSection;

                EnableScrolling = !string.Equals(section, "overview", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(section, "seasons", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(section, "itemmovies", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(section, "itemtrailers", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(section, "itemseries", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(section, "itemepisodes", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(section, "itemalbums", StringComparison.OrdinalIgnoreCase);
            }
        }

        protected override async Task<IEnumerable<TabItem>> GetSections()
        {
            _presentationManager.ShowLoadingAnimation();

            try
            {
                var item = ItemViewModel;

                var themeMediaTask = GetThemeMedia(item.Item);
                var criticReviewsTask = GetCriticReviews(item.Item);

                await Task.WhenAll(themeMediaTask, criticReviewsTask);

                return GetMenuList(item.Item, themeMediaTask.Result, criticReviewsTask.Result);
            }
            finally
            {
                _presentationManager.HideLoadingAnimation();
            }
        }

        private IEnumerable<TabItem> GetMenuList(BaseItemDto item, AllThemeMediaResult themeMediaResult, ItemReviewsResult reviewsResult)
        {
            var views = new List<TabItem>
                {
                    new TabItem
                    {
                        Name = "overview",
                        DisplayName = "Overview"
                    }
                };

            if (item.ChildCount > 0)
            {
                if (item.IsType("series"))
                {
                    views.Add(new TabItem
                    {
                        Name = "seasons",
                        DisplayName = "Seasons"
                    });
                }
                else if (item.IsType("season"))
                {
                    views.Add(new TabItem
                    {
                        Name = "episodes",
                        DisplayName = "Episodes"
                    });
                }
                else if (item.IsType("musicalbum"))
                {
                    views.Add(new TabItem
                    {
                        Name = "songs",
                        DisplayName = "Songs"
                    });
                }
            }

            if (item.People.Length > 0)
            {
                views.Add(new TabItem
                {
                    Name = "cast",
                    DisplayName = "Cast"
                });
            }

            if (item.LocalTrailerCount > 1)
            {
                views.Add(new TabItem
                {
                    Name = "trailers",
                    DisplayName = "Trailers"
                });
            }

            if (item.Chapters != null && item.Chapters.Count > 0)
            {
                views.Add(new TabItem
                {
                    Name = "scenes",
                    DisplayName = "Scenes"
                });
            }

            if (item.MediaStreams != null && item.MediaStreams.Count > 0)
            {
                views.Add(new TabItem
                {
                    Name = "media info",
                    DisplayName = "Media Info"
                });
            }

            if (item.SpecialFeatureCount > 0)
            {
                views.Add(new TabItem
                {
                    Name = "special features",
                    DisplayName = "Special Features"
                });
            }

            if (item.IsType("movie") || item.IsType("trailer") || item.IsType("series") || item.IsType("musicalbum") || item.IsGame)
            {
                views.Add(new TabItem
                {
                    Name = "similar",
                    DisplayName = "Similar"
                });
            }

            if (reviewsResult.TotalRecordCount > 0 || !string.IsNullOrEmpty(item.CriticRatingSummary))
            {
                views.Add(new TabItem
                {
                    Name = "reviews",
                    DisplayName = "Reviews"
                });
            }

            if (item.SoundtrackIds != null)
            {
                if (item.SoundtrackIds.Length > 1)
                {
                    views.Add(new TabItem
                    {
                        Name = "soundtracks",
                        DisplayName = "Soundtracks"
                    });
                }
                else if (item.SoundtrackIds.Length > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "soundtrack",
                        DisplayName = "Soundtrack"
                    });
                }
            }

            if (themeMediaResult.ThemeVideosResult.TotalRecordCount > 0 || themeMediaResult.ThemeSongsResult.TotalRecordCount > 0)
            {
                views.Add(new TabItem
                {
                    Name = "themes",
                    DisplayName = "Themes"
                });
            }

            if (item.IsArtist || item.IsGameGenre || item.IsGenre || item.IsMusicGenre || item.IsPerson || item.IsStudio)
            {
                if (item.MovieCount.HasValue && item.MovieCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemmovies",
                        DisplayName = string.Format("Movies ({0})", item.MovieCount.Value)
                    });
                }

                if (item.SeriesCount.HasValue && item.SeriesCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemseries",
                        DisplayName = string.Format("Series ({0})", item.SeriesCount.Value)
                    });
                }

                if (item.EpisodeCount.HasValue && item.EpisodeCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemepisodes",
                        DisplayName = string.Format("Episodes ({0})", item.EpisodeCount.Value)
                    });
                }

                if (item.TrailerCount.HasValue && item.TrailerCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemtrailers",
                        DisplayName = string.Format("Trailers ({0})", item.TrailerCount.Value)
                    });
                }

                if (item.GameCount.HasValue && item.GameCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemgames",
                        DisplayName = string.Format("Games ({0})", item.GameCount.Value)
                    });
                }

                if (item.AlbumCount.HasValue && item.AlbumCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemalbums",
                        DisplayName = string.Format("Albums ({0})", item.AlbumCount.Value)
                    });
                }

                if (item.SongCount.HasValue && item.SongCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemsongs",
                        DisplayName = string.Format("Songs ({0})", item.SongCount.Value)
                    });
                }

                if (item.MusicVideoCount.HasValue && item.MusicVideoCount.Value > 0)
                {
                    views.Add(new TabItem
                    {
                        Name = "itemmusicvideos",
                        DisplayName = string.Format("Music Videos ({0})", item.MusicVideoCount.Value)
                    });
                }
            }

            if (GalleryViewModel.GetImages(item, _apiClient, null, null, true).Any())
            {
                views.Add(new TabItem
                {
                    Name = "gallery",
                    DisplayName = "Gallery"
                });
            }

            return views;
        }

        protected override object GetContentViewModel(string section)
        {
            if (string.Equals(section, "overview"))
            {
                return _itemViewModel;
            }
            if (string.Equals(section, "reviews"))
            {
                return new CriticReviewListViewModel(_presentationManager, _apiClient, _imageManager, _itemViewModel.Item.Id);
            }
            if (string.Equals(section, "scenes"))
            {
                return new ChapterInfoListViewModel(_apiClient, _imageManager, _playback, _presentationManager)
                {
                    Item = _itemViewModel.Item,
                    ImageWidth = 380
                };
            }
            if (string.Equals(section, "cast"))
            {
                return new ItemPersonListViewModel(_apiClient, _imageManager, _presentationManager, _navigation)
                {
                    Item = _itemViewModel.Item,
                    ImageWidth = 300,
                    ViewType = Context
                };
            }
            if (string.Equals(section, "gallery"))
            {
                const int imageHeight = 280;

                var vm = new GalleryViewModel(_apiClient, _imageManager, _navigation)
                {
                    ImageHeight = imageHeight,
                    Item = _itemViewModel.Item
                };

                var imageUrls = GalleryViewModel.GetImages(_itemViewModel.Item, _apiClient, null, imageHeight, true);

                vm.AddImages(imageUrls);

                return vm;
            }
            if (string.Equals(section, "similar"))
            {
                return new ItemListViewModel(GetSimilarItemsAsync, _presentationManager, _imageManager, _apiClient, _navigation, _playback, _logger, _serverEvents)
                {
                    ImageDisplayWidth = 300,
                    EnableBackdropsForCurrentItem = false,
                    Context = Context
                };
            }
            if (string.Equals(section, "special features"))
            {
                return new ItemListViewModel(GetSpecialFeatures, _presentationManager, _imageManager, _apiClient, _navigation, _playback, _logger, _serverEvents)
                {
                    ImageDisplayWidth = 576,
                    EnableBackdropsForCurrentItem = false,
                    ListType = "SpecialFeatures",
                    Context = Context
                };
            }
            if (string.Equals(section, "themes"))
            {
                return new ItemListViewModel(GetConvertedThemeMediaResult, _presentationManager, _imageManager, _apiClient, _navigation, _playback, _logger, _serverEvents)
                {
                    ImageDisplayWidth = 576,
                    EnableBackdropsForCurrentItem = false,
                    ListType = "SpecialFeatures"
                };
            }
            if (string.Equals(section, "soundtrack") || string.Equals(section, "soundtracks"))
            {
                return new ItemListViewModel(GetSoundtracks, _presentationManager, _imageManager, _apiClient, _navigation, _playback, _logger, _serverEvents)
                {
                    ImageDisplayWidth = 400,
                    EnableBackdropsForCurrentItem = false,
                    Context = Context
                };
            }
            if (string.Equals(section, "seasons") || string.Equals(section, "episodes") || string.Equals(section, "songs"))
            {
                return new ItemListViewModel(GetChildren, _presentationManager, _imageManager, _apiClient, _navigation, _playback, _logger, _serverEvents)
                {
                    ImageDisplayWidth = 300,
                    EnableBackdropsForCurrentItem = false,
                    Context = Context
                };
            }
            if (string.Equals(section, "trailers"))
            {
                return new ItemListViewModel(GetTrailers, _presentationManager, _imageManager, _apiClient, _navigation, _playback, _logger, _serverEvents)
                {
                    ImageDisplayWidth = 384,
                    EnableBackdropsForCurrentItem = false,
                    ListType = "Trailers",
                    Context = Context
                };
            }
            if (string.Equals(section, "itemmovies"))
            {
                return GetItemByNameItemListViewModel("Movie", 200, 300);
            }
            if (string.Equals(section, "itemtrailers"))
            {
                return GetItemByNameItemListViewModel("Trailer", 200, 300);
            }
            if (string.Equals(section, "itemseries"))
            {
                return GetItemByNameItemListViewModel("Series", 200, 300);
            }
            if (string.Equals(section, "itemalbums"))
            {
                return GetItemByNameItemListViewModel("MusicAlbum", 280, 280);
            }
            if (string.Equals(section, "itemepisodes"))
            {
                return GetItemByNameItemListViewModel("Episode", 496, 279);
            }
            if (string.Equals(section, "media info"))
            {
                return _itemViewModel.MediaStreams.Select(i => new MediaStreamViewModel { MediaStream = i, OwnerItem = _itemViewModel.Item }).ToList();
            }

            return _itemViewModel;
        }

        protected override void DisposePreviousSection(object old)
        {
            // Don't dispose the page view model on tab change
            if (old is ItemViewModel)
            {
                return;
            }

            base.DisposePreviousSection(old);
        }

        private ItemListViewModel GetItemByNameItemListViewModel(string type, int width, int height)
        {
            Func<ItemListViewModel, Task<ItemsResult>> itemGenerator = (vm) => GetItemByNameItemsAsync(type);

            var viewModel = new ItemListViewModel(itemGenerator, _presentationManager, _imageManager, _apiClient, _navigation, _playback, _logger, _serverEvents)
            {
                ViewType = ListViewTypes.Poster,
                ImageDisplayWidth = width,
                ImageDisplayHeightGenerator = GetImageDisplayHeight,

                ItemContainerWidth = width + 20,
                ItemContainerHeight = height + 20,

                ItemContainerHeightGenerator = vm => GetImageDisplayHeight(vm) + 20,

                DisplayNameGenerator = HomePageViewModel.GetDisplayName,
                PreferredImageTypesGenerator = vm => new[] { ImageType.Primary },

                ShowSidebarGenerator = vm => false,
                ScrollDirectionGenerator = vm => ScrollDirection.Horizontal,

                AutoSelectFirstItem = false,

                ShowLoadingAnimation = true,
                EnableBackdropsForCurrentItem = false
            };

            return viewModel;
        }

        private double GetImageDisplayHeight(ItemListViewModel viewModel)
        {
            var imageDisplayWidth = viewModel.ImageDisplayWidth;
            var medianPrimaryImageAspectRatio = viewModel.MedianPrimaryImageAspectRatio ?? 0;

            if (!medianPrimaryImageAspectRatio.Equals(0))
            {
                double height = imageDisplayWidth;
                height /= medianPrimaryImageAspectRatio;

                return height;
            }

            return viewModel.DefaultImageDisplayHeight;
        }

        private Task<ItemsResult> GetItemByNameItemsAsync(string type)
        {
            var item = ItemViewModel.Item;

            var query = new ItemQuery
            {
                UserId = _sessionManager.CurrentUser.Id,
                Fields = new[]
                        {
                                 ItemFields.PrimaryImageAspectRatio,
                                 ItemFields.DateCreated
                        },

                SortBy = new[] { ItemSortBy.SortName },

                IncludeItemTypes = new[] { type },

                Recursive = true
            };

            if (item.IsPerson)
            {
                query.Person = item.Name;
            }
            else if (item.IsStudio)
            {
                query.Studios = new[] { item.Name };
            }
            else if (item.IsGenre || item.IsMusicGenre || item.IsGameGenre)
            {
                query.Genres = new[] { item.Name };
            }
            else if (item.IsArtist)
            {
                query.Artists = new[] { item.Name };
            }

            return _apiClient.GetItemsAsync(query);
        }

        private async Task<ItemReviewsResult> GetCriticReviews(BaseItemDto item)
        {
            if (item.IsPerson || item.IsStudio || item.IsArtist || item.IsGameGenre || item.IsMusicGenre || item.IsGenre)
            {
                return new ItemReviewsResult();
            }

            try
            {
                return await _apiClient.GetCriticReviews(item.Id, CancellationToken.None);
            }
            catch
            {
                // Logged at lower levels
                return new ItemReviewsResult();
            }
        }

        private async Task<AllThemeMediaResult> GetThemeMedia(BaseItemDto item)
        {
            if (item.IsPerson || item.IsStudio || item.IsArtist || item.IsGameGenre || item.IsMusicGenre || item.IsGenre)
            {
                return new AllThemeMediaResult();
            }

            try
            {
                return await _apiClient.GetAllThemeMediaAsync(_sessionManager.CurrentUser.Id, item.Id, false, CancellationToken.None);
            }
            catch
            {
                // Logged at lower levels
                return new AllThemeMediaResult();
            }
        }

        private async Task<ItemsResult> GetConvertedThemeMediaResult(ItemListViewModel viewModel)
        {
            var allThemeMedia = await GetThemeMedia(ItemViewModel.Item);

            return new ItemsResult
            {
                TotalRecordCount = allThemeMedia.ThemeSongsResult.TotalRecordCount + allThemeMedia.ThemeVideosResult.TotalRecordCount,
                Items = allThemeMedia.ThemeVideosResult.Items.Concat(allThemeMedia.ThemeSongsResult.Items).ToArray()
            };
        }

        private Task<ItemsResult> GetSimilarItemsAsync(ItemListViewModel viewModel)
        {
            var item = ItemViewModel.Item;

            var query = new SimilarItemsQuery
            {
                UserId = _sessionManager.CurrentUser.Id,
                Limit = item.IsGame || item.IsType("musicalbum") ? 6 : 12,
                Fields = new[]
                        {
                                 ItemFields.PrimaryImageAspectRatio,
                                 ItemFields.DateCreated
                        },
                Id = item.Id
            };

            if (item.IsType("trailer"))
            {
                return _apiClient.GetSimilarTrailersAsync(query);
            }
            if (item.IsGame)
            {
                return _apiClient.GetSimilarGamesAsync(query);
            }
            if (item.IsType("musicalbum"))
            {
                return _apiClient.GetSimilarAlbumsAsync(query);
            }
            if (item.IsType("series"))
            {
                return _apiClient.GetSimilarSeriesAsync(query);
            }

            return _apiClient.GetSimilarMoviesAsync(query);
        }

        private Task<ItemsResult> GetSoundtracks(ItemListViewModel viewModel)
        {
            var item = ItemViewModel.Item;

            var query = new ItemQuery
            {
                UserId = _sessionManager.CurrentUser.Id,
                Fields = new[]
                        {
                                 ItemFields.PrimaryImageAspectRatio,
                                 ItemFields.DateCreated
                        },
                Ids = item.SoundtrackIds,
                SortBy = new[] { ItemSortBy.SortName }
            };

            return _apiClient.GetItemsAsync(query);
        }

        private async Task<ItemsResult> GetTrailers(ItemListViewModel viewModel)
        {
            var item = ItemViewModel.Item;

            var items = await _apiClient.GetLocalTrailersAsync(_sessionManager.CurrentUser.Id, item.Id);

            return new ItemsResult
            {
                Items = items,
                TotalRecordCount = items.Length
            };
        }

        private Task<ItemsResult> GetChildren(ItemListViewModel viewModel)
        {
            var item = ItemViewModel.Item;

            var query = new ItemQuery
            {
                UserId = _sessionManager.CurrentUser.Id,
                Fields = new[]
                        {
                                 ItemFields.PrimaryImageAspectRatio,
                                 ItemFields.DateCreated
                        },
                ParentId = item.Id,
                SortBy = new[] { ItemSortBy.SortName },

                MinIndexNumber = item.IsType("Series") ? 1 : (int?)null
            };

            return _apiClient.GetItemsAsync(query);
        }

        private async Task<ItemsResult> GetSpecialFeatures(ItemListViewModel viewModel)
        {
            var item = ItemViewModel.Item;

            var items = await _apiClient.GetSpecialFeaturesAsync(_sessionManager.CurrentUser.Id, item.Id);

            return new ItemsResult
            {
                TotalRecordCount = items.Length,
                Items = items
            };
        }

        public void HandlePlayCommand()
        {
            var accepts = ContentViewModel as IAcceptsPlayCommand;

            if (accepts != null)
            {
                accepts.HandlePlayCommand();
            }
        }
    }
}
