﻿using DirectShowLib;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Logging;
using MediaBrowser.Theater.Interfaces.Playback;
using MediaBrowser.Theater.Interfaces.Presentation;
using MediaFoundation;
using MediaFoundation.EVR;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace MediaBrowser.Theater.DirectShow
{
    public class DirectShowPlayer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IHiddenWindow _hiddenWindow;
        private InternalDirectShowPlayer _playerWrapper;

        private DirectShowLib.IGraphBuilder _graphBuilder;

        private DirectShowLib.IMediaControl _mediaControl;
        private DirectShowLib.IMediaEventEx _mediaEventEx;
        private DirectShowLib.IVideoWindow _videoWindow;
        private DirectShowLib.IBasicAudio _basicAudio;
        private DirectShowLib.IBasicVideo _basicVideo;
        private DirectShowLib.IMediaSeeking _mediaSeeking;
        private DirectShowLib.IMediaPosition _mediaPosition;
        private DirectShowLib.IBaseFilter _sourceFilter;
        private DirectShowLib.IFilterGraph2 _filterGraph;
        private DirectShowLib.IBaseFilter _pSource;

        private LAVAudio _lavaudio;
        private LAVVideo _lavvideo;

        // EVR filter
        private DirectShowLib.IBaseFilter _mPEvr;
        private IMFVideoDisplayControl _mPDisplay;
        private IMFVideoMixerControl _mPMixer;
        private IMFVideoPositionMapper _mPMapper;

        private DefaultAudioRenderer _defaultAudioRenderer;
        private ReclockAudioRenderer _reclockAudioRenderer;

        // Caps bits for IMediaSeeking
        private AMSeekingSeekingCapabilities _mSeekCaps;

        private readonly Control _owner;

        private BaseItemDto _item;

        public DirectShowPlayer(Control owner, ILogger logger, IHiddenWindow hiddenWindow, InternalDirectShowPlayer playerWrapper)
        {
            _owner = owner;
            _logger = logger;
            _hiddenWindow = hiddenWindow;
            _playerWrapper = playerWrapper;
        }

        private PlayState _playstate;
        public PlayState PlayState
        {
            get { return _playstate; }
            private set
            {
                _playstate = value;

                _playerWrapper.OnPlayStateChanged();
            }
        }

        public long? CurrentPositionTicks
        {
            get
            {
                if (_mediaSeeking != null)
                {
                    long pos;

                    var hr = _mediaSeeking.GetCurrentPosition(out pos);

                    return pos;
                }

                return null;
            }
        }

        public void Play(BaseItemDto item)
        {
            _item = item;

            Initialize(item.Path, false);

            var hr = _mediaControl.Run();
            DsError.ThrowExceptionForHR(hr);

            PlayState = PlayState.Playing;
        }

        private void InitializeGraph()
        {
            _graphBuilder = (DirectShowLib.IGraphBuilder)new FilterGraphNoThread();

            // QueryInterface for DirectShow interfaces
            _mediaControl = (DirectShowLib.IMediaControl)_graphBuilder;
            _mediaEventEx = (DirectShowLib.IMediaEventEx)_graphBuilder;
            _mediaSeeking = (DirectShowLib.IMediaSeeking)_graphBuilder;
            _mediaPosition = (DirectShowLib.IMediaPosition)_graphBuilder;

            // Query for video interfaces, which may not be relevant for audio files
            _videoWindow = _graphBuilder as DirectShowLib.IVideoWindow;
            _basicVideo = _graphBuilder as DirectShowLib.IBasicVideo;

            // Query for audio interfaces, which may not be relevant for video-only files
            _basicAudio = _graphBuilder as DirectShowLib.IBasicAudio;

            // Set up event notification.
            var hr = _mediaEventEx.SetNotifyWindow(_owner.Handle, 0x00008002, IntPtr.Zero);
            DsError.ThrowExceptionForHR(hr);
        }

        private void Initialize(string path, bool enableReclock)
        {
            InitializeGraph();

            var hr = _graphBuilder.AddSourceFilter(path, path, out _pSource);
            DsError.ThrowExceptionForHR(hr);

            // Try to render the streams.
            RenderStreams(_pSource, enableReclock);

            // Get the seeking capabilities.
            hr = _mediaSeeking.GetCapabilities(out _mSeekCaps);
            DsError.ThrowExceptionForHR(hr);
        }

        private void RenderStreams(DirectShowLib.IBaseFilter pSource, bool enableReclock)
        {
            int hr;

            _filterGraph = _graphBuilder as DirectShowLib.IFilterGraph2;
            if (_filterGraph == null)
            {
                throw new Exception("Could not QueryInterface for the IFilterGraph2");
            }

            // Add video renderer
            _mPEvr = (DirectShowLib.IBaseFilter)new EnhancedVideoRenderer();
            hr = _graphBuilder.AddFilter(_mPEvr, "EVR");
            DsError.ThrowExceptionForHR(hr);

            InitializeEvr(_mPEvr, 1, out _mPDisplay);

            SetVideoWindow();

            // Add audio renderer
            var useDefaultRenderer = true;

            if (enableReclock)
            {
                _reclockAudioRenderer = new ReclockAudioRenderer();
                var aRenderer = _reclockAudioRenderer as DirectShowLib.IBaseFilter;
                if (aRenderer != null)
                {
                    _graphBuilder.AddFilter(aRenderer, "Reclock Audio Renderer");
                    useDefaultRenderer = false;
                }
            }

            if (useDefaultRenderer)
            {
                _defaultAudioRenderer = new DefaultAudioRenderer();
                var aRenderer = _defaultAudioRenderer as DirectShowLib.IBaseFilter;
                if (aRenderer != null)
                {
                    _graphBuilder.AddFilter(aRenderer, "Default Audio Renderer");
                }
            }

            _lavvideo = new LAVVideo();
            var vlavvideo = _lavvideo as DirectShowLib.IBaseFilter;
            if (vlavvideo != null)
            {
                _graphBuilder.AddFilter(vlavvideo, "LAV Video Decoder");
            }

            _lavaudio = new LAVAudio();
            var vlavaudio = _lavaudio as DirectShowLib.IBaseFilter;
            if (vlavaudio != null)
            {
                _graphBuilder.AddFilter(vlavaudio, "LAV Audio Decoder");
            }

            DirectShowLib.IEnumPins pEnum;
            hr = pSource.EnumPins(out pEnum);
            DsError.ThrowExceptionForHR(hr);

            DirectShowLib.IPin[] pins = { null };

            /* Counter for how many pins successfully rendered */
            int pinsRendered = 0;
            /* Loop over each pin of the source filter */
            while (pEnum.Next(1, pins, IntPtr.Zero) == 0)
            {
                if (_filterGraph.RenderEx(pins[0], AMRenderExFlags.RenderToExistingRenderers, IntPtr.Zero) >= 0)
                    pinsRendered++;

                Marshal.ReleaseComObject(pins[0]);
            }

            if (pinsRendered == 0)
            {
                throw new Exception("Could not render any streams from the source Uri");
            }

            _logger.Info("Completed RenderStreams with {0} pins.", pinsRendered);
        }

        private void InitializeEvr(DirectShowLib.IBaseFilter pEvr, int dwStreams, out IMFVideoDisplayControl ppDisplay)
        {
            IMFVideoDisplayControl pDisplay;

            // Continue with the rest of the set-up.

            // Set the video window.
            object o;
            var pGetService = (IMFGetService)pEvr;
            pGetService.GetService(MFServices.MR_VIDEO_RENDER_SERVICE, typeof(IMFVideoDisplayControl).GUID, out o);

            try
            {
                pDisplay = (IMFVideoDisplayControl)o;
            }
            catch
            {
                Marshal.ReleaseComObject(o);
                throw;
            }

            // Set the number of streams.
            pDisplay.SetVideoWindow(_owner.Handle);

            if (dwStreams > 1)
            {
                var pConfig = (IEVRFilterConfig)pEvr;
                pConfig.SetNumberOfStreams(dwStreams);
            }

            // Return the IMFVideoDisplayControl pointer to the caller.
            ppDisplay = pDisplay;

            _mPMixer = null;
        }

        private void SetVideoWindow()
        {
            // Set the display position to the entire window.
            var rc = new MFRect(0, 0, Convert.ToInt32(_hiddenWindow.ContentWidth), Convert.ToInt32(_hiddenWindow.ContentHeight));

            _mPDisplay.SetVideoPosition(null, rc);

            _hiddenWindow.SizeChanged += _hiddenWindow_SizeChanged;
        }

        void _hiddenWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_mPDisplay != null)
            {
                var rc = new MFRect(0, 0, Convert.ToInt32(_hiddenWindow.ContentWidth), Convert.ToInt32(_hiddenWindow.ContentHeight));

                _mPDisplay.SetVideoPosition(null, rc);
            }
        }

        public void Pause()
        {
            if (_mediaControl == null)
                return;

            if (_mediaControl.Pause() >= 0)
                PlayState = PlayState.Paused;
        }

        public void Unpause()
        {
            if (_mediaControl == null)
                return;

            if (_mediaControl.Run() >= 0)
                PlayState = PlayState.Playing;
        }

        public void Stop()
        {
            var hr = 0;

            // Stop media playback
            if (_mediaControl != null)
                hr = _mediaControl.Stop();

            DsError.ThrowExceptionForHR(hr);

            OnStopped();
        }

        public void Seek(long ticks)
        {
            // In Directx time is measured in 100 nanoseconds. 
            var pos = new DsLong(ticks);

            long duration;

            var hr = _mediaSeeking.GetDuration(out duration);

            // Seek to the position
            hr = _mediaSeeking.SetPositions(new DsLong(ticks), AMSeekingSeekingFlags.AbsolutePositioning, new DsLong(duration), AMSeekingSeekingFlags.AbsolutePositioning);
        }

        private void OnStopped()
        {
            // Clear global flags
            PlayState = PlayState.Idle;

            _playerWrapper.OnPlaybackStopped(_item, CurrentPositionTicks);
        }

        private void HandleGraphEvent()
        {
            EventCode evCode;
            IntPtr evParam1, evParam2;

            // Make sure that we don't access the media event interface
            // after it has already been released.
            if (_mediaEventEx == null)
                return;

            // Process all queued events
            while (_mediaEventEx.GetEvent(out evCode, out evParam1, out evParam2, 0) == 0)
            {
                // Free memory associated with callback, since we're not using it
                var hr = _mediaEventEx.FreeEventParams(evCode, evParam1, evParam2);

                // If this is the end of the clip, close
                if (evCode == EventCode.Complete)
                {
                    OnStopped();
                }
            }
        }

        //protected override void WndProc(ref Message m)
        //{
        //    const int wmGraphNotify = 0x0400 + 13;

        //    switch (m.Msg)
        //    {
        //        case wmGraphNotify:
        //            {
        //                HandleGraphEvent();
        //                break;
        //            }
        //    }

        //    // Pass this message to the video window for notification of system changes
        //    if (_videoWindow != null)
        //        _videoWindow.NotifyOwnerMessage(m.HWnd, m.Msg, m.WParam, m.LParam);

        //    base.WndProc(ref m);
        //}

        private void CloseInterfaces()
        {
            _hiddenWindow.SizeChanged -= _hiddenWindow_SizeChanged;

            // Relinquish ownership (IMPORTANT!) after hiding video window
            var hr = _videoWindow.put_Visible(OABool.False);

            hr = _videoWindow.put_Owner(IntPtr.Zero);

            if (_mediaEventEx != null)
            {
                hr = _mediaEventEx.SetNotifyWindow(IntPtr.Zero, 0, IntPtr.Zero);
            }

            if (_lavaudio != null)
            {
                Marshal.ReleaseComObject(_lavaudio);
            }
            _lavaudio = null;

            if (_lavvideo != null)
            {
                Marshal.ReleaseComObject(_lavvideo);
            }
            _lavvideo = null;

            if (_defaultAudioRenderer != null)
            {
                Marshal.ReleaseComObject(_defaultAudioRenderer);
            }
            _defaultAudioRenderer = null;

            if (_reclockAudioRenderer != null)
            {
                Marshal.ReleaseComObject(_reclockAudioRenderer);
            }
            _reclockAudioRenderer = null;

            if (_filterGraph != null)
            {
                Marshal.ReleaseComObject(_filterGraph);
            }
            _filterGraph = null;

            if (_mPEvr != null)
            {
                Marshal.ReleaseComObject(_mPEvr);
            }
            _mPEvr = null;

            // Release and zero DirectShow interfaces
            _mediaEventEx = null;
            _mediaSeeking = null;
            _mediaPosition = null;
            _mediaControl = null;
            _basicAudio = null;
            _basicVideo = null;
            _videoWindow = null;

            if (_sourceFilter != null)
            {
                Marshal.ReleaseComObject(_sourceFilter);
            }

            _sourceFilter = null;

            if (_graphBuilder != null)
                Marshal.ReleaseComObject(_graphBuilder);

            _graphBuilder = null;
            _mSeekCaps = 0;

            GC.Collect();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool dispose)
        {
            CloseInterfaces();
        }
    }
}
