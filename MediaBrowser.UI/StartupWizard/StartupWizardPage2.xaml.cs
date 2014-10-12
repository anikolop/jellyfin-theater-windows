﻿using MediaBrowser.ApiInteraction;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Interfaces.System;
using MediaBrowser.Theater.Interfaces.Theming;
using MediaBrowser.Theater.Presentation.Pages;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace MediaBrowser.UI.StartupWizard
{
    /// <summary>
    /// Interaction logic for StartupWizardPage2.xaml
    /// </summary>
    public partial class StartupWizardPage2 : BasePage
    {
        private readonly IPresentationManager _presentation;
        private readonly INavigationService _nav;
        private readonly ITheaterConfigurationManager _config;
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger _logger;
        private readonly IMediaFilters _mediaFilters;

        private readonly CultureInfo _usCulture = new CultureInfo("en-US");

        public StartupWizardPage2(INavigationService nav, ITheaterConfigurationManager config, IConnectionManager connectionManager, IPresentationManager presentation, ILogger logger, IMediaFilters mediaFilters)
        {
            _nav = nav;
            _config = config;
            _connectionManager = connectionManager;
            _presentation = presentation;
            _logger = logger;
            _mediaFilters = mediaFilters;
            InitializeComponent();
        }

        protected override async void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            TxtHost.Text = string.Empty;
            TxtPort.Text = string.Empty;
            
            try
            {
                var result = (await new ServerLocator(_logger).FindServers(500, CancellationToken.None).ConfigureAwait(false)).FirstOrDefault();

                if (result != null)
                {
                    var uri = new Uri(result.Address);

                    TxtHost.Text = uri.Host;

                    if (!uri.IsDefaultPort)
                    {
                        TxtPort.Text = uri.Port.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error attempting to locate server.", ex);
            }

            Loaded += StartupWizardPage_Loaded;
            BtnNext.Click += BtnNext_Click;
            BtnBack.Click += BtnBack_Click;
        }

        async void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            await _nav.NavigateBack();
        }

        async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                var serverAddress = string.Format("http://{0}", TxtHost.Text);
                if (!string.IsNullOrEmpty(TxtPort.Text))
                {
                    serverAddress += ":" + TxtPort.Text;
                }

                try
                {
                    var connectionResult = await _connectionManager.Connect(serverAddress, CancellationToken.None);

                    if (connectionResult.State == ConnectionState.Unavailable)
                    {
                        ShowUnavailableMessage();
                        return;
                    }

                    await _nav.Navigate(new StartupWizardFinish(_nav, _presentation));
                }
                catch (Exception)
                {
                    ShowUnavailableMessage();
                }
            }
        }

        private void ShowUnavailableMessage()
        {
            _presentation.ShowMessage(new MessageBoxInfo
            {
                Button = MessageBoxButton.OK,
                Caption = "Error",
                Icon = MessageBoxIcon.Error,
                Text = "Unable to establish a connection with the server. Please check your connection information and try again."
            });
        }

        void StartupWizardPage_Loaded(object sender, RoutedEventArgs e)
        {
            _presentation.SetDefaultPageTitle();
        }

        private bool ValidateInput()
        {
            int port;

            if (!string.IsNullOrEmpty(TxtPort.Text) && !int.TryParse(TxtPort.Text, NumberStyles.Integer, _usCulture, out port))
            {
                TxtPort.Focus();

                _presentation.ShowMessage(new MessageBoxInfo
                {
                    Button = MessageBoxButton.OK,
                    Caption = "Error",
                    Icon = MessageBoxIcon.Error,
                    Text = "Please enter a valid port number."
                });

                return false;
            }

            return true;
        }
    }
}
