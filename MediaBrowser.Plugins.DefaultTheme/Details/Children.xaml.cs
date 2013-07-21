﻿using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.Session;
using MediaBrowser.Theater.Presentation.Controls;
using MediaBrowser.Theater.Presentation.ViewModels;
using System;
using System.Threading.Tasks;

namespace MediaBrowser.Plugins.DefaultTheme.Details
{
    /// <summary>
    /// Interaction logic for Children.xaml
    /// </summary>
    public partial class Children : BaseItemsControl
    {
        private readonly BaseItemDto _item;

        public Children(Model.Entities.DisplayPreferences displayPreferences, IApiClient apiClient, IImageManager imageManager, ISessionManager sessionManager, INavigationService navigationManager, IPresentationManager appWindow, BaseItemDto item)
            : base(displayPreferences, apiClient, imageManager, sessionManager, navigationManager, appWindow)
        {
            _item = item;
            InitializeComponent();
        }

        protected override ExtendedListBox ItemsList
        {
            get { return LstItems; }
        }

        protected override async void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            LstItems.ItemInvoked += LstItems_ItemInvoked;
        }

        void LstItems_ItemInvoked(object sender, ItemEventArgs<object> e)
        {
            var item = (BaseItemDtoViewModel)e.Argument;

            NavigationManager.NavigateToItem(item.Item, string.Empty);
        }

        protected override Task<ItemsResult> GetItemsAsync()
        {
            var query = new ItemQuery
                {
                    UserId = SessionManager.CurrentUser.Id,
                    Fields = new[]
                        {
                                 ItemFields.UserData,
                                 ItemFields.PrimaryImageAspectRatio,
                                 ItemFields.DateCreated,
                                 ItemFields.MediaStreams,
                                 ItemFields.Taglines,
                                 ItemFields.Genres,
                                 ItemFields.SeriesInfo,
                                 ItemFields.Overview,
                                 ItemFields.DisplayPreferencesId
                        },
                    ParentId = _item.Id,
                    SortBy = GetSortOrder()
                };

            return ApiClient.GetItemsAsync(query);
        }

        private string[] GetSortOrder()
        {
            if (_item.IsType("series"))
            {
                
            }

            return new[] {ItemSortBy.SortName};
        }

        protected override double GetImageDisplayHeight(Model.Entities.DisplayPreferences displayPreferences, double medianPrimaryImageAspectRatio)
        {
            if (medianPrimaryImageAspectRatio.Equals(1))
            {
                medianPrimaryImageAspectRatio = 1.777777777777778;
            }

            double height = displayPreferences.PrimaryImageWidth;

            return height / medianPrimaryImageAspectRatio;
        }

        protected override bool SetBackdropsOnCurrentItemChanged
        {
            get
            {
                return false;
            }
        }
    }
}
