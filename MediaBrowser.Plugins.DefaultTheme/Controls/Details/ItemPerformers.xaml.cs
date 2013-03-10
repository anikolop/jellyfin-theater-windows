﻿using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.UI;
using MediaBrowser.UI.ViewModels;
using System.Collections.ObjectModel;

namespace MediaBrowser.Plugins.DefaultTheme.Controls.Details
{
    /// <summary>
    /// Interaction logic for ItemPerformers.xaml
    /// </summary>
    public partial class ItemPerformers : BaseDetailsControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemPerformers" /> class.
        /// </summary>
        public ItemPerformers()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The _itemsResult
        /// </summary>
        private ItemsResult _itemsResult;
        /// <summary>
        /// Gets or sets the children of the Folder being displayed
        /// </summary>
        /// <value>The children.</value>
        public ItemsResult ItemsResult
        {
            get { return _itemsResult; }

            private set
            {
                _itemsResult = value;
                OnPropertyChanged("ItemsResult");

                Items = DtoBaseItemViewModel.GetObservableItems(ItemsResult.Items);

                double width = 300;
                double height = 300;

                if (Items.Count > 0)
                {
                    height = width / Items[0].AveragePrimaryImageAspectRatio;
                }

                foreach (var item in Items)
                {
                    item.ImageWidth = width;
                    item.ImageHeight = height;
                    item.ImageType = ImageType.Primary;
                }
            }
        }

        /// <summary>
        /// The _display children
        /// </summary>
        private ObservableCollection<DtoBaseItemViewModel> _items;
        /// <summary>
        /// Gets the actual children that should be displayed.
        /// Subclasses should bind to this, not ItemsResult.
        /// </summary>
        /// <value>The display children.</value>
        public ObservableCollection<DtoBaseItemViewModel> Items
        {
            get { return _items; }

            private set
            {
                _items = value;
                lstItems.ItemsSource = value;
                OnPropertyChanged("Items");
            }
        }

        /// <summary>
        /// Called when [item changed].
        /// </summary>
        protected override async void OnItemChanged()
        {
            ItemsResult = await App.Instance.ApiClient.GetAllPeopleAsync(new ItemsByNameQuery
            {
                ItemId = Item.Id,
                UserId = App.Instance.CurrentUser.Id,
                Fields = new[] { ItemFields.PrimaryImageAspectRatio }
            });
        }
    }
}
