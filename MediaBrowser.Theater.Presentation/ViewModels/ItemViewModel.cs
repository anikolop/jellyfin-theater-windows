﻿using System.Collections.Generic;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Reflection;
using MediaBrowser.Theater.Interfaces.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MediaBrowser.Theater.Presentation.ViewModels
{
    [TypeDescriptionProvider(typeof(HyperTypeDescriptionProvider))]
    public class ItemViewModel : BaseViewModel, IDisposable
    {
        private readonly IApiClient _apiClient;
        private readonly IImageManager _imageManager;
        private readonly IPlaybackManager _playbackManager;
        private readonly IPresentationManager _presentation;
        private readonly ILogger _logger;

        public ICommand PlayCommand { get; private set; }
        public ICommand ResumeCommand { get; private set; }
        public ICommand PlayTrailerCommand { get; private set; }

        public ICommand ToggleLikesCommand { get; private set; }
        public ICommand ToggleDislikesCommand { get; private set; }
        public ICommand ToggleIsFavoriteCommand { get; private set; }

        public ItemViewModel(IApiClient apiClient, IImageManager imageManager, IPlaybackManager playbackManager, IPresentationManager presentation, ILogger logger)
        {
            _apiClient = apiClient;
            _imageManager = imageManager;
            _playbackManager = playbackManager;
            _presentation = presentation;
            _logger = logger;

            PlayCommand = new RelayCommand(Play);
            ResumeCommand = new RelayCommand(Resume);
            PlayTrailerCommand = new RelayCommand(PlayTrailer);

            ToggleLikesCommand = new RelayCommand(ToggleLikes);
            ToggleDislikesCommand = new RelayCommand(ToggleDislikes);
            ToggleIsFavoriteCommand = new RelayCommand(ToggleIsFavorite);
        }

        private BaseItemDto _item;
        public BaseItemDto Item
        {
            get { return _item; }

            set
            {
                var changed = _item != value;

                _item = value;

                if (changed)
                {
                    OnPropertyChanged("Item");
                    OnPropertyChanged("ItemType");
                    OnPropertyChanged("IsFolder");
                    OnPropertyChanged("IsNew");
                    OnPropertyChanged("RecentlyAddedItemCount");
                    OnPropertyChanged("IsOffline");
                    OnPropertyChanged("DurationShortTimeString");
                    OnPropertyChanged("CommunityRating");
                    OnPropertyChanged("CriticRating");
                    OnPropertyChanged("HasPositiveCriticRating");
                    OnPropertyChanged("HasNegativeCriticRating");
                    OnPropertyChanged("AudioCodec");
                    OnPropertyChanged("VideoCodec");
                    OnPropertyChanged("AudioChannels");
                    OnPropertyChanged("Resolution");
                    OnPropertyChanged("OfficialRating");
                    OnPropertyChanged("RuntimeMinutesText");
                    OnPropertyChanged("Tagline");
                    OnPropertyChanged("Overview");
                    OnPropertyChanged("MediaType");
                    OnPropertyChanged("CanPlay");
                    OnPropertyChanged("HasTrailer");
                    OnPropertyChanged("Players");
                    OnPropertyChanged("GameSystem");
                    OnPropertyChanged("PremiereShortDate");
                    OnPropertyChanged("PremieresInFuture");
                    OnPropertyChanged("PremiereDate");
                    OnPropertyChanged("Genres");
                    OnPropertyChanged("GenreCount");
                    OnPropertyChanged("StudioNames");
                    OnPropertyChanged("StudioCount");
                    OnPropertyChanged("Directors");
                    OnPropertyChanged("DirectorCount");

                    RefreshUserDataFields();
                }
            }
        }

        private void RefreshUserDataFields()
        {
            OnPropertyChanged("PlayedPercentage");
            OnPropertyChanged("CanResume");
            OnPropertyChanged("IsLiked");
            OnPropertyChanged("IsDisliked");
            OnPropertyChanged("IsFavorite");
            OnPropertyChanged("IsInProgress");
            OnPropertyChanged("IsPlayed");
        }

        public bool CanPlay
        {
            get
            {
                return _item != null && !_item.IsFolder;
            }
        }

        public bool CanResume
        {
            get
            {
                return _item != null && !_item.IsFolder && _item.UserData != null && _item.UserData.PlaybackPositionTicks > 0;
            }
        }

        public bool HasTrailer
        {
            get { return _item != null && _item.LocalTrailerCount > 0; }
        }

        private ImageType[] _preferredImageTypes = new[] { ImageType.Primary };
        public ImageType[] PreferredImageTypes
        {
            get { return _preferredImageTypes; }

            set
            {
                var changed = !_preferredImageTypes.SequenceEqual(value);

                _preferredImageTypes = value;

                if (changed)
                {
                    OnPropertyChanged("PreferredImageTypes");
                }
            }
        }

        private bool _hasImage;
        public bool HasImage
        {
            get { return _hasImage; }

            set
            {
                var changed = _hasImage != value;
                _hasImage = value;

                if (changed)
                {
                    OnPropertyChanged("HasImage");
                }
            }
        }

        private CancellationTokenSource _imageCancellationTokenSource;

        private BitmapImage _image;
        public BitmapImage Image
        {
            get
            {
                var img = _image;

                var tokenSource = _imageCancellationTokenSource;

                if (img == null && (tokenSource == null || tokenSource.IsCancellationRequested))
                {
                    DownloadImage();
                }

                return _image;
            }

            private set
            {
                var changed = !Equals(_image, value);

                _image = value;

                if (value == null)
                {
                    var tokenSource = _imageCancellationTokenSource;

                    if (tokenSource != null)
                    {
                        tokenSource.Cancel();
                    }
                }

                if (changed)
                {
                    OnPropertyChanged("Image");
                }
            }
        }

        private string _displayName;
        public string DisplayName
        {
            get { return _displayName; }

            set
            {
                var changed = !string.Equals(_displayName, value);

                _displayName = value;

                if (changed)
                {
                    OnPropertyChanged("DisplayName");
                }
            }
        }

        public string AudioCodec
        {
            get
            {

                if (_item != null && _item.MediaStreams != null)
                {
                    var stream = _item.MediaStreams
                        .OrderBy(i => i.Index)
                        .FirstOrDefault(i => i.Type == MediaStreamType.Audio);

                    if (stream != null)
                    {
                        return string.Equals(stream.Codec, "dca", StringComparison.OrdinalIgnoreCase) ? stream.Profile : stream.Codec;
                    }
                }

                return null;
            }
        }

        public int? AudioChannels
        {
            get
            {

                if (_item != null && _item.MediaStreams != null)
                {
                    var stream = _item.MediaStreams
                        .OrderBy(i => i.Index)
                        .FirstOrDefault(i => i.Type == MediaStreamType.Audio);

                    if (stream != null)
                    {
                        return stream.Channels;
                    }
                }

                return null;
            }
        }

        public string VideoCodec
        {
            get
            {

                if (_item != null && _item.MediaStreams != null)
                {
                    var stream = _item.MediaStreams
                        .OrderBy(i => i.Index)
                        .FirstOrDefault(i => i.Type == MediaStreamType.Video);

                    if (stream != null)
                    {
                        return stream.Codec;
                    }
                }

                return null;
            }
        }

        public string Resolution
        {
            get
            {

                if (_item != null && _item.MediaStreams != null)
                {
                    var stream = _item.MediaStreams
                        .OrderBy(i => i.Index)
                        .FirstOrDefault(i => i.Type == MediaStreamType.Video);

                    if (stream != null)
                    {
                        if (stream.Width.HasValue && stream.Height.HasValue)
                        {
                            if (stream.Width.Value == 1920)
                            {
                                return "1080p";
                            }
                            if (stream.Width.Value == 1280)
                            {
                                return "720p";
                            }
                            if (stream.Width.Value == 720)
                            {
                                return "480p";
                            }
                            return stream.Width + "/" + stream.Height;
                        }
                    }
                }

                return null;
            }
        }

        public int StudioCount
        {
            get { return _item == null ? 0 : _item.Studios.Length; }
        }

        public List<string> StudioNames
        {
            get { return _item == null ? null : _item.Studios.Select(i => i.Name).Take(3).ToList(); }
        }

        public int DirectorCount
        {
            get { return _item == null ? 0 : Directors.Count; }
        }

        public List<string> Directors
        {
            get
            {
                return _item == null ? 
                    null : 
                    _item.People
                    .Where(i => string.Equals(i.Type, PersonType.Director, StringComparison.OrdinalIgnoreCase) || string.Equals(i.Role, PersonType.Director, StringComparison.OrdinalIgnoreCase))
                    .Select(i => i.Name)
                    .Take(3)
                    .ToList();
            }
        }

        public int GenreCount
        {
            get { return _item == null ? 0 : _item.Genres.Count; }
        }

        public List<string> Genres
        {
            get { return _item == null ? null : _item.Genres.Take(3).ToList(); }
        }

        public bool PremieresInFuture
        {
            get { return _item != null && _item.PremiereDate.HasValue && _item.PremiereDate.Value > DateTime.UtcNow; }
        }

        public string PremiereShortDate
        {
            get { return _item == null || !_item.PremiereDate.HasValue ? null : _item.PremiereDate.Value.ToLocalTime().ToShortDateString(); }
        }

        public DateTime? PremiereDate
        {
            get { return _item == null ? null : _item.PremiereDate; }
        }

        public string ItemType
        {
            get { return _item == null ? null : _item.Type; }
        }

        public string Overview
        {
            get { return _item == null ? null : _item.Overview; }
        }

        public string MediaType
        {
            get { return _item == null ? null : _item.MediaType; }
        }

        public string GameSystem
        {
            get { return _item == null ? null : _item.GameSystem; }
        }

        public int? Players
        {
            get { return _item == null ? null : _item.Players; }
        }

        public string DurationShortTimeString
        {
            get { return _item == null ? null : GetMinutesString(_item); }
        }

        public double PlayedPercentage
        {
            get
            {
                var item = _item;

                if (item != null)
                {
                    if (item.IsFolder)
                    {
                        return item.PlayedPercentage ?? 0;
                    }

                    if (item.RunTimeTicks.HasValue)
                    {
                        if (item.UserData != null && item.UserData.PlaybackPositionTicks > 0)
                        {
                            double percent = item.UserData.PlaybackPositionTicks;
                            percent /= item.RunTimeTicks.Value;

                            return percent * 100;
                        }
                    }
                }

                return 0;
            }
        }

        public string RuntimeMinutesText
        {
            get
            {
                var ticks = Item.RunTimeTicks;

                if (ticks.HasValue)
                {
                    var minutes = Math.Round(TimeSpan.FromTicks(ticks.Value).TotalMinutes);

                    return minutes < 2 ? minutes + " min" : minutes + " mins";
                }

                return null;
            }
        }

        public string OfficialRating
        {
            get { return _item == null ? null : _item.OfficialRating; }
        }

        public string Tagline
        {
            get { return _item == null || _item.Taglines == null || _item.Taglines.Count == 0 ? null : _item.Taglines[0]; }
        }

        public float? CommunityRating
        {
            get { return _item == null ? null : _item.CommunityRating; }
        }

        public float? CriticRating
        {
            get { return _item == null ? null : _item.CriticRating; }
        }

        public bool HasPositiveCriticRating
        {
            get { return _item != null && _item.CriticRating != null && _item.CriticRating.Value >= 60; }
        }

        public bool HasNegativeCriticRating
        {
            get { return _item != null && _item.CriticRating != null && _item.CriticRating.Value < 60; }
        }

        public int RecentlyAddedItemCount
        {
            get { return _item == null ? 0 : _item.RecentlyAddedItemCount ?? 0; }
        }

        public bool IsFolder
        {
            get { return _item != null && _item.IsFolder; }
        }

        public bool IsLiked
        {
            get { return _item != null && _item.UserData != null && _item.UserData.Likes.HasValue && _item.UserData.Likes.Value; }
        }

        public bool IsDisliked
        {
            get { return _item != null && _item.UserData != null && _item.UserData.Likes.HasValue && !_item.UserData.Likes.Value; }
        }

        public bool IsFavorite
        {
            get { return _item != null && _item.UserData != null && _item.UserData.IsFavorite; }
        }

        public bool IsInProgress
        {
            get
            {
                var pct = PlayedPercentage;

                return pct > 0 && pct < 100;
            }
        }

        public bool IsOffline
        {
            get { return _item != null && _item.LocationType == LocationType.Offline; }
        }

        public bool IsNew
        {
            get { return _item.DateCreated.HasValue && (DateTime.UtcNow - _item.DateCreated.Value).TotalDays < 14; }
        }

        public bool IsPlayed
        {
            get
            {
                var item = _item;

                if (item != null)
                {
                    if (item.UserData != null)
                    {
                        return item.UserData.Played;
                    }
                }

                return false;
            }
        }

        private bool _downloadPrimaryImageAtExactSize;
        public bool DownloadPrimaryImageAtExactSize
        {
            get { return _downloadPrimaryImageAtExactSize; }

            set
            {
                var changed = _downloadPrimaryImageAtExactSize != value;

                _downloadPrimaryImageAtExactSize = value;

                if (changed)
                {
                    OnPropertyChanged("DownloadPrimaryImageAtExactSize");
                }
            }
        }

        private bool _downloadImagesAtExactSize;
        public bool DownloadImagesAtExactSize
        {
            get { return _downloadImagesAtExactSize; }

            set
            {
                var changed = _downloadImagesAtExactSize != value;

                _downloadImagesAtExactSize = value;

                if (changed)
                {
                    OnPropertyChanged("DownloadImagesAtExactSize");
                }
            }
        }

        private string _viewType;
        public string ViewType
        {
            get { return _viewType; }

            set
            {
                var changed = !string.Equals(_viewType, value);

                _viewType = value;

                if (changed)
                {
                    OnPropertyChanged("ViewType");
                }
            }
        }

        private int? _imageWidth;
        public int? ImageWidth
        {
            get { return _imageWidth; }

            set
            {
                var changed = _imageWidth != value;

                _imageWidth = value;

                if (changed)
                {
                    OnPropertyChanged("ImageWidth");
                }
            }
        }

        private int? _imageHeight;
        public int? ImageHeight
        {
            get { return _imageHeight; }

            set
            {
                var changed = _imageHeight != value;

                _imageHeight = value;

                if (changed)
                {
                    OnPropertyChanged("ImageHeight");
                }
            }
        }

        public void SetDisplayPreferences(int imageDisplayWidth, int imageDisplayHeight, string viewType)
        {
            var changed = !imageDisplayHeight.Equals(ImageHeight ?? 0) || !imageDisplayWidth.Equals(ImageWidth ?? 0) || !string.Equals(viewType, ViewType);

            ImageHeight = imageDisplayHeight;
            ImageWidth = imageDisplayWidth;
            ViewType = viewType;

            if (changed)
            {
                Image = null;
            }
        }

        private string GetMinutesString(BaseItemDto item)
        {
            var time = TimeSpan.FromTicks(item.RunTimeTicks ?? 0);

            return time.ToString(time.TotalHours < 1 ? "m':'ss" : "h':'mm':'ss");
        }

        /// <summary>
        /// Gets an image url that can be used to download an image from the api
        /// </summary>
        /// <param name="imageType">The type of image requested</param>
        /// <param name="imageIndex">The image index, if there are multiple. Currently only applies to backdrops. Supply null or 0 for first backdrop.</param>
        /// <returns>System.String.</returns>
        private string GetImageUrl(ImageType imageType, int? imageIndex = null)
        {
            var imageOptions = new ImageOptions
            {
                ImageType = imageType,
                ImageIndex = imageIndex,
                Width = ImageWidth
            };

            if ((imageType == ImageType.Primary && DownloadPrimaryImageAtExactSize)
                || (imageType != ImageType.Primary && DownloadImagesAtExactSize))
            {
                imageOptions.Height = ImageHeight;
            }

            return _apiClient.GetImageUrl(Item, imageOptions);
        }

        public async void DownloadImage()
        {
            var preferredImageTypes = PreferredImageTypes;
            var item = Item;

            foreach (var imageType in preferredImageTypes)
            {
                if (imageType == ImageType.Backdrop)
                {
                    if (item.BackdropCount == 0)
                    {
                        continue;
                    }
                }
                else
                {
                    if (!item.ImageTags.ContainsKey(imageType))
                    {
                        continue;
                    }
                }

                var url = GetImageUrl(imageType);

                var tokenSource = _imageCancellationTokenSource = new CancellationTokenSource();

                try
                {
                    var img = await _imageManager.GetRemoteBitmapAsync(url, tokenSource.Token);

                    tokenSource.Token.ThrowIfCancellationRequested();

                    Image = img;

                    HasImage = true;
                }
                catch (OperationCanceledException)
                {

                }
                catch
                {
                    // Logged at lower levels
                    HasImage = false;
                }
                finally
                {
                    tokenSource.Dispose();

                    if (tokenSource == _imageCancellationTokenSource)
                    {
                        _imageCancellationTokenSource = null;
                    }
                }

                return;
            }

            HasImage = false;
        }

        /// <summary>
        /// Gets the bitmap image async.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{BitmapImage}.</returns>
        public Task<BitmapImage> GetBitmapImageAsync(ImageOptions options, CancellationToken cancellationToken)
        {
            var url = _apiClient.GetImageUrl(Item, options);

            return _imageManager.GetRemoteBitmapAsync(url, cancellationToken);
        }

        private async void Play(object commandParameter)
        {
            await _playbackManager.Play(new PlayOptions(_item));
        }

        private async void Resume(object commandParameter)
        {
            await _playbackManager.Play(new PlayOptions(_item)
            {
                StartPositionTicks = _item.UserData.PlaybackPositionTicks
            });
        }

        private async void PlayTrailer(object commandParameter)
        {
            try
            {
                var trailers = await _apiClient.GetLocalTrailersAsync(_apiClient.CurrentUserId, _item.Id);

                await _playbackManager.Play(new PlayOptions(trailers.First()));
            }
            catch (HttpException)
            {
                _presentation.ShowDefaultErrorMessage();
            }
        }

        private async void ToggleLikes(object commandParameter)
        {
            if (IsLiked)
            {
                ClearUserItemRating();
                return;
            }

            try
            {
                _item.UserData = await _apiClient.UpdateUserItemRatingAsync(_item.Id, _apiClient.CurrentUserId, true);

                RefreshUserDataFields();
            }
            catch
            {
                _presentation.ShowDefaultErrorMessage();
            }
        }

        private async void ToggleDislikes(object commandParameter)
        {
            if (IsDisliked)
            {
                ClearUserItemRating();
                return;
            }

            try
            {
                _item.UserData = await _apiClient.UpdateUserItemRatingAsync(_item.Id, _apiClient.CurrentUserId, false);

                RefreshUserDataFields();
            }
            catch
            {
                _presentation.ShowDefaultErrorMessage();
            }
        }

        private async void ToggleIsFavorite(object commandParameter)
        {
            try
            {
                _item.UserData = await _apiClient.UpdateFavoriteStatusAsync(_item.Id, _apiClient.CurrentUserId, !IsFavorite);

                RefreshUserDataFields();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error updating favorite status", ex);

                _presentation.ShowDefaultErrorMessage();
            }
        }

        private async void ClearUserItemRating()
        {
            try
            {
                _item.UserData = await _apiClient.ClearUserItemRatingAsync(_item.Id, _apiClient.CurrentUserId);

                RefreshUserDataFields();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error clearing user rating", ex);

                _presentation.ShowDefaultErrorMessage();
            }
        }

        public void Dispose()
        {
            DisposeCancellationTokenSource();
        }

        private void DisposeCancellationTokenSource()
        {
            if (_imageCancellationTokenSource != null)
            {
                _imageCancellationTokenSource.Cancel();
                _imageCancellationTokenSource.Dispose();
                _imageCancellationTokenSource = null;
            }
        }
    }
}
