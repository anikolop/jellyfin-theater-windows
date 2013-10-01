﻿using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using System;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace MediaBrowser.Theater.Interfaces.ViewModels
{
    public class PageContentViewModel : BaseViewModel, IDisposable
    {
        protected readonly INavigationService NavigationService;
        protected readonly ISessionManager SessionManager;
        protected readonly IPlaybackManager PlaybackManager;

        public ICommand HomeCommand { get; private set; }
        public ICommand FullscreenVideoCommand { get; private set; }
        public ICommand SettingsCommand { get; private set; }

        private readonly Timer _clockTimer;
        private readonly Dispatcher _dispatcher;

        public PageContentViewModel(INavigationService navigationService, ISessionManager sessionManager, IPlaybackManager playbackManager)
        {
            NavigationService = navigationService;
            SessionManager = sessionManager;
            PlaybackManager = playbackManager;

            NavigationService.Navigated += NavigationServiceNavigated;
            SessionManager.UserLoggedIn += SessionManagerUserLoggedIn;
            SessionManager.UserLoggedOut += SessionManagerUserLoggedOut;
            PlaybackManager.PlaybackStarted += PlaybackManager_PlaybackStarted;
            PlaybackManager.PlaybackCompleted += PlaybackManager_PlaybackCompleted;

            SettingsCommand = new RelayCommand(i => NavigationService.NavigateToSettingsPage());
            HomeCommand = new RelayCommand(i => NavigationService.NavigateToHomePage());
            FullscreenVideoCommand = new RelayCommand(i => NavigationService.NavigateToInternalPlayerPage());

            _dispatcher = Dispatcher.CurrentDispatcher;

            _clockTimer = new Timer(ClockTimerCallback, null, 0, 10000);
        }

        void PlaybackManager_PlaybackCompleted(object sender, PlaybackStopEventArgs e)
        {
            IsPlayingInternalVideo = false;
        }

        void PlaybackManager_PlaybackStarted(object sender, PlaybackStartEventArgs e)
        {
            IsPlayingInternalVideo = e.Player is IInternalMediaPlayer && e.Player is IVideoPlayer;
        }

        private void ClockTimerCallback(object state)
        {
            _dispatcher.InvokeAsync(() => OnPropertyChanged("DateTime"), DispatcherPriority.Background);
        }

        void SessionManagerUserLoggedOut(object sender, EventArgs e)
        {
            IsLoggedIn = false;
        }

        void SessionManagerUserLoggedIn(object sender, EventArgs e)
        {
            IsLoggedIn = true;
        }

        void NavigationServiceNavigated(object sender, NavigationEventArgs e)
        {
            IsOnHomePage = e.NewPage is IHomePage;
            IsOnFullscreenVideo = e.NewPage is IFullscreenVideoPage;
        }

        protected DateTime DateTime
        {
            get { return DateTime.Now; }
        }

        private bool _isLoggedIn = false;
        public bool IsLoggedIn
        {
            get { return _isLoggedIn; }

            set
            {
                var changed = _isLoggedIn != value;

                _isLoggedIn = value;

                if (changed)
                {
                    OnPropertyChanged("IsLoggedIn");
                }
            }
        }

        private bool _isOnHomePage = false;
        public bool IsOnHomePage
        {
            get { return _isOnHomePage; }

            set
            {
                var changed = _isOnHomePage != value;

                _isOnHomePage = value;

                if (changed)
                {
                    OnPropertyChanged("IsOnHomePage");
                }
            }
        }

        private bool _isOnFullscreenVideo = false;
        public bool IsOnFullscreenVideo
        {
            get { return _isOnHomePage; }

            set
            {
                var changed = _isOnFullscreenVideo != value;

                _isOnFullscreenVideo = value;

                if (changed)
                {
                    OnPropertyChanged("IsOnFullscreenVideo");
                }
            }
        }

        private bool _isPlayingInternalVideo = false;
        public bool IsPlayingInternalVideo
        {
            get { return _isPlayingInternalVideo; }

            set
            {
                var changed = _isPlayingInternalVideo != value;

                _isPlayingInternalVideo = value;

                if (changed)
                {
                    OnPropertyChanged("IsPlayingInternalVideo");
                }
            }
        }

        private bool _showGlobalContent = true;
        public bool ShowGlobalContent
        {
            get { return _showGlobalContent; }

            set
            {
                var changed = _showGlobalContent != value;

                _showGlobalContent = value;

                if (changed)
                {
                    OnPropertyChanged("ShowGlobalContent");
                }
            }
        }

        private bool _showDefaultPageTitle = true;
        public bool ShowDefaultPageTitle
        {
            get { return _showDefaultPageTitle; }

            set
            {
                var changed = _showDefaultPageTitle != value;

                _showDefaultPageTitle = value;

                if (changed)
                {
                    OnPropertyChanged("ShowDefaultPageTitle");
                }
            }
        }

        private string _pageTitle;
        public string PageTitle
        {
            get { return _pageTitle; }

            set
            {
                var changed = !string.Equals(_pageTitle, value);

                _pageTitle = value;

                if (changed)
                {
                    OnPropertyChanged("PageTitle");
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                _clockTimer.Dispose();

                NavigationService.Navigated -= NavigationServiceNavigated;
                SessionManager.UserLoggedIn -= SessionManagerUserLoggedIn;
                SessionManager.UserLoggedOut -= SessionManagerUserLoggedOut;
                PlaybackManager.PlaybackStarted -= PlaybackManager_PlaybackStarted;
                PlaybackManager.PlaybackCompleted -= PlaybackManager_PlaybackCompleted;
            }
        }
    }
}
