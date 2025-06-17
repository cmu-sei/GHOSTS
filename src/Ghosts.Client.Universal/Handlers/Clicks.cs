// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

public class Clicks(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override Task RunOnce()
    {
        var handler = this.Handler;

        return Task.Run(() =>
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                var pos = GetCursorPosition();
                DoLeftMouseClick(pos.X, pos.Y);

                _log.Trace($"Click: {pos.X}:{pos.Y}");
                Thread.Sleep(Jitter.Randomize(
                    timelineEvent.CommandArgs[0],
                    timelineEvent.CommandArgs[1],
                    timelineEvent.CommandArgs[2]
                ));
                Report(new ReportItem
                {
                    Handler = handler.HandlerType.ToString(),
                    Command = timelineEvent.Command,
                    Trackable = timelineEvent.TrackableId,
                    Result = $"{pos.X}:{pos.Y}"
                });

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(timelineEvent.DelayAfterActual);
            }
        }, this.Token);
    }

    private static void DoLeftMouseClick(int x, int y)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetCursorPos(x, y);
            mouse_event(MouseEventFlags.LEFTDOWN | MouseEventFlags.LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var display = XOpenDisplay(IntPtr.Zero);
            var root = XDefaultRootWindow(display);
            XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
            XFlush(display);
            XTestFakeButtonEvent(display, 1, true, 0);
            XTestFakeButtonEvent(display, 1, false, 0);
            XFlush(display);
            XCloseDisplay(display);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var loc = new CGPoint { x = x, y = y };
            CGWarpMouseCursorPosition(loc);
            var down = CGEventCreateMouseEvent(
                IntPtr.Zero,
                CGEventType.LeftMouseDown,
                loc,
                CGMouseButton.Left
            );
            var up = CGEventCreateMouseEvent(
                IntPtr.Zero,
                CGEventType.LeftMouseUp,
                loc,
                CGMouseButton.Left
            );
            CGEventPost(CGEventTapLocation.HID, down);
            CGEventPost(CGEventTapLocation.HID, up);
            CFRelease(down);
            CFRelease(up);
        }
    }

    private static Point GetCursorPosition()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            GetCursorPos(out POINT pt);
            return new Point(pt.X, pt.Y);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var display = XOpenDisplay(IntPtr.Zero);
            var root = XDefaultRootWindow(display);
            XQueryPointer(
                display,
                root,
                out IntPtr rootWin,
                out IntPtr childWin,
                out int rootX,
                out int rootY,
                out int winX,
                out int winY,
                out uint mask
            );
            XCloseDisplay(display);
            return new Point(rootX, rootY);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var loc = CGEventGetLocation(CGEventCreate(IntPtr.Zero));
            return new Point((int)loc.x, (int)loc.y);
        }

        throw new PlatformNotSupportedException("Unsupported OS for cursor position.");
    }

    // ──────────────────────── Windows ────────────────────────

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [Flags]
    private enum MouseEventFlags : uint
    {
        LEFTDOWN = 0x0002,
        LEFTUP = 0x0004
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    private static extern void mouse_event(MouseEventFlags dwFlags, uint dx, uint dy, uint cButtons,
        UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    // ──────────────────────── Linux ────────────────────────

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XWarpPointer(
        IntPtr display,
        IntPtr src_w,
        IntPtr dest_w,
        int src_x,
        int src_y,
        uint src_width,
        uint src_height,
        int dest_x,
        int dest_y
    );

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XQueryPointer(
        IntPtr display,
        IntPtr window,
        out IntPtr root_return,
        out IntPtr child_return,
        out int root_x_return,
        out int root_y_return,
        out int win_x_return,
        out int win_y_return,
        out uint mask_return
    );

    [DllImport("libXtst.so.6")]
    private static extern int XTestFakeButtonEvent(
        IntPtr display,
        uint button,
        bool is_press,
        ulong delay
    );

    // ──────────────────────── macOS ────────────────────────

    private enum CGEventType : uint
    {
        LeftMouseDown = 1,
        LeftMouseUp = 2
    }

    private enum CGMouseButton : uint
    {
        Left = 0
    }

    private enum CGEventTapLocation : uint
    {
        HID = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double x;
        public double y;
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGWarpMouseCursorPosition(CGPoint newPos);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGPoint CGEventGetLocation(IntPtr evt);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreateMouseEvent(
        IntPtr source,
        CGEventType mouseType,
        CGPoint mouseCursorPosition,
        CGMouseButton mouseButton
    );

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventPost(CGEventTapLocation tap, IntPtr evt);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}
