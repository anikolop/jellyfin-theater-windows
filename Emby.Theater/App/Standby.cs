﻿using System;
using System.Runtime.InteropServices;

namespace Emby.Theater.App
{
    /// <summary>
    /// Class NativeApp
    /// </summary>
    public static class Standby
    {
        public static void PreventSystemStandby()
        {
            SystemHelper.ResetStandbyTimer();
        }

        [Flags]
        internal enum EXECUTION_STATE : uint
        {
            ES_NONE = 0,
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_USER_PRESENT = 0x00000004,
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000
        }

        public class SystemHelper
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

            public static void ResetStandbyTimer()
            {
                EXECUTION_STATE es = SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED);
            }
        }
    }
}
