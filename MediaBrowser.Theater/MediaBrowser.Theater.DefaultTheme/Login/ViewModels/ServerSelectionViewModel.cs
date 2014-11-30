﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.ApiClient;
using MediaBrowser.Theater.Api.Navigation;
using MediaBrowser.Theater.Presentation.ViewModels;

namespace MediaBrowser.Theater.DefaultTheme.Login.ViewModels
{
    public class ServerSelectionViewModel
        : BaseViewModel
    {
        private readonly IConnectionManager _connectionManager;
        private readonly bool _enableFindServer;
        private readonly INavigator _navigator;
        private List<ServerInfo> _servers;

        public ServerSelectionViewModel(IConnectionManager connectionManager, INavigator navigator, IEnumerable<ServerInfo> servers = null)
        {
            _connectionManager = connectionManager;
            _navigator = navigator;
            _enableFindServer = servers == null;

            Servers = new RangeObservableCollection<ServerConnectionViewModel>();

            if (servers != null) {
                _servers = servers.ToList();
            }
        }

        public RangeObservableCollection<ServerConnectionViewModel> Servers { get; private set; }

        public override async Task Initialize()
        {
            if (_servers == null) {
                _servers = await _connectionManager.GetAvailableServers(CancellationToken.None);
            }

            Servers.Clear();
            Servers.AddRange(_servers.Select(s => new ServerConnectionViewModel(_connectionManager, _navigator, s)));

            if (_enableFindServer) {
                Servers.Add(new ServerConnectionViewModel(_connectionManager, _navigator, null));
            }

            await base.Initialize();
        }
    }
}