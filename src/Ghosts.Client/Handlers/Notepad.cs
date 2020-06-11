// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Domain;

namespace Ghosts.Client.Handlers
{
    public class Notepad : BaseHandler
    {
        //include FindWindowEx
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        //include SendMessage
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);

        //this is a constant indicating the window that we want to send a text message
        const int WM_SETTEXT = 0X000C;

        public Process notepadProccess;

        public Notepad(TimelineHandler handler)
        {
            //TODO - this class is just stubbed
            Process.Start("notepad.exe");
            Thread.Sleep(1000);
            this.notepadProccess = Process.GetProcessesByName("notepad")[0];

            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                if (timelineEvent.DelayBefore > 0)
                    Thread.Sleep(timelineEvent.DelayBefore);

                this.Paste(timelineEvent.Command);
                Thread.Sleep(1000);
                
                //this.Report(timelineEvent);

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(timelineEvent.DelayAfter);
            }
        }

        public void Paste(string text)
        {
            //getting notepad's textbox handle from the main window's handle
            //the textbox is called 'Edit'
            IntPtr notepadTextbox = FindWindowEx(notepadProccess.MainWindowHandle, IntPtr.Zero, "Edit", null);
            //sending the message to the textbox
            SendMessage(notepadTextbox, WM_SETTEXT, 0, text);
        }
    }
}