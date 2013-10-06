﻿using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Plugins.DefaultTheme.UserProfileMenu;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Interfaces.ViewModels;
using System;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MediaBrowser.Plugins.DefaultTheme
{
    public class DefaultThemePageContentViewModel : PageContentViewModel
    {
        private readonly IApiClient _apiClient;
        private readonly IImageManager _imageManager;
        private readonly IPresentationManager _presentation;

        public ICommand UserCommand { get; private set; }
        public ICommand DisplayPreferencesCommand { get; private set; }

        public DefaultThemePageContentViewModel(INavigationService navigationService, ISessionManager sessionManager, IApiClient apiClient, IImageManager imageManager, IPresentationManager presentation, IPlaybackManager playbackManager)
            : base(navigationService, sessionManager, playbackManager)
        {
            _apiClient = apiClient;
            _imageManager = imageManager;
            _presentation = presentation;

            NavigationService.Navigated += NavigationService_Navigated;
            SessionManager.UserLoggedIn += SessionManager_UserLoggedIn;
            SessionManager.UserLoggedOut += SessionManager_UserLoggedOut;
            UserCommand = new RelayCommand(i => ShowUserMenu());

            DisplayPreferencesCommand = new RelayCommand(i => ShowDisplayPreferences());
        }

        void SessionManager_UserLoggedOut(object sender, EventArgs e)
        {
            RefreshHomeButton();
        }

        void SessionManager_UserLoggedIn(object sender, EventArgs e)
        {
            UpdateUserImage();
            RefreshHomeButton();
        }

        private async void UpdateUserImage()
        {
            var user = SessionManager.CurrentUser;

            if (user.HasPrimaryImage)
            {
                var imageUrl = _apiClient.GetUserImageUrl(user, new ImageOptions
                {
                    ImageType = ImageType.Primary
                });

                try
                {
                    UserImage = await _imageManager.GetRemoteBitmapAsync(imageUrl);

                    ShowDefaultUserImage = false;
                }
                catch (Exception ex)
                {
                    ShowDefaultUserImage = true;
                }
            }
            else
            {
                ShowDefaultUserImage = true;
            }
        }

        void NavigationService_Navigated(object sender, NavigationEventArgs e)
        {
            IsOnPageWithDisplayPreferences = e.NewPage is IHasDisplayPreferences;
            RefreshHomeButton();
        }

        private BitmapImage _userImage;
        public BitmapImage UserImage
        {
            get { return _userImage; }

            set
            {
                _userImage = value;

                OnPropertyChanged("UserImage");
            }
        }

        private bool _showDefaultUserImage;
        public bool ShowDefaultUserImage
        {
            get { return _showLogoImage; }

            set
            {
                var changed = _showDefaultUserImage != value;

                _showDefaultUserImage = value;

                if (changed)
                {
                    OnPropertyChanged("ShowDefaultUserImage");
                }
            }
        }

        private bool _showHomeButton;
        public bool ShowHomeButton
        {
            get { return _showHomeButton; }

            set
            {
                var changed = _showHomeButton != value;

                _showHomeButton = value;

                if (changed)
                {
                    OnPropertyChanged("ShowHomeButton");
                }
            }
        }

        private bool _showLogoImage;
        public bool ShowLogoImage
        {
            get { return _showLogoImage; }

            set
            {
                var changed = _showLogoImage != value;

                _showLogoImage = value;

                if (changed)
                {
                    OnPropertyChanged("ShowLogoImage");
                }
            }
        }
        
        private BitmapImage _logoImage;
        public BitmapImage LogoImage
        {
            get { return _logoImage; }

            set
            {
                _logoImage = value;

                OnPropertyChanged("LogoImage");
            }
        }

        private bool _isOnPageWithDisplayPreferences = false;
        public bool IsOnPageWithDisplayPreferences
        {
            get { return _isOnPageWithDisplayPreferences; }

            set
            {
                var changed = _isOnPageWithDisplayPreferences != value;

                _isOnPageWithDisplayPreferences = value;

                if (changed)
                {
                    OnPropertyChanged("IsOnPageWithDisplayPreferences");
                }
            }
        }
        
        private string _timeLeft;
        public string TimeLeft
        {
            get { return _timeLeft; }

            set
            {
                var changed = !string.Equals(_timeLeft, value);

                _timeLeft = value;

                if (changed)
                {
                    OnPropertyChanged("TimeLeft");
                }
            }
        }

        private string _timeRight;
        public string TimeRight
        {
            get { return _timeRight; }

            set
            {
                var changed = !string.Equals(_timeRight, value);

                _timeRight = value;

                if (changed)
                {
                    OnPropertyChanged("TimeRight");
                }
            }
        }

        private void RefreshHomeButton()
        {
            ShowHomeButton = SessionManager.CurrentUser != null && !(NavigationService.CurrentPage is IHomePage) && !(NavigationService.CurrentPage is ILoginPage);
        }

        private void ShowUserMenu()
        {
            new UserProfileWindow(SessionManager, _imageManager, _apiClient).ShowModal(_presentation.Window);
        }

        private void ShowDisplayPreferences()
        {
            var page = NavigationService.CurrentPage as IHasDisplayPreferences;

            if (page != null)
            {
                page.ShowDisplayPreferencesMenu();
            }
        }

        public async void SetPageTitle(BaseItemDto item)
        {
            if (item.HasLogo || !string.IsNullOrEmpty(item.ParentLogoItemId))
            {
                var url = _apiClient.GetLogoImageUrl(item, new ImageOptions
                {
                });

                try
                {
                    LogoImage = await _imageManager.GetRemoteBitmapAsync(url);

                    ShowDefaultPageTitle = false;
                    PageTitle = string.Empty;
                    ShowLogoImage = true;
                }
                catch
                {
                    SetPageTitleText(item);
                }
            }
            else
            {
                SetPageTitleText(item);
            }
        }

        private void SetPageTitleText(BaseItemDto item)
        {
            var title = item.Name;

            if (item.IsType("Season"))
            {
                title = item.SeriesName + " | " + item.Name;
            }
            else if (item.IsType("Episode"))
            {
                title = item.SeriesName;

                if (item.ParentIndexNumber.HasValue)
                {
                    title += " | " + string.Format("Season {0}", item.ParentIndexNumber.Value.ToString());
                }
            }
            else if (item.IsType("MusicAlbum"))
            {
                if (!string.IsNullOrEmpty(item.AlbumArtist))
                {
                    title = item.AlbumArtist + " | " + title;
                }
            }

            PageTitle = title;
            ShowDefaultPageTitle = string.IsNullOrEmpty(PageTitle);
            ShowLogoImage = false;
        }

        public override void OnPropertyChanged(string name)
        {
            base.OnPropertyChanged(name);

            if (string.Equals(name, "PageTitle"))
            {
                ShowLogoImage = false;

                if (!string.IsNullOrEmpty(PageTitle))
                {
                    ShowDefaultPageTitle = false;
                }
            }
            else if (string.Equals(name, "ShowDefaultPageTitle"))
            {
                if (ShowDefaultPageTitle)
                {
                    ShowLogoImage = false;
                    PageTitle = string.Empty;
                }
            }
            else if (string.Equals(name, "DateTime"))
            {
                UpdateTime();
            }
        }

        private void UpdateTime()
        {
            var now = DateTime;

            TimeLeft = now.ToString("h:mm");

            if (CultureInfo.CurrentCulture.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            {
                var time = now.ToString("t");
                var values = time.Split(' ');
                TimeRight = values[values.Length - 1].ToLower();
            }
            else
            {
                TimeRight = string.Empty;
            }
        }

        protected override void Dispose(bool dispose)
        {
            if (dispose)
            {
                NavigationService.Navigated -= NavigationService_Navigated;
                SessionManager.UserLoggedIn -= SessionManager_UserLoggedIn;
                SessionManager.UserLoggedOut -= SessionManager_UserLoggedOut;
            }
            
            base.Dispose(dispose);
        }
    }
}
