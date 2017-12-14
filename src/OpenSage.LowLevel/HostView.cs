﻿using System;
using LL.Graphics3D;
using LL.Input;

namespace OpenSage.LowLevel
{
    public partial class HostView
    {
        private GraphicsDevice GraphicsDevice => HostPlatform.GraphicsDevice;

        protected virtual void OnGraphicsInitialized(EventArgs args) { }

        protected virtual void OnGraphicsDraw(EventArgs args) { }

        protected virtual void OnGraphicsResized(EventArgs args) { }

        protected virtual void OnGraphicsUninitialized(EventArgs args) { }

        public KeyboardState GetKeyboardState() => PlatformGetKeyboardState();
        public MouseState GetMouseState() => PlatformGetMouseState();

        public void SetCursor(HostCursor cursor) => PlatformSetCursor(cursor);
    }
}
