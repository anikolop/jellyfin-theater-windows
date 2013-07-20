﻿using MediaBrowser.Common.Events;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MediaBrowser.Theater.Implementations.Playback
{
    public class PlaybackManager : IPlaybackManager
    {
        private readonly ITheaterConfigurationManager _configurationManager;
        private readonly ILogger _logger;
        private readonly IApiClient _apiClient;
        private readonly INavigationService _nav;
        private readonly IPresentationManager _appWindow;

        public event EventHandler<PlaybackStartEventArgs> PlaybackStarted;

        public event EventHandler<PlaybackStopEventArgs> PlaybackCompleted;

        public void AddParts(IEnumerable<IMediaPlayer> mediaPlayers)
        {
            _mediaPlayers.AddRange(mediaPlayers);
        }

        private readonly List<IMediaPlayer> _mediaPlayers = new List<IMediaPlayer>();

        public PlaybackManager(ITheaterConfigurationManager configurationManager, ILogger logger, IApiClient apiClient, INavigationService nav, IPresentationManager appWindow)
        {
            _configurationManager = configurationManager;
            _logger = logger;
            _apiClient = apiClient;
            _nav = nav;
            _appWindow = appWindow;
        }

        public IEnumerable<IMediaPlayer> MediaPlayers
        {
            get { return _mediaPlayers; }
        }

        /// <summary>
        /// Plays the specified options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>Task.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// options
        /// or
        /// options
        /// </exception>
        /// <exception cref="System.InvalidOperationException">There are no available players.</exception>
        public async Task Play(PlayOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.Items == null || options.Items.Count(i => i.LocationType != LocationType.Offline) == 0)
            {
                throw new ArgumentException("At least one item must be supplied.");
            }

            PlayerConfiguration config;
            var player = GetPlayer(options.Items, out config);

            if (player == null)
            {
                throw new InvalidOperationException("There are no available players.");
            }

            await StopAllPlayback();

            await Play(player, options, config);
        }

        /// <summary>
        /// Plays the specified player.
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="options">The options.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>Task.</returns>
        private async Task Play(IMediaPlayer player, PlayOptions options, PlayerConfiguration configuration)
        {
            if (options.Shuffle)
            {
                options.Items = options.Items.OrderBy(i => new Guid()).ToList();
            }

            var firstItem = options.Items[0];

            if (options.StartPositionTicks == 0 && player.SupportsMultiFilePlayback && firstItem.IsVideo && firstItem.LocationType == LocationType.FileSystem)
            {
                try
                {
                    var intros = await _apiClient.GetIntrosAsync(firstItem.Id, _apiClient.CurrentUserId);

                    options.Items.InsertRange(0, intros.Select(GetPlayableItem));
                }
                catch (HttpException ex)
                {
                    _logger.ErrorException("Error retrieving intros", ex);
                }
            }

            options.Configuration = configuration;

            await player.Play(options);

            if (player is IInternalMediaPlayer)
            {
                await _appWindow.Window.Dispatcher.InvokeAsync(() =>
                {
                    _appWindow.BackdropContainer.Visibility = Visibility.Collapsed;
                    _appWindow.WindowOverlay.SetResourceReference(FrameworkElement.StyleProperty, "WindowBackgroundContentDuringPlayback");
                });

                if (options.GoFullScreen)
                {
                    await _nav.NavigateToInternalPlayerPage();
                }
            }

            OnPlaybackStarted(player, options);
        }

        /// <summary>
        /// Called when [playback started].
        /// </summary>
        /// <param name="player">The player.</param>
        /// <param name="options">The options.</param>
        private async void OnPlaybackStarted(IMediaPlayer player, PlayOptions options)
        {
            EventHelper.QueueEventIfNotNull(PlaybackStarted, this, new PlaybackStartEventArgs
            {
                Options = options,
                Player = player
            }, _logger);

            await new PlaybackProgressReporter(_apiClient, player, _logger).Start().ConfigureAwait(false);
        }

        /// <summary>
        /// Reports the playback completed.
        /// </summary>
        /// <param name="eventArgs">The <see cref="PlaybackStopEventArgs"/> instance containing the event data.</param>
        public async void ReportPlaybackCompleted(PlaybackStopEventArgs eventArgs)
        {
            if (eventArgs.Player is IInternalMediaPlayer)
            {
                await _appWindow.Window.Dispatcher.InvokeAsync(() =>
                {
                    _appWindow.BackdropContainer.Visibility = Visibility.Visible;
                    _appWindow.WindowOverlay.SetResourceReference(FrameworkElement.StyleProperty, "WindowBackgroundContent");
                });
            }

            EventHelper.QueueEventIfNotNull(PlaybackCompleted, this, eventArgs, _logger);
        }

        /// <summary>
        /// Stops all playback.
        /// </summary>
        /// <returns>Task.</returns>
        public Task StopAllPlayback()
        {
            var tasks = MediaPlayers
                .Where(p => p.PlayState == PlayState.Playing || p.PlayState == PlayState.Paused)
                .Select(p => p.Stop());

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gets the playable item.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>BaseItemDto.</returns>
        private BaseItemDto GetPlayableItem(string path)
        {
            return new BaseItemDto
            {
                Path = path,
                Name = Path.GetFileName(path),
                Type = "Video",
                MediaType = "Video",
                VideoType = VideoType.VideoFile,
                IsFolder = false
            };
        }

        /// <summary>
        /// Gets the playable item.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="name">The name.</param>
        /// <returns>BaseItemDto.</returns>
        private BaseItemDto GetPlayableItem(Uri uri, string name)
        {
            return new BaseItemDto
            {
                Path = uri.ToString(),
                Name = name,
                Type = "Video",
                MediaType = "Video",
                VideoType = VideoType.VideoFile,
                IsFolder = false,
                LocationType = LocationType.Remote
            };
        }

        /// <summary>
        /// Gets the player.
        /// </summary>
        /// <param name="items">The items.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>IMediaPlayer.</returns>
        private IMediaPlayer GetPlayer(IEnumerable<BaseItemDto> items, out PlayerConfiguration configuration)
        {
            var configuredPlayer = _configurationManager.Configuration.MediaPlayers.FirstOrDefault(p => IsConfiguredToPlay(p, items));

            if (configuredPlayer != null)
            {
                var player = MediaPlayers.FirstOrDefault(i => string.Equals(i.Name, configuredPlayer.PlayerName, StringComparison.OrdinalIgnoreCase));

                if (player != null)
                {
                    configuration = configuredPlayer;
                    return player;
                }
            }

            configuration = null;

            // If there's no explicit configuration just find the first matching player who says they can play it
            return MediaPlayers.FirstOrDefault(p => items.All(p.CanPlayByDefault));
        }

        /// <summary>
        /// Determines whether [is configured to play] [the specified configuration].
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="items">The items.</param>
        /// <returns><c>true</c> if [is configured to play] [the specified configuration]; otherwise, <c>false</c>.</returns>
        private bool IsConfiguredToPlay(PlayerConfiguration configuration, IEnumerable<BaseItemDto> items)
        {
            return items.All(i => IsConfiguredToPlay(configuration, i));
        }

        /// <summary>
        /// Determines whether [is configured to play] [the specified configuration].
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="item">The item.</param>
        /// <returns><c>true</c> if [is configured to play] [the specified configuration]; otherwise, <c>false</c>.</returns>
        private bool IsConfiguredToPlay(PlayerConfiguration configuration, BaseItemDto item)
        {
            // Make this configurable if/when needed
            if (item.LocationType != LocationType.FileSystem)
            {
                return false;
            }

            // If it's configured for specific item types
            if (!string.Equals(configuration.MediaType, item.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(configuration.MediaType, MediaType.Video))
            {
                if (!item.VideoType.HasValue)
                {
                    return false;
                }

                if (item.VideoType.Value == VideoType.VideoFile)
                {
                    // If it's configured for specific file extensions
                    if (!IsConfiguredForFileExtension(configuration, item.Path))
                    {
                        return false;
                    }
                }

                if (item.VideoType.Value == VideoType.BluRay && !configuration.PlayBluray)
                {
                    return false;
                }
                if (item.VideoType.Value == VideoType.Dvd && !configuration.PlayDvd)
                {
                    return false;
                }

                if (!configuration.Play3DVideo && item.Video3DFormat.HasValue)
                {
                    return false;
                }

                if (item.VideoType.Value == VideoType.Iso & configuration.IsoMethod == IsoConfiguration.None)
                {
                    return false;
                }
            }

            else if (string.Equals(configuration.MediaType, MediaType.Audio))
            {
                // If it's configured for specific file extensions
                if (!IsConfiguredForFileExtension(configuration, item.Path))
                {
                    return false;
                }
            }

            else if (string.Equals(configuration.MediaType, MediaType.Game))
            {
                // If it's configured for specific file extensions
                if (!string.Equals(item.GameSystem, configuration.GameSystem, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsConfiguredForFileExtension(PlayerConfiguration configuration, string path)
        {
            var extensions = configuration.FileExtensions.Select(i => i.TrimStart('.'));

            return extensions.Contains(Path.GetExtension(path).TrimStart('.'), StringComparer.OrdinalIgnoreCase);
        }
    }
}
