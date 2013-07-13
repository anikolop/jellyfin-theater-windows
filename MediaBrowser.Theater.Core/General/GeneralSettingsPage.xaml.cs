﻿using MediaBrowser.Model.Updates;
using MediaBrowser.Theater.Interfaces.Configuration;
using MediaBrowser.Theater.Presentation.Controls;
using MediaBrowser.Theater.Presentation.Pages;
using System;
using System.Collections.Generic;
using System.Windows;

namespace MediaBrowser.Theater.Core.General
{
    /// <summary>
    /// Interaction logic for GeneralSettingsPage.xaml
    /// </summary>
    public partial class GeneralSettingsPage : BasePage
    {
        private readonly ITheaterConfigurationManager _config;

        public GeneralSettingsPage(ITheaterConfigurationManager config)
        {
            _config = config;
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            SelectUpdateLevel.Options = new List<SelectListItem> 
            { 
                 new SelectListItem{ Text = "Dev", Value = PackageVersionClass.Dev.ToString()},
                 new SelectListItem{ Text = "Beta", Value = PackageVersionClass.Beta.ToString()},
                 new SelectListItem{ Text = "Official Release", Value = PackageVersionClass.Release.ToString()}
            };

            Loaded += GeneralSettingsPage_Loaded;
            Unloaded += GeneralSettingsPage_Unloaded;
        }

        void GeneralSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            SelectUpdateLevel.SelectedValue = _config.Configuration.SystemUpdateLevel.ToString();

            ChkAutoRun.IsChecked = _config.Configuration.RunAtStartup;
        }

        void GeneralSettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            PackageVersionClass updateLevel;

            if (Enum.TryParse(SelectUpdateLevel.SelectedValue, out updateLevel))
            {
                _config.Configuration.SystemUpdateLevel = updateLevel;
            }

            _config.Configuration.RunAtStartup = ChkAutoRun.IsChecked ?? false;

            _config.SaveConfiguration();
        }
    }
}
