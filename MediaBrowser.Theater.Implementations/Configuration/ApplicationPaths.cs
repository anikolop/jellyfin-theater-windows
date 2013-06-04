﻿using MediaBrowser.Common.Implementations;

namespace MediaBrowser.Theater.Implementations.Configuration
{
    /// <summary>
    /// Class UIApplicationPaths
    /// </summary>
    public class ApplicationPaths : BaseApplicationPaths
    {
#if DEBUG
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationPaths" /> class.
        /// </summary>
        public ApplicationPaths()
            : base(true)
        {
        }
#else
        public UIApplicationPaths()
            : base(false)
        {
        }
#endif

    }
}
