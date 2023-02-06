
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ghosts.Client.Infrastructure
{
    /// <summary>
    /// Make various functions from winuser API here to make them accessible
    /// </summary>
    public class Winuser
    {
        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowExA(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetAncestor(IntPtr hWndChild, uint gaFlags);

        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetClassName(IntPtr hWnd, StringBuilder buffer, int bufsize);


        [DllImport("USER32.DLL", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }


    }
}
