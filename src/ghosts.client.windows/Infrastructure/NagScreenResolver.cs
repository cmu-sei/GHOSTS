using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ghosts.Client.Infrastructure
{
    internal static class NagScreenResolver
    {

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass,
            string lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        const uint BM_CLICK = 0x00F5;

        internal static void Resolve()
        {
            Console.WriteLine("Watcher thread started.");
            var windowHandle = IntPtr.Zero;

            while (true)
            {
                windowHandle = FindWindow(null, "Outlook Redemption");

                if (windowHandle != IntPtr.Zero)
                {
                    var radioButtonHandle = FindWindowEx(windowHandle, IntPtr.Zero, "TRadioButton", "I agree");
                    var okButtonHandle = FindWindowEx(windowHandle, IntPtr.Zero, "TButton", "OK");

                    if (radioButtonHandle != IntPtr.Zero)
                    {
                        // Select the radio button
                        SendMessage(radioButtonHandle, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    }

                    if (okButtonHandle != IntPtr.Zero)
                    {
                        // Click the "OK" button
                        SendMessage(okButtonHandle, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                
                Console.WriteLine("Window not found, retrying in 1 second...");
                Thread.Sleep((int)(1000 * 1.5));
            }
        }
    }
}