﻿using MediaBrowser.Model.Dto;
using MediaBrowser.Theater.Interfaces.Presentation;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MediaBrowser.Theater.Interfaces.Theming
{
    /// <summary>
    /// Interface ITheme
    /// </summary>
    public interface ITheme
    {
        /// <summary>
        /// Gets the global resources.
        /// </summary>
        /// <returns>IEnumerable{ResourceDictionary}.</returns>
        IEnumerable<ResourceDictionary> GetGlobalResources();

        /// <summary>
        /// Gets the login page.
        /// </summary>
        /// <returns>Page.</returns>
        Page GetLoginPage();

        /// <summary>
        /// Gets the internal player page.
        /// </summary>
        /// <returns>Page.</returns>
        Page GetInternalPlayerPage();

        /// <summary>
        /// Gets the item page.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="context">The context.</param>
        /// <returns>Page.</returns>
        Page GetItemPage(BaseItemDto item, string context);

        /// <summary>
        /// Shows the default error message.
        /// </summary>
        void ShowDefaultErrorMessage();

        /// <summary>
        /// Shows the message.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>MessageBoxResult.</returns>
        MessageBoxResult ShowMessage(MessageBoxInfo options);

        /// <summary>
        /// Shows the notification.
        /// </summary>
        /// <param name="caption">The caption.</param>
        /// <param name="text">The text.</param>
        /// <param name="icon">The icon.</param>
        void ShowNotification(string caption, string text, BitmapImage icon);

        /// <summary>
        /// Sets the page title.
        /// </summary>
        /// <param name="title">The title.</param>
        void SetPageTitle(string title);

        /// <summary>
        /// Sets the default page title.
        /// </summary>
        void SetDefaultPageTitle();

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        string Name { get; }
    }
}
