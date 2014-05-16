﻿using System.Windows.Forms;
using System.Windows.Input;
using MediaBrowser.Theater.Api.Commands;
using MediaBrowser.Theater.Api.UserInterface;
using MediaBrowser.Theater.Presentation;
using MediaBrowser.Theater.Presentation.ViewModels;

namespace MediaBrowser.Theater.DefaultTheme.Core.Commands
{
    public class SleepMenuCommand
        : IMenuCommand
    {
        public SleepMenuCommand( /*IPlaybackManager playbackManager*/)
        {
            ExecuteCommand = new RelayCommand(arg => {
                //playbackManager.StopAllPlayback();
                Application.SetSuspendState(PowerState.Suspend, false, false);
            });
        }

        public string DisplayName
        {
            get { return "MediaBrowser.Theater.DefaultTheme:Strings:Core_SleepCommand".Localize(); }
        }

        public ICommand ExecuteCommand { get; private set; }

        public IViewModel IconViewModel
        {
            get { return new SleepMenuCommandIconViewModel(); }
        }

        public MenuCommandGroup Group
        {
            get { return MenuCommandGroup.Power; }
        }

        public int SortOrder
        {
            get { return 20; }
        }
    }

    public class SleepMenuCommandIconViewModel
        : BaseViewModel { }
}