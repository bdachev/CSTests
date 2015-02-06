using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace WpfApplication2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public enum CommonControls : uint
        {
            ICC_LISTVIEW_CLASSES = 0x00000001, // listview, header
            ICC_TREEVIEW_CLASSES = 0x00000002, // treeview, tooltips
            ICC_BAR_CLASSES = 0x00000004, // toolbar, statusbar, trackbar, tooltips
            ICC_TAB_CLASSES = 0x00000008, // tab, tooltips
            ICC_UPDOWN_CLASS = 0x00000010, // updown
            ICC_PROGRESS_CLASS = 0x00000020, // progress
            ICC_HOTKEY_CLASS = 0x00000040, // hotkey
            ICC_ANIMATE_CLASS = 0x00000080, // animate
            ICC_WIN95_CLASSES = 0x000000FF,
            ICC_DATE_CLASSES = 0x00000100, // month picker, date picker, time picker, updown
            ICC_USEREX_CLASSES = 0x00000200, // comboex
            ICC_COOL_CLASSES = 0x00000400, // rebar (coolbar) control
            ICC_INTERNET_CLASSES = 0x00000800,
            ICC_PAGESCROLLER_CLASS = 0x00001000,  // page scroller
            ICC_NATIVEFNTCTL_CLASS = 0x00002000,  // native font control
            ICC_STANDARD_CLASSES = 0x00004000,
            ICC_LINK_CLASS = 0x00008000
        }
        struct INITCOMMONCONTROLSEX
        {
            private int dwSize;
            public uint dwICC;

            public INITCOMMONCONTROLSEX(uint dwICC)
                : this()
            {
                dwSize = Marshal.SizeOf(typeof(INITCOMMONCONTROLSEX));
                this.dwICC = dwICC;
            }

            public INITCOMMONCONTROLSEX(CommonControls ICC)
                : this((uint)ICC)
            { }

            public CommonControls ICC { get { return (CommonControls)dwICC; } set { dwICC = (uint)value; } }
        }

        [DllImport("comctl32.dll", EntryPoint = "InitCommonControlsEx", CallingConvention = CallingConvention.StdCall)]
        static extern bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX iccex); 

        public App()
        {
            //var icce = new INITCOMMONCONTROLSEX(uint.MaxValue);
            //InitCommonControlsEx(ref icce);
            //System.Windows.Forms.Application.EnableVisualStyles();
        }
    }
}
