﻿using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Implementations.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Theater.Api.Configuration
{
    public class ConfigurationManager : BaseConfigurationManager, ITheaterConfigurationManager
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ConfigurationManager" /> class.
        /// </summary>
        /// <param name="applicationPaths">The application paths.</param>
        /// <param name="logManager">The log manager.</param>
        /// <param name="xmlSerializer">The XML serializer.</param>
        public ConfigurationManager(IApplicationPaths applicationPaths, ILogManager logManager, IXmlSerializer xmlSerializer)
            : base(applicationPaths, logManager, xmlSerializer) { }

        /// <summary>
        ///     Gets the type of the configuration.
        /// </summary>
        /// <value>The type of the configuration.</value>
        protected override Type ConfigurationType
        {
            get { return typeof (ApplicationConfiguration); }
        }

        /// <summary>
        ///     Gets the configuration.
        /// </summary>
        /// <value>The configuration.</value>
        public ApplicationConfiguration Configuration
        {
            get { return (ApplicationConfiguration) CommonConfiguration; }
        }
    }
}