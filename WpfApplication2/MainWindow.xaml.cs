using System;
using System.Drawing.Printing;
using System.IO;
using System.IO.Packaging;
using System.Printing;
using System.Printing.Interop;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Xps.Packaging;

namespace WpfApplication2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        PageSettings _pageSettings;
        PrintQueue _queue;
        PrintTicket _ticket;

        public MainWindow()
        {
            _queue = LocalPrintServer.GetDefaultPrintQueue();
            _pageSettings = new PageSettings();
            _ticket = _queue.UserPrintTicket ?? _queue.DefaultPrintTicket ?? new PrintTicket();

            InitializeComponent();
        }

        #region Native PageSetupDlg related
        [Flags]
        enum PSD_
        {

            DEFAULTMINMARGINS = 0x00000000,

            DISABLEMARGINS = 0x00000010,

            DISABLEORIENTATION = 0x00000100,

            DISABLEPAGEPAINTING = 0x00080000,

            DISABLEPAPER = 0x00000200,

            DISABLEPRINTER = 0x00000020,

            ENABLEPAGEPAINTHOOK = 0x00040000,

            ENABLEPAGESETUPHOOK = 0x00002000,

            ENABLEPAGESETUPTEMPLATE = 0x00008000,

            ENABLEPAGESETUPTEMPLATEHANDLE = 0x00020000,

            INHUNDREDTHSOFMILLIMETERS = 0x00000008,

            INTHOUSANDTHSOFINCHES = 0x00000004,

            INWININIINTLMEASURE = 0x00000000,

            MARGINS = 0x00000002,

            MINMARGINS = 0x00000001,

            NONETWORKBUTTON = 0x00200000,

            NOWARNING = 0x00000080,

            RETURNDEFAULT = 0x00000400,

            SHOWHELP = 0x00000800,

        }

        #region POINT
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public POINT(System.Drawing.Point pt) : this(pt.X, pt.Y) { }

            public static implicit operator System.Drawing.Point(POINT p)
            {
                return new System.Drawing.Point(p.X, p.Y);
            }

            public static implicit operator POINT(System.Drawing.Point p)
            {
                return new POINT(p.X, p.Y);
            }
        }
        #endregion

        #region RECT
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public RECT(System.Drawing.Rectangle r) : this(r.Left, r.Top, r.Right, r.Bottom) { }

            public int X
            {
                get { return Left; }
                set { Right -= (Left - value); Left = value; }
            }

            public int Y
            {
                get { return Top; }
                set { Bottom -= (Top - value); Top = value; }
            }

            public int Height
            {
                get { return Bottom - Top; }
                set { Bottom = value + Top; }
            }

            public int Width
            {
                get { return Right - Left; }
                set { Right = value + Left; }
            }

            public System.Drawing.Point Location
            {
                get { return new System.Drawing.Point(Left, Top); }
                set { X = value.X; Y = value.Y; }
            }

            public System.Drawing.Size Size
            {
                get { return new System.Drawing.Size(Width, Height); }
                set { Width = value.Width; Height = value.Height; }
            }

            public static implicit operator System.Drawing.Rectangle(RECT r)
            {
                return new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
            }

            public static implicit operator RECT(System.Drawing.Rectangle r)
            {
                return new RECT(r);
            }

            public static bool operator ==(RECT r1, RECT r2)
            {
                return r1.Equals(r2);
            }

            public static bool operator !=(RECT r1, RECT r2)
            {
                return !r1.Equals(r2);
            }

            public bool Equals(RECT r)
            {
                return r.Left == Left && r.Top == Top && r.Right == Right && r.Bottom == Bottom;
            }

            public override bool Equals(object obj)
            {
                if (obj is RECT)
                    return Equals((RECT)obj);
                else if (obj is System.Drawing.Rectangle)
                    return Equals(new RECT((System.Drawing.Rectangle)obj));
                return false;
            }

            public override int GetHashCode()
            {
                return ((System.Drawing.Rectangle)this).GetHashCode();
            }

            public override string ToString()
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{{Left={0},Top={1},Right={2},Bottom={3}}}", Left, Top, Right, Bottom);
            }
        }
        #endregion

        #region PAGESETUPDLG_STRUCT
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PAGESETUPDLG_STRUCT
        {
            public int lStructSize;     // 00h
            public IntPtr hwndOwner;    // 08h
            public IntPtr hDevMode;     // 10h
            public IntPtr hDevNames;    // 18h
            public PSD_ Flags;          // 20h
            public POINT ptPaperSize;   // 28h
            public RECT rtMinMargin;    // 30h
            public RECT rtMargin;       // 40h
            public IntPtr hInstance;    // 50h
            public int lCustData;       // 58h
            public IntPtr lpfnPageSetupHook;        // 60h
            public IntPtr lpfnPagePaintHook;        // 68h
            public IntPtr lpPageSetupTemplateName;  // 70h
            public IntPtr hPageSetupTemplate;       // 78h
        }
        #endregion

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool PageSetupDlgW(ref PAGESETUPDLG_STRUCT lppsd);
        [DllImport("kernel32.dll")]
        static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalUnlock(IntPtr hMem);
        #endregion // Native PageSetupDlg related

        private void PageSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var ptc = new PrintTicketConverter(_queue.Name, PrintTicketConverter.MaxPrintSchemaVersion))
                {

                    //var psd = new System.Windows.Forms.PageSetupDialog();
                    //psd.PageSettings = _pageSettings;
                    //psd.PrinterSettings = new System.Drawing.Printing.PrinterSettings();
                    //if (psd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    //{
                    //    //_pageSettings = psd.PageSettings;
                    //    //_ticket.PageMediaSize = new PageMediaSize(_pageSettings.PaperSize.Width * 0.96, _pageSettings.PaperSize.Height * 0.96);
                    //}

                    var dMode = ptc.ConvertPrintTicketToDevMode(_ticket, BaseDevModeType.UserDefault, PrintTicketScope.PageScope);
                    IntPtr ptrDevMode = Marshal.AllocHGlobal(dMode.Length);
                    Marshal.Copy(dMode, 0, ptrDevMode, dMode.Length);

                    var wih = new WindowInteropHelper(this);
                    var pss = new PAGESETUPDLG_STRUCT()
                    {
                        lStructSize = Marshal.SizeOf(typeof(PAGESETUPDLG_STRUCT)),
                        hDevMode = ptrDevMode,
                        hwndOwner = wih.Handle,
                        Flags = PSD_.INTHOUSANDTHSOFINCHES,
                    };
                    if (PageSetupDlgW(ref pss))
                    {
                        if (pss.hDevMode != IntPtr.Zero)
                        {
                            var ptr = GlobalLock(pss.hDevMode);
                            // get actual size
                            int size = (int)Marshal.ReadInt16(ptr, 0x44) + (int)Marshal.ReadInt16(ptr, 0x46);
                            var data = new byte[size];
                            Marshal.Copy(ptr, data, 0, size);
                            _ticket = ptc.ConvertDevModeToPrintTicket(data);
                            GlobalUnlock(ptr);
                        }
                    }
                    if (pss.hDevMode != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(pss.hDevMode);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var pDialog = new PrintDialog();
            pDialog.PrintQueue = _queue;
            pDialog.PrintTicket = _ticket;
            if (pDialog.ShowDialog() == true)
            {
                FlowDocument flowDocumentCopy = GetFlowDocumentCopy();
                using (var fixDoc = new FixedDocumentHelper(flowDocumentCopy))
                {
                    pDialog.PrintDocument(fixDoc.FixedDocumentSequence.DocumentPaginator, "Test print job");
                }
            }
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var docview = new DocumentViewer();

            var wnd = new Window();
            wnd.Owner = this;
            wnd.Content = docview;

            FlowDocument flowDocumentCopy = GetFlowDocumentCopy();
            using (var fixDoc = new FixedDocumentHelper(flowDocumentCopy))
            {
                docview.Document = fixDoc.FixedDocumentSequence;
                wnd.ShowDialog();
            }
        }

        private FlowDocument GetFlowDocumentCopy()
        {
            var flowDocument = __flowDocViewer.Document;
            FlowDocument flowDocumentCopy;
            using (var ms = new System.IO.MemoryStream())
            {
                var source = new TextRange(flowDocument.ContentStart, flowDocument.ContentEnd);
                source.Save(ms, DataFormats.XamlPackage);
                flowDocumentCopy = new FlowDocument();
                var dest = new TextRange(flowDocumentCopy.ContentStart, flowDocumentCopy.ContentEnd);
                dest.Load(ms, DataFormats.XamlPackage);
            }
            __flowDocViewer.Document = flowDocument;

            flowDocumentCopy.PageWidth = _ticket.PageMediaSize.Width ?? 210 / 0.254 * 0.96;
            flowDocumentCopy.PageHeight = _ticket.PageMediaSize.Height ?? 297 / 0.254 * 0.96;
            return flowDocumentCopy;
        }

        #region FixedDocumentHelper
        class FixedDocumentHelper : IDisposable
        {
            MemoryStream _mstream;
            Package _package;
            Uri _uri;
            public FixedDocumentSequence FixedDocumentSequence { get; private set; }

            public FixedDocumentHelper(FlowDocument flowDocument)
            {
                _mstream = new MemoryStream();
                _package = Package.Open(_mstream, FileMode.Create, FileAccess.ReadWrite);

                string uriName = "urn:triomotion:xpsdoc:" + System.IO.Path.GetRandomFileName();
                _uri = new Uri(uriName);
                PackageStore.AddPackage(_uri, _package);

                using (var xpsDoc = new XpsDocument(_package, CompressionOption.Maximum, uriName))
                {
                    var writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
                    writer.Write(((IDocumentPaginatorSource)flowDocument).DocumentPaginator);
                    FixedDocumentSequence = xpsDoc.GetFixedDocumentSequence();
                }
            }

            public void Dispose()
            {
                if (_uri != null)
                {
                    PackageStore.RemovePackage(_uri);
                    _uri = null;
                }
                if (_package != null)
                {
                    _package.Close();
                    _package = null;
                }
                if (_mstream != null)
                {
                    _mstream.Dispose();
                    _mstream = null;
                }
            }
        }
        #endregion // FixedDocumentHelper
    }
}
