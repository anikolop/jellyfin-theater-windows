﻿using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.UI.EntryPoints
{
    class ThemeSongEntryPoint : IStartupEntryPoint, IDisposable
    {
        private readonly INavigationService _nav;
        private readonly IPlaybackManager _playback;
        private readonly ISessionManager _session;
        private readonly IApiClient _apiClient;

        private string _currentPlayingOwnerId;

        private string _lastPlayedOwnerId;

        public ThemeSongEntryPoint(INavigationService nav, IPlaybackManager playback, IApiClient apiClient, ISessionManager session)
        {
            _nav = nav;
            _playback = playback;
            _apiClient = apiClient;
            _session = session;
        }

        public void Run()
        {
            _nav.Navigated += _nav_Navigated;
            _playback.PlaybackCompleted += _playback_PlaybackCompleted;
        }

        void _playback_PlaybackCompleted(object sender, PlaybackStopEventArgs e)
        {
            _currentPlayingOwnerId = null;
        }

        async void _nav_Navigated(object sender, NavigationEventArgs e)
        {
            // If something is already playing, and it's not a theme song, leave it alone
            if (string.IsNullOrEmpty(_currentPlayingOwnerId) && _playback.MediaPlayers.Any(i => i.PlayState != PlayState.Idle))
            {
                return;
            }

            var itemPage = e.NewPage as ISupportsItemThemeMedia;

            if (itemPage == null || _session.CurrentUser == null || string.IsNullOrEmpty(itemPage.ThemeMediaItemId))
            {
                if (!string.IsNullOrEmpty(_currentPlayingOwnerId) && !(e.NewPage is ISupportsThemeMedia))
                {
                    _playback.StopAllPlayback();
                }
                return;
            }

            var itemId = itemPage.ThemeMediaItemId;

            var themeMediaResult = await GetThemeMedia(itemId).ConfigureAwait(false);

            if (string.Equals(_currentPlayingOwnerId, themeMediaResult.OwnerId))
            {
                return;
            }

            // Don't replay the same one over and over
            if (string.Equals(_lastPlayedOwnerId, themeMediaResult.OwnerId))
            {
                return;
            }

            _lastPlayedOwnerId = null;

            if (themeMediaResult.Items.Length > 0)
            {
                await Play(GetItemsToPlay(themeMediaResult)).ConfigureAwait(false);

                _currentPlayingOwnerId = themeMediaResult.OwnerId;
                _lastPlayedOwnerId = themeMediaResult.OwnerId;
            }
            else if (!string.IsNullOrEmpty(_currentPlayingOwnerId))
            {
                _playback.StopAllPlayback();
            }
        }

        private IEnumerable<BaseItemDto> GetItemsToPlay(ThemeMediaResult result)
        {
            var items = result.Items.ToList();

            var i = 0;

            while (i < 5)
            {
                items.AddRange(result.Items);
                i++;
            }

            return items;
        }

        private Task Play(IEnumerable<BaseItemDto> items)
        {
            return _playback.Play(new PlayOptions
            {
                GoFullScreen = false,
                Items = items.ToList(),
                Shuffle = true,
                ShowLoadingAnimation = false
            });
        }

        private async Task<ThemeMediaResult> GetThemeMedia(string itemId)
        {
            var themeMediaResult = await _apiClient.GetAllThemeMediaAsync(_session.CurrentUser.Id, itemId, true, CancellationToken.None).ConfigureAwait(false);

            if (themeMediaResult.ThemeVideosResult.Items.Length > 0)
            {
                return themeMediaResult.ThemeVideosResult;
            }

            return themeMediaResult.ThemeSongsResult;
        }

        public void Dispose()
        {
            _nav.Navigated -= _nav_Navigated;
            _playback.PlaybackCompleted -= _playback_PlaybackCompleted;
        }
    }
}
