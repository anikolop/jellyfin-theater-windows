﻿using MediaBrowser.Common;
using MediaBrowser.Common.Updates;
using MediaBrowser.Model.Updates;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Presentation.Controls;
using MediaBrowser.Theater.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MediaBrowser.UI.Pages.Plugins
{
    /// <summary>
    /// Interaction logic for PluginCategory.xaml
    /// </summary>
    public partial class PluginCategory : UserControl
    {
        private readonly IEnumerable<PackageInfo> _packages;
        private readonly INavigationService _nav;
        private readonly IApplicationHost _appHost;
        private readonly IInstallationManager _installationManager;

        public PluginCategory(string name, IEnumerable<PackageInfo> packages, INavigationService nav, IApplicationHost appHost, IInstallationManager installationManager)
        {
            _packages = packages;
            _nav = nav;
            _appHost = appHost;
            _installationManager = installationManager;

            InitializeComponent();

            TxtName.Text = name;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            LstItems.ItemInvoked += LstItems_ItemInvoked;

            var items = new RangeObservableCollection<PackageInfoViewModel>();
            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(items);
            LstItems.ItemsSource = view;

            items.AddRange(_packages.Select(i => new PackageInfoViewModel
                {
                    Name = i.name,
                    ThumbUri = i.thumbImage,
                    DefaultImageVisibility = string.IsNullOrEmpty(i.thumbImage) ? Visibility.Visible : Visibility.Collapsed,
                    ThumbImageVisibility = !string.IsNullOrEmpty(i.thumbImage) ? Visibility.Visible : Visibility.Collapsed,
                    PackageInfo = i

                }));
        }

        async void LstItems_ItemInvoked(object sender, ItemEventArgs<object> e)
        {
            var packageInfo = (PackageInfoViewModel)e.Argument;

            await _nav.Navigate(new PackageInfoPage(packageInfo.PackageInfo, _appHost, _installationManager, _nav));
        }
    }

    public class PackageInfoViewModel
    {
        public string Name { get; set; }
        public string ThumbUri { get; set; }
        public Visibility DefaultImageVisibility { get; set; }
        public Visibility ThumbImageVisibility { get; set; }
        public PackageInfo PackageInfo { get; set; }
    }
}
