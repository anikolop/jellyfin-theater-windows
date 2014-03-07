﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Navigation;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaBrowser.Theater.Presentation.Playback;

namespace MediaBrowser.Theater.Implementations.Command
{
    
    public class CommandEventArgs
    {
        public Command Command;
        public Object Args;
        public Boolean Handled = false;
    }

    public delegate void ActionDelagate(Object sender, CommandEventArgs args);

    public class CommandActionMapping
    {
        public Command Command;
        public ActionDelagate Action;
        public Object Args;

        public CommandActionMapping(Command command, ActionDelagate action, Object args = null)
        {
            Command = command;
            Action = action;
            Args = args;
        }
    }

    public class CommandActionMap : List<CommandActionMapping>
    {
    }

    public class GlobalCommandActionMap
    {
        private readonly IPresentationManager _presenation;
        private readonly IPlaybackManager _playback;
        private readonly INavigationService _navigation;
        private readonly ILogger _logger;
        private readonly CommandActionMapping _nullCommandActionMapping;
        private readonly CommandActionMap _globalCommandActionMap;

        public GlobalCommandActionMap(IPresentationManager presenation, IPlaybackManager playback, INavigationService navigation, ILogManager logManager)
        {
            _presenation = presenation;
            _playback = playback;
            _navigation = navigation;
            _logger = logManager.GetLogger(GetType().Name);
            _globalCommandActionMap = CreateGlobalCommandActionMap();
            _nullCommandActionMapping = new CommandActionMapping(Command.Null, NullAction);
        }

        private CommandActionMap CreateGlobalCommandActionMap()
        {
            return new CommandActionMap
            {
                new CommandActionMapping( Command.Null,            NullAction),
                new CommandActionMapping( Command.Play,            Play),
                new CommandActionMapping( Command.PlayPause,       PlayPause),
                new CommandActionMapping( Command.Pause,           Pause),
                new CommandActionMapping( Command.TogglePause,     TogglePause),
                new CommandActionMapping( Command.Queue,           NullAction),
                new CommandActionMapping( Command.FastForward,     NullAction),
                new CommandActionMapping( Command.Rewind,          NullAction),
                new CommandActionMapping( Command.PlaySpeedRatio,  NullAction),
                new CommandActionMapping( Command.NextTrack,       NextTrackOrChapter),
                new CommandActionMapping( Command.PrevisousTrack,  PreviousTrackOrChapter),
                new CommandActionMapping( Command.Left,            NullAction),
                new CommandActionMapping( Command.Right,           NullAction),
                new CommandActionMapping( Command.Up,              NullAction),
                new CommandActionMapping( Command.PageUp,          NullAction),
                new CommandActionMapping( Command.PageDown,        NullAction),
                new CommandActionMapping( Command.FirstPage,       NullAction),
                new CommandActionMapping( Command.Rewind,          NullAction),
                new CommandActionMapping( Command.PlaySpeedRatio,  NullAction),
                new CommandActionMapping( Command.LastPage,        NullAction),
                new CommandActionMapping( Command.Select,          NullAction),
                new CommandActionMapping( Command.Back,            NullAction),
                new CommandActionMapping( Command.Forward,         NullAction),
                new CommandActionMapping( Command.GotoHome,        GotoHome),
                new CommandActionMapping( Command.GotoSearch,      GotoSearch),
                new CommandActionMapping( Command.GotoSettings,    GotoSettings),
                new CommandActionMapping( Command.GotoPage,        Stop),
                new CommandActionMapping( Command.Info,            Info),
                new CommandActionMapping( Command.SkipNext,        SkipForward,        60),    // skip forward 60  seconds, boxed arguments
                new CommandActionMapping( Command.SkipPrevious,    SkipBackward,       60),
                new CommandActionMapping( Command.Step,            SkipForward,        60),     
                new CommandActionMapping( Command.SmallStepForward,SkipForward,        10),
                new CommandActionMapping( Command.SmallStepBack,   SkipBackward,       10),
                new CommandActionMapping( Command.StepBack,        SkipBackward,       60),
                new CommandActionMapping( Command.BigStepFoward,   SkipForward,        300),
                new CommandActionMapping( Command.BigStepBack,     SkipBackward,       300),
                new CommandActionMapping( Command.FullScreen,      FullScreen),
                new CommandActionMapping( Command.MinimizeScreen,  MinimizeScreen),
                new CommandActionMapping( Command.RestoreScreen,   RestoreScreen),
                new CommandActionMapping( Command.ToggleFullScreen,ToggleFullscreen),
                new CommandActionMapping( Command.Volume,          NullAction),
                new CommandActionMapping( Command.VolumeUp,        NullAction),
                new CommandActionMapping( Command.VolumeDown,      NullAction),
                new CommandActionMapping( Command.VolumneOn,       NullAction),
                new CommandActionMapping( Command.VolumeOff,       NullAction),
                new CommandActionMapping( Command.VolumeMute,      NullAction),
                new CommandActionMapping( Command.Subtitles,       NullAction),
                new CommandActionMapping( Command.NextSubtitle,    NullAction),
                new CommandActionMapping( Command.AspectRatio,     NullAction),
                new CommandActionMapping( Command.OSD,             OSD)
            };
        }

