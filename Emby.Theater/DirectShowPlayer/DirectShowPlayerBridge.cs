﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CoreAudioApi;
using Emby.Theater.DirectShow;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Emby.Theater.App;
using Emby.Theater.DirectShow.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using SocketHttpListener.Net;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace Emby.Theater.DirectShowPlayer
{
    public class DirectShowPlayerBridge
    {
        private readonly InternalDirectShowPlayer _player;
        private readonly IJsonSerializer _json;
        private readonly ILogger _logger;
        private bool _isVideo;

        public DirectShowPlayerBridge(ILogManager logManager
            , IApplicationPaths appPaths
            , IIsoManager isoManager
            , IZipClient zipClient
            , IHttpClient httpClient,
            IConfigurationManager configurationManager, IJsonSerializer json, Dispatcher context)
        {
            _json = json;
            _logger = logManager.GetLogger("DirectShowPlayerBridge");
            _player = new InternalDirectShowPlayer(logManager, appPaths, isoManager, zipClient, httpClient, configurationManager, context);
        }

        public void HandleWindowSizeChanged()
        {
            _player.HandleWindowSizeChanged();
        }

        public void Play(string path, long startPositionTicks, bool isVideo, MediaSourceInfo mediaSource, BaseItemDto item, bool isFullScreen, IntPtr videoWindowHandle)
        {
            _isFadingOut = false;
            _isVideo = isVideo;

            if (mediaSource.Protocol == MediaProtocol.File && mediaSource.VideoType.HasValue)
            {
                if (mediaSource.VideoType.Value == VideoType.VideoFile)
                {
                    if (File.Exists(mediaSource.Path) && !string.Equals(Path.GetExtension(mediaSource.Path), "dvr-ms", StringComparison.OrdinalIgnoreCase))
                    {
                        path = mediaSource.Path;
                    }
                }
                else if (mediaSource.VideoType.Value == VideoType.Iso)
                {
                    if (File.Exists(mediaSource.Path))
                    {
                        path = mediaSource.Path;
                    }
                }
                else
                {
                    if (Directory.Exists(mediaSource.Path))
                    {
                        path = GetFolderRipPath(mediaSource.VideoType.Value, mediaSource.Path);
                    }
                }
            }

            if (_player.PlayState != PlayState.Idle)
            {
                _player.Stop();
            }

            _logger.Info("Playing media source {0}", _json.SerializeToString(mediaSource));

            var config = new DirectShowPlayerConfiguration();

            config.VideoConfig.SetDefaults();
            config.AudioConfig.SetDefaults();
            config.SubtitleConfig.SetDefaults();

            _player.Play(path, startPositionTicks, isVideo, item, mediaSource, isFullScreen, videoWindowHandle, config);
        }

        private static string GetFolderRipPath(VideoType videoType, string root)
        {
            if (videoType == VideoType.BluRay)
            {
                return GetBlurayPath(root);
            }

            if (videoType == VideoType.Dvd)
            {
                var videoTsPath = Path.Combine(root, "video_ts");
                if (Directory.Exists(videoTsPath))
                {
                    return videoTsPath;
                }
            }

            return root;
        }

        private static string GetBlurayPath(string root)
        {
            var file = new DirectoryInfo(root)
                .EnumerateFiles("index.bdmv", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (file != null)
            {
                Uri uri;

                if (Uri.TryCreate(file.FullName, UriKind.RelativeOrAbsolute, out uri))
                {
                    return uri.OriginalString;
                }

                return file.FullName;
            }

            return root;
        }

        /// <summary>
        /// TODO: Notify JS
        /// </summary>
        private void OnError()
        {
        }

        public bool IsPaused()
        {
            return _player.PlayState == PlayState.Paused;
        }

        public void Pause()
        {
            _player.Pause();
        }

        private bool _isFadingOut = false;

        public void Stop(bool fade)
        {
            if (!_isVideo && fade)
            {
                _isFadingOut = true;

                for (int i = -100; i > -10000; i -= 20)
                {
                    if (!_isFadingOut)
                    {
                        break;
                    }

                    _player.SetVolume(i);
                    Thread.Sleep(1);
                }
                //reset volume just in case this graph gets reused
                _player.SetVolume(0);
            }
            // Make sure if system volume is altered, that it is restored when complete
            _player.Stop();
        }

        public void UnPause()
        {
            _player.UnPause();
        }

        public long? GetDurationTicks()
        {
            return _player.CurrentDurationTicks;
        }

        public bool IsMuted()
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                return audioDevice.AudioEndpointVolume.Mute;
            }

            return false;
        }

        public void SetMute(bool muted)
        {
            if (muted)
            {
                var audioDevice = AudioDevice;

                if (audioDevice != null)
                {
                    audioDevice.AudioEndpointVolume.Mute = true;
                }
            }
            else
            {
                var audioDevice = AudioDevice;

                if (audioDevice != null)
                {
                    audioDevice.AudioEndpointVolume.Mute = false;
                }
            }
        }

        public float GetVolume()
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                return audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100;
            }

            return 0;
        }

        public void SetVolume(float volume)
        {
            var audioDevice = AudioDevice;

            if (audioDevice != null)
            {
                audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100;
            }
        }

        public long? GetPositionTicks()
        {
            return _player.CurrentPositionTicks;
        }

        public void SetPositionTicks(long positionTicks)
        {
            _player.Seek(positionTicks);
        }

        private MMDevice _audioDevice;
        private MMDevice AudioDevice
        {
            get
            {
                EnsureAudioDevice();
                return _audioDevice;
            }
        }

        private bool _lastMuteValue;
        private float _lastVolumeValue;

        private void EnsureAudioDevice()
        {
            if (_audioDevice == null)
            {
                var devEnum = new MMDeviceEnumerator();

                try
                {
                    _audioDevice = devEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia);
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error attempting to discover default audio device", ex);
                    return;
                }

                _audioDevice.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;

                _lastMuteValue = _audioDevice.AudioEndpointVolume.Mute;
                _lastVolumeValue = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
            }
        }

        void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            OnVolumeChanged();
        }

        private void OnVolumeChanged()
        {
            if (_lastMuteValue != _audioDevice.AudioEndpointVolume.Mute)
            {
                _lastMuteValue = _audioDevice.AudioEndpointVolume.Mute;
                //EventHelper.FireEventIfNotNull(_volumeChanged, this, EventArgs.Empty, _logger);
            }
            else if (!_lastVolumeValue.Equals(_audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar))
            {
                _lastVolumeValue = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                //EventHelper.FireEventIfNotNull(_volumeChanged, this, EventArgs.Empty, _logger);
            }
        }

        public void ProcessRequest(HttpListenerContext context, string localPath)
        {
            ProcessRequestInternal(context, localPath);
        }

        private void ProcessRequestInternal(HttpListenerContext context, string localPath)
        {
            var command = localPath.Split('/').LastOrDefault();
            long? positionTicks = null;

            if (string.Equals(command, "getaudiodevices", StringComparison.OrdinalIgnoreCase))
            {
                var response = context.Response;
                var jsnString = _json.SerializeToString(AudioConfigurationUtils.GetAudioDevices());
                var bytes = Encoding.UTF8.GetBytes(jsnString);

                response.ContentType = "application/json";
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                return;
            }

            if (string.Equals(command, "play", StringComparison.OrdinalIgnoreCase))
            {
                var playRequest = _json.DeserializeFromStream<PlayRequest>(context.Request.InputStream);

                var playerWindowHandle = new IntPtr(long.Parse(playRequest.windowHandle, CultureInfo.InvariantCulture));

                Play(playRequest.url, playRequest.startPositionTicks ?? 0, playRequest.isVideo, playRequest.mediaSource, playRequest.item, playRequest.fullscreen, playerWindowHandle);
            }
            else if (string.Equals(command, "pause", StringComparison.OrdinalIgnoreCase))
            {
                Pause();
            }
            else if (string.Equals(command, "unpause", StringComparison.OrdinalIgnoreCase))
            {
                UnPause();
            }
            else if (string.Equals(command, "playpause", StringComparison.OrdinalIgnoreCase))
            {
                var currentState = _player.PlayState;
                if (currentState == PlayState.Paused)
                {
                    UnPause();
                }
                else if (currentState == PlayState.Playing)
                {
                    Pause();
                }
            }
            else if (string.Equals(command, "stopfade", StringComparison.OrdinalIgnoreCase))
            {
                positionTicks = GetPositionTicks();
                Stop(true);
            }
            else if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
            {
                positionTicks = GetPositionTicks();
                Stop(false);
            }
            else if (string.Equals(command, "mute", StringComparison.OrdinalIgnoreCase))
            {
                SetMute(true);
            }
            else if (string.Equals(command, "unmute", StringComparison.OrdinalIgnoreCase))
            {
                SetMute(false);
            }
            else if (string.Equals(command, "volume", StringComparison.OrdinalIgnoreCase))
            {
                float value;
                if (float.TryParse(context.Request.QueryString["val"], NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    SetVolume(value);
                }
            }
            else if (string.Equals(command, "positionticks", StringComparison.OrdinalIgnoreCase))
            {
                long value;
                if (long.TryParse(context.Request.QueryString["val"], NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    SetPositionTicks(value);
                }
                else
                {
                    _logger.Error("Unrecognized positionticks value {0}", context.Request.QueryString["val"]);
                }
            }
            else if (string.Equals(command, "setAudioStreamIndex", StringComparison.OrdinalIgnoreCase))
            {
                int value;
                if (int.TryParse(context.Request.QueryString["index"], NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    _player.SetAudioStreamIndex(value);
                }
                else
                {
                    _logger.Error("Unrecognized setAudioStreamIndex value {0}", context.Request.QueryString["index"]);
                }
            }
            else if (string.Equals(command, "setSubtitleStreamIndex", StringComparison.OrdinalIgnoreCase))
            {
                int value;
                if (int.TryParse(context.Request.QueryString["index"], NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    _player.SetSubtitleStreamIndex(value);
                }
                else
                {
                    _logger.Error("Unrecognized setSubtitleStreamIndex value {0}", context.Request.QueryString["index"]);
                }
            }

            SendResponse(context, positionTicks);
        }

        private void SendResponse(HttpListenerContext context, long? positionTicks)
        {
            var response = context.Response;

            var state = GetState();
            state.positionTicks = positionTicks ?? state.positionTicks;

            var bytes = Encoding.UTF8.GetBytes(_json.SerializeToString(state));

            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private PlayerState GetState()
        {
            var state = new PlayerState();

            state.isPaused = IsPaused();
            state.isMuted = IsMuted();
            state.volume = GetVolume();
            state.playstate = _player.PlayState.ToString().ToLower();

            if (_player.PlayState != PlayState.Idle)
            {
                state.positionTicks = GetPositionTicks();
                state.durationTicks = GetDurationTicks();
            }

            return state;
        }

        class PlayerState
        {
            public bool isPaused { get; set; }
            public bool isMuted { get; set; }
            public float volume { get; set; }
            public long? positionTicks { get; set; }
            public long? durationTicks { get; set; }
            public string playstate { get; set; }
        }

        class PlayRequest
        {
            public string url { get; set; }
            public bool isVideo { get; set; }
            public bool fullscreen { get; set; }
            public long? startPositionTicks { get; set; }
            public BaseItemDto item { get; set; }
            public MediaSourceInfo mediaSource { get; set; }
            public string windowHandle { get; set; }
        }
    }
}
