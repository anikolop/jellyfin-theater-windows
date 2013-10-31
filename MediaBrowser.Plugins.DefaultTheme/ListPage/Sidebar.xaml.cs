﻿using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Theater.Presentation.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace MediaBrowser.Plugins.DefaultTheme.ListPage
{
    /// <summary>
    /// Interaction logic for Sidebar.xaml
    /// </summary>
    public partial class Sidebar : UserControl
    {
        /// <summary>
        /// The _item
        /// </summary>
        private ItemViewModel _viewModel;

        private ItemViewModel ViewModel
        {
            get
            {
                return _viewModel;
            }
            set
            {
                _viewModel = value;
                OnItemChanged();
            }
        }

        public Sidebar()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            DataContextChanged += ItemInfoFooter_DataContextChanged;

            ViewModel = DataContext as ItemViewModel;
        }

        void ItemInfoFooter_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ViewModel = DataContext as ItemViewModel;
        }

        /// <summary>
        /// Called when [item changed].
        /// </summary>
        protected void OnItemChanged()
        {
            if (ViewModel != null)
            {
                OnItemChanged(ViewModel, ViewModel.Item);
            }
        }

        private void OnItemChanged(ItemViewModel viewModel, BaseItemDto item)
        {
            UpdateLogo(viewModel, item);

            TxtGenres.Visibility = item.Genres.Count > 0 && !item.IsType("episode") && !item.IsType("season") ? Visibility.Visible : Visibility.Collapsed;

            TxtGenres.Text = string.Join(" / ", item.Genres.Take(3).ToArray());
        }

        private CancellationTokenSource _logoCancellationTokenSource;

        private async void UpdateLogo(ItemViewModel viewModel, BaseItemDto item)
        {
            DisposeLogoCancellationToken(_logoCancellationTokenSource, true);

            if (string.Equals(viewModel.ViewType, ListViewTypes.List))
            {
                PnlTitle.Visibility = Visibility.Visible;

                UpdateLogoForListView(viewModel, item);

                return;
            }

            if (item != null && (item.HasLogo || item.ParentLogoImageTag.HasValue))
            {
                var tokenSource = new CancellationTokenSource();

                _logoCancellationTokenSource = tokenSource;

                try
                {
                    var img = await viewModel.GetBitmapImageAsync(new ImageOptions
                    {
                        ImageType = ImageType.Logo

                    }, tokenSource.Token);

                    LogoImage.Source = img;

                    LogoImage.Visibility = Visibility.Visible;
                    LogoImage.HorizontalAlignment = HorizontalAlignment.Center;

                    // If the logo is owned by the current item, don't show the title
                    if (item.HasLogo)
                    {
                        PnlTitle.Visibility = Visibility.Collapsed;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    LogoImage.Visibility = Visibility.Collapsed;
                    PnlTitle.Visibility = Visibility.Visible;
                }
                finally
                {
                    DisposeLogoCancellationToken(tokenSource, false);
                }
            }
            else
            {
                LogoImage.Visibility = Visibility.Collapsed;
                PnlTitle.Visibility = Visibility.Visible;
            }
        }

        private async void UpdateLogoForListView(ItemViewModel viewModel, BaseItemDto item)
        {
            if (item != null && item.HasPrimaryImage)
            {
                var tokenSource = new CancellationTokenSource();

                _logoCancellationTokenSource = tokenSource;

                try
                {
                    var img = await viewModel.GetBitmapImageAsync(new ImageOptions
                    {
                        ImageType = ImageType.Primary

                    }, tokenSource.Token);

                    tokenSource.Token.ThrowIfCancellationRequested();

                    LogoImage.Source = img;

                    LogoImage.Visibility = Visibility.Visible;
                    LogoImage.HorizontalAlignment = HorizontalAlignment.Left;
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    LogoImage.Visibility = Visibility.Collapsed;
                }
                finally
                {
                    DisposeLogoCancellationToken(tokenSource, false);
                }
            }
            else
            {
                LogoImage.Visibility = Visibility.Collapsed;
            }
        }

        private void DisposeLogoCancellationToken(CancellationTokenSource current, bool cancel)
        {
            if (current == _logoCancellationTokenSource)
            {
                _logoCancellationTokenSource = null;
            }

            if (current != null)
            {
                if (cancel)
                {
                    try
                    {
                        current.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {

                    }
                }
                current.Dispose();
            }
        }
    }
}
