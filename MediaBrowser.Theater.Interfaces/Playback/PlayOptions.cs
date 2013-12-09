﻿using MediaBrowser.Model.Dto;
using MediaBrowser.Theater.Interfaces.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Theater.Interfaces.Playback
{
    /// <summary>
    /// Class PlayOptions
    /// </summary>
    public class PlayOptions
    {
        /// <summary>
        /// Gets or sets the items.
        /// </summary>
        /// <value>The items.</value>
        public List<BaseItemDto> Items { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [enable custom players].
        /// </summary>
        /// <value><c>true</c> if [enable custom players]; otherwise, <c>false</c>.</value>
        public bool EnableCustomPlayers { get; set; }

        /// <summary>
        /// If true, the PlayableItems will be shuffled before playback
        /// </summary>
        /// <value><c>true</c> if shuffle; otherwise, <c>false</c>.</value>
        public bool Shuffle { get; set; }

        /// <summary>
        /// If true, Playback will be resumed from the last known position
        /// </summary>
        /// <value><c>true</c> if resume; otherwise, <c>false</c>.</value>
        public bool Resume { get; set; }

        private long? _startPositionTicks;
        /// <summary>
        /// Gets or sets the start position ticks.
        /// </summary>
        /// <value>The start position ticks.</value>
        public long StartPositionTicks
        {
            get
            {
                if (_startPositionTicks.HasValue)
                {
                    return _startPositionTicks.Value;
                }

                if (Resume && Items.Count > 0)
                {
                    var item = Items[0];

                    if (item.UserData != null)
                    {
                        return item.UserData.PlaybackPositionTicks;
                    }
                }

                return 0;
            }
            set
            {
                _startPositionTicks = value;
            }
        }

        /// <summary>
        /// Holds the time that playback was started
        /// </summary>
        /// <value>The playback start time.</value>
        public DateTime PlaybackStartTime { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether [go full screen].
        /// </summary>
        /// <value><c>true</c> if [go full screen]; otherwise, <c>false</c>.</value>
        public bool GoFullScreen { get; set; }

        public PlayerConfiguration Configuration { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayOptions"/> class.
        /// </summary>
        public PlayOptions()
            : this(new BaseItemDto[] { })
        {
        }

        public PlayOptions(BaseItemDto item)
            : this(new[] { item })
        {

        }

        public PlayOptions(IEnumerable<BaseItemDto> items)
        {
            GoFullScreen = true;
            Items = items.ToList();
            EnableCustomPlayers = true;
        }
    }
}
