using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Input;

namespace WpfApplication1
{
    public partial class NavigationRedirectControl : UserControl
    {
        public HwndHost WpfHost { get; set; }

        public NavigationRedirectControl()
        {
            InitializeComponent();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (base.ProcessDialogKey(keyData))
                return true;
            if ((keyData & (Keys.Alt | Keys.Control)) == Keys.None)
            {
                Keys keyCode = (Keys)keyData & Keys.KeyCode;
                if (keyCode == Keys.Tab)
                {
                    IKeyboardInputSink sink = WpfHost;
                    return sink.KeyboardInputSite.OnNoMoreTabStops(
                      new TraversalRequest((keyData & Keys.Shift) == Keys.None ?
                        FocusNavigationDirection.Next : FocusNavigationDirection.Previous));
                }
            }
            return false;
        }
    }
}
