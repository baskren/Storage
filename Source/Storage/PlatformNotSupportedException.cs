﻿using System;
using System.Runtime.CompilerServices;

namespace P42.Storage
{
    public class PlatformNotSupportedException : Exception
    {
        public PlatformNotSupportedException([CallerMemberName] string caller = null) : base ("Method ["+caller+"] is not supported in "+Xamarin.Essentials.DeviceInfo.Platform+".")
        {
            
        }
    }
}