        private CommandActionMapping MapCommand(Command command)
        {
            return _globalCommandActionMap.FirstOrDefault(a => a.Command == command) ?? _nullCommandActionMapping;
        }

        public Boolean ExecuteCommand(Command command)
        {
            var commandAction = MapCommand(command);
            return ExecuteCommandAction(commandAction);
        }

        private Boolean ExecuteCommandAction(CommandActionMapping commandActionMapping)
        {
            _logger.Debug("ExecuteCommandAction {0} {1}", commandActionMapping.Command, commandActionMapping.Args);
            var handled = false;

            try
            {
                var commandEventArgs = new CommandEventArgs()
                {
                    Args = commandActionMapping.Args,
                    Command = commandActionMapping.Command,
                    Handled = false
                };

                commandActionMapping.Action(this, commandEventArgs);
                handled = commandEventArgs.Handled;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error executing CommandAction", ex);
            }
            return handled;
        }

        private void NullAction(Object sender, CommandEventArgs args)
        {
            // do nothing
        }

        private IInternalMediaPlayer GetActiveInternalMediaPlayer()
        {
            return _playback.MediaPlayers.OfType<IInternalMediaPlayer>().FirstOrDefault(i => i.PlayState != PlayState.Idle);
        }

        private void Play(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                if (activePlayer.PlayState == PlayState.Paused)
                {
                    activePlayer.UnPause();
                }

                ShowFullscreenVideoOsd();
            }
            else
            {
                SendPlayCommandToPresentation();
            }
            args.Handled = true;
        }

        private void PlayPause(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                if (activePlayer.PlayState == PlayState.Paused)
                {
                    activePlayer.UnPause();
                }
                else
                {
                    activePlayer.Pause();
                }

                ShowFullscreenVideoOsd();
            }
            else
            {
                SendPlayCommandToPresentation();
            }
            args.Handled = true;
        }

        private void Pause(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null && activePlayer.PlayState != PlayState.Paused)
            {
                activePlayer.Pause();
                ShowFullscreenVideoOsd();
            }
            args.Handled = true;
        }

        private void TogglePause(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                if (activePlayer.PlayState == PlayState.Paused)
                {
                    activePlayer.UnPause();
                }
                else
                {
                    activePlayer.Pause();
                }

                ShowFullscreenVideoOsd();
            }
            args.Handled = true;
        }

        private void Close()
        {
            // ToDo - close application gracefully
        }

        private void SkipBackward(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();
            if (activePlayer != null)
            {
                if (args.Args != null)
                {
                    activePlayer.SkipBackward((int)args.Args);
                }
                else
                {
                    activePlayer.SkipBackward();
                }
                ShowFullscreenVideoOsd();
            }
            args.Handled = true;
        }

        private void SkipForward(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                if (args.Args != null)
                {
                    activePlayer.SkipForward((int)args.Args);
                }
                else
                {
                    activePlayer.SkipForward();
                }
                ShowFullscreenVideoOsd();
            }
            args.Handled = true;
        }

        private void SendPlayCommandToPresentation()
        {
            _presenation.Window.Dispatcher.InvokeAsync(() =>
            {
                var currentPage = _navigation.CurrentPage;

                var accepts = currentPage.DataContext as IAcceptsPlayCommand;

                if (accepts != null)
                {
                    accepts.HandlePlayCommand();
                }
            });
        }

        private void Stop(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                activePlayer.Stop();
            }
            args.Handled = true;
        }

        private void NextTrack(Object sender, CommandEventArgs args)
        {
            // TODO - NextTrack Audio
        }

        private void PreviousTrack(Object sender, CommandEventArgs args)
        {
            // TODO - NextTrack Audio
        }

        private void NextChapter(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                activePlayer.GoToNextChapter();
                ShowFullscreenVideoOsd();
            }
            args.Handled = true;
        }

        private void PreviousChapter(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                activePlayer.GoToPreviousChapter();
                ShowFullscreenVideoOsd();
            }
            args.Handled = true;
        }

        private void NextTrackOrChapter(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null && activePlayer.CurrentMedia != null && activePlayer.CurrentMedia.IsVideo)
            {
                NextChapter(sender, args);
            }
            else
            {
                NextTrack(sender, args);
            }
            args.Handled = true;
        }

        private void PreviousTrackOrChapter(Object sender, CommandEventArgs args)
        {
            var activePlayer = _playback.MediaPlayers
                .OfType<IInternalMediaPlayer>()
                .FirstOrDefault(i => i.PlayState != PlayState.Idle);

            if (activePlayer != null && activePlayer.CurrentMedia != null && activePlayer.CurrentMedia.IsVideo)
            {
                PreviousChapter(sender, args);
            }
            else
            {
                PreviousTrack(sender, args);
            }
            args.Handled = true;
        }

        private void OSD(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                ShowFullscreenVideoOsd();
            }
            args.Handled = true;
        }

        private void ShowFullscreenVideoOsd()
        {
            _presenation.Window.Dispatcher.InvokeAsync(ShowFullscreenVideoOsdInternal);
        }

        private void ShowFullscreenVideoOsdInternal()
        {
            var page = _navigation.CurrentPage as IFullscreenVideoPage;

            if (page != null)
            {
                page.ShowOnScreenDisplay();
            }
        }

        private void Info(Object sender, CommandEventArgs args)
        {
            var activePlayer = GetActiveInternalMediaPlayer();

            if (activePlayer != null)
            {
                ToggleInfoPanel();
            }
            args.Handled = true;

        }

        private void ToggleInfoPanel()
        {
            _presenation.Window.Dispatcher.InvokeAsync(ShowInfoPanelInternal);
        }

        private void ShowInfoPanelInternal()
        {
            var page = _navigation.CurrentPage as IFullscreenVideoPage;

            if (page != null)
            {
                page.ToggleInfoPanel();
            }
        }

        private void GotoHome(Object sender, CommandEventArgs args)
        {
            _navigation.NavigateToHomePage();
            args.Handled = true;
        }

        private void GotoSettings(Object sender, CommandEventArgs args)
        {
            _navigation.NavigateToSettingsPage();
            args.Handled = true;
        }

        private void GotoSearch(Object sender, CommandEventArgs args)
        {
            _navigation.NavigateToSearchPage();
            args.Handled = true;
        }

        private void YellowButton()
        {
        }

        private void BlueButton()
        {
        }

        private void RedButton()
        {
        }

        private void GreenButton()
        {
        }

        public class Interop
        {
            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            public static IntPtr GetWindowHandle(Window window)
            {
                return new WindowInteropHelper(window).Handle;
            }
        }

        public void EnsureApplicationWindowHasFocus()
        {
            IntPtr window = Interop.GetWindowHandle(_presenation.Window);
            IntPtr focused = Interop.GetForegroundWindow();
            if (window != focused)
            {
                Interop.SetForegroundWindow(window);
            }
        }

        public void FullScreen(Object sender, CommandEventArgs args)
        {
            _presenation.Window.WindowState = WindowState.Maximized;
            EnsureApplicationWindowHasFocus();
        }

        public void MinimizeScreen(Object sender, CommandEventArgs args)
        {
            _presenation.Window.WindowState = WindowState.Minimized;
            EnsureApplicationWindowHasFocus();
        }

        public void RestoreScreen(Object sender, CommandEventArgs args)
        {
            _presenation.Window.WindowState = WindowState.Normal;
            EnsureApplicationWindowHasFocus();
        }

        public void ToggleFullscreen(Object sender, CommandEventArgs args)
        {
            if (_presenation.Window.WindowState == WindowState.Maximized)
            {
                RestoreScreen(sender, args);
            }
            else
            {
                FullScreen(sender, args);
            }
        }
    }
}