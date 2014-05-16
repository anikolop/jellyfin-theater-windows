﻿
namespace MediaBrowser.Theater.Api.Playback
{
    /// <summary>
    /// Interface IAcceptsPlayCommand
    /// </summary>
    public interface IAcceptsPlayCommand
    {
        /// <summary>
        /// Handles the play command.
        /// </summary>
        void HandlePlayCommand();
    }
}
