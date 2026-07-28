using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace QuietReader
{
    enum ReaderMode { Account, Line, Hidden }
    enum ReadingViewMode { Immersive, Scrolling }
    enum CommandPageKind { None, Message, Bookshelf, Discovery, DiscoveryFilter, BookDetail, Catalog, Reading }
    sealed class MainForm : Form
    {
        const string HomeUrl = "https://www.qidian.com/";
        const string BookshelfUrl = "https://my.qidian.com/bookcase/";
        const string LoginUrl = "https://www.qidian.com/account/login";
        readonly Panel page = new Panel();
        readonly Panel workspace = new Panel();
        readonly Panel statusBar = new Panel();
        readonly Panel browserHost = new Panel();
        readonly FlowLayoutPanel pagedDocumentHost = new FlowLayoutPanel();
        readonly RichTextBox decoy = new RichTextBox();
        readonly Label status = new Label();
        readonly WebView2 browser = new WebView2();
        readonly Panel commandArea = new Panel();
        readonly TextBox commandInput = new TextBox();
        readonly ListBox commandSuggestions = new ListBox();
        readonly Panel guideOverlay = new Panel();
        readonly Panel guidePaper = new Panel();
        readonly RichTextBox guideText = new RichTextBox();
        readonly Button guideBackButton = new Button();
        readonly List<BookItem> bookshelf = new List<BookItem>();
        readonly List<BookItem> discoveryBooks = new List<BookItem>();
        readonly List<DiscoveryFilterGroup> discoveryFilterGroups = new List<DiscoveryFilterGroup>();
        readonly List<ChapterItem> chapters = new List<ChapterItem>();
        readonly Stack<NavigationState> navigationHistory = new Stack<NavigationState>();
        readonly List<Form> authenticationWindows = new List<Form>();
        readonly System.Windows.Forms.Timer bookshelfTimer = new System.Windows.Forms.Timer();
        readonly Label[] tabLabels = new Label[10];
        readonly ToolTip ribbonTips = new ToolTip();
        readonly ContextMenuStrip styleMenu = new ContextMenuStrip();
        readonly List<Button> styleCards = new List<Button>();
        readonly List<Panel> renderedPagePanels = new List<Panel>();
        readonly List<RichTextBox> renderedPageEditors = new List<RichTextBox>();
        Panel ribbonCommands;
        Panel clipboardGroup;
        Panel fontGroup;
        Panel paragraphGroup;
        Panel stylesGroup;
        Panel editingGroup;
        Panel addinsGroup;
        FlowLayoutPanel stylesGallery;
        Panel ribbonRoot;
        Panel titleBar;
        ComboBox readingSpeedSelector;
        ComboBox readingViewSelector;
        TrackBar zoomSlider;
        Label zoomLabel;
        Button homeButton;
        Button bookshelfButton;
        Button selectButton;
        Button backButton;
        Button forwardButton;
        Button refreshButton;
        Button accountButton;
        Button lineButton;
        Button hideButton;
        Button languageButton;
        Button noviceGuideButton;
        Button minimizeButton;
        Button maximizeButton;
        Button closeButton;
        Button subscribeButton;
        Button styleUpButton;
        Button styleDownButton;
        Button styleMoreButton;
        CheckBox typingScroll;
        Label shortcutHint;
        ReaderMode mode = ReaderMode.Hidden;
        ReadingViewMode readingViewMode = ReadingViewMode.Immersive;
        bool ready;
        bool chinese = true;
        bool showingCommandHint;
        bool loginFlowActive;
        bool loginCompleted;
        bool loadingBookshelf;
        bool extractingBookshelf;
        bool openingBook;
        bool loadingDiscovery;
        bool extractingDiscovery;
        bool discoveryFilterRequested;
        bool discoveryIsRanking;
        bool appendingDiscoveryPage;
        bool loadingBookDetail;
        bool extractingBookDetail;
        bool startReadingAfterCatalog;
        bool loadingCatalog;
        bool extractingCatalog;
        bool openingChapter;
        bool preparingChapter;
        bool readingActive;
        bool chapterEnded;
        bool ocrReadingActive;
        bool ocrBusy;
        bool ocrPrefetching;
        bool updatingRibbonSelectors;
        bool updatingZoom;
        bool keepBrowserRunningBehindDocument;
        bool subscribingChapter;
        bool subscriptionAttemptPending;
        bool guideBrowserWasVisible;
        Control guideReturnFocus;
        readonly bool showReaderStatusDetails = false;
        DateTime bookshelfStartedAt;
        int bookshelfOperationId;
        int bookshelfNavigationRetries;
        int loginOperationId;
        int discoveryOperationId;
        int bookDetailOperationId;
        int discoveryFilterGroupIndex;
        int discoveryDocumentPageIndex;
        int discoveryPageCountBeforeAppend;
        int discoveryRemotePage = 1;
        int discoveryRemotePageMax = 1;
        int catalogOperationId;
        int chapterOperationId;
        int catalogCollectedCount;
        int ocrOperationId;
        int currentChapterIndex = -1;
        int linesPerKey = 1;
        int charactersPerKey;
        int documentZoom = 100;
        int renderedPageCount = 1;
        int styleCardOffset;
        string bookshelfStage = String.Empty;
        string bookshelfNotice = String.Empty;
        string discoveryTitle = String.Empty;
        string discoveryUrl = String.Empty;
        string discoveryNextPageUrl = String.Empty;
        string loginCookieSummary = String.Empty;
        BookItem selectedBook;
        BookDetail currentBookDetail;
        CommandPageKind currentPageKind = CommandPageKind.None;
        string currentChapterTitle = String.Empty;
        string previousChapterUrl = String.Empty;
        string nextChapterUrl = String.Empty;
        string currentCatalogUrl = String.Empty;
        string catalogStage = String.Empty;
        readonly List<string> ocrLines = new List<string>();
        readonly SortedDictionary<int, string> ocrParagraphs = new SortedDictionary<int, string>();
        readonly List<OcrPageMarker> ocrPageMarkers = new List<OcrPageMarker>();
        int ocrRevealedLineCount;
        int ocrRevealedCharacterCount;
        bool ocrPageEnded;
        OcrEngine ocrEngine;
        Process externalOcrProcess;
        readonly object externalOcrLock = new object();
        readonly SemaphoreSlim externalOcrSemaphore = new SemaphoreSlim(1, 1);
        string ocrProvider = "Windows OCR";
        const int OcrCachePageLimit = 4;
        const int EstimatedCharactersPerLine = 22;
        const int BasePageWidth = 680;
        const int BasePageHeight = 962;
        static readonly int[] SpeedCharacterValues = { 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 30, 50, 100 };
        const int WmNcHitTest = 0x0084;
        const int WmNcLButtonDown = 0x00A1;
        const int HtCaption = 2;
        const int HtLeft = 10;
        const int HtRight = 11;
        const int HtTop = 12;
        const int HtTopLeft = 13;
        const int HtTopRight = 14;
        const int HtBottom = 15;
        const int HtBottomLeft = 16;
        const int HtBottomRight = 17;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, IntPtr data);

        sealed class BookItem
        {
            public string Title { get; set; }
            public string BookUrl { get; set; }
            public string ProgressTitle { get; set; }
            public string ProgressUrl { get; set; }
            public string Author { get; set; }
            public string Category { get; set; }
            public string Status { get; set; }
            public string Intro { get; set; }
            public string LatestChapter { get; set; }
            public string Extra { get; set; }
        }
        sealed class BookDetail
        {
            public string Title { get; set; }
            public string BookUrl { get; set; }
            public string Author { get; set; }
            public string AuthorUrl { get; set; }
            public string AuthorInfo { get; set; }
            public string Intro { get; set; }
            public string Category { get; set; }
            public string Status { get; set; }
            public string WordCount { get; set; }
            public string TotalRecommendations { get; set; }
            public string WeeklyRecommendations { get; set; }
            public string Achievements { get; set; }
            public string LatestChapter { get; set; }
            public string LatestUpdate { get; set; }
            public string CatalogUrl { get; set; }
            public string ReadUrl { get; set; }
            public bool InBookshelf { get; set; }
            public string Error { get; set; }
        }
        sealed class DiscoveryProbe
        {
            public BookItem[] Items { get; set; }
            public DiscoveryFilterGroup[] Groups { get; set; }
            public string PageTitle { get; set; }
            public string NextPageUrl { get; set; }
            public int CurrentPage { get; set; }
            public int PageMax { get; set; }
            public string Error { get; set; }
        }
        sealed class DiscoveryFilterGroup
        {
            public string Title { get; set; }
            public DiscoveryFilterOption[] Options { get; set; }
        }
        sealed class DiscoveryFilterOption
        {
            public string Text { get; set; }
            public string Url { get; set; }
            public bool Selected { get; set; }
        }
        sealed class NavigationState
        {
            public CommandPageKind PageKind { get; set; }
            public List<BookItem> DiscoveryBooks { get; set; }
            public List<DiscoveryFilterGroup> DiscoveryFilterGroups { get; set; }
            public List<ChapterItem> Chapters { get; set; }
            public BookItem SelectedBook { get; set; }
            public BookDetail BookDetail { get; set; }
            public string DiscoveryTitle { get; set; }
            public string DiscoveryUrl { get; set; }
            public int CurrentChapterIndex { get; set; }
            public int DiscoveryFilterGroupIndex { get; set; }
            public bool DiscoveryFilterRequested { get; set; }
            public bool DiscoveryIsRanking { get; set; }
            public int DiscoveryDocumentPageIndex { get; set; }
            public string DiscoveryNextPageUrl { get; set; }
            public int DiscoveryRemotePage { get; set; }
            public int DiscoveryRemotePageMax { get; set; }
        }
        sealed class ChapterItem
        {
            public string Title { get; set; }
            public string Url { get; set; }
            public bool IsVip { get; set; }
        }
        sealed class CatalogProbe
        {
            public ChapterItem[] Items { get; set; }
            public string ProgressUrl { get; set; }
            public string BookTitle { get; set; }
            public bool IsLogin { get; set; }
            public string NextPageUrl { get; set; }
            public string[] RangeKeys { get; set; }
            public string ActiveRange { get; set; }
            public int ExpectedCount { get; set; }
            public string Error { get; set; }
        }
        sealed class CatalogDomState
        {
            public int Count { get; set; }
            public bool Clicked { get; set; }
        }
        sealed class ChapterProbe
        {
            public string Title { get; set; }
            public string PreviousUrl { get; set; }
            public string NextUrl { get; set; }
            public string CatalogUrl { get; set; }
            public bool IsLocked { get; set; }
            public bool HasReader { get; set; }
        }
        sealed class SubscriptionProbe
        {
            public bool CanSubscribe { get; set; }
            public bool IsLocked { get; set; }
            public bool HasReader { get; set; }
            public bool ConfirmVisible { get; set; }
            public bool ConfirmClicked { get; set; }
            public string Detail { get; set; }
            public string Error { get; set; }
        }
        sealed class ReadingAdvanceResult
        {
            public bool Ended { get; set; }
        }
        sealed class ReadingViewportResult
        {
            public bool Ended { get; set; }
            public bool Moved { get; set; }
            public double ScrollY { get; set; }
        }
        sealed class ExternalOcrResponse
        {
            public int imageWidth { get; set; }
            public int imageHeight { get; set; }
            public ExternalOcrBlock[] blocks { get; set; }
            public string error { get; set; }
        }
        sealed class ExternalOcrBlock
        {
            public string text { get; set; }
            public double left { get; set; }
            public double top { get; set; }
            public double right { get; set; }
            public double bottom { get; set; }
        }
        sealed class OcrViewportGeometry
        {
            public double ViewportWidth { get; set; }
            public double ViewportHeight { get; set; }
            public double ScrollY { get; set; }
            public bool Ended { get; set; }
            public OcrParagraphRect[] Paragraphs { get; set; }
        }
        sealed class OcrParagraphRect
        {
            public int Index { get; set; }
            public double Left { get; set; }
            public double Top { get; set; }
            public double Right { get; set; }
            public double Bottom { get; set; }
        }
        sealed class OcrPageMarker
        {
            public int EndCharacterCount { get; set; }
            public double ScrollY { get; set; }
        }
        sealed class BookshelfProbe
        {
            public BookItem[] Items { get; set; }
            public string Title { get; set; }
            public string Text { get; set; }
            public string ReadyState { get; set; }
            public int AnchorCount { get; set; }
            public bool IsChallenge { get; set; }
            public bool IsLogin { get; set; }
        }
        sealed class DevToolsCookieResponse
        {
            public DevToolsCookie[] cookies { get; set; }
        }
        sealed class DevToolsCookie
        {
            public string name { get; set; }
            public string domain { get; set; }
            public string path { get; set; }
        }
        public MainForm()
        {
            Text = "Document1 - Word";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(35, 64, 112);
            Padding = new Padding(1);
            Width = 1260;
            Height = 820;
            MinimumSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            Font = new Font("Segoe UI", 9F);
            Panel ribbon = BuildRibbon();
            status.Dock = DockStyle.Bottom;
            status.Height = 24;
            status.Padding = new Padding(12, 4, 0, 0);
            status.BackColor = Color.FromArgb(245, 245, 245);
            status.Text = "Page 1 of 1    Words: 286    English (United States)";
            workspace.Dock = DockStyle.Fill;
            workspace.BackColor = Color.FromArgb(232, 232, 232);
            workspace.AutoScroll = true;
            page.Dock = DockStyle.None;
            page.BackColor = Color.White;
            page.BorderStyle = BorderStyle.FixedSingle;
            page.Padding = new Padding(64, 52, 64, 52);
            pagedDocumentHost.BackColor = workspace.BackColor;
            pagedDocumentHost.AutoScroll = true;
            pagedDocumentHost.FlowDirection = FlowDirection.TopDown;
            pagedDocumentHost.WrapContents = false;
            pagedDocumentHost.Visible = false;
            workspace.Controls.Add(pagedDocumentHost);
            workspace.Controls.Add(page);
            Controls.Add(workspace);
            statusBar.Dock = DockStyle.Bottom;
            statusBar.Height = 24;
            statusBar.BackColor = Color.FromArgb(247, 247, 247);
            status.Dock = DockStyle.Fill;
            status.Height = 24;
            status.Padding = new Padding(10, 4, 0, 0);
            Panel zoomControls = new Panel { Dock = DockStyle.Right, Width = 258, BackColor = statusBar.BackColor };
            Label viewState = new Label { Dock = DockStyle.Left, Width = 62, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(80, 80, 80), Text = "▣   ▤" };
            Button zoomOut = new Button { Location = new Point(64, 2), Size = new Size(24, 20), FlatStyle = FlatStyle.Flat, Text = "−", BackColor = statusBar.BackColor };
            zoomOut.FlatAppearance.BorderSize = 0;
            zoomOut.Click += delegate { SetDocumentZoom(documentZoom - 10); };
            zoomSlider = new TrackBar { Location = new Point(88, 1), Size = new Size(104, 22), Minimum = 50, Maximum = 200, TickStyle = TickStyle.None, SmallChange = 10, LargeChange = 10, Value = documentZoom };
            zoomSlider.Scroll += delegate { if (!updatingZoom) SetDocumentZoom(zoomSlider.Value); };
            Button zoomIn = new Button { Location = new Point(193, 2), Size = new Size(24, 20), FlatStyle = FlatStyle.Flat, Text = "+", BackColor = statusBar.BackColor };
            zoomIn.FlatAppearance.BorderSize = 0;
            zoomIn.Click += delegate { SetDocumentZoom(documentZoom + 10); };
            zoomLabel = new Label { Location = new Point(218, 0), Size = new Size(40, 24), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 80, 80), Text = "100%" };
            zoomControls.Controls.Add(viewState);
            zoomControls.Controls.Add(zoomOut);
            zoomControls.Controls.Add(zoomSlider);
            zoomControls.Controls.Add(zoomIn);
            zoomControls.Controls.Add(zoomLabel);
            statusBar.Controls.Add(status);
            statusBar.Controls.Add(zoomControls);
            Controls.Add(statusBar);
            Controls.Add(ribbon);
            BuildGuideOverlay();
            decoy.Dock = DockStyle.None;
            decoy.BorderStyle = BorderStyle.None;
            decoy.BackColor = Color.White;
            decoy.Font = new Font("Calibri", 11F);
            decoy.ReadOnly = true;
            decoy.DetectUrls = false;
            decoy.ScrollBars = RichTextBoxScrollBars.None;
            decoy.MouseWheel += OnDocumentMouseWheel;
            decoy.Text = "Quarterly Operations Review\n\nExecutive summary\n\n" +
                "This document consolidates the current operating plan, key milestones, and follow-up items for the next review cycle. The working group will continue to validate assumptions, resolve dependencies, and maintain a clear record of decisions.\n\n" +
                "1. Delivery overview\n\nThe delivery schedule remains aligned with the approved baseline. Teams should update risks before the weekly checkpoint and identify any changes that may affect scope, timing, or quality.\n\n" +
                "2. Action items\n\nOwners will confirm completion dates, document open questions, and prepare supporting materials for the next meeting.";
            page.Controls.Add(decoy);

            commandArea.Dock = DockStyle.None;
            commandArea.Height = 28;
            commandArea.BackColor = Color.FromArgb(43, 87, 154);
            commandInput.Dock = DockStyle.Top;
            commandInput.Height = 26;
            commandInput.BorderStyle = BorderStyle.None;
            commandInput.BackColor = Color.FromArgb(43, 87, 154);
            commandInput.ForeColor = Color.White;
            commandInput.Font = new Font("Microsoft YaHei", 9F);
            commandInput.AcceptsTab = true;
            commandInput.TextChanged += OnCommandTextChanged;
            commandInput.KeyDown += OnCommandKeyDown;
            commandInput.Enter += delegate { ClearCommandHint(); };
            commandInput.Leave += delegate { if (String.IsNullOrWhiteSpace(commandInput.Text)) SetCommandHint(CurrentCommandHint()); };
            commandSuggestions.Dock = DockStyle.Fill;
            commandSuggestions.Font = new Font("Microsoft YaHei", 10F);
            commandSuggestions.Visible = false;
            commandSuggestions.DoubleClick += delegate { CompleteSelectedCommand(); };
            commandSuggestions.MouseClick += delegate { CompleteSelectedCommand(); };
            commandArea.Controls.Add(commandSuggestions);
            commandArea.Controls.Add(commandInput);
            Controls.Add(commandArea);
            commandArea.BringToFront();
            browserHost.BackColor = Color.White;
            browserHost.Visible = false;
            page.Controls.Add(browserHost);
            browser.Dock = DockStyle.Fill;
            browser.DefaultBackgroundColor = Color.White;
            browserHost.Controls.Add(browser);
            Resize += delegate { ApplyLayout(); LayoutRibbonCommandArea(); LayoutGuideOverlay(); UpdateWindowButtonGlyph(); };
            page.Resize += delegate { ApplyLayout(); };
            workspace.Resize += delegate { ApplyLayout(); };
            workspace.Scroll += delegate { UpdateModeStatus(); };
            pagedDocumentHost.Scroll += delegate { UpdateModeStatus(); };
            workspace.MouseWheel += OnDocumentMouseWheel;
            bookshelfTimer.Interval = 1000;
            bookshelfTimer.Tick += OnBookshelfTimerTick;
            FormClosing += delegate
            {
                bookshelfTimer.Stop();
                foreach (Form authenticationWindow in authenticationWindows.ToArray()) authenticationWindow.Close();
                browser.Dispose();
            };
            ApplyLanguage();
            ApplyLayout();
            LayoutRibbonCommandArea();
            LayoutGuideOverlay();
        }

        protected override async void OnShown(EventArgs args)
        {
            base.OnShown(args);
            ApplyLayout();
            LayoutGuideOverlay();
            await InitializeBrowser();
            ApplyLayout();
        }

        Panel BuildRibbon()
        {
            Color officeBlue = Color.FromArgb(43, 87, 154);
            ribbonRoot = new Panel { Dock = DockStyle.Top, Height = 142, BackColor = Color.White };

            titleBar = new Panel { Dock = DockStyle.Top, Height = 31, BackColor = officeBlue };
            Label quickAccess = new Label
            {
                Location = new Point(12, 0), Size = new Size(170, 31), TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White, Font = new Font("Segoe UI Symbol", 10.5F), Text = "▣   ↶   ↷   ↻   ▾"
            };
            Label titleText = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI", 9F), Text = "Document1 - Word" };
            titleBar.Controls.Add(quickAccess);
            titleBar.Controls.Add(titleText);
            Panel titleActions = new Panel { Dock = DockStyle.Right, Width = 334, BackColor = officeBlue };
            accountButton = AddRibbonButton(titleActions, 4, 4, 50, 23, delegate { BeginLogin(); }, true);
            noviceGuideButton = AddTitleBarButton(titleActions, 58, 0, 78, 31, delegate { ToggleGuide(); });
            languageButton = AddRibbonButton(titleActions, 140, 4, 42, 23, delegate { chinese = !chinese; ApplyLanguage(); }, true);
            minimizeButton = AddTitleBarButton(titleActions, 190, 0, 46, 31, delegate { WindowState = FormWindowState.Minimized; });
            maximizeButton = AddTitleBarButton(titleActions, 236, 0, 46, 31, delegate { ToggleMaximize(); });
            closeButton = AddTitleBarButton(titleActions, 282, 0, 46, 31, delegate { Close(); });
            minimizeButton.Text = "—";
            closeButton.Text = "×";
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            titleBar.Controls.Add(titleActions);
            titleActions.BringToFront();
            ribbonRoot.Controls.Add(titleBar);
            AttachTitleDrag(titleBar);
            AttachTitleDrag(quickAccess);
            AttachTitleDrag(titleText);

            Panel tabs = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = officeBlue };
            ribbonRoot.Controls.Add(tabs);
            tabs.BringToFront();
            int[] tabLeft = { 0, 42, 90, 138, 186, 234, 290, 346, 394, 442 };
            int[] tabWidth = { 42, 48, 48, 48, 48, 56, 56, 48, 48, 48 };
            for (int index = 0; index < tabLabels.Length; index++)
            {
                tabLabels[index] = AddTab(tabs, tabLeft[index], index == 1);
                tabLabels[index].Width = tabWidth[index];
            }
            tabs.Controls.Add(new Label
            {
                Location = new Point(493, 0), Size = new Size(24, 28), Text = "◉", TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White, BackColor = officeBlue, Font = new Font("Segoe UI Symbol", 9F)
            });

            ribbonCommands = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 250, 250) };
            ribbonRoot.Controls.Add(ribbonCommands);
            ribbonCommands.BringToFront();

            clipboardGroup = AddRibbonGroup(ribbonCommands, 0, 96, "剪贴板");
            hideButton = AddRibbonButton(clipboardGroup, 6, 4, 42, 49, delegate { ShowCommandPage(); }, false);
            hideButton.Font = new Font("Microsoft YaHei", 9F);
            hideButton.Text = "▣\n粘贴";
            AddRibbonText(clipboardGroup, "✂ 剪切", 50, 4, 42, 16);
            AddRibbonText(clipboardGroup, "▧ 复制", 50, 22, 42, 16);
            AddRibbonText(clipboardGroup, "刷 格式", 50, 40, 42, 16);

            fontGroup = AddRibbonGroup(ribbonCommands, 96, 280, "字体");
            AddRibbonText(fontGroup, "等线（中文正文）⌄", 6, 4, 126, 21, true);
            readingSpeedSelector = new ComboBox { Location = new Point(136, 4), Size = new Size(46, 22), DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei", 8.5F) };
            readingSpeedSelector.SelectedIndexChanged += OnReadingSpeedChanged;
            fontGroup.Controls.Add(readingSpeedSelector);
            AddRibbonText(fontGroup, "A↑", 184, 4, 28, 21);
            AddRibbonText(fontGroup, "A↓", 212, 4, 28, 21);
            AddRibbonText(fontGroup, "Aa⌄", 240, 4, 34, 21);
            AddRibbonText(fontGroup, "B", 6, 31, 24, 20);
            AddRibbonText(fontGroup, "I", 31, 31, 20, 20);
            AddRibbonText(fontGroup, "U⌄", 52, 31, 28, 20);
            AddRibbonText(fontGroup, "abc", 81, 31, 32, 20);
            AddRibbonText(fontGroup, "x₂", 114, 31, 25, 20);
            AddRibbonText(fontGroup, "x²", 140, 31, 25, 20);
            AddRibbonText(fontGroup, "A", 170, 31, 25, 20);
            AddRibbonText(fontGroup, "🖍", 196, 31, 25, 20);
            AddRibbonText(fontGroup, "A", 222, 31, 25, 20);
            AddRibbonText(fontGroup, "清", 250, 31, 24, 20);

            paragraphGroup = AddRibbonGroup(ribbonCommands, 376, 244, "段落");
            backButton = AddRibbonButton(paragraphGroup, 5, 4, 28, 21, delegate { NavigateBack(); }, false);
            forwardButton = AddRibbonButton(paragraphGroup, 34, 4, 28, 21, delegate { if (browser.CanGoForward) browser.GoForward(); }, false);
            refreshButton = AddRibbonButton(paragraphGroup, 63, 4, 28, 21, delegate { if (ready) browser.Reload(); }, false);
            AddRibbonText(paragraphGroup, "• 列表⌄", 94, 4, 48, 21);
            AddRibbonText(paragraphGroup, "1. 编号⌄", 143, 4, 58, 21);
            AddRibbonText(paragraphGroup, "↔", 203, 4, 32, 21);
            AddRibbonText(paragraphGroup, "▤", 5, 31, 26, 20);
            AddRibbonText(paragraphGroup, "≡", 32, 31, 26, 20);
            AddRibbonText(paragraphGroup, "≣", 59, 31, 26, 20);
            AddRibbonText(paragraphGroup, "≡", 86, 31, 26, 20);
            AddRibbonText(paragraphGroup, "⇤", 113, 31, 26, 20);
            AddRibbonText(paragraphGroup, "⇥", 140, 31, 26, 20);
            AddRibbonText(paragraphGroup, "↕", 167, 31, 26, 20);
            AddRibbonText(paragraphGroup, "▦", 194, 31, 41, 20);

            stylesGroup = AddRibbonGroup(ribbonCommands, 620, 500, "样式");
            stylesGallery = new FlowLayoutPanel
            {
                Location = new Point(4, 3), Height = 53, Width = stylesGroup.Width - 32,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = false,
                BackColor = Color.FromArgb(250, 250, 250), Margin = Padding.Empty, Padding = Padding.Empty
            };
            stylesGroup.Controls.Add(stylesGallery);
            styleUpButton = AddRibbonButton(stylesGroup, stylesGroup.Width - 27, 3, 23, 17, delegate { ScrollStyleCards(-1); }, false);
            styleDownButton = AddRibbonButton(stylesGroup, stylesGroup.Width - 27, 20, 23, 17, delegate { ScrollStyleCards(1); }, false);
            styleMoreButton = AddRibbonButton(stylesGroup, stylesGroup.Width - 27, 37, 23, 17, delegate { ShowStyleMenu(); }, false);
            styleUpButton.Text = "⌃";
            styleDownButton.Text = "⌄";
            styleMoreButton.Text = "▾";
            lineButton = AddStyleCard("正文", "AaBbCc", delegate { ResumeReading(); });
            AddStyleCard("无间隔", "AaBbCc", delegate { SetReadingView(ReadingViewMode.Immersive); });
            AddStyleCard("标题 1", "AaBb", delegate { ShowCatalog(); });
            AddStyleCard("标题 2", "AaBb", delegate { OpenAdjacentChapter(-1); });
            AddStyleCard("标题", "AaB", delegate { OpenAdjacentChapter(1); });
            AddStyleCard("副标题", "AaBbCc", delegate { SetReadingView(ReadingViewMode.Scrolling); });
            AddStyleCard("强调", "AaBbCc", delegate { LoadBookshelf(); });
            AddStyleCard("引用", "AaBbCc", delegate { BeginLogin(); });
            AddStyleCard("明显引用", "AaBbCc", delegate { });
            AddStyleCard("书籍标题", "AaBbCc", delegate { });
            AddStyleCard("列表段落", "AaBbCc", delegate { });
            AddStyleCard("强烈引用", "AaBbCc", delegate { });
            AddStyleCard("细微强调", "AaBbCc", delegate { });
            AddStyleCard("明显强调", "AaBbCc", delegate { });
            AddStyleCard("细微参考", "AaBbCc", delegate { });
            AddStyleCard("明显参考", "AaBbCc", delegate { });
            AddStyleCard("Intense", "AaBbCc", delegate { });
            AddStyleCard("段落标题", "AaBbCc", delegate { });
            AddStyleCard("题注", "AaBbCc", delegate { });
            AddStyleCard("目录", "AaBbCc", delegate { ShowCatalog(); });
            AddStyleCard("页眉", "AaBbCc", delegate { });
            AddStyleCard("页脚", "AaBbCc", delegate { });

            editingGroup = AddRibbonGroup(ribbonCommands, 1120, 118, "编辑");
            homeButton = AddRibbonButton(editingGroup, 6, 0, 106, 20, delegate { BeginLogin(); }, false);
            bookshelfButton = AddRibbonButton(editingGroup, 6, 20, 106, 20, delegate { LoadBookshelf(); }, false);
            selectButton = AddRibbonButton(editingGroup, 6, 40, 106, 20, delegate { ShowCatalog(); }, false);
            homeButton.Font = new Font("Microsoft YaHei", 8F);
            bookshelfButton.Font = new Font("Microsoft YaHei", 8F);
            selectButton.Font = new Font("Microsoft YaHei", 8F);
            homeButton.Image = CreateSearchIcon();
            bookshelfButton.Image = CreateReplaceIcon();
            selectButton.Image = CreateSelectIcon();
            homeButton.ImageAlign = bookshelfButton.ImageAlign = selectButton.ImageAlign = ContentAlignment.MiddleLeft;
            homeButton.TextAlign = bookshelfButton.TextAlign = selectButton.TextAlign = ContentAlignment.MiddleCenter;
            homeButton.Padding = bookshelfButton.Padding = selectButton.Padding = new Padding(5, 0, 0, 0);
            selectButton.Text = "选择⌄";

            addinsGroup = AddRibbonGroup(ribbonCommands, 1238, 86, "加载项");
            subscribeButton = AddRibbonButton(addinsGroup, 8, 3, 70, 52, delegate { BeginChapterSubscription(); }, false);
            subscribeButton.Text = "◆\n订阅本章";

            readingViewSelector = new ComboBox { Visible = false, DropDownStyle = ComboBoxStyle.DropDownList };
            ribbonCommands.Controls.Add(readingViewSelector);
            readingViewSelector.SelectedIndexChanged += OnReadingViewChanged;
            typingScroll = new CheckBox { Visible = false, Checked = true };
            shortcutHint = new Label { Visible = false };
            ribbonCommands.Controls.Add(typingScroll);
            ribbonCommands.Controls.Add(shortcutHint);
            ribbonCommands.Resize += delegate { LayoutRibbonCommands(); };

            ribbonTips.SetToolTip(hideButton, "返回文档视图");
            ribbonTips.SetToolTip(lineButton, "按历史进度继续阅读");
            ribbonTips.SetToolTip(homeButton, "打开登录页面");
            ribbonTips.SetToolTip(bookshelfButton, "读取个人书架");
            ribbonTips.SetToolTip(subscribeButton, "使用起点账户余额订阅当前单章；执行前会再次确认");
            ribbonTips.SetToolTip(languageButton, "切换中英文界面");
            ribbonTips.SetToolTip(noviceGuideButton, "打开完整操作说明，返回后保留当前阅读位置");
            PopulateReadingSelectors();
            LayoutRibbonCommands();
            UpdateWindowButtonGlyph();
            return ribbonRoot;
        }
        static Label AddTab(Panel parent, int left, bool selected)
        {
            Label label = new Label {
                Location = new Point(left, 0), Size = new Size(52, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = selected ? Color.FromArgb(35, 35, 35) : Color.White,
                BackColor = selected ? Color.White : Color.FromArgb(43, 87, 154),
                Font = new Font("Microsoft YaHei", 9F)
            };
            parent.Controls.Add(label);
            return label;
        }

        static Panel AddRibbonGroup(Panel parent, int left, int width, string caption)
        {
            Panel group = new Panel { Location = new Point(left, 0), Size = new Size(width, 78), BackColor = Color.FromArgb(250, 250, 250), BorderStyle = BorderStyle.None };
            group.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Color.FromArgb(205, 205, 205) });
            group.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 17, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(100, 100, 100), Font = new Font("Microsoft YaHei", 7.5F), Text = caption });
            parent.Controls.Add(group);
            return group;
        }

        static Button AddRibbonButton(Control parent, int left, int top, int width, int height, Action action, bool titleButton)
        {
            Button button = new Button { Location = new Point(left, top), Size = new Size(width, height), FlatStyle = FlatStyle.Flat, BackColor = titleButton ? Color.White : Color.FromArgb(250, 250, 250), Font = new Font("Microsoft YaHei", 8.5F), Padding = Padding.Empty };
            button.FlatAppearance.BorderColor = titleButton ? Color.White : Color.FromArgb(210, 210, 210);
            button.Click += delegate { action(); };
            parent.Controls.Add(button);
            return button;
        }

        static Button AddTitleBarButton(Control parent, int left, int top, int width, int height, Action action)
        {
            Button button = new Button
            {
                Location = new Point(left, top), Size = new Size(width, height), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(43, 87, 154), ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", 8.5F), Padding = Padding.Empty, TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 106, 174);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(31, 70, 128);
            button.Click += delegate { action(); };
            parent.Controls.Add(button);
            return button;
        }

        static Bitmap CreateSearchIcon()
        {
            Bitmap bitmap = new Bitmap(15, 15);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(Color.FromArgb(45, 45, 45), 1.5F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.DrawEllipse(pen, 2, 2, 7, 7);
                graphics.DrawLine(pen, 8, 8, 13, 13);
            }
            return bitmap;
        }

        static Bitmap CreateReplaceIcon()
        {
            Bitmap bitmap = new Bitmap(15, 15);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(Color.FromArgb(45, 85, 145), 1.4F))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.DrawLine(pen, 2, 5, 12, 5);
                graphics.DrawLine(pen, 10, 3, 12, 5);
                graphics.DrawLine(pen, 10, 7, 12, 5);
                graphics.DrawLine(pen, 13, 10, 3, 10);
                graphics.DrawLine(pen, 5, 8, 3, 10);
                graphics.DrawLine(pen, 5, 12, 3, 10);
            }
            return bitmap;
        }

        static Bitmap CreateSelectIcon()
        {
            Bitmap bitmap = new Bitmap(15, 15);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(55, 55, 55)))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillPolygon(brush, new[] { new Point(3, 1), new Point(4, 12), new Point(7, 9), new Point(10, 14), new Point(12, 13), new Point(9, 8), new Point(13, 7) });
            }
            return bitmap;
        }

        void AttachTitleDrag(Control control)
        {
            control.MouseDown += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
            };
            control.DoubleClick += delegate { ToggleMaximize(); };
        }

        void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
            }
            else
            {
                MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
            UpdateWindowButtonGlyph();
        }

        void UpdateWindowButtonGlyph()
        {
            if (maximizeButton == null) return;
            maximizeButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
        }

        static Label AddRibbonText(Control parent, string text, int left, int top, int width, int height, bool border)
        {
            Label label = new Label { Location = new Point(left, top), Size = new Size(width, height), Text = text, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(250, 250, 250), ForeColor = Color.FromArgb(45, 45, 45), Font = new Font("Microsoft YaHei", 8.5F), BorderStyle = border ? BorderStyle.FixedSingle : BorderStyle.None };
            parent.Controls.Add(label);
            return label;
        }

        static Label AddRibbonText(Control parent, string text, int left, int top, int width, int height)
        {
            return AddRibbonText(parent, text, left, top, width, height, false);
        }

        Button AddStyleCard(string caption, string sample, Action action)
        {
            Button button = new Button
            {
                Size = new Size(76, 50), Margin = new Padding(0, 0, 2, 0), Padding = Padding.Empty,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(45, 45, 45),
                Font = new Font("Microsoft YaHei", 9F), TextAlign = ContentAlignment.MiddleCenter,
                Text = sample + Environment.NewLine + caption
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(205, 205, 205);
            button.Click += delegate { action(); };
            stylesGallery.Controls.Add(button);
            styleCards.Add(button);
            ToolStripMenuItem menuItem = new ToolStripMenuItem(caption);
            menuItem.Font = new Font("Microsoft YaHei", 9F);
            menuItem.Click += delegate { action(); };
            styleMenu.Items.Add(menuItem);
            return button;
        }

        void ScrollStyleCards(int direction)
        {
            if (styleCards.Count == 0) return;
            int capacity = Math.Max(1, stylesGallery.ClientSize.Width / 78);
            int maximum = Math.Max(0, styleCards.Count - capacity);
            styleCardOffset = Math.Max(0, Math.Min(maximum, styleCardOffset + direction));
            LayoutStyleCards();
        }

        void LayoutStyleCards()
        {
            if (stylesGallery == null || styleCards.Count == 0) return;
            int capacity = Math.Max(1, stylesGallery.ClientSize.Width / 78);
            int maximum = Math.Max(0, styleCards.Count - capacity);
            styleCardOffset = Math.Max(0, Math.Min(maximum, styleCardOffset));
            for (int index = 0; index < styleCards.Count; index++)
                styleCards[index].Visible = index >= styleCardOffset && index < styleCardOffset + capacity;
            styleUpButton.Enabled = styleCardOffset > 0;
            styleDownButton.Enabled = styleCardOffset < maximum;
        }

        void ShowStyleMenu()
        {
            if (styleMoreButton == null || styleMenu.Items.Count == 0) return;
            styleMenu.Show(styleMoreButton, new Point(styleMoreButton.Width, styleMoreButton.Height), ToolStripDropDownDirection.BelowLeft);
        }

        void LayoutRibbonCommandArea()
        {
            if (commandArea == null || ribbonRoot == null || commandArea.IsDisposed) return;
            int clientWidth = Math.Max(640, ClientSize.Width);
            bool compact = clientWidth < 900;
            for (int index = 6; index < tabLabels.Length; index++)
                if (tabLabels[index] != null) tabLabels[index].Visible = !compact;
            int left = clientWidth >= 1100 ? 520 : clientWidth >= 900 ? 495 : 300;
            int width = Math.Max(125, Math.Min(330, clientWidth - left - 210));
            commandArea.Bounds = new Rectangle(left, 31, width, commandArea.Height);
            commandArea.BringToFront();
        }

        void LayoutRibbonCommands()
        {
            if (ribbonCommands == null || fontGroup == null || stylesGroup == null || editingGroup == null) return;
            int width = ribbonCommands.ClientSize.Width;
            bool showClipboard = width >= 960;
            bool showParagraph = width >= 820;
            bool showAddins = width >= 1080;
            clipboardGroup.Visible = showClipboard;
            paragraphGroup.Visible = showParagraph;
            addinsGroup.Visible = showAddins;

            int left = 0;
            if (showClipboard)
            {
                clipboardGroup.Left = left;
                left += clipboardGroup.Width;
            }
            fontGroup.Left = left;
            left += fontGroup.Width;
            if (showParagraph)
            {
                paragraphGroup.Left = left;
                left += paragraphGroup.Width;
            }

            if (showAddins)
            {
                addinsGroup.Left = Math.Max(left + 210, width - addinsGroup.Width);
                editingGroup.Left = Math.Max(left + 92, addinsGroup.Left - editingGroup.Width);
            }
            else editingGroup.Left = Math.Max(left + 92, width - editingGroup.Width);
            int right = editingGroup.Left;
            int stylesWidth = Math.Max(150, right - left);
            stylesGroup.Left = left;
            stylesGroup.Width = stylesWidth;
            if (stylesGallery != null) stylesGallery.Width = Math.Max(80, stylesWidth - 32);
            if (styleUpButton != null) styleUpButton.Left = stylesWidth - 27;
            if (styleDownButton != null) styleDownButton.Left = stylesWidth - 27;
            if (styleMoreButton != null) styleMoreButton.Left = stylesWidth - 27;
            LayoutStyleCards();
            LayoutRibbonCommandArea();
        }
        void PopulateReadingSelectors()
        {
            if (readingSpeedSelector == null || readingViewSelector == null) return;
            updatingRibbonSelectors = true;
            int speedIndex = GetReadingSpeedIndex();
            readingSpeedSelector.Items.Clear();
            foreach (int value in SpeedCharacterValues) readingSpeedSelector.Items.Add(value.ToString());
            readingSpeedSelector.Items.Add(chinese ? "一行" : "1 line");
            readingSpeedSelector.Items.Add(chinese ? "两行" : "2 lines");
            readingSpeedSelector.SelectedIndex = speedIndex;
            readingViewSelector.Items.Clear();
            readingViewSelector.Items.Add(chinese ? "沉浸模式" : "Immersive");
            readingViewSelector.Items.Add(chinese ? "滚动模式" : "Scrolling");
            readingViewSelector.SelectedIndex = readingViewMode == ReadingViewMode.Scrolling ? 1 : 0;
            updatingRibbonSelectors = false;
        }

        int GetReadingSpeedIndex()
        {
            if (charactersPerKey <= 0) return SpeedCharacterValues.Length + (linesPerKey >= 2 ? 1 : 0);
            int index = Array.IndexOf(SpeedCharacterValues, charactersPerKey);
            return index >= 0 ? index : Math.Max(0, Array.FindIndex(SpeedCharacterValues, value => value >= charactersPerKey));
        }

        void OnReadingSpeedChanged(object sender, EventArgs args)
        {
            if (updatingRibbonSelectors || readingSpeedSelector.SelectedIndex < 0) return;
            int index = readingSpeedSelector.SelectedIndex;
            if (index < SpeedCharacterValues.Length) ApplyReadingStep(true, SpeedCharacterValues[index]);
            else ApplyReadingStep(false, index == SpeedCharacterValues.Length ? 1 : 2);
        }

        void OnReadingViewChanged(object sender, EventArgs args)
        {
            if (updatingRibbonSelectors || readingViewSelector.SelectedIndex < 0) return;
            SetReadingView(readingViewSelector.SelectedIndex == 1 ? ReadingViewMode.Scrolling : ReadingViewMode.Immersive);
        }

        void ApplyReadingStep(bool useCharacters, int amount)
        {
            int visibleCharacters = GetVisibleOcrCharacterCount();
            charactersPerKey = useCharacters ? Math.Max(1, amount) : 0;
            if (!useCharacters) linesPerKey = Math.Max(1, Math.Min(amount, 50));
            if (ocrReadingActive)
            {
                if (useCharacters) ocrRevealedCharacterCount = visibleCharacters;
                else ocrRevealedLineCount = (int)Math.Ceiling((double)visibleCharacters / EstimatedCharactersPerLine);
                RenderOcrText();
            }
            bookshelfNotice = String.Empty;
            PopulateReadingSelectors();
            SetCommandHint(CurrentCommandHint());
        }

        void SetReadingView(ReadingViewMode next)
        {
            if (readingViewMode == next) return;
            readingViewMode = next;
            workspace.AutoScrollPosition = Point.Empty;
            if (ocrReadingActive) RenderOcrText();
            ApplyLayout();
            PopulateReadingSelectors();
            UpdateModeStatus();
        }

        string[] AvailableCommands()
        {
            return new[] {
                "/登录", "/书架", "/搜索 ", "/分类", "/排行", "/筛选", "/排序", "/结果", "/详情", "/加入书架", "/阅读", "/目录", "/继续", "/返回", "/下一章", "/上一章", "/订阅", "/文字", "/网页", "/滚动", "/沉浸", "/行数 ", "/字数 ", "/帮助", "/隐藏",
                "/login", "/bookshelf", "/search ", "/category", "/rank", "/filter", "/sort", "/results", "/detail", "/add", "/read", "/catalog", "/resume", "/back", "/next", "/previous", "/subscribe", "/text", "/web", "/scroll", "/immersive", "/lines ", "/chars ", "/help", "/hide"
            };
        }
        string CurrentCommandHint()
        {
            if (!ready) return chinese ? "正在初始化，请稍候……" : "Initializing, please wait...";
            if (loadingBookshelf)
            {
                int elapsed = Math.Max(0, (int)(DateTime.Now - bookshelfStartedAt).TotalSeconds);
                string stage = String.IsNullOrWhiteSpace(bookshelfStage) ? (chinese ? "正在读取书架" : "Loading bookshelf") : bookshelfStage;
                return stage + "  " + elapsed + (chinese ? " 秒" : "s");
            }
            if (loadingCatalog)
            {
                string stage = String.IsNullOrWhiteSpace(catalogStage) ? (chinese ? "正在读取目录" : "Loading catalog") : catalogStage;
                return stage + (catalogCollectedCount > 0 ? (chinese ? "；已发现 " + catalogCollectedCount + " 章" : "; " + catalogCollectedCount + " chapters found") : String.Empty);
            }
            if (loadingDiscovery) return chinese ? "正在读取小说列表……" : "Loading book list...";
            if (loadingBookDetail) return chinese ? "正在读取小说详情……" : "Loading book details...";
            if (subscribingChapter) return chinese ? "正在通过起点官方页面处理本章订阅……" : "Processing this chapter through the official Qidian page...";
            if (openingChapter) return chinese ? "正在打开章节并验证阅读权限……" : "Opening the chapter and checking access...";
            if (!String.IsNullOrWhiteSpace(bookshelfNotice)) return bookshelfNotice;
            if (readingActive)
            {
                if (chapterEnded) return chinese ? "本章已结束，可输入 /下一章、/目录 或 /书架" : "Chapter finished. Enter /next, /catalog, or /bookshelf";
                if (ocrBusy) return chinese ? "正在识别当前正文画面，请稍候……" : "Recognizing the current reading view...";
                string step = charactersPerKey > 0
                    ? (chinese ? "每次按键推进约 " + charactersPerKey + " 字" : "Each key advances about " + charactersPerKey + " characters")
                    : (chinese ? "每次按键推进 " + linesPerKey + " 行" : "Each key advances " + linesPerKey + " lines");
                string source = ocrReadingActive ? (chinese ? "；" + ocrProvider + " 文字模式" : "; " + ocrProvider + " text mode") : (chinese ? "；官方网页模式" : "; official web mode");
                string cache = ocrReadingActive ? (chinese ? "；缓存 " + GetUnreadCachedPageCount() + "/" + OcrCachePageLimit + " 页" : "; cache " + GetUnreadCachedPageCount() + "/" + OcrCachePageLimit + " pages") : String.Empty;
                if (ocrPrefetching) cache += chinese ? "（后台识别中）" : " (prefetching)";
                return step + source + cache + (chinese ? "；输入 / 查看阅读命令" : "; type / for reading commands");
            }
            if (currentPageKind == CommandPageKind.BookDetail) return chinese ? "输入 /阅读、/目录、/加入书架 或 /返回" : "Enter /read, /catalog, /add, or /back";
            if (currentPageKind == CommandPageKind.DiscoveryFilter) return chinese ? "输入筛选项序号；/结果 查看小说；/返回 回到上一级" : "Enter a filter number; /results shows books; /back returns";
            if (currentPageKind == CommandPageKind.Discovery) return chinese ? "N 下一张 A4；P 上一张；输入书籍序号查看详情" : "N next A4 page; P previous; enter a book number for details";
            if (chapters.Count > 0) return chinese ? "输入章节序号阅读；/继续 按进度阅读；/目录 返回目录" : "Enter a chapter number; /resume uses saved progress; /catalog returns to the catalog";
            if (bookshelf.Count > 0) return chinese ? "输入书籍序号继续；输入 / 查看命令；// 表示换行" : "Enter a book number; type / for commands; // inserts a line break";
            if (loginCompleted) return chinese ? "登录完成，请输入 /书架；输入 / 查看命令；// 表示换行" : "Login complete. Enter /bookshelf; type / for commands";
            return chinese ? "请输入 /登录；输入 / 查看命令；// 表示换行" : "Enter /login; type / for commands; // inserts a line break";
        }

        void SetCommandHint(string text)
        {
            showingCommandHint = true;
            commandInput.ForeColor = Color.FromArgb(225, 235, 248);
            commandInput.Text = text;
            commandInput.SelectionStart = 0;
        }

        void ClearCommandHint()
        {
            if (!showingCommandHint) return;
            showingCommandHint = false;
            commandInput.Text = String.Empty;
            commandInput.ForeColor = Color.White;
        }

        void OnCommandTextChanged(object sender, EventArgs args)
        {
            if (showingCommandHint) return;
            string text = commandInput.Text.Trim();
            commandSuggestions.Items.Clear();
            if (text.StartsWith("/") && !text.StartsWith("//"))
            {
                foreach (string command in AvailableCommands())
                {
                    if (command.StartsWith(text, StringComparison.OrdinalIgnoreCase)) commandSuggestions.Items.Add(command);
                }
            }
            commandSuggestions.Visible = commandSuggestions.Items.Count > 0;
            commandArea.Height = commandSuggestions.Visible ? 146 : 28;
            LayoutRibbonCommandArea();
            if (commandSuggestions.Visible) commandSuggestions.SelectedIndex = 0;
        }

        void OnCommandKeyDown(object sender, KeyEventArgs args)
        {
            if (showingCommandHint && args.KeyCode != Keys.Enter && args.KeyCode != Keys.Tab && args.KeyCode != Keys.ShiftKey && args.KeyCode != Keys.ControlKey && args.KeyCode != Keys.Menu)
            {
                ClearCommandHint();
            }
            if (args.KeyCode == Keys.Tab && commandSuggestions.Visible)
            {
                CompleteSelectedCommand();
                args.SuppressKeyPress = true;
                return;
            }
            if (args.KeyCode == Keys.Down && commandSuggestions.Visible)
            {
                commandSuggestions.SelectedIndex = Math.Min(commandSuggestions.Items.Count - 1, commandSuggestions.SelectedIndex + 1);
                args.SuppressKeyPress = true;
                return;
            }
            if (args.KeyCode == Keys.Up && commandSuggestions.Visible)
            {
                commandSuggestions.SelectedIndex = Math.Max(0, commandSuggestions.SelectedIndex - 1);
                args.SuppressKeyPress = true;
                return;
            }
            if (args.KeyCode == Keys.Enter)
            {
                string input = showingCommandHint ? String.Empty : commandInput.Text;
                args.SuppressKeyPress = true;
                ExecuteCommand(input);
            }
        }

        void CompleteSelectedCommand()
        {
            if (!commandSuggestions.Visible || commandSuggestions.SelectedItem == null) return;
            commandInput.Text = commandSuggestions.SelectedItem.ToString();
            commandInput.SelectionStart = commandInput.Text.Length;
            commandSuggestions.Visible = false;
            commandArea.Height = 28;
            LayoutRibbonCommandArea();
            commandInput.Focus();
        }

        void ExecuteCommand(string rawInput)
        {
            string input = (rawInput ?? String.Empty).Trim();
            commandSuggestions.Visible = false;
            commandArea.Height = 28;
            LayoutRibbonCommandArea();
            commandInput.Clear();
            if (String.IsNullOrWhiteSpace(input))
            {
                SetCommandHint(CurrentCommandHint());
                return;
            }
            bookshelfNotice = String.Empty;

            int bookNumber;
            if (Int32.TryParse(input, out bookNumber))
            {
                if (currentPageKind == CommandPageKind.Catalog) OpenChapterWithHistory(bookNumber);
                else if (currentPageKind == CommandPageKind.DiscoveryFilter) SelectDiscoveryFilter(bookNumber);
                else if (currentPageKind == CommandPageKind.Discovery) OpenDiscoveryBook(bookNumber);
                else if (currentPageKind == CommandPageKind.Bookshelf) OpenBook(bookNumber);
                else ShowCommandError(chinese ? "当前页面没有可选择的序号。" : "The current page has no numbered selection.");
            }
            else if (input == "/登录" || input.Equals("/login", StringComparison.OrdinalIgnoreCase))
            {
                BeginLogin();
            }
            else if (input == "/书架" || input.Equals("/bookshelf", StringComparison.OrdinalIgnoreCase))
            {
                PushNavigationState();
                LoadBookshelf();
            }
            else if (input.StartsWith("/搜索 ") || input.StartsWith("/search ", StringComparison.OrdinalIgnoreCase))
            {
                SearchBooks(CommandArgument(input));
            }
            else if (input == "/搜索" || input.Equals("/search", StringComparison.OrdinalIgnoreCase))
            {
                ShowCommandError(chinese ? "请输入搜索关键词，例如：/搜索 诡秘之主" : "Enter keywords, for example: /search Lord of Mysteries");
            }
            else if (input == "/分类" || input.Equals("/category", StringComparison.OrdinalIgnoreCase))
            {
                StartCategoryBrowsing();
            }
            else if (input.StartsWith("/分类 ") || input.StartsWith("/category ", StringComparison.OrdinalIgnoreCase))
            {
                BrowseCategory(CommandArgument(input));
            }
            else if (input == "/排行" || input.Equals("/rank", StringComparison.OrdinalIgnoreCase))
            {
                ShowRankingMenu();
            }
            else if (input.StartsWith("/排行 ") || input.StartsWith("/rank ", StringComparison.OrdinalIgnoreCase))
            {
                BrowseRanking(CommandArgument(input));
            }
            else if (input == "/筛选" || input.Equals("/filter", StringComparison.OrdinalIgnoreCase))
            {
                ShowNextDiscoveryFilter();
            }
            else if (input == "/排序" || input.Equals("/sort", StringComparison.OrdinalIgnoreCase))
            {
                ShowDiscoverySort();
            }
            else if (input == "/结果" || input.Equals("/results", StringComparison.OrdinalIgnoreCase))
            {
                RenderDiscoveryBooks();
            }
            else if (input == "/详情" || input.Equals("/detail", StringComparison.OrdinalIgnoreCase))
            {
                RenderBookDetail();
            }
            else if (input == "/加入书架" || input.Equals("/add", StringComparison.OrdinalIgnoreCase))
            {
                AddCurrentBookToBookshelf();
            }
            else if (input == "/阅读" || input.Equals("/read", StringComparison.OrdinalIgnoreCase))
            {
                BeginCurrentBookReading();
            }
            else if (input == "/目录" || input.Equals("/catalog", StringComparison.OrdinalIgnoreCase))
            {
                if (currentPageKind != CommandPageKind.Catalog) PushNavigationState();
                ShowCatalog();
            }
            else if (input == "/继续" || input.Equals("/resume", StringComparison.OrdinalIgnoreCase))
            {
                PushNavigationState();
                ResumeReading();
            }
            else if (input == "/返回" || input.Equals("/back", StringComparison.OrdinalIgnoreCase))
            {
                NavigateBack();
            }
            else if (input == "/下一章" || input.Equals("/next", StringComparison.OrdinalIgnoreCase))
            {
                OpenAdjacentChapter(1);
            }
            else if (input == "/上一章" || input.Equals("/previous", StringComparison.OrdinalIgnoreCase))
            {
                OpenAdjacentChapter(-1);
            }
            else if (input == "/订阅" || input.Equals("/subscribe", StringComparison.OrdinalIgnoreCase))
            {
                BeginChapterSubscription();
            }
            else if (input == "/文字" || input.Equals("/text", StringComparison.OrdinalIgnoreCase))
            {
                BeginOcrReading();
            }
            else if (input == "/网页" || input.Equals("/web", StringComparison.OrdinalIgnoreCase))
            {
                ShowOfficialReadingView();
            }
            else if (input == "/滚动" || input.Equals("/scroll", StringComparison.OrdinalIgnoreCase))
            {
                SetReadingView(ReadingViewMode.Scrolling);
            }
            else if (input == "/沉浸" || input.Equals("/immersive", StringComparison.OrdinalIgnoreCase))
            {
                SetReadingView(ReadingViewMode.Immersive);
            }
            else if (input.StartsWith("/行数 ") || input.StartsWith("/lines ", StringComparison.OrdinalIgnoreCase))
            {
                SetReadingStep(input, false);
            }
            else if (input.StartsWith("/字数 ") || input.StartsWith("/chars ", StringComparison.OrdinalIgnoreCase))
            {
                SetReadingStep(input, true);
            }
            else if (input == "/隐藏" || input.Equals("/hide", StringComparison.OrdinalIgnoreCase))
            {
                ShowCommandPage();
            }
            else if (input == "/帮助" || input.Equals("/help", StringComparison.OrdinalIgnoreCase) || input == "/")
            {
                ShowHelp();
            }
            else
            {
                decoy.AppendText(Environment.NewLine + input.Replace("//", Environment.NewLine));
            }
            SetCommandHint(CurrentCommandHint());
        }

        static string CommandArgument(string input)
        {
            int separator = input.IndexOf(' ');
            return separator < 0 ? String.Empty : input.Substring(separator + 1).Trim();
        }

        void ShowCommandError(string message)
        {
            ShowCommandPage();
            if (currentPageKind == CommandPageKind.None || currentPageKind == CommandPageKind.Message) decoy.Text = message;
            else decoy.AppendText(Environment.NewLine + Environment.NewLine + message);
            bookshelfNotice = message;
            SetCommandHint(CurrentCommandHint());
        }

        void PushNavigationState()
        {
            if (currentPageKind == CommandPageKind.None || loadingBookshelf || loadingCatalog || loadingDiscovery || loadingBookDetail) return;
            navigationHistory.Push(new NavigationState
            {
                PageKind = currentPageKind,
                DiscoveryBooks = new List<BookItem>(discoveryBooks),
                DiscoveryFilterGroups = new List<DiscoveryFilterGroup>(discoveryFilterGroups),
                Chapters = new List<ChapterItem>(chapters),
                SelectedBook = selectedBook,
                BookDetail = currentBookDetail,
                DiscoveryTitle = discoveryTitle,
                DiscoveryUrl = discoveryUrl,
                CurrentChapterIndex = currentChapterIndex,
                DiscoveryFilterGroupIndex = discoveryFilterGroupIndex,
                DiscoveryFilterRequested = discoveryFilterRequested,
                DiscoveryIsRanking = discoveryIsRanking,
                DiscoveryDocumentPageIndex = discoveryDocumentPageIndex,
                DiscoveryNextPageUrl = discoveryNextPageUrl,
                DiscoveryRemotePage = discoveryRemotePage,
                DiscoveryRemotePageMax = discoveryRemotePageMax
            });
        }

        void NavigateBack()
        {
            if (guideOverlay.Visible)
            {
                HideGuide();
                return;
            }
            if (navigationHistory.Count == 0)
            {
                if (ready && browser.CanGoBack) browser.GoBack();
                else ShowCommandError(chinese ? "已经是最早的页面。" : "This is the earliest page.");
                return;
            }
            CancelPageOperations();
            NavigationState previous = navigationHistory.Pop();
            discoveryBooks.Clear();
            discoveryBooks.AddRange(previous.DiscoveryBooks ?? new List<BookItem>());
            discoveryFilterGroups.Clear();
            discoveryFilterGroups.AddRange(previous.DiscoveryFilterGroups ?? new List<DiscoveryFilterGroup>());
            chapters.Clear();
            chapters.AddRange(previous.Chapters ?? new List<ChapterItem>());
            selectedBook = previous.SelectedBook;
            currentBookDetail = previous.BookDetail;
            discoveryTitle = previous.DiscoveryTitle ?? String.Empty;
            discoveryUrl = previous.DiscoveryUrl ?? String.Empty;
            currentChapterIndex = previous.CurrentChapterIndex;
            discoveryFilterGroupIndex = previous.DiscoveryFilterGroupIndex;
            discoveryFilterRequested = previous.DiscoveryFilterRequested;
            discoveryIsRanking = previous.DiscoveryIsRanking;
            discoveryDocumentPageIndex = previous.DiscoveryDocumentPageIndex;
            discoveryNextPageUrl = previous.DiscoveryNextPageUrl ?? String.Empty;
            discoveryRemotePage = Math.Max(1, previous.DiscoveryRemotePage);
            discoveryRemotePageMax = Math.Max(discoveryRemotePage, previous.DiscoveryRemotePageMax);
            currentPageKind = previous.PageKind;
            bookshelfNotice = String.Empty;
            ShowCommandPage();
            if (currentPageKind == CommandPageKind.Bookshelf) RenderBookshelf();
            else if (currentPageKind == CommandPageKind.Discovery) RenderDiscoveryBooks();
            else if (currentPageKind == CommandPageKind.DiscoveryFilter) RenderDiscoveryFilter();
            else if (currentPageKind == CommandPageKind.BookDetail) RenderBookDetail();
            else if (currentPageKind == CommandPageKind.Catalog) RenderCatalog();
            else
            {
                currentPageKind = CommandPageKind.Message;
                decoy.Text = chinese ? "已返回上一页。" : "Returned to the previous page.";
            }
            SetCommandHint(CurrentCommandHint());
        }

        void CancelPageOperations()
        {
            bookshelfOperationId++;
            catalogOperationId++;
            discoveryOperationId++;
            bookDetailOperationId++;
            chapterOperationId++;
            ocrOperationId++;
            loadingBookshelf = false;
            extractingBookshelf = false;
            loadingCatalog = false;
            extractingCatalog = false;
            loadingDiscovery = false;
            extractingDiscovery = false;
            loadingBookDetail = false;
            extractingBookDetail = false;
            openingChapter = false;
            preparingChapter = false;
            readingActive = false;
            ocrReadingActive = false;
            ocrBusy = false;
            keepBrowserRunningBehindDocument = false;
            startReadingAfterCatalog = false;
            appendingDiscoveryPage = false;
            bookshelfTimer.Stop();
        }

        void SearchBooks(string keywords)
        {
            if (String.IsNullOrWhiteSpace(keywords))
            {
                ShowCommandError(chinese ? "搜索关键词不能为空。" : "Search keywords cannot be empty.");
                return;
            }
            StartDiscovery(chinese ? "搜索：“" + keywords + "”" : "Search: " + keywords,
                "https://www.qidian.com/soushu/" + Uri.EscapeDataString(keywords) + ".html");
        }

        void ShowCategoryHelp()
        {
            PushNavigationState();
            CancelPageOperations();
            ShowCommandPage();
            currentPageKind = CommandPageKind.Message;
            decoy.Text = chinese
                ? "小说分类\n\n直接输入 /分类，程序会读取起点当前页面中的全部筛选层级，并依次显示主分类、子分类、连载状态、作品字数、标签和人气排序等选项。\n\n每一级输入对应序号即可继续；输入 /结果 可随时查看当前小说列表，/筛选 继续选择，/排序 直接进入人气等排序栏，/返回 撤销上一次选择。\n\n仍可使用快捷命令，例如：/分类 玄幻。"
                : "Book categories\n\nEnter /category to read the current filter levels from Qidian, including primary category, subcategory, status, word count, tags, and popularity ordering.\n\nEnter an option number at each level. /results shows the current books, /filter continues, /sort opens ordering, and /back undoes the last choice. Shortcuts such as /category fantasy remain available.";
            SetCommandHint(CurrentCommandHint());
        }

        void StartCategoryBrowsing()
        {
            StartDiscovery(chinese ? "小说分类" : "Book categories", "https://www.qidian.com/all/", true, false, 0, true);
        }

        void ShowRankingMenu()
        {
            PushNavigationState();
            CancelPageOperations();
            discoveryBooks.Clear();
            discoveryFilterGroups.Clear();
            discoveryTitle = chinese ? "排行榜" : "Rankings";
            discoveryUrl = "https://www.qidian.com/rank/";
            discoveryFilterRequested = true;
            discoveryIsRanking = true;
            discoveryFilterGroupIndex = 0;
            discoveryFilterGroups.Add(new DiscoveryFilterGroup
            {
                Title = chinese ? "榜单类型" : "Ranking type",
                Options = new[] {
                    new DiscoveryFilterOption { Text = chinese ? "月票榜" : "Monthly", Url = "https://www.qidian.com/rank/yuepiao/" },
                    new DiscoveryFilterOption { Text = chinese ? "畅销榜" : "Best sellers", Url = "https://www.qidian.com/rank/hotsales/" },
                    new DiscoveryFilterOption { Text = chinese ? "阅读榜" : "Reading", Url = "https://www.qidian.com/rank/readindex/" },
                    new DiscoveryFilterOption { Text = chinese ? "推荐榜" : "Recommendations", Url = "https://www.qidian.com/rank/recom/" },
                    new DiscoveryFilterOption { Text = chinese ? "收藏榜" : "Collections", Url = "https://www.qidian.com/rank/collect/" },
                    new DiscoveryFilterOption { Text = chinese ? "更新榜" : "Updates", Url = "https://www.qidian.com/rank/vipup/" },
                    new DiscoveryFilterOption { Text = chinese ? "新书榜" : "New books", Url = "https://www.qidian.com/rank/newbook/" }
                }
            });
            ShowCommandPage();
            RenderDiscoveryFilter();
            SetCommandHint(CurrentCommandHint());
        }

        void BrowseCategory(string category)
        {
            string key = (category ?? String.Empty).Trim().ToLowerInvariant();
            string channelId = String.Empty;
            if (key == "玄幻" || key == "fantasy") channelId = "21";
            else if (key == "奇幻") channelId = "1";
            else if (key == "武侠" || key == "wuxia") channelId = "2";
            else if (key == "仙侠" || key == "xianxia") channelId = "22";
            else if (key == "都市" || key == "urban") channelId = "4";
            else if (key == "现实" || key == "reality") channelId = "15";
            else if (key == "军事" || key == "military") channelId = "6";
            else if (key == "历史" || key == "history") channelId = "5";
            else if (key == "游戏" || key == "games") channelId = "7";
            else if (key == "体育" || key == "sports") channelId = "8";
            else if (key == "科幻" || key == "sci-fi" || key == "scifi") channelId = "9";
            else if (key == "诸天无限" || key == "诸天") channelId = "20109";
            else if (key == "悬疑" || key == "suspense") channelId = "10";
            else if (key == "轻小说" || key == "light novel") channelId = "12";
            if (String.IsNullOrWhiteSpace(channelId))
            {
                ShowCategoryHelp();
                return;
            }
            StartDiscovery((chinese ? "分类：" : "Category: ") + category,
                "https://www.qidian.com/all/chanId" + channelId + "/", true, false, 1, true);
        }

        void BrowseRanking(string ranking)
        {
            string key = (ranking ?? String.Empty).Trim().ToLowerInvariant();
            string route = "yuepiao";
            if (key == "畅销" || key == "hotsales" || key == "sales") route = "hotsales";
            else if (key == "阅读" || key == "read" || key == "readindex") route = "readindex";
            else if (key == "推荐" || key == "recommend" || key == "recom") route = "recom";
            else if (key == "收藏" || key == "collect") route = "collect";
            else if (key == "更新" || key == "update" || key == "vipup") route = "vipup";
            else if (key == "新书" || key == "new" || key == "newbook") route = "newbook";
            else if (!(key == "月票" || key == "monthly" || String.IsNullOrWhiteSpace(key)))
            {
                ShowCommandError(chinese ? "支持的榜单：月票、畅销、阅读、推荐、收藏、更新、新书。" : "Supported rankings: monthly, sales, read, recommend, collect, update, new.");
                return;
            }
            StartDiscovery((chinese ? "排行榜：" : "Ranking: ") + ranking,
                "https://www.qidian.com/rank/" + route + "/", true, true, 0, true);
        }

        void StartDiscovery(string title, string url, bool requestFilters = false, bool ranking = false, int filterGroupIndex = 0, bool pushHistory = true)
        {
            if (!ready || browser.CoreWebView2 == null)
            {
                ShowCommandError(chinese ? "浏览器尚未初始化完成。" : "The browser is not ready.");
                return;
            }
            if (pushHistory) PushNavigationState();
            CancelPageOperations();
            discoveryOperationId++;
            loadingDiscovery = true;
            extractingDiscovery = false;
            discoveryTitle = title;
            discoveryUrl = url;
            discoveryBooks.Clear();
            discoveryFilterGroups.Clear();
            discoveryFilterRequested = requestFilters;
            discoveryIsRanking = ranking;
            discoveryFilterGroupIndex = Math.Max(0, filterGroupIndex);
            discoveryDocumentPageIndex = 0;
            discoveryNextPageUrl = String.Empty;
            discoveryRemotePage = 1;
            discoveryRemotePageMax = 1;
            appendingDiscoveryPage = false;
            selectedBook = null;
            currentBookDetail = null;
            chapters.Clear();
            bookshelfNotice = String.Empty;
            keepBrowserRunningBehindDocument = true;
            ShowCommandPage();
            currentPageKind = CommandPageKind.Message;
            decoy.Text = (chinese ? "正在打开" : "Opening ") + title + "……";
            SetCommandHint(CurrentCommandHint());
            Navigate(url);
        }

        async Task ExtractDiscovery(int operationId)
        {
            try
            {
                DiscoveryProbe probe = null;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    if (!loadingDiscovery || operationId != discoveryOperationId) return;
                    probe = await ReadDiscoveryProbe();
                    bool hasItems = probe != null && probe.Items != null && probe.Items.Length > 0;
                    bool hasGroups = probe != null && probe.Groups != null && probe.Groups.Length > 0;
                    if (hasItems && (!discoveryFilterRequested || hasGroups)) break;
                    await Task.Delay(500);
                }
                if (probe == null || probe.Items == null || probe.Items.Length == 0)
                {
                    FailDiscovery(operationId, probe != null && !String.IsNullOrWhiteSpace(probe.Error)
                        ? probe.Error
                        : (chinese ? "页面已加载，但没有识别到小说条目。" : "The page loaded, but no books were detected."));
                    return;
                }
                loadingDiscovery = false;
                extractingDiscovery = false;
                keepBrowserRunningBehindDocument = false;
                bool appended = appendingDiscoveryPage;
                if (appended)
                {
                    HashSet<string> known = new HashSet<string>(discoveryBooks.Select(book => book.BookUrl ?? book.Title), StringComparer.OrdinalIgnoreCase);
                    foreach (BookItem item in probe.Items ?? new BookItem[0])
                        if (item != null && known.Add(item.BookUrl ?? item.Title)) discoveryBooks.Add(item);
                    discoveryDocumentPageIndex = discoveryPageCountBeforeAppend;
                }
                else
                {
                    discoveryBooks.Clear();
                    discoveryBooks.AddRange(probe.Items ?? new BookItem[0]);
                    discoveryFilterGroups.Clear();
                    discoveryFilterGroups.AddRange((probe.Groups ?? new DiscoveryFilterGroup[0]).Where(group => group != null && group.Options != null && group.Options.Length > 1));
                }
                discoveryRemotePage = probe.CurrentPage > 0 ? probe.CurrentPage : (appended ? discoveryRemotePage + 1 : 1);
                discoveryRemotePageMax = probe.PageMax > 0 ? Math.Max(discoveryRemotePage, probe.PageMax) : Math.Max(discoveryRemotePageMax, discoveryRemotePage);
                discoveryNextPageUrl = probe.NextPageUrl ?? String.Empty;
                if (browser.Source != null && String.Equals(discoveryNextPageUrl.TrimEnd('/'), browser.Source.AbsoluteUri.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    discoveryNextPageUrl = String.Empty;
                appendingDiscoveryPage = false;
                ShowCommandPage();
                if (!appended && discoveryFilterRequested && discoveryFilterGroupIndex < discoveryFilterGroups.Count) RenderDiscoveryFilter();
                else RenderDiscoveryBooks();
                SetCommandHint(CurrentCommandHint());
            }
            catch (Exception exception)
            {
                FailDiscovery(operationId, exception.GetType().Name + " - " + exception.Message);
            }
        }

        async Task<DiscoveryProbe> ReadDiscoveryProbe()
        {
            string script = "(function(){try{const clean=x=>(x||'').replace(/\\s+/g,' ').trim();const abs=u=>{try{return new URL(u,location.href).href}catch(e){return ''}};const result=[];const seen=new Set();const allAnchors=Array.from(document.querySelectorAll('a[href]'));" +
                "const groups=[];const signatures=new Set();const addGroup=(title,nodes)=>{title=clean(title);const options=[];const used=new Set();for(const node of nodes){const text=clean(node.innerText||node.textContent);const url=abs(node.getAttribute('href')||'');if(!text||text.length>40||!url||used.has(text))continue;used.add(text);const cls=String(node.className||'')+' '+String(node.parentElement&&node.parentElement.className||'');options.push({Text:text,Url:url,Selected:/active|selected|current|act/i.test(cls)||node.getAttribute('aria-current')==='page'||url.replace(/[?#].*$/,'').replace(/\\/$/,'')===location.href.replace(/[?#].*$/,'').replace(/\\/$/,'')});}if(options.length<2)return;const signature=options.map(o=>o.Text).join('|');if(signatures.has(signature))return;signatures.add(signature);groups.push({Title:title||'筛选',Options:options.slice(0,80)});};" +
                "const addByToken=(title,token)=>{const matches=allAnchors.filter(a=>(a.getAttribute('href')||'').indexOf(token)>=0);if(!matches.length)return;const nodes=[];for(const match of matches){const box=match.closest('dl,ul,.select-list,.filter-list,[class*=filter],[class*=sort],[class*=category]');for(const a of Array.from((box||match.parentElement||document).querySelectorAll('a[href]')))nodes.push(a);}addGroup(title,nodes.length?nodes:matches);};" +
                "if(/\\/all\\//i.test(location.pathname)){addByToken('主分类','chanId');addByToken('子分类','subCateId');addByToken('连载状态','action');addByToken('作品字数','size');addByToken('人气排序','orderId');addByToken('作品标签','tag');}" +
                "if(/\\/rank\\//i.test(location.pathname)){addByToken('作品分类','chanId');addByToken('子分类','subCateId');addByToken('年份','year');addByToken('月份','month');}" +
                "for(const dl of Array.from(document.querySelectorAll('dl'))){const titleNode=dl.querySelector('dt,[class*=title],[class*=label]');const title=clean(titleNode&&titleNode.innerText);if(title&&title.length<=24)addGroup(title,Array.from(dl.querySelectorAll('a[href]')));}" +
                "const sortNodes=allAnchors.filter(a=>/^(人气排序|更新时间|总收藏|总字数|推荐票|月票|会员点击|书友点击)$/.test(clean(a.innerText)));addGroup('人气与排序',sortNodes);" +
                "const pager=document.querySelector('#page-container,[data-pagemax][data-page]');const currentPage=Math.max(1,parseInt(pager&&pager.getAttribute('data-page')||'1',10)||1);const pageMax=Math.max(currentPage,parseInt(pager&&pager.getAttribute('data-pagemax')||String(currentPage),10)||currentPage);let next=pager&&pager.querySelector('a.lbf-pagination-next:not(.lbf-pagination-disabled),a[rel=next]');if(!next&&pager&&currentPage<pageMax)next=pager.querySelector('a[data-page=\"'+(currentPage+1)+'\"]');if(!next)next=allAnchors.find(a=>(a.getAttribute('rel')==='next'||/下一页|下页|next|>|›|»/i.test(clean(a.innerText)))&&!/disabled|unavailable/i.test(String(a.className||'')+' '+String(a.parentElement&&a.parentElement.className||'')));" +
                "const anchors=Array.from(document.querySelectorAll('a[href*=\"/book/\"]'));for(const a of anchors){const href=abs(a.getAttribute('href')||'');const match=href.match(/\\/book\\/(\\d+)(?:\\/|$)/);if(!match||/\\/catalog|\\/chapter\\//i.test(href)||seen.has(match[1]))continue;" +
                "const box=a.closest('li,article,.book-item,.book-layout,.book-mid-info,.rank-list-row,.book-img-text>ul>li')||a.parentElement;const heading=box&&box.querySelector('h1,h2,h3,h4,[class*=book-name],[class*=book-title]');const title=clean((heading&&heading.innerText)||a.getAttribute('title')||a.innerText);if(!title||title.length>80)continue;" +
                "const authorLink=box&&box.querySelector('a[href*=\"/author/\"],.author a,a.name');const introNode=box&&box.querySelector('[class*=intro],.intro,p');const categoryNode=box&&box.querySelector('[class*=tag] a,[class*=category],a[href*=\"chanId\"]');const latest=box&&box.querySelector('a[href*=\"/chapter/\"]');const text=clean(box&&box.innerText);" +
                "seen.add(match[1]);result.push({Title:title,BookUrl:'https://www.qidian.com/book/'+match[1]+'/',ProgressTitle:'',ProgressUrl:'',Author:clean(authorLink&&authorLink.innerText),Category:clean(categoryNode&&categoryNode.innerText),Status:/完本|完结/.test(text)?'完本':(/连载/.test(text)?'连载':''),Intro:clean(introNode&&introNode.innerText).slice(0,260),LatestChapter:clean(latest&&latest.innerText),Extra:text.slice(0,300)});}" +
                "return JSON.stringify({Items:result,Groups:groups,PageTitle:clean(document.title),NextPageUrl:abs(next&&next.getAttribute('href')),CurrentPage:currentPage,PageMax:pageMax,Error:''});}catch(error){return JSON.stringify({Items:[],Groups:[],PageTitle:'',NextPageUrl:'',CurrentPage:1,PageMax:1,Error:String(error&&error.stack||error)});}})()";
            string encoded = await browser.ExecuteScriptAsync(script);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Deserialize<string>(encoded);
            return String.IsNullOrWhiteSpace(json) ? null : serializer.Deserialize<DiscoveryProbe>(json);
        }

        void FailDiscovery(int operationId, string reason)
        {
            if (!loadingDiscovery || operationId != discoveryOperationId) return;
            loadingDiscovery = false;
            extractingDiscovery = false;
            keepBrowserRunningBehindDocument = false;
            if (appendingDiscoveryPage && discoveryBooks.Count > 0)
            {
                appendingDiscoveryPage = false;
                bookshelfNotice = (chinese ? "下一页读取失败：" : "Failed to load the next page: ") + reason;
                ShowCommandPage();
                RenderDiscoveryBooks();
                SetCommandHint(CurrentCommandHint());
                return;
            }
            ShowCommandPage();
            currentPageKind = CommandPageKind.Message;
            decoy.Text = (chinese ? "小说列表读取失败\n\n原因：" : "Book list loading failed\n\nReason: ") + reason +
                (chinese ? "\n\n可输入 /网页 查看官方页面，或 /返回。" : "\n\nUse /web for the official page, or /back.");
            SetCommandHint(CurrentCommandHint());
        }

        void RenderDiscoveryBooks()
        {
            currentPageKind = CommandPageKind.Discovery;
            StringBuilder document = new StringBuilder();
            document.AppendLine(discoveryTitle + "（" + discoveryBooks.Count + (chinese ? " 本）" : " books)"));
            if (discoveryRemotePageMax > 1)
                document.AppendLine(chinese
                    ? "起点结果页：已读取第 1–" + discoveryRemotePage + " 页 / 共 " + discoveryRemotePageMax + " 页"
                    : "Qidian result pages loaded: 1–" + discoveryRemotePage + " of " + discoveryRemotePageMax);
            document.AppendLine();
            for (int index = 0; index < discoveryBooks.Count; index++)
            {
                BookItem book = discoveryBooks[index];
                string meta = String.Join(" · ", new[] { book.Author, book.Category, book.Status }.Where(value => !String.IsNullOrWhiteSpace(value)).ToArray());
                document.AppendLine((index + 1) + ". " + book.Title + (String.IsNullOrWhiteSpace(meta) ? String.Empty : "    " + meta));
                if (!String.IsNullOrWhiteSpace(book.Intro)) document.AppendLine("    " + book.Intro);
                document.AppendLine();
            }
            if (discoveryFilterGroups.Count > 0)
            {
                if (discoveryFilterGroupIndex >= discoveryFilterGroups.Count)
                    document.AppendLine(chinese
                        ? "已完成当前页面识别到的 " + discoveryFilterGroups.Count + " 级筛选。输入 /筛选 可重新调整，/排序 可调整人气等排序。"
                        : "All " + discoveryFilterGroups.Count + " detected filter levels are complete. /filter revisits them and /sort adjusts ordering.");
                else
                    document.AppendLine(chinese
                        ? "共识别到 " + discoveryFilterGroups.Count + " 级筛选；输入 /筛选 可继续第 " + (discoveryFilterGroupIndex + 1) + " 级，/排序 可选择人气、收藏、字数、推荐票或月票。"
                        : discoveryFilterGroups.Count + " filter levels were detected. /filter continues at level " + (discoveryFilterGroupIndex + 1) + " and /sort opens ordering.");
            }
            Size pageSize = GetScaledPageSize();
            Padding margins = GetScaledPagePadding(pageSize);
            Font font = CreateDocumentFont();
            int contentWidth = Math.Max(120, pageSize.Width - margins.Horizontal);
            int contentHeight = Math.Max(160, pageSize.Height - margins.Vertical);
            List<string> pages = PaginateText(document.ToString(), font, contentWidth, Math.Max(100, contentHeight - 88), Math.Max(100, contentHeight - 88));
            EnsureRenderedPageCount(pages.Count);
            renderedPageCount = Math.Max(1, pages.Count);
            discoveryDocumentPageIndex = Math.Max(0, Math.Min(discoveryDocumentPageIndex, renderedPageCount - 1));
            for (int index = 0; index < renderedPageEditors.Count; index++)
            {
                bool hasLocalNext = index + 1 < pages.Count;
                string nextHint = hasLocalNext
                    ? (chinese ? "按 N 查看下一张 A4 书单" : "Press N for the next A4 page")
                    : (!String.IsNullOrWhiteSpace(discoveryNextPageUrl)
                        ? (chinese ? "按 N 读取起点第 " + (discoveryRemotePage + 1) + "/" + discoveryRemotePageMax + " 页并创建新 A4 书单" : "Press N to load Qidian page " + (discoveryRemotePage + 1) + "/" + discoveryRemotePageMax + " and create more A4 pages")
                        : (chinese ? "已经是当前书单最后一页" : "This is the final book-list page"));
                string footer = Environment.NewLine + Environment.NewLine + "—— " +
                    (chinese ? "第 " + (index + 1) + " 页 / 共 " + pages.Count + " 页" : "Page " + (index + 1) + " of " + pages.Count) +
                    " ——" + Environment.NewLine + nextHint +
                    (index > 0 ? (chinese ? "；按 P 返回上一页" : "; press P for the previous page") : String.Empty) +
                    (chinese ? "；输入书籍序号查看详情" : "; enter a book number for details");
                RichTextBox editor = renderedPageEditors[index];
                editor.Font = font;
                editor.Text = pages[index] + footer;
                editor.SelectionStart = 0;
            }
            decoy.Text = String.Empty;
            ApplyLayout();
            ShowDiscoveryDocumentPage(discoveryDocumentPageIndex);
            UpdateModeStatus();
        }

        void ShowDiscoveryDocumentPage(int index)
        {
            if (renderedPagePanels.Count == 0) return;
            discoveryDocumentPageIndex = Math.Max(0, Math.Min(index, renderedPagePanels.Count - 1));
            BeginInvoke((MethodInvoker)delegate
            {
                if (!IsPagedDiscoveryView() || discoveryDocumentPageIndex >= renderedPagePanels.Count) return;
                pagedDocumentHost.ScrollControlIntoView(renderedPagePanels[discoveryDocumentPageIndex]);
                UpdateModeStatus();
            });
        }

        void MoveDiscoveryDocumentPage(int direction)
        {
            if (currentPageKind != CommandPageKind.Discovery || loadingDiscovery) return;
            int target = discoveryDocumentPageIndex + direction;
            if (target >= 0 && target < renderedPageCount)
            {
                ShowDiscoveryDocumentPage(target);
                return;
            }
            if (direction > 0 && !String.IsNullOrWhiteSpace(discoveryNextPageUrl))
            {
                LoadNextDiscoveryPage();
                return;
            }
            bookshelfNotice = direction > 0
                ? (chinese ? "已经是当前书单最后一页。" : "This is the final book-list page.")
                : (chinese ? "已经是当前书单第一页。" : "This is the first book-list page.");
            SetCommandHint(CurrentCommandHint());
        }

        void LoadNextDiscoveryPage()
        {
            if (loadingDiscovery || String.IsNullOrWhiteSpace(discoveryNextPageUrl)) return;
            string nextUrl = discoveryNextPageUrl;
            discoveryOperationId++;
            loadingDiscovery = true;
            extractingDiscovery = false;
            appendingDiscoveryPage = true;
            discoveryPageCountBeforeAppend = Math.Max(1, renderedPageCount);
            keepBrowserRunningBehindDocument = true;
            bookshelfNotice = chinese ? "正在读取下一页书单并创建新的 A4 页面……" : "Loading the next book-list page and creating more A4 pages...";
            SetCommandHint(CurrentCommandHint());
            Navigate(nextUrl);
        }

        void RenderDiscoveryFilter()
        {
            if (discoveryFilterGroups.Count == 0)
            {
                RenderDiscoveryBooks();
                return;
            }
            discoveryFilterGroupIndex = Math.Max(0, Math.Min(discoveryFilterGroupIndex, discoveryFilterGroups.Count - 1));
            DiscoveryFilterGroup group = discoveryFilterGroups[discoveryFilterGroupIndex];
            currentPageKind = CommandPageKind.DiscoveryFilter;
            StringBuilder output = new StringBuilder();
            output.AppendLine(discoveryTitle);
            output.AppendLine();
            output.AppendLine(chinese
                ? "筛选层级：第 " + (discoveryFilterGroupIndex + 1) + " 级 / 共 " + discoveryFilterGroups.Count + " 级"
                : "Filter level " + (discoveryFilterGroupIndex + 1) + " of " + discoveryFilterGroups.Count);
            output.AppendLine((chinese ? "当前选择：" : "Current group: ") + group.Title);
            output.AppendLine();
            for (int index = 0; index < group.Options.Length; index++)
            {
                DiscoveryFilterOption option = group.Options[index];
                output.AppendLine((index + 1) + ". " + option.Text + (option.Selected ? (chinese ? "  [当前]" : "  [current]") : String.Empty));
            }
            output.AppendLine();
            output.AppendLine(chinese
                ? "输入选项序号进入下一级。当前只读取了起点结果第 " + discoveryRemotePage + "/" + discoveryRemotePageMax + " 页，共 " + discoveryBooks.Count + " 本；输入 /结果 查看后，可在 A4 末页继续按 N 逐页读取全部结果。/排序 可进入人气排序，/返回 可回到上一级。"
                : "Enter an option number for the next level. Qidian result page " + discoveryRemotePage + "/" + discoveryRemotePageMax + " is currently loaded with " + discoveryBooks.Count + " books. Use /results, then press N at the final A4 page to load every following result page. /sort opens ordering and /back returns.");
            decoy.Text = output.ToString();
            ApplyLayout();
        }

        void SelectDiscoveryFilter(int number)
        {
            if (discoveryFilterGroups.Count == 0 || discoveryFilterGroupIndex < 0 || discoveryFilterGroupIndex >= discoveryFilterGroups.Count)
            {
                ShowCommandError(chinese ? "当前没有可选择的筛选层级。" : "No filter level is available.");
                return;
            }
            DiscoveryFilterGroup group = discoveryFilterGroups[discoveryFilterGroupIndex];
            if (number < 1 || number > group.Options.Length)
            {
                ShowCommandError(chinese ? "无效的筛选项序号。" : "Invalid filter option number.");
                return;
            }
            DiscoveryFilterOption option = group.Options[number - 1];
            if (String.IsNullOrWhiteSpace(option.Url))
            {
                ShowCommandError(chinese ? "该筛选项没有可用的官方链接。" : "This filter option has no usable official URL.");
                return;
            }
            PushNavigationState();
            bool rankingType = (group.Title ?? String.Empty).IndexOf("榜单", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (group.Title ?? String.Empty).IndexOf("Ranking type", StringComparison.OrdinalIgnoreCase) >= 0;
            int nextGroup = rankingType ? 0 : discoveryFilterGroupIndex + 1;
            StartDiscovery(discoveryTitle + " > " + option.Text, option.Url, true, discoveryIsRanking, nextGroup, false);
        }

        void ShowNextDiscoveryFilter()
        {
            if (discoveryFilterGroups.Count == 0)
            {
                ShowCommandError(chinese ? "当前列表没有识别到更多筛选项。" : "No additional filters were detected for this list.");
                return;
            }
            if (discoveryFilterGroupIndex >= discoveryFilterGroups.Count) discoveryFilterGroupIndex = 0;
            discoveryFilterRequested = true;
            ShowCommandPage();
            RenderDiscoveryFilter();
            SetCommandHint(CurrentCommandHint());
        }

        void ShowDiscoverySort()
        {
            int sortIndex = -1;
            for (int index = 0; index < discoveryFilterGroups.Count; index++)
            {
                DiscoveryFilterGroup group = discoveryFilterGroups[index];
                string optionText = String.Join(" ", (group.Options ?? new DiscoveryFilterOption[0]).Select(option => option.Text).ToArray());
                if ((group.Title ?? String.Empty).IndexOf("排序", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    optionText.IndexOf("人气", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    optionText.IndexOf("总收藏", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    optionText.IndexOf("推荐票", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    optionText.IndexOf("月票", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sortIndex = index;
                    break;
                }
            }
            if (sortIndex < 0)
            {
                ShowCommandError(chinese ? "当前页面没有识别到独立的人气排序栏。" : "No separate popularity ordering group was detected.");
                return;
            }
            discoveryFilterGroupIndex = sortIndex;
            discoveryFilterRequested = true;
            ShowCommandPage();
            RenderDiscoveryFilter();
            SetCommandHint(CurrentCommandHint());
        }

        void OpenDiscoveryBook(int number)
        {
            if (number < 1 || number > discoveryBooks.Count)
            {
                ShowCommandError(chinese ? "无效的书籍序号。" : "Invalid book number.");
                return;
            }
            PushNavigationState();
            selectedBook = discoveryBooks[number - 1];
            LoadBookDetail(selectedBook);
        }

        void LoadBookDetail(BookItem book)
        {
            if (!ready || browser.CoreWebView2 == null || book == null || String.IsNullOrWhiteSpace(book.BookUrl))
            {
                ShowCommandError(chinese ? "当前小说没有有效的详情页地址。" : "This book has no valid details URL.");
                return;
            }
            CancelPageOperations();
            bookDetailOperationId++;
            loadingBookDetail = true;
            extractingBookDetail = false;
            selectedBook = book;
            currentBookDetail = null;
            chapters.Clear();
            currentChapterIndex = -1;
            bookshelfNotice = String.Empty;
            keepBrowserRunningBehindDocument = true;
            ShowCommandPage();
            currentPageKind = CommandPageKind.Message;
            decoy.Text = chinese ? "正在读取《" + book.Title + "》的详情……" : "Loading details for " + book.Title + "...";
            SetCommandHint(CurrentCommandHint());
            Navigate(book.BookUrl);
        }

        async Task ExtractBookDetail(int operationId)
        {
            try
            {
                BookDetail detail = null;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    if (!loadingBookDetail || operationId != bookDetailOperationId) return;
                    detail = await ReadBookDetail();
                    if (detail != null && !String.IsNullOrWhiteSpace(detail.Title)) break;
                    await Task.Delay(500);
                }
                if (detail == null || String.IsNullOrWhiteSpace(detail.Title))
                {
                    FailBookDetail(operationId, detail != null && !String.IsNullOrWhiteSpace(detail.Error)
                        ? detail.Error
                        : (chinese ? "详情区域没有完成渲染。" : "The details area did not finish rendering."));
                    return;
                }
                loadingBookDetail = false;
                extractingBookDetail = false;
                keepBrowserRunningBehindDocument = false;
                currentBookDetail = detail;
                selectedBook.Title = detail.Title;
                selectedBook.BookUrl = detail.BookUrl;
                selectedBook.Author = detail.Author;
                selectedBook.Category = detail.Category;
                selectedBook.Status = detail.Status;
                selectedBook.Intro = detail.Intro;
                if (!String.IsNullOrWhiteSpace(detail.CatalogUrl)) currentCatalogUrl = detail.CatalogUrl;
                ShowCommandPage();
                RenderBookDetail();
                SetCommandHint(CurrentCommandHint());
            }
            catch (Exception exception)
            {
                FailBookDetail(operationId, exception.GetType().Name + " - " + exception.Message);
            }
        }

        async Task<BookDetail> ReadBookDetail()
        {
            string script = "(function(){try{const clean=x=>(x||'').replace(/\\s+/g,' ').trim();const abs=u=>{try{return new URL(u,location.href).href}catch(e){return ''}};const text=clean(document.body&&document.body.innerText);" +
                "const pick=s=>{for(const q of s){const e=document.querySelector(q);if(e&&clean(e.innerText||e.textContent))return e}return null};const value=(regex)=>{const m=text.match(regex);return m?clean(m[1]):''};" +
                "const titleNode=pick(['h1','.book-info h1','.book-information h1','[class*=book-name]']);const authorNode=pick(['a[href*=\"/author/\"]','.writer a','.author a']);const introNode=pick(['#book-intro-detail','.book-intro-detail','#book-intro','.book-intro p','[class*=book-intro] p','[class*=intro]']);" +
                "const categoryNodes=Array.from(document.querySelectorAll('.tag a,.book-info a[href*=\"chanId\"],a[href*=\"/all/\"]')).filter(e=>clean(e.innerText).length<20).slice(0,4);const statusNode=Array.from(document.querySelectorAll('span,em,i')).find(e=>/连载|完本|完结/.test(clean(e.innerText)));" +
                "const catalog=Array.from(document.querySelectorAll('a[href*=\"/catalog\"]')).find(e=>e.offsetParent!==null)||document.querySelector('a[href*=\"/catalog\"]');const read=Array.from(document.querySelectorAll('a[href*=\"/chapter/\"]')).find(e=>/阅读|试读|继续|最新章节|开始/.test(clean(e.innerText)))||document.querySelector('a[href*=\"/chapter/\"]');" +
                "const latest=Array.from(document.querySelectorAll('a[href*=\"/chapter/\"]')).find(e=>/最新/.test(clean((e.parentElement&&e.parentElement.innerText)||e.innerText)))||read;const add=Array.from(document.querySelectorAll('a,button')).find(e=>/加入书架|已在书架|移出书架/.test(clean(e.innerText)));" +
                "const authorBox=authorNode&&(authorNode.closest('.author-info,.writer,.book-info')||authorNode.parentElement);const achievements=Array.from(document.querySelectorAll('[class*=honor] li,[class*=achievement] li,.book-label li')).map(e=>clean(e.innerText)).filter(Boolean).slice(0,12).join('；');" +
                "return JSON.stringify({Title:clean(titleNode&&titleNode.innerText),BookUrl:location.origin+location.pathname,Author:clean(authorNode&&authorNode.innerText),AuthorUrl:abs(authorNode&&authorNode.getAttribute('href')),AuthorInfo:clean(authorBox&&authorBox.innerText).slice(0,300),Intro:clean(introNode&&introNode.innerText).slice(0,1600),Category:categoryNodes.map(e=>clean(e.innerText)).join(' · '),Status:clean(statusNode&&statusNode.innerText),WordCount:value(/([\\d.万亿]+)\\s*字/),TotalRecommendations:value(/([\\d.万亿]+)\\s*总推荐/),WeeklyRecommendations:value(/([\\d.万亿]+)\\s*(?:周推荐|本周推荐)/),Achievements:achievements,LatestChapter:clean(latest&&latest.innerText),LatestUpdate:value(/最新更新[：:]?\\s*([^ ]{2,80})/),CatalogUrl:abs(catalog&&catalog.getAttribute('href')),ReadUrl:abs(read&&read.getAttribute('href')),InBookshelf:!!(add&&/已在书架|移出书架/.test(clean(add.innerText))),Error:''});" +
                "}catch(error){return JSON.stringify({Error:String(error&&error.stack||error)});}})()";
            string encoded = await browser.ExecuteScriptAsync(script);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Deserialize<string>(encoded);
            return String.IsNullOrWhiteSpace(json) ? null : serializer.Deserialize<BookDetail>(json);
        }

        void FailBookDetail(int operationId, string reason)
        {
            if (!loadingBookDetail || operationId != bookDetailOperationId) return;
            loadingBookDetail = false;
            extractingBookDetail = false;
            keepBrowserRunningBehindDocument = false;
            ShowCommandPage();
            currentPageKind = CommandPageKind.Message;
            decoy.Text = (chinese ? "小说详情读取失败\n\n原因：" : "Book details loading failed\n\nReason: ") + reason +
                (chinese ? "\n\n可输入 /网页 查看官方详情页，或 /返回。" : "\n\nUse /web for the official details page, or /back.");
            SetCommandHint(CurrentCommandHint());
        }

        void RenderBookDetail()
        {
            if (currentBookDetail == null)
            {
                ShowCommandError(chinese ? "当前没有已读取的小说详情。" : "No book details are loaded.");
                return;
            }
            currentPageKind = CommandPageKind.BookDetail;
            BookDetail detail = currentBookDetail;
            StringBuilder output = new StringBuilder();
            output.AppendLine("《" + detail.Title + "》");
            output.AppendLine();
            AppendDetailLine(output, chinese ? "作者" : "Author", detail.Author);
            AppendDetailLine(output, chinese ? "作者信息" : "Author info", detail.AuthorInfo);
            AppendDetailLine(output, chinese ? "分类" : "Category", detail.Category);
            AppendDetailLine(output, chinese ? "状态" : "Status", detail.Status);
            AppendDetailLine(output, chinese ? "字数" : "Word count", detail.WordCount);
            AppendDetailLine(output, chinese ? "总推荐" : "Total recommendations", detail.TotalRecommendations);
            AppendDetailLine(output, chinese ? "周推荐" : "Weekly recommendations", detail.WeeklyRecommendations);
            AppendDetailLine(output, chinese ? "最新章节" : "Latest chapter", detail.LatestChapter);
            AppendDetailLine(output, chinese ? "最近更新" : "Latest update", detail.LatestUpdate);
            AppendDetailLine(output, chinese ? "作品成绩" : "Achievements", detail.Achievements);
            AppendDetailLine(output, chinese ? "书架状态" : "Bookshelf", detail.InBookshelf ? (chinese ? "已在书架" : "Added") : (chinese ? "未加入" : "Not added"));
            output.AppendLine();
            output.AppendLine(chinese ? "简介" : "Introduction");
            output.AppendLine(String.IsNullOrWhiteSpace(detail.Intro) ? (chinese ? "暂无可读取简介。" : "No introduction was detected.") : detail.Intro);
            output.AppendLine();
            output.AppendLine(chinese ? "可用命令：/加入书架  /目录  /阅读  /返回" : "Commands: /add  /catalog  /read  /back");
            decoy.Text = output.ToString();
        }

        static void AppendDetailLine(StringBuilder output, string label, string value)
        {
            if (!String.IsNullOrWhiteSpace(value)) output.AppendLine(label + "：" + value);
        }

        async void AddCurrentBookToBookshelf()
        {
            if (currentBookDetail == null || browser.CoreWebView2 == null || browser.Source == null || !IsBookDetailAddress(browser.Source))
            {
                ShowCommandError(chinese ? "请先从搜索、分类或排行进入小说详情页。" : "Open a book details page first.");
                return;
            }
            if (currentBookDetail.InBookshelf)
            {
                bookshelfNotice = chinese ? "该小说已在书架中。" : "This book is already in your bookshelf.";
                SetCommandHint(CurrentCommandHint());
                return;
            }
            try
            {
                string script = "(function(){const clean=x=>(x||'').replace(/\\s+/g,' ').trim();const target=Array.from(document.querySelectorAll('a,button')).find(e=>e.offsetParent!==null&&/^(加入书架|加书架)$/.test(clean(e.innerText)));if(!target)return 'missing';target.scrollIntoView({block:'center'});target.click();return 'clicked';})()";
                string encoded = await browser.ExecuteScriptAsync(script);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string result = serializer.Deserialize<string>(encoded);
                if (result != "clicked")
                {
                    ShowCommandError(chinese ? "未找到官方“加入书架”控件，页面结构可能已变化。可输入 /网页 手动操作。" : "The official Add to bookshelf control was not found. Use /web to add it manually.");
                    return;
                }
                await Task.Delay(1200);
                if (browser.Source != null && IsLoginAddress(browser.Source))
                {
                    bookshelfNotice = chinese ? "请在官方页面登录，完成后再输入 /加入书架。" : "Sign in on the official page, then use /add again.";
                    SetMode(ReaderMode.Account);
                }
                else
                {
                    currentBookDetail.InBookshelf = true;
                    bookshelfNotice = chinese ? "已通过官方页面提交加入书架。" : "The official page accepted the bookshelf request.";
                    RenderBookDetail();
                }
                SetCommandHint(CurrentCommandHint());
            }
            catch (Exception exception)
            {
                ShowCommandError((chinese ? "加入书架失败：" : "Failed to add to bookshelf: ") + exception.GetType().Name);
            }
        }

        void BeginCurrentBookReading()
        {
            if (selectedBook == null || currentBookDetail == null)
            {
                ShowCommandError(chinese ? "请先打开一本小说的详情。" : "Open a book's details first.");
                return;
            }
            PushNavigationState();
            if (!String.IsNullOrWhiteSpace(currentBookDetail.ReadUrl))
            {
                OpenChapterUrl(currentBookDetail.ReadUrl, 0);
                return;
            }
            startReadingAfterCatalog = true;
            LoadCatalog();
        }

        void OpenChapterWithHistory(int number)
        {
            PushNavigationState();
            OpenChapter(number);
        }

        void ShowHelp()
        {
            ShowGuide();
        }

        void BuildGuideOverlay()
        {
            guideOverlay.Visible = false;
            guideOverlay.BackColor = Color.FromArgb(232, 232, 232);
            guideOverlay.TabStop = true;
            guidePaper.BackColor = Color.White;
            guidePaper.BorderStyle = BorderStyle.FixedSingle;
            guideOverlay.Controls.Add(guidePaper);

            Label heading = new Label
            {
                Dock = DockStyle.Top, Height = 52, Padding = new Padding(28, 13, 0, 0),
                Font = new Font("Microsoft YaHei", 18F), ForeColor = Color.FromArgb(40, 40, 40), Text = "新手指引"
            };
            guideBackButton.Dock = DockStyle.Bottom;
            guideBackButton.Height = 38;
            guideBackButton.FlatStyle = FlatStyle.Flat;
            guideBackButton.BackColor = Color.FromArgb(43, 87, 154);
            guideBackButton.ForeColor = Color.White;
            guideBackButton.Font = new Font("Microsoft YaHei", 10F);
            guideBackButton.Click += delegate { HideGuide(); };
            guideText.Dock = DockStyle.Fill;
            guideText.BorderStyle = BorderStyle.None;
            guideText.BackColor = Color.White;
            guideText.ReadOnly = true;
            guideText.DetectUrls = false;
            guideText.Font = new Font("Microsoft YaHei", 10.5F);
            guideText.Padding = new Padding(24);
            guidePaper.Controls.Add(guideText);
            guidePaper.Controls.Add(guideBackButton);
            guidePaper.Controls.Add(heading);
            Controls.Add(guideOverlay);
            UpdateGuideText();
        }

        void LayoutGuideOverlay()
        {
            if (guideOverlay == null || guideOverlay.IsDisposed || titleBar == null) return;
            int top = titleBar.Bottom + Padding.Top;
            guideOverlay.Bounds = new Rectangle(Padding.Left, top, Math.Max(1, ClientSize.Width - Padding.Horizontal), Math.Max(1, ClientSize.Height - top - Padding.Bottom));
            int paperWidth = Math.Max(320, Math.Min(780, guideOverlay.ClientSize.Width - 48));
            int paperHeight = Math.Max(260, guideOverlay.ClientSize.Height - 48);
            guidePaper.Bounds = new Rectangle(Math.Max(24, (guideOverlay.ClientSize.Width - paperWidth) / 2), 24, paperWidth, paperHeight);
        }

        void ToggleGuide()
        {
            if (guideOverlay.Visible) HideGuide();
            else ShowGuide();
        }

        void ShowGuide()
        {
            if (guideOverlay.Visible) return;
            guideReturnFocus = ActiveControl;
            guideBrowserWasVisible = browserHost.Visible;
            browserHost.Visible = false;
            UpdateGuideText();
            LayoutGuideOverlay();
            guideOverlay.Visible = true;
            guideOverlay.BringToFront();
            guideText.SelectionStart = 0;
            guideText.SelectionLength = 0;
            guideText.ScrollToCaret();
            guideBackButton.Focus();
        }

        void HideGuide()
        {
            if (!guideOverlay.Visible) return;
            guideOverlay.Visible = false;
            browserHost.Visible = guideBrowserWasVisible;
            if (browserHost.Visible) browserHost.BringToFront();
            else if (mode == ReaderMode.Hidden) decoy.BringToFront();
            Control returnFocus = guideReturnFocus;
            guideReturnFocus = null;
            if (returnFocus != null && !returnFocus.IsDisposed && returnFocus.CanFocus) returnFocus.Focus();
            else if (mode == ReaderMode.Hidden) commandInput.Focus();
        }

        void UpdateGuideText()
        {
            if (guideBackButton == null) return;
            noviceGuideButton.Text = chinese ? "新手指引" : "Guide";
            guideBackButton.Text = chinese ? "返回原位置" : "Return to reading";
            guideText.Text = chinese
                ? "一、开始使用\n\n1. 点击顶部“登录”，在起点官方页面完成登录。\n2. 输入 /书架，或点击“替换”，读取个人书架。\n3. 输入书籍序号进入目录；输入章节序号打开对应章节。\n4. 输入 /继续，可从书架保存的历史进度继续。\n\n二、阅读与订阅\n\n• /文字：识别当前已授权章节并切换为纯文字阅读。\n• /网页：临时返回起点官方页面。\n• VIP 章节可点击右侧“订阅本章”或输入 /订阅；执行前会显示单章确认。\n• 订阅只使用官方页面和账户余额，不启用自动订阅或自动充值；失败原因显示在顶部。\n• 普通按键用于继续显示正文；章节结束后使用 /下一章。\n\n三、显示速度\n\n• 在“开始”选项卡的字号下拉框中选择数字，表示每次按键显示的字数。\n• 选择“一行”或“两行”，表示每次按键显示对应行数。\n• 也可输入 /字数 100 或 /行数 2。\n\n四、阅读视图\n\n• 沉浸模式：只保留当前阅读区域，适合隐蔽阅读。\n• 滚动模式：已读内容保留在连续 A4 页面中，可向上回看。\n• Ctrl + 鼠标滚轮或右下角缩放条：仅调整纸张和正文缩放。\n• OCR 会预取并缓存邻近页面；状态栏显示当前缓存和识别状态。\n\n五、导航命令\n\n/书架    返回并刷新个人书架\n/目录    返回当前书籍目录\n/下一章  打开下一章\n/上一章  打开上一章\n/订阅    使用账户余额订阅当前单章\n/继续    从记录的进度继续\n/隐藏    返回伪装文档\n/帮助    打开本指引\n\n六、命令输入与快捷键\n\n• 输入 / 显示命令候选；Tab 或鼠标单击可补全。\n• // 表示在普通文本中换行。\n• F8、F9、Ctrl+Alt+Space 可快速返回文档视图。\n• 顶部 EN/中 切换界面语言。\n\n打开本指引不会重新加载章节，也不会修改已经显示的文字、页码、滚动位置或阅读进度。"
                : "1. Getting started\n\nSign in on the official Qidian page, use /bookshelf, select a book number, then select a chapter number. Use /resume to continue from saved progress.\n\n2. Reading and subscription\n\n/text recognizes the currently authorized chapter. /web returns to the official page. Use Subscribe chapter or /subscribe for one VIP chapter. A confirmation is always shown first; auto-subscribe and automatic recharge stay disabled. Failures appear in the top message area.\n\n3. Reveal speed\n\nChoose a number, One line, or Two lines from the font-size selector. Commands /chars 100 and /lines 2 remain available.\n\n4. Views and zoom\n\nImmersive mode focuses on the current text. Scrolling mode keeps read text on continuous A4 pages. Use Ctrl+mouse wheel or the lower-right zoom slider.\n\n5. Navigation\n\n/bookshelf, /catalog, /next, /previous, /subscribe, /resume, /hide, /help. Type / for suggestions and press Tab or click to complete.\n\nClosing this guide restores the exact previous reading screen without reloading the chapter or resetting text, pages, scroll position, or progress.";
            guideText.Text = (chinese
                ? "小说发现命令\n\n/搜索 书名或作者    搜索小说\n/分类               读取全部分类筛选层级\n/排行               先选择榜单，再选择分类或时间层级\n/筛选               继续当前的下一级筛选\n/排序               选择人气、收藏、字数、推荐票或月票等排序\n/结果               随时查看当前小说列表\n\n书单会自动排成连续 A4 页面。按 N 查看下一张 A4，按 P 返回上一张；到达已加载书单末页后继续按 N，会读取起点下一页并创建更多 A4 页面。状态栏会显示当前书单页码。\n\n筛选页面会显示“第几级 / 共几级”。输入选项序号进入下一级；/返回 可逐级撤销选择。输入书籍序号打开详情；详情页支持 /加入书架、/阅读 和 /目录。\n\n"
                : "Book discovery commands\n\n/search keywords searches books. /category reads every available category level. /rank first selects a ranking type, then category or time filters. /filter continues to the next level, /sort opens popularity ordering, and /results shows the current books.\n\nBook lists are arranged on continuous A4 pages. Press N for the next page and P for the previous page. At the final loaded page, N loads Qidian's next result page and creates more A4 pages. The status bar shows the current list page.\n\nFilter pages show the current and total level count. Enter a number to continue; /back undoes one selection. Enter a book number for details, then use /add, /read, or /catalog.\n\n") + guideText.Text;
        }

        void ShowCommandPage()
        {
            mode = ReaderMode.Hidden;
            ApplyLayout();
            commandInput.Focus();
            SetCommandHint(CurrentCommandHint());
        }

        void ApplyLanguage()
        {
            string[] chineseTabs = { "文件", "开始", "插入", "设计", "布局", "引用", "邮件", "审阅", "视图", "帮助" };
            string[] englishTabs = { "File", "Home", "Insert", "Design", "Layout", "References", "Mailings", "Review", "View", "Help" };
            string[] selectedTabs = chinese ? chineseTabs : englishTabs;
            for (int index = 0; index < tabLabels.Length; index++) tabLabels[index].Text = selectedTabs[index];
            homeButton.Text = chinese ? "查找" : "Find";
            bookshelfButton.Text = chinese ? "替换" : "Replace";
            selectButton.Text = chinese ? "选择⌄" : "Select⌄";
            backButton.Text = "↶";
            forwardButton.Text = "↷";
            refreshButton.Text = "↻";
            accountButton.Text = chinese ? "登录" : "Sign in";
            noviceGuideButton.Text = chinese ? "新手指引" : "Guide";
            lineButton.Text = chinese ? "AaBbCc\n正文" : "AaBbCc\nNormal";
            hideButton.Text = chinese ? "▣\n粘贴" : "▣\nPaste";
            languageButton.Text = chinese ? "EN" : "中";
            subscribeButton.Text = chinese ? "◆\n订阅本章" : "◆\nSubscribe";
            typingScroll.Text = chinese ? "打字翻行" : "Typing scroll";
            shortcutHint.Text = chinese ? "F8 切换    F9 隐藏" : "F8 view    F9 hide";
            UpdateGuideText();
            PopulateReadingSelectors();
            decoy.Font = CreateDocumentFont();
            if (readingActive && ocrReadingActive) RenderOcrText();
            else if (currentPageKind == CommandPageKind.BookDetail && currentBookDetail != null) RenderBookDetail();
            else if (currentPageKind == CommandPageKind.Discovery && discoveryBooks.Count > 0) RenderDiscoveryBooks();
            else if (currentPageKind == CommandPageKind.DiscoveryFilter && discoveryFilterGroups.Count > 0) RenderDiscoveryFilter();
            else if (currentPageKind == CommandPageKind.Catalog && chapters.Count > 0 && selectedBook != null) RenderCatalog();
            else if (currentPageKind == CommandPageKind.Bookshelf && bookshelf.Count > 0) RenderBookshelf();
            else if (loginCompleted)
                decoy.Text = chinese ? "登录状态已保存。\n\n请输入 /书架 读取个人书库。" : "Login state saved.\n\nEnter /bookshelf to load your library.";
            else
                decoy.Text = chinese
                    ? "季度运营复盘\n\n执行摘要\n\n本文档汇总当前运营计划、关键里程碑以及下一轮复盘所需的跟进事项。工作组将继续核对前提条件、解决依赖问题，并完整记录相关决策。\n\n1. 交付概览\n\n当前交付计划与已批准的基线保持一致。各团队应在每周检查前更新风险，并标记可能影响范围、时间或质量的变化。\n\n2. 后续事项\n\n各负责人需确认完成日期、记录待解决问题，并为下一次会议准备支持材料。"
                    : "Quarterly Operations Review\n\nExecutive summary\n\nThis document consolidates the current operating plan, key milestones, and follow-up items for the next review cycle.\n\n1. Delivery overview\n\nThe delivery schedule remains aligned with the approved baseline.\n\n2. Action items\n\nOwners will confirm completion dates and document open questions.";
            SetCommandHint(CurrentCommandHint());
            UpdateModeStatus();
        }

        void UpdateModeStatus()
        {
            if (IsPagedDiscoveryView())
            {
                int pages = Math.Max(1, renderedPageCount);
                Size pageSize = GetScaledPageSize();
                int gap = Math.Max(14, (int)Math.Round(24 * documentZoom / 100.0));
                int scrollY = Math.Max(0, -pagedDocumentHost.AutoScrollPosition.Y - 24);
                discoveryDocumentPageIndex = Math.Max(0, Math.Min(pages - 1, scrollY / Math.Max(1, pageSize.Height + gap)));
                status.Text = chinese
                    ? "A4 第 " + (discoveryDocumentPageIndex + 1) + "/" + pages + " 页    起点第 " + discoveryRemotePage + "/" + discoveryRemotePageMax + " 页    已加载 " + discoveryBooks.Count + " 本    N 下一页 / P 上一页"
                    : "A4 " + (discoveryDocumentPageIndex + 1) + "/" + pages + "    Qidian " + discoveryRemotePage + "/" + discoveryRemotePageMax + "    " + discoveryBooks.Count + " books    N next / P previous";
                return;
            }
            if (readingActive && ocrReadingActive)
            {
                int pages = readingViewMode == ReadingViewMode.Scrolling ? Math.Max(1, renderedPageCount) : 1;
                int currentPage = 1;
                if (readingViewMode == ReadingViewMode.Scrolling && renderedPagePanels.Count > 0)
                {
                    Size pageSize = GetScaledPageSize();
                    int gap = Math.Max(14, (int)Math.Round(24 * documentZoom / 100.0));
                    int scrollY = Math.Max(0, -pagedDocumentHost.AutoScrollPosition.Y - 24);
                    currentPage = Math.Max(1, Math.Min(pages, scrollY / Math.Max(1, pageSize.Height + gap) + 1));
                }
                string viewName = readingViewMode == ReadingViewMode.Scrolling
                    ? (chinese ? "滚动模式" : "Scrolling")
                    : (chinese ? "沉浸模式" : "Immersive");
                status.Text = chinese
                    ? "第 " + currentPage + " 页，共 " + pages + " 页    字数：" + GetVisibleOcrCharacterCount() + "    " + viewName
                    : "Page " + currentPage + " of " + pages + "    Characters: " + GetVisibleOcrCharacterCount() + "    " + viewName;
                return;
            }
            if (showReaderStatusDetails && mode == ReaderMode.Account)
                status.Text = chinese ? "账户模式：请在起点官方页面完成登录、选书和订阅。" : "Account mode: use the official Qidian page for login, bookshelf, and subscription.";
            else if (showReaderStatusDetails && mode == ReaderMode.Line)
                status.Text = chinese ? "网页模式：普通按键推进阅读，按 F9 返回文档。" : "Web mode: use normal keys to advance and F9 to return to the document.";
            else
                status.Text = chinese ? "第 1 页，共 1 页    中文（中国）" : "Page 1 of 1    English (United States)";
        }
        async void BeginLogin()
        {
            if (!ready)
            {
                SetCommandHint(CurrentCommandHint());
                return;
            }
            loginOperationId++;
            int operationId = loginOperationId;
            loginFlowActive = true;
            loginCompleted = false;
            bookshelfNotice = chinese ? "请在下方官方页面完成登录，程序正在验证登录凭据" : "Complete login below while the application verifies your session";
            SetMode(ReaderMode.Account);
            SetCommandHint(CurrentCommandHint());
            Navigate(LoginUrl);

            for (int attempt = 0; attempt < 300; attempt++)
            {
                if (!loginFlowActive || operationId != loginOperationId) return;
                if (await HasBookshelfLoginCookie())
                {
                    loginFlowActive = false;
                    loginCompleted = true;
                    bookshelfNotice = String.Empty;
                    ShowCommandPage();
                    decoy.Text = chinese
                        ? "登录凭据验证成功。\n\n请输入 /书架 读取个人书库。"
                        : "Login credentials verified.\n\nEnter /bookshelf to load your library.";
                    SetCommandHint(CurrentCommandHint());
                    return;
                }
                if (attempt > 0 && attempt % 5 == 0)
                {
                    bookshelfNotice = chinese
                        ? "登录页面已打开，但尚未检测到书架身份凭据。" + loginCookieSummary
                        : "The login page is open, but no bookshelf credentials have been detected. " + loginCookieSummary;
                    SetCommandHint(CurrentCommandHint());
                }
                await Task.Delay(1000);
            }

            if (loginFlowActive && operationId == loginOperationId)
            {
                bookshelfNotice = chinese
                    ? "登录验证超时：未检测到适用于 my.qidian.com 的身份 Cookie"
                    : "Login verification timed out: no identity cookie for my.qidian.com was detected";
                SetCommandHint(CurrentCommandHint());
            }
        }

        async Task<bool> HasBookshelfLoginCookie()
        {
            try
            {
                string json = await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Storage.getCookies", "{}");
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                DevToolsCookieResponse response = serializer.Deserialize<DevToolsCookieResponse>(json);
                List<string> identityCookies = new List<string>();
                List<string> applicableCookieNames = new List<string>();
                int applicableCookieCount = 0;
                DevToolsCookie[] cookies = response == null || response.cookies == null ? new DevToolsCookie[0] : response.cookies;
                foreach (DevToolsCookie cookie in cookies)
                {
                    string domain = (cookie.domain ?? String.Empty).TrimStart('.');
                    string path = String.IsNullOrWhiteSpace(cookie.path) ? "/" : cookie.path;
                    bool domainMatches = String.Equals(domain, "my.qidian.com", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(domain, "qidian.com", StringComparison.OrdinalIgnoreCase);
                    bool pathMatches = "/bookcase/".StartsWith(path, StringComparison.OrdinalIgnoreCase);
                    if (!domainMatches || !pathMatches) continue;
                    applicableCookieCount++;
                    applicableCookieNames.Add(cookie.name);
                    if (IsIdentityCookieName(cookie.name)) identityCookies.Add(cookie.name);
                }
                loginCookieSummary = chinese
                    ? "适用 Cookie：" + applicableCookieCount + " 个（" + String.Join("、", applicableCookieNames.ToArray()) + "）；身份 Cookie：" + (identityCookies.Count == 0 ? "未检测到" : String.Join("、", identityCookies.ToArray()))
                    : "Applicable cookies: " + applicableCookieCount + " (" + String.Join(", ", applicableCookieNames.ToArray()) + "); identity cookies: " + (identityCookies.Count == 0 ? "none" : String.Join(", ", identityCookies.ToArray()));
                return identityCookies.Count > 0;
            }
            catch (Exception exception)
            {
                loginCookieSummary = "CDP error: " + exception.GetType().Name;
            }
            try
            {
                string encoded = await browser.ExecuteScriptAsync("document.cookie");
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string visibleCookies = serializer.Deserialize<string>(encoded) ?? String.Empty;
                List<string> names = new List<string>();
                foreach (string item in visibleCookies.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string name = item.Split('=')[0].Trim();
                    if (IsIdentityCookieName(name)) names.Add(name);
                }
                loginCookieSummary += (chinese ? "；页面 Cookie 身份项：" : "; page identity cookies: ") +
                    (names.Count == 0 ? (chinese ? "未检测到" : "none") : String.Join(", ", names.ToArray()));
                return names.Count > 0;
            }
            catch (Exception fallbackException)
            {
                loginCookieSummary += "; fallback error: " + fallbackException.GetType().Name;
                return false;
            }
        }

        static bool IsIdentityCookieName(string cookieName)
        {
            string name = (cookieName ?? String.Empty).ToLowerInvariant();
            return name == "ywkey";
        }

        async Task InitializeBrowser()
        {
            try
            {
                browserHost.Bounds = new Rectangle(-2, -2, 1, 1);
                browserHost.Visible = true;
                browser.Visible = true;
                SetCommandHint(chinese ? "正在启动内置浏览器……" : "Starting embedded browser...");
                string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuietReader", "WebView2");
                Directory.CreateDirectory(data);
                Task<CoreWebView2Environment> environmentTask = CoreWebView2Environment.CreateAsync(null, data);
                if (await Task.WhenAny(environmentTask, Task.Delay(12000)) != environmentTask)
                {
                    ShowBrowserInitializationFailure(chinese ? "浏览器环境启动超时。" : "Browser environment startup timed out.");
                    return;
                }
                CoreWebView2Environment environment = await environmentTask;
                SetCommandHint(chinese ? "正在连接内置浏览器……" : "Connecting embedded browser...");
                Task controllerTask = browser.EnsureCoreWebView2Async(environment);
                if (await Task.WhenAny(controllerTask, Task.Delay(12000)) != controllerTask)
                {
                    ShowBrowserInitializationFailure(chinese ? "浏览器控件创建超时。" : "Browser control creation timed out.");
                    return;
                }
                await controllerTask;
                ready = true;
                SetCommandHint(CurrentCommandHint());
                browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                browser.CoreWebView2.Settings.IsWebMessageEnabled = true;
                browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
                browser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                browser.CoreWebView2.ContentLoading += OnContentLoading;
                browser.CoreWebView2.DOMContentLoaded += OnDomContentLoaded;
                browser.CoreWebView2.WebResourceResponseReceived += OnWebResourceResponseReceived;
                browser.CoreWebView2.ProcessFailed += OnBrowserProcessFailed;
                browser.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                browser.CoreWebView2.WebMessageReceived += OnBrowserWebMessageReceived;
                browser.KeyDown += OnBrowserKeyDown;
                browser.CoreWebView2.DocumentTitleChanged += delegate { UpdateModeStatus(); };
                ApplyLayout();
                Navigate(HomeUrl);
            }
            catch (Exception exception)
            {
                ShowBrowserInitializationFailure((chinese ? "浏览器初始化失败：" : "Browser initialization failed: ") + exception.GetType().Name);
            }
        }

        void ShowBrowserInitializationFailure(string reason)
        {
            ready = false;
            browserHost.Visible = false;
            decoy.Visible = true;
            bookshelfNotice = chinese ? "初始化失败，请重启程序" : "Initialization failed. Restart the application";
            decoy.Text = chinese
                ? "内置浏览器初始化失败\n\n原因：" + reason + "\n\n请关闭程序后重新打开。如果仍然失败，请确认 Microsoft Edge WebView2 Runtime 可正常运行。"
                : "Embedded browser initialization failed\n\nReason: " + reason + "\n\nRestart the application. If it still fails, verify that Microsoft Edge WebView2 Runtime is available.";
            SetCommandHint(CurrentCommandHint());
            ApplyLayout();
        }

        async void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            Uri target;
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out target) ||
                (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp))
            {
                args.Handled = true;
                return;
            }

            if (!loginFlowActive)
            {
                args.Handled = true;
                browser.Source = target;
                return;
            }

            CoreWebView2Deferral deferral = args.GetDeferral();
            Form authenticationWindow = null;
            WebView2 authenticationBrowser = null;
            try
            {
                authenticationWindow = new Form();
                authenticationWindow.Text = chinese ? "账户验证" : "Account verification";
                authenticationWindow.StartPosition = FormStartPosition.CenterParent;
                authenticationWindow.Size = new Size(760, 680);
                authenticationWindow.MinimumSize = new Size(520, 520);
                authenticationBrowser = new WebView2();
                authenticationBrowser.Dock = DockStyle.Fill;
                authenticationBrowser.DefaultBackgroundColor = Color.White;
                authenticationWindow.Controls.Add(authenticationBrowser);
                await authenticationBrowser.EnsureCoreWebView2Async(browser.CoreWebView2.Environment);
                authenticationBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                authenticationBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                authenticationBrowser.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
                authenticationWindow.FormClosed += delegate
                {
                    authenticationWindows.Remove(authenticationWindow);
                    authenticationBrowser.Dispose();
                };
                authenticationWindows.Add(authenticationWindow);
                args.NewWindow = authenticationBrowser.CoreWebView2;
                args.Handled = true;
                authenticationWindow.Show(this);
                bookshelfNotice = chinese
                    ? "已打开内置账户验证窗口，完成后会自动检查书架权限"
                    : "An embedded account verification window is open; bookshelf access will be checked automatically";
                SetCommandHint(CurrentCommandHint());
            }
            catch (Exception exception)
            {
                args.Handled = true;
                if (authenticationWindow != null) authenticationWindow.Close();
                bookshelfNotice = (chinese ? "无法创建内置账户验证窗口：" : "Unable to create the embedded account verification window: ") + exception.GetType().Name;
                SetCommandHint(CurrentCommandHint());
            }
            finally
            {
                deferral.Complete();
            }
        }

        async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess || browser.Source == null)
            {
                if (loadingBookshelf)
                {
                    int operationId = bookshelfOperationId;
                    string error = args.WebErrorStatus.ToString();
                    bool transient = error.IndexOf("Aborted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("Connect", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (transient && bookshelfNavigationRetries < 2)
                    {
                        bookshelfNavigationRetries++;
                        UpdateBookshelfStage(chinese
                            ? "连接中断，正在自动重试 " + bookshelfNavigationRetries + "/2"
                            : "Connection interrupted; retrying " + bookshelfNavigationRetries + "/2");
                        await Task.Delay(1200);
                        if (loadingBookshelf && operationId == bookshelfOperationId) Navigate(BookshelfUrl);
                        return;
                    }
                    FailBookshelf(bookshelfOperationId, chinese
                        ? "书架页面加载失败：" + args.WebErrorStatus
                        : "Bookshelf navigation failed: " + args.WebErrorStatus);
                }
                else if (loadingDiscovery) FailDiscovery(discoveryOperationId, (chinese ? "页面加载失败：" : "Page navigation failed: ") + args.WebErrorStatus);
                else if (loadingBookDetail) FailBookDetail(bookDetailOperationId, (chinese ? "页面加载失败：" : "Page navigation failed: ") + args.WebErrorStatus);
                return;
            }
            Uri current = browser.Source;

            if (loginFlowActive)
            {
                if (!IsLoginAddress(current) && IsQidianHost(current.Host))
                {
                    bookshelfNotice = chinese
                        ? "登录页面已跳转，正在验证 my.qidian.com 身份 Cookie"
                        : "The login page redirected; verifying the identity cookie for my.qidian.com";
                    SetCommandHint(CurrentCommandHint());
                }
            }

            if (loadingDiscovery)
            {
                int operationId = discoveryOperationId;
                if (IsLoginAddress(current))
                {
                    FailDiscovery(operationId, chinese ? "页面跳转到了登录页。" : "The page redirected to sign-in.");
                    return;
                }
                if (IsDiscoveryAddress(current))
                {
                    if (!extractingDiscovery)
                    {
                        extractingDiscovery = true;
                        await ExtractDiscovery(operationId);
                    }
                    return;
                }
                FailDiscovery(operationId, chinese ? "小说列表跳转到了无法识别的页面。" : "The book list redirected to an unknown page.");
                return;
            }

            if (loadingBookDetail)
            {
                int operationId = bookDetailOperationId;
                if (IsLoginAddress(current))
                {
                    FailBookDetail(operationId, chinese ? "详情页跳转到了登录页。" : "The details page redirected to sign-in.");
                    return;
                }
                if (IsBookDetailAddress(current))
                {
                    if (!extractingBookDetail)
                    {
                        extractingBookDetail = true;
                        await ExtractBookDetail(operationId);
                    }
                    return;
                }
                FailBookDetail(operationId, chinese ? "详情请求跳转到了其它页面。" : "The details request redirected elsewhere.");
                return;
            }

            if (loadingBookshelf)
            {
                int operationId = bookshelfOperationId;
                if (IsLoginAddress(current))
                {
                    loadingBookshelf = false;
                    extractingBookshelf = false;
                    keepBrowserRunningBehindDocument = false;
                    bookshelfTimer.Stop();
                    loginFlowActive = true;
                    bookshelfNotice = chinese ? "请在下方官方页面完成登录" : "Complete login in the official page below";
                    SetMode(ReaderMode.Account);
                    SetCommandHint(CurrentCommandHint());
                    return;
                }
                if (IsBookshelfAddress(current))
                {
                    if (!extractingBookshelf)
                    {
                        extractingBookshelf = true;
                        await ExtractBookshelf(operationId);
                    }
                    return;
                }
                FailBookshelf(operationId, IsQidianHost(current.Host)
                    ? (chinese ? "书架请求被重定向到其它起点页面，登录状态可能已失效。" : "The bookshelf request was redirected to another Qidian page. Your login may have expired.")
                    : (chinese ? "书架请求跳转到了非起点页面，已停止读取。" : "The bookshelf request left Qidian, so loading was stopped."));
            }

            if (loadingCatalog)
            {
                int operationId = catalogOperationId;
                if (IsLoginAddress(current))
                {
                    FailCatalog(operationId, chinese ? "登录状态已失效。" : "Your login session expired.");
                    return;
                }
                if (IsCatalogAddress(current))
                {
                    if (!extractingCatalog)
                    {
                        extractingCatalog = true;
                        await ExtractCatalog(operationId);
                    }
                    return;
                }
                FailCatalog(operationId, chinese ? "目录请求跳转到了其它页面。" : "The catalog request was redirected to another page.");
                return;
            }

            if (openingChapter)
            {
                if (IsLoginAddress(current))
                {
                    FailChapter(chinese ? "登录状态已失效，请重新登录。" : "Your login expired. Sign in again.");
                    return;
                }
                if (IsChapterAddress(current))
                {
                    if (!preparingChapter)
                    {
                        preparingChapter = true;
                        await PrepareChapter();
                    }
                    return;
                }
                if (IsSubscriptionAddress(current))
                {
                    openingChapter = false;
                    preparingChapter = false;
                    readingActive = false;
                    keepBrowserRunningBehindDocument = false;
                    bookshelfNotice = chinese ? "该章节需要订阅，请在官方页面完成后输入 /继续" : "This chapter requires a subscription. Complete it on the official page, then enter /resume";
                    SetMode(ReaderMode.Account);
                    SetCommandHint(CurrentCommandHint());
                    return;
                }
                int operationId = chapterOperationId;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    await Task.Delay(400);
                    if (!openingChapter || operationId != chapterOperationId) return;
                    Uri latest = browser.Source;
                    if (latest == null) continue;
                    if (IsChapterAddress(latest))
                    {
                        if (!preparingChapter)
                        {
                            preparingChapter = true;
                            await PrepareChapter();
                        }
                        return;
                    }
                    if (IsSubscriptionAddress(latest))
                    {
                        openingChapter = false;
                        preparingChapter = false;
                        readingActive = false;
                        keepBrowserRunningBehindDocument = true;
                        bookshelfNotice = chinese ? "该 VIP 章节尚未订阅，可点击右侧“订阅本章”或输入 /订阅" : "This VIP chapter is not subscribed. Use Subscribe chapter or enter /subscribe";
                        SetMode(ReaderMode.Hidden);
                        SetCommandHint(CurrentCommandHint());
                        return;
                    }
                }
                FailChapter(chinese ? "章节请求最终停留在无法识别的页面。" : "The chapter request remained on an unknown page.");
                return;
            }

            if (openingBook)
            {
                openingBook = false;
                keepBrowserRunningBehindDocument = false;
                ShowCommandPage();
                string progress = selectedBook == null ? String.Empty : selectedBook.ProgressTitle;
                decoy.Text = chinese
                    ? "已在内置后台进入《" + selectedBook.Title + "》的阅读进度页。\n\n" +
                        (String.IsNullOrWhiteSpace(progress) ? "未读取到章节名称。" : "当前进度：" + progress) +
                        "\n\n页面不会单独弹出。下一步可继续实现正文的文本化阅读。"
                    : "Opened the saved reading position for " + selectedBook.Title + " inside the embedded browser.\n\n" +
                        (String.IsNullOrWhiteSpace(progress) ? "No chapter title was detected." : "Current position: " + progress) +
                        "\n\nNo separate browser window was opened.";
                SetCommandHint(CurrentCommandHint());
            }
        }

        static bool IsLoginAddress(Uri address)
        {
            return address.Host.IndexOf("passport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                address.AbsolutePath.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsBookshelfAddress(Uri address)
        {
            return String.Equals(address.Host, "my.qidian.com", StringComparison.OrdinalIgnoreCase) ||
                address.AbsoluteUri.IndexOf("bookcase", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsCatalogAddress(Uri address)
        {
            return IsQidianHost(address.Host) && address.AbsolutePath.IndexOf("/catalog", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsDiscoveryAddress(Uri address)
        {
            if (!IsQidianHost(address.Host)) return false;
            string path = address.AbsolutePath;
            return path.IndexOf("/soushu/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/search", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/all/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/rank/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsBookDetailAddress(Uri address)
        {
            if (!IsQidianHost(address.Host) || IsCatalogAddress(address) || IsChapterAddress(address)) return false;
            string[] parts = address.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && String.Equals(parts[0], "book", StringComparison.OrdinalIgnoreCase) && parts[1].All(Char.IsDigit);
        }

        static bool IsChapterAddress(Uri address)
        {
            return IsQidianHost(address.Host) && address.AbsolutePath.IndexOf("/chapter/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsSubscriptionAddress(Uri address)
        {
            return address.AbsolutePath.IndexOf("subscribe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                address.Host.IndexOf("pay.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void OnBookshelfTimerTick(object sender, EventArgs args)
        {
            if (!loadingBookshelf) return;
            int elapsed = Math.Max(0, (int)(DateTime.Now - bookshelfStartedAt).TotalSeconds);
            if (elapsed >= 60)
            {
                FailBookshelf(bookshelfOperationId, chinese
                    ? "读取超过 60 秒，已自动停止。最后状态：" + bookshelfStage
                    : "Loading exceeded 60 seconds and was stopped. Last stage: " + bookshelfStage);
                return;
            }
            RenderBookshelfProgress();
        }

        void UpdateBookshelfStage(string stage)
        {
            if (!loadingBookshelf) return;
            bookshelfStage = stage;
            RenderBookshelfProgress();
        }

        void RenderBookshelfProgress()
        {
            if (!loadingBookshelf) return;
            int elapsed = Math.Max(0, (int)(DateTime.Now - bookshelfStartedAt).TotalSeconds);
            string stage = String.IsNullOrWhiteSpace(bookshelfStage)
                ? (chinese ? "准备读取" : "Preparing")
                : bookshelfStage;
            decoy.Text = chinese
                ? "正在读取个人书架\n\n状态：" + stage + "\n已用时：" + elapsed + " 秒\n\n后台页面仍在持续运行；超过 60 秒会自动停止并显示最后阶段。"
                : "Loading personal bookshelf\n\nStatus: " + stage + "\nElapsed: " + elapsed + "s\n\nThe background page remains active; the operation stops after 60 seconds and reports its last stage.";
            if (showingCommandHint || String.IsNullOrWhiteSpace(commandInput.Text)) SetCommandHint(CurrentCommandHint());
        }

        void FailBookshelf(int operationId, string reason)
        {
            if (operationId != bookshelfOperationId || !loadingBookshelf) return;
            int elapsed = Math.Max(0, (int)(DateTime.Now - bookshelfStartedAt).TotalSeconds);
            loadingBookshelf = false;
            extractingBookshelf = false;
            keepBrowserRunningBehindDocument = false;
            bookshelfTimer.Stop();
            bookshelfNotice = chinese ? "读取失败，可输入 /书架 重试" : "Loading failed. Enter /bookshelf to retry";
            ShowCommandPage();
            decoy.Text = chinese
                ? "书架读取失败\n\n原因：" + reason + "\n耗时：" + elapsed + " 秒\n\n可先输入 /登录 确认登录状态，再输入 /书架 重试。"
                : "Bookshelf loading failed\n\nReason: " + reason + "\nElapsed: " + elapsed + "s\n\nUse /login to confirm your session, then retry /bookshelf.";
            SetCommandHint(CurrentCommandHint());
        }

        void CompleteBookshelf(int operationId, BookItem[] items)
        {
            if (operationId != bookshelfOperationId || !loadingBookshelf) return;
            loadingBookshelf = false;
            extractingBookshelf = false;
            keepBrowserRunningBehindDocument = false;
            bookshelfTimer.Stop();
            bookshelfNotice = String.Empty;
            loginCompleted = true;
            bookshelf.Clear();
            bookshelf.AddRange(items ?? new BookItem[0]);
            ShowCommandPage();
            if (bookshelf.Count == 0)
            {
                decoy.Text = chinese ? "书架页面已读取，但当前书架为空。" : "The bookshelf page loaded, but the bookshelf is empty.";
            }
            else
            {
                RenderBookshelf();
            }
            SetCommandHint(CurrentCommandHint());
        }

        async void LoadBookshelf()
        {
            if (!ready)
            {
                SetCommandHint(CurrentCommandHint());
                return;
            }
            bookshelfOperationId++;
            readingActive = false;
            chapterEnded = false;
            loadingCatalog = false;
            openingChapter = false;
            preparingChapter = false;
            chapters.Clear();
            selectedBook = null;
            currentChapterIndex = -1;
            loadingBookshelf = true;
            extractingBookshelf = false;
            keepBrowserRunningBehindDocument = true;
            bookshelfStartedAt = DateTime.Now;
            bookshelfNavigationRetries = 0;
            bookshelfStage = chinese ? "正在检查 my.qidian.com 登录权限" : "Checking login permission for my.qidian.com";
            bookshelfNotice = String.Empty;
            bookshelf.Clear();
            ShowCommandPage();
            bookshelfTimer.Start();
            RenderBookshelfProgress();
            if (!await HasBookshelfLoginCookie())
            {
                FailBookshelf(bookshelfOperationId, chinese
                    ? "未检测到适用于书架域名的登录身份。" + loginCookieSummary
                    : "No login identity was detected for the bookshelf domain. " + loginCookieSummary);
                return;
            }
            UpdateBookshelfStage(chinese ? "登录凭据有效，正在请求书架页面" : "Login credentials verified; requesting bookshelf page");
            Navigate(BookshelfUrl);
        }

        async Task ExtractBookshelf(int operationId)
        {
            string script = "(function(){" +
                "const result=[];const seen=new Set();const links=Array.from(document.querySelectorAll('a[href]'));" +
                "const challenge=!!document.querySelector('script[src*=\"probe.js\"],script[src*=\"/probe\"]')||((window.buid||'')&&!(document.body.innerText||'').trim());" +
                "const login=/login|passport/i.test(location.href)||!!document.querySelector('input[type=\"password\"],iframe[src*=\"passport\"]');" +
                "for(const link of links){const href=link.href||'';const match=href.match(/(?:book\\.qidian\\.com\\/info\\/|\\/book\\/|\\/info\\/)(\\d+)/i)||href.match(/[?&]bookId=(\\d+)/i);" +
                "if(!match)continue;const key=match[1]||href;if(seen.has(key))continue;" +
                "const image=link.querySelector('img');const title=((link.getAttribute('title')||'').trim()||(image&&image.getAttribute('alt')||'').trim()||(link.textContent||'').trim());" +
                "if(!title||title.length>100)continue;const row=link.closest('tr,li,[data-bid],.book-item,.shelf-book')||link.parentElement;" +
                "const rowLinks=row?Array.from(row.querySelectorAll('a[href]')):[];const progress=rowLinks.find(x=>/chapter|reader|read/i.test(x.href)&&x.href!==href);" +
                "seen.add(key);result.push({Title:title,BookUrl:href,ProgressTitle:progress?(progress.textContent||progress.getAttribute('title')||'').trim():'',ProgressUrl:progress?progress.href:''});}" +
                "return JSON.stringify({Items:result,Title:document.title||'',Text:(document.body.innerText||'').trim().slice(0,500),ReadyState:document.readyState,AnchorCount:links.length,IsChallenge:!!challenge,IsLogin:!!login});})();";

            BookshelfProbe lastProbe = null;
            try
            {
                for (int attempt = 1; attempt <= 10; attempt++)
                {
                    if (!loadingBookshelf || operationId != bookshelfOperationId) return;
                    UpdateBookshelfStage(chinese
                        ? "正在解析页面，第 " + attempt + "/10 次"
                        : "Parsing page, attempt " + attempt + "/10");
                    string encoded = await browser.ExecuteScriptAsync(script);
                    if (!loadingBookshelf || operationId != bookshelfOperationId) return;
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    string json = serializer.Deserialize<string>(encoded);
                    if (String.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Empty script result");
                    lastProbe = serializer.Deserialize<BookshelfProbe>(json);
                    if (lastProbe == null) throw new InvalidOperationException("Invalid script result");
                    if (lastProbe.IsLogin)
                    {
                        loadingBookshelf = false;
                        extractingBookshelf = false;
                        keepBrowserRunningBehindDocument = false;
                        bookshelfTimer.Stop();
                        loginFlowActive = true;
                        bookshelfNotice = chinese ? "请在下方官方页面完成登录" : "Complete login in the official page below";
                        SetMode(ReaderMode.Account);
                        SetCommandHint(CurrentCommandHint());
                        return;
                    }
                    if (lastProbe.Items != null && lastProbe.Items.Length > 0)
                    {
                        CompleteBookshelf(operationId, lastProbe.Items);
                        return;
                    }
                    if (lastProbe.IsChallenge)
                    {
                        UpdateBookshelfStage(chinese ? "起点正在进行安全验证，等待页面放行" : "Waiting for Qidian security verification");
                    }
                    else if (!String.Equals(lastProbe.ReadyState, "complete", StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateBookshelfStage(chinese ? "页面脚本仍在加载" : "Page scripts are still loading");
                    }
                    else
                    {
                        UpdateBookshelfStage(chinese ? "页面已加载，等待书架内容渲染" : "Page loaded; waiting for bookshelf content");
                    }
                    await Task.Delay(1300);
                }

                if (lastProbe != null && lastProbe.IsChallenge)
                {
                    FailBookshelf(operationId, chinese
                        ? "起点安全验证页面没有在限定时间内放行。"
                        : "Qidian's security verification did not finish in time.");
                }
                else if (lastProbe != null && lastProbe.AnchorCount == 0)
                {
                    FailBookshelf(operationId, chinese
                        ? "页面已打开，但没有生成可读取的链接，可能仍处于安全验证或登录已失效。"
                        : "The page opened but produced no readable links. Security verification may still be active or the session may have expired.");
                }
                else if (lastProbe != null && !String.IsNullOrWhiteSpace(lastProbe.Text) &&
                    (lastProbe.Text.IndexOf("暂无", StringComparison.OrdinalIgnoreCase) >= 0 || lastProbe.Text.IndexOf("书架为空", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    CompleteBookshelf(operationId, new BookItem[0]);
                }
                else
                {
                    FailBookshelf(operationId, chinese
                        ? "页面已完成加载，但未识别到书籍。起点页面结构可能已经变化。"
                        : "The page finished loading, but no books were detected. Qidian may have changed the page structure.");
                }
            }
            catch (Exception exception)
            {
                FailBookshelf(operationId, chinese
                    ? "页面解析发生异常（" + exception.GetType().Name + "），已停止读取。"
                    : "Page parsing failed (" + exception.GetType().Name + ") and was stopped.");
            }
        }

        void RenderBookshelf()
        {
            currentPageKind = CommandPageKind.Bookshelf;
            decoy.Text = chinese ? "我的书架\n\n" : "My bookshelf\n\n";
            for (int index = 0; index < bookshelf.Count; index++)
            {
                BookItem book = bookshelf[index];
                string progress = String.IsNullOrWhiteSpace(book.ProgressTitle) ? String.Empty : "    " + book.ProgressTitle;
                decoy.AppendText((index + 1) + ". " + book.Title + progress + Environment.NewLine);
            }
            decoy.AppendText(chinese ? "\n请在第一行输入对应序号。" : "\nEnter a book number in the first line.");
        }

        void OpenBook(int number)
        {
            if (number < 1 || number > bookshelf.Count)
            {
                decoy.AppendText(chinese ? "\n\n无效的书籍序号。" : "\n\nInvalid book number.");
                return;
            }
            PushNavigationState();
            selectedBook = bookshelf[number - 1];
            currentBookDetail = null;
            chapters.Clear();
            currentChapterIndex = -1;
            currentChapterTitle = String.Empty;
            LoadCatalog();
        }

        void ShowCatalog()
        {
            ocrOperationId++;
            ocrReadingActive = false;
            readingActive = false;
            chapterEnded = false;
            if (chapters.Count > 0)
            {
                ShowCommandPage();
                RenderCatalog();
                SetCommandHint(CurrentCommandHint());
                return;
            }
            if (selectedBook == null)
            {
                ShowCommandPage();
                decoy.Text = chinese ? "请先输入 /书架 并选择一本书。" : "Load the bookshelf and select a book first.";
                return;
            }
            LoadCatalog();
        }

        void LoadCatalog()
        {
            if (!ready || selectedBook == null) return;
            catalogOperationId++;
            catalogCollectedCount = 0;
            catalogStage = chinese ? "正在打开目录页面" : "Opening catalog page";
            loadingCatalog = true;
            extractingCatalog = false;
            openingChapter = false;
            preparingChapter = false;
            readingActive = false;
            keepBrowserRunningBehindDocument = true;
            currentCatalogUrl = BuildCatalogUrl(selectedBook.BookUrl);
            ShowCommandPage();
            decoy.Text = chinese ? "正在读取《" + selectedBook.Title + "》的目录……" : "Loading the catalog for " + selectedBook.Title + "...";
            SetCommandHint(chinese ? "正在读取目录，请稍候……" : "Loading catalog...");
            Navigate(currentCatalogUrl);
        }

        static string BuildCatalogUrl(string bookUrl)
        {
            Uri address;
            if (!Uri.TryCreate(bookUrl, UriKind.Absolute, out address)) return bookUrl;
            string[] parts = address.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string bookId = parts.Length == 0 ? String.Empty : parts[parts.Length - 1];
            if (String.IsNullOrWhiteSpace(bookId)) return bookUrl;
            return "https://www.qidian.com/book/" + bookId + "/catalog/";
        }

        async Task ExtractCatalog(int operationId)
        {
            List<ChapterItem> collected = new List<ChapterItem>();
            HashSet<string> seenChapters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> processedRanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> visitedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string progressUrl = String.Empty;
            string bookTitle = String.Empty;
            int expectedCount = 0;
            try
            {
                if (browser.Source != null) visitedPages.Add(browser.Source.AbsoluteUri);
                for (int routeAttempt = 0; routeAttempt < 120; routeAttempt++)
                {
                    if (!loadingCatalog || operationId != catalogOperationId) return;
                    await StabilizeCatalogDom(operationId);
                    CatalogProbe probe = await ReadCatalogProbe();
                    if (probe != null && probe.IsLogin)
                    {
                        loadingCatalog = false;
                        extractingCatalog = false;
                        keepBrowserRunningBehindDocument = false;
                        bookshelfNotice = chinese ? "登录状态已失效，请重新输入 /登录" : "Your login expired. Enter /login again";
                        ShowCommandPage();
                        return;
                    }
                    if (probe == null)
                    {
                        await Task.Delay(500);
                        continue;
                    }
                    if (!String.IsNullOrWhiteSpace(probe.Error))
                    {
                        FailCatalog(operationId, (chinese ? "目录页面脚本错误：" : "Catalog page script error: ") + probe.Error);
                        return;
                    }
                    foreach (ChapterItem item in probe.Items ?? new ChapterItem[0])
                    {
                        if (item == null || String.IsNullOrWhiteSpace(item.Url) || !seenChapters.Add(item.Url)) continue;
                        collected.Add(item);
                    }
                    if (!String.IsNullOrWhiteSpace(probe.ProgressUrl)) progressUrl = probe.ProgressUrl;
                    if (!String.IsNullOrWhiteSpace(probe.BookTitle)) bookTitle = probe.BookTitle;
                    expectedCount = Math.Max(expectedCount, probe.ExpectedCount);
                    catalogCollectedCount = collected.Count;
                    catalogStage = chinese ? "正在合并完整目录" : "Merging the complete catalog";
                    SetCommandHint(CurrentCommandHint());

                    if (!String.IsNullOrWhiteSpace(probe.ActiveRange)) processedRanges.Add(probe.ActiveRange);
                    string nextRange = (probe.RangeKeys ?? new string[0]).FirstOrDefault(key => !String.IsNullOrWhiteSpace(key) && !processedRanges.Contains(key));
                    if (!String.IsNullOrWhiteSpace(nextRange))
                    {
                        catalogStage = chinese ? "正在加载章节区间 " + nextRange : "Loading chapter range " + nextRange;
                        SetCommandHint(CurrentCommandHint());
                        processedRanges.Add(nextRange);
                        if (await ClickCatalogRange(nextRange))
                        {
                            await Task.Delay(700);
                            continue;
                        }
                    }

                    if (!String.IsNullOrWhiteSpace(probe.NextPageUrl) && visitedPages.Add(probe.NextPageUrl))
                    {
                        catalogStage = chinese ? "正在读取下一页目录" : "Loading the next catalog page";
                        SetCommandHint(CurrentCommandHint());
                        browser.CoreWebView2.Navigate(probe.NextPageUrl);
                        await WaitForCatalogDocument(operationId);
                        continue;
                    }

                    if (collected.Count > 0)
                    {
                        CompleteCatalog(operationId, new CatalogProbe
                        {
                            Items = collected.ToArray(), ProgressUrl = progressUrl, BookTitle = bookTitle,
                            IsLogin = false, ExpectedCount = expectedCount
                        });
                        return;
                    }
                    await Task.Delay(700);
                }
                FailCatalog(operationId, chinese ? "目录分页读取次数超过安全上限。" : "Catalog pagination exceeded the safety limit.");
            }
            catch (Exception exception)
            {
                FailCatalog(operationId, (chinese ? "目录解析失败：" : "Catalog parsing failed: ") + exception.GetType().Name + " - " + exception.Message);
            }
        }

        async Task StabilizeCatalogDom(int operationId)
        {
            string script = "(function(){const visible=e=>!!e&&e.offsetParent!==null;const text=e=>(e&&(e.innerText||e.textContent)||'').trim().replace(/\\s+/g,' ');let clicked=false;const more=Array.from(document.querySelectorAll('button,a,[role=button]')).find(e=>visible(e)&&/^(展开全部章节|展开全部|加载更多|查看更多章节|显示更多)$/.test(text(e)));if(more){more.click();clicked=true;}window.scrollTo(0,Math.max(document.body.scrollHeight,document.documentElement.scrollHeight));const count=document.querySelectorAll('a[href*=\"/chapter/\"]').length;return JSON.stringify({Count:count,Clicked:clicked});})()";
            int lastCount = -1;
            int stableCount = 0;
            for (int attempt = 0; attempt < 18; attempt++)
            {
                if (!loadingCatalog || operationId != catalogOperationId) return;
                string encoded = await browser.ExecuteScriptAsync(script);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Deserialize<string>(encoded);
                if (String.IsNullOrWhiteSpace(json)) return;
                CatalogDomState state = serializer.Deserialize<CatalogDomState>(json);
                int count = state == null ? 0 : state.Count;
                catalogStage = state != null && state.Clicked
                    ? (chinese ? "正在展开更多章节" : "Expanding more chapters")
                    : (chinese ? "正在等待目录内容稳定" : "Waiting for catalog content");
                SetCommandHint(CurrentCommandHint());
                if (count == lastCount && (state == null || !state.Clicked)) stableCount++;
                else stableCount = 0;
                lastCount = count;
                if (stableCount >= 3) return;
                await Task.Delay(350);
            }
        }

        async Task<CatalogProbe> ReadCatalogProbe()
        {
            string script = "(function(){try{" +
                "const clean=x=>(x||'').trim().replace(/\\s+/g,' ');const items=[];const seen=new Set();const allLinks=Array.from(document.querySelectorAll('a[href*=\"/chapter/\"]'));const rowOf=a=>a.closest('li,dd,[data-cid],[data-chapter-id],[class~=\"chapter-item\"],[class~=\"catalog-chapter\"]');const structuredLinks=allLinks.filter(a=>!!rowOf(a));const links=structuredLinks.length>=3?structuredLinks:allLinks;" +
                "for(const a of links){const title=clean(a.innerText||a.title);if(!title||title.length>140||/^(上一章|下一章|目录|继续阅读|立即阅读)$/.test(title))continue;const row=rowOf(a);if(structuredLinks.length>=3&&!row)continue;if(!row&&!/^(第|序章|楔子|卷)/.test(title))continue;const url=new URL(a.href,location.href).href;if(seen.has(url))continue;const marker=((row&&row.outerHTML)||'').toLowerCase();const vip=!!(row&&row.querySelector('em.iconfont,[class*=lock]'))||/vip|subscribe|lock|订阅/.test(marker);seen.add(url);items.push({Title:title,Url:url,IsVip:vip});}" +
                "const controls=Array.from(document.querySelectorAll('button,a,[role=tab],[data-page]'));const rangePattern=/^(第)?\\s*\\d+\\s*[-—~至]\\s*\\d+\\s*(章)?$/;const ranges=[];let active='';for(const control of controls){const value=clean(control.innerText||control.textContent);if(!rangePattern.test(value)||ranges.includes(value))continue;ranges.push(value);if(control.getAttribute('aria-selected')==='true'||/(^|\\s)(active|current|selected)(\\s|$)/i.test(control.className||''))active=value;}if(!active&&ranges.length)active=ranges[0];" +
                "const next=Array.from(document.querySelectorAll('a[href]')).find(a=>!/\\/chapter\\//.test(a.href)&&((a.rel||'').toLowerCase()==='next'||/^(下一页|下页|Next)$/.test(clean(a.innerText||a.textContent)))&&!/(disabled|unavailable)/i.test(a.className||''));" +
                "const progress=document.querySelector('a.read-progress[href*=\"/chapter/\"]');const login=/login|passport/i.test(location.href)||!!document.querySelector('input[type=\"password\"]');const heading=document.querySelector('h1,.book-info h2,.book-information h1');const body=document.body?(document.body.innerText||''):'';const match=body.match(/共\\s*(\\d+)\\s*章/);" +
                "return JSON.stringify({Items:items,ProgressUrl:progress?new URL(progress.href,location.href).href:'',BookTitle:heading?clean(heading.innerText):'',IsLogin:login,NextPageUrl:next?new URL(next.href,location.href).href:'',RangeKeys:ranges,ActiveRange:active,ExpectedCount:match?parseInt(match[1],10):0,Error:''});}catch(error){return JSON.stringify({Items:[],ProgressUrl:'',BookTitle:'',IsLogin:false,NextPageUrl:'',RangeKeys:[],ActiveRange:'',ExpectedCount:0,Error:String(error&&error.stack||error)});}})()";
            string encoded = await browser.ExecuteScriptAsync(script);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Deserialize<string>(encoded);
            if (String.IsNullOrWhiteSpace(json)) return null;
            return serializer.Deserialize<CatalogProbe>(json);
        }

        async Task<bool> ClickCatalogRange(string rangeKey)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string key = serializer.Serialize(rangeKey);
            string script = "(function(){const key=" + key + ";const clean=x=>(x||'').trim().replace(/\\s+/g,' ');const target=Array.from(document.querySelectorAll('button,a,[role=tab],[data-page]')).find(e=>e.offsetParent!==null&&clean(e.innerText||e.textContent)===key);if(!target)return false;target.scrollIntoView({block:'center'});target.click();window.scrollTo(0,0);return true;})()";
            string encoded = await browser.ExecuteScriptAsync(script);
            return serializer.Deserialize<bool>(encoded);
        }

        async Task WaitForCatalogDocument(int operationId)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                if (!loadingCatalog || operationId != catalogOperationId) return;
                try
                {
                    string encoded = await browser.ExecuteScriptAsync("document.readyState");
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    string state = serializer.Deserialize<string>(encoded);
                    if (String.Equals(state, "complete", StringComparison.OrdinalIgnoreCase) || String.Equals(state, "interactive", StringComparison.OrdinalIgnoreCase)) return;
                }
                catch { }
                await Task.Delay(300);
            }
        }

        void CompleteCatalog(int operationId, CatalogProbe probe)
        {
            if (!loadingCatalog || operationId != catalogOperationId) return;
            loadingCatalog = false;
            extractingCatalog = false;
            keepBrowserRunningBehindDocument = false;
            catalogStage = String.Empty;
            catalogCollectedCount = 0;
            chapters.Clear();
            foreach (ChapterItem item in probe.Items ?? new ChapterItem[0])
            {
                item.Url = NormalizeChapterUrl(item.Url);
                chapters.Add(item);
            }
            string progressUrl = !String.IsNullOrWhiteSpace(probe.ProgressUrl) ? probe.ProgressUrl : selectedBook.ProgressUrl;
            currentChapterIndex = FindChapterIndex(progressUrl);
            if (currentChapterIndex < 0 && chapters.Count > 0) currentChapterIndex = 0;
            if (startReadingAfterCatalog && chapters.Count > 0)
            {
                startReadingAfterCatalog = false;
                OpenChapter(currentChapterIndex + 1);
                return;
            }
            startReadingAfterCatalog = false;
            ShowCommandPage();
            RenderCatalog();
            SetCommandHint(CurrentCommandHint());
        }

        void FailCatalog(int operationId, string reason)
        {
            if (!loadingCatalog || operationId != catalogOperationId) return;
            loadingCatalog = false;
            extractingCatalog = false;
            keepBrowserRunningBehindDocument = false;
            catalogStage = String.Empty;
            catalogCollectedCount = 0;
            ShowCommandPage();
            decoy.Text = (chinese ? "目录读取失败\n\n原因：" : "Catalog loading failed\n\nReason: ") + reason;
            SetCommandHint(CurrentCommandHint());
        }

        void RenderCatalog()
        {
            currentPageKind = CommandPageKind.Catalog;
            decoy.Text = (chinese ? "《" + selectedBook.Title + "》目录（共 " + chapters.Count + " 章）\n\n" : selectedBook.Title + " - Catalog (" + chapters.Count + " chapters)\n\n");
            for (int index = 0; index < chapters.Count; index++)
            {
                ChapterItem chapter = chapters[index];
                string progress = index == currentChapterIndex ? (chinese ? "  [上次进度]" : "  [saved progress]") : String.Empty;
                string vip = chapter.IsVip ? "  [VIP]" : String.Empty;
                decoy.AppendText((index + 1) + ". " + chapter.Title + vip + progress + Environment.NewLine);
            }
            decoy.AppendText(chinese ? "\n输入章节序号阅读，或输入 /继续。" : "\nEnter a chapter number, or use /resume.");
        }

        int FindChapterIndex(string url)
        {
            if (String.IsNullOrWhiteSpace(url)) return -1;
            Uri target;
            if (!Uri.TryCreate(url, UriKind.Absolute, out target)) return -1;
            for (int index = 0; index < chapters.Count; index++)
            {
                Uri chapter;
                if (Uri.TryCreate(chapters[index].Url, UriKind.Absolute, out chapter) &&
                    String.Equals(chapter.AbsolutePath.TrimEnd('/'), target.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return index;
            }
            return -1;
        }

        void ResumeReading()
        {
            if (chapters.Count == 0)
            {
                ShowCatalog();
                return;
            }
            OpenChapter(currentChapterIndex >= 0 ? currentChapterIndex + 1 : 1);
        }

        void OpenChapter(int number)
        {
            if (number < 1 || number > chapters.Count)
            {
                decoy.AppendText(chinese ? "\n\n无效的章节序号。" : "\n\nInvalid chapter number.");
                return;
            }
            currentChapterIndex = number - 1;
            chapterOperationId++;
            ocrOperationId++;
            openingChapter = true;
            preparingChapter = false;
            readingActive = false;
            chapterEnded = false;
            ocrReadingActive = false;
            ocrBusy = false;
            ocrLines.Clear();
            keepBrowserRunningBehindDocument = true;
            ShowCommandPage();
            decoy.Text = chinese ? "正在打开 " + chapters[currentChapterIndex].Title + "……" : "Opening " + chapters[currentChapterIndex].Title + "...";
            SetCommandHint(chinese ? "正在验证章节阅读权限……" : "Checking chapter access...");
            Navigate(NormalizeChapterUrl(chapters[currentChapterIndex].Url));
        }

        void OpenAdjacentChapter(int direction)
        {
            if (direction > 0 && !String.IsNullOrWhiteSpace(nextChapterUrl))
            {
                OpenChapterUrl(nextChapterUrl, currentChapterIndex + 1);
                return;
            }
            if (direction < 0 && !String.IsNullOrWhiteSpace(previousChapterUrl))
            {
                OpenChapterUrl(previousChapterUrl, currentChapterIndex - 1);
                return;
            }
            int target = currentChapterIndex + direction;
            if (target >= 0 && target < chapters.Count) OpenChapter(target + 1);
            else
            {
                bookshelfNotice = direction > 0 ? (chinese ? "已经是最后一章" : "Already at the last chapter") : (chinese ? "已经是第一章" : "Already at the first chapter");
                SetCommandHint(CurrentCommandHint());
            }
        }

        void OpenChapterUrl(string url, int expectedIndex)
        {
            int matched = FindChapterIndex(url);
            currentChapterIndex = matched >= 0 ? matched : expectedIndex;
            chapterOperationId++;
            ocrOperationId++;
            openingChapter = true;
            preparingChapter = false;
            readingActive = false;
            chapterEnded = false;
            ocrReadingActive = false;
            ocrBusy = false;
            ocrLines.Clear();
            keepBrowserRunningBehindDocument = true;
            Navigate(NormalizeChapterUrl(url));
        }

        static string NormalizeChapterUrl(string url)
        {
            Uri address;
            if (!Uri.TryCreate(url, UriKind.Absolute, out address)) return url;
            if (String.Equals(address.Host, "www.qidian.com", StringComparison.OrdinalIgnoreCase) &&
                address.AbsolutePath.IndexOf("/chapter/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                UriBuilder builder = new UriBuilder(address);
                builder.Host = "read.qidian.com";
                return builder.Uri.AbsoluteUri;
            }
            return address.AbsoluteUri;
        }

        void SetReadingStep(string input, bool useCharacters)
        {
            int separator = input.IndexOf(' ');
            int amount;
            if (separator < 0 || !Int32.TryParse(input.Substring(separator + 1).Trim(), out amount) || amount < 1 || amount > 2000)
            {
                bookshelfNotice = chinese ? "请输入 1 到 2000 之间的数值" : "Enter a value from 1 to 2000";
                return;
            }
            ApplyReadingStep(useCharacters, amount);
            if (!ocrReadingActive && readingActive) browser.Focus();
        }

        async Task PrepareChapter()
        {
            string script = "(function(){" +
                "const reader=document.querySelector('.j_readContent')||document.querySelector('[id^=\"j_\"][class*=\"read-content\"]')||document.querySelector('#reader-content')||document.querySelector('#reader');const heading=document.querySelector('.j_chapterName')||Array.from(document.querySelectorAll('h1,h2,h3')).find(x=>/^第.{0,18}(章|节|回)/.test((x.innerText||'').trim()));const bodyText=document.body?document.body.innerText||'':'';" +
                "const find=t=>{const a=Array.from(document.querySelectorAll('a[href]')).find(x=>(x.innerText||'').trim()===t);return a?new URL(a.href,location.href).href:'';};" +
                "const chapterRoot=heading?(heading.closest('.text-wrap')||heading.closest('.chapter-wrapper')||heading.parentElement):null;const readerText=reader?(reader.innerText||''):'';const paragraphCount=reader?reader.querySelectorAll('p').length:0;const locked=/订阅本章|购买本章|登录后阅读|本章为VIP/.test(bodyText)&&readerText.length<500;const ready=!!reader&&!!heading&&!!chapterRoot&&readerText.length>500&&paragraphCount>=2;" +
                "if(ready&&!document.getElementById('quiet-reader-style')){" +
                    "let node=chapterRoot;while(node&&node!==reader){const parent=node.parentElement;if(!parent)break;Array.from(parent.children).forEach(x=>{if(x!==node)x.style.setProperty('display','none','important');});node=parent;}node=reader;while(node){node.style.setProperty('width','100%','important');node.style.setProperty('max-width','100%','important');node.style.setProperty('min-width','0','important');node.style.setProperty('margin','0','important');node.style.setProperty('padding','0','important');node.style.setProperty('box-sizing','border-box','important');node.style.setProperty('transform','none','important');if(node===document.body)break;const parent=node.parentElement;if(!parent)break;Array.from(parent.children).forEach(x=>{if(x!==node)x.style.setProperty('display','none','important');});node=parent;}" +
                    "const style=document.createElement('style');style.id='quiet-reader-style';style.textContent='html,body{background:#fff!important;color:#111!important;margin:0!important;padding:0!important;overflow-x:hidden!important;scrollbar-width:none!important}::-webkit-scrollbar{display:none!important;width:0!important;height:0!important}#reader,#reader-content,.j_readContent,.read-content{background:#fff!important;width:100%!important;max-width:100%!important;min-width:0!important;box-sizing:border-box!important;margin:0!important;padding:18px 12px 100px!important;box-shadow:none!important;transform:none!important}.j_readContent p,.read-content p,.chapter-wrapper p{font-size:22px!important;line-height:1.75!important;color:#111!important;margin:0 0 18px!important;max-width:100%!important;white-space:normal!important;overflow-wrap:anywhere!important;word-break:normal!important}[class*=chapter-end],[class*=review],[class*=comment],.chapter-date,header,aside,button,.side-button,.admire-wrap{display:none!important}';document.head.appendChild(style);document.addEventListener('keydown',e=>{if(e.ctrlKey||e.altKey||e.metaKey)return;const key=e.key||'';if(key==='/'||key==='Divide'){e.preventDefault();window.chrome.webview.postMessage('quiet:command');return;}if(key.length===1||key===' '){e.preventDefault();window.chrome.webview.postMessage('quiet:advance');}},true);window.__quietReaderCharOffset=0;reader.scrollIntoView({block:'start'});window.scrollBy(0,-8);}" +
                "const title=heading?(heading.innerText||'').split('\\n')[0].trim().replace(/\\s*\\d+\\s*$/,''):'';return JSON.stringify({Title:title,PreviousUrl:find('上一章'),NextUrl:find('下一章'),CatalogUrl:find('目录'),IsLocked:locked,HasReader:ready});})()";
            try
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    if (!openingChapter) return;
                    string encoded = await browser.ExecuteScriptAsync(script);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    string json = serializer.Deserialize<string>(encoded);
                    ChapterProbe probe = serializer.Deserialize<ChapterProbe>(json);
                    if (probe != null && probe.IsLocked)
                    {
                        openingChapter = false;
                        preparingChapter = false;
                        readingActive = false;
                        keepBrowserRunningBehindDocument = true;
                        if (subscriptionAttemptPending)
                        {
                            subscriptionAttemptPending = false;
                            bookshelfNotice = chinese ? "订阅未成功：可能是余额不足、登录失效或官方安全校验未通过" : "Subscription failed: balance, login, or an official security check may have prevented it";
                        }
                        else bookshelfNotice = chinese ? "该 VIP 章节尚未订阅，可点击右侧“订阅本章”或输入 /订阅" : "This VIP chapter is not subscribed. Use Subscribe chapter or enter /subscribe";
                        decoy.Text = (chapters.Count > currentChapterIndex && currentChapterIndex >= 0 ? chapters[currentChapterIndex].Title + Environment.NewLine + Environment.NewLine : String.Empty) + bookshelfNotice;
                        SetMode(ReaderMode.Hidden);
                        SetCommandHint(CurrentCommandHint());
                        return;
                    }
                    if (probe != null && probe.HasReader)
                    {
                        openingChapter = false;
                        preparingChapter = false;
                        readingActive = true;
                        currentPageKind = CommandPageKind.Reading;
                        chapterEnded = false;
                        keepBrowserRunningBehindDocument = false;
                        currentChapterTitle = probe.Title ?? String.Empty;
                        previousChapterUrl = probe.PreviousUrl ?? String.Empty;
                        nextChapterUrl = probe.NextUrl ?? String.Empty;
                        if (!String.IsNullOrWhiteSpace(probe.CatalogUrl)) currentCatalogUrl = probe.CatalogUrl;
                        int matched = FindChapterIndex(browser.Source.AbsoluteUri);
                        if (matched >= 0) currentChapterIndex = matched;
                        bookshelfNotice = String.Empty;
                        subscriptionAttemptPending = false;
                        SetMode(ReaderMode.Line);
                        await ActivateOcrReading();
                        return;
                    }
                    await Task.Delay(700);
                }
                FailChapter(chinese ? "章节页面已打开，但正文区域没有完成渲染。" : "The chapter opened, but the reading area did not finish rendering.");
            }
            catch (Exception exception)
            {
                FailChapter((chinese ? "章节准备失败：" : "Chapter preparation failed: ") + exception.GetType().Name);
            }
        }

        async void BeginChapterSubscription()
        {
            if (subscribingChapter) return;
            if (!ready || browser.CoreWebView2 == null || browser.Source == null || !IsQidianHost(browser.Source.Host))
            {
                bookshelfNotice = chinese ? "当前没有可订阅的起点章节页面" : "No subscribable Qidian chapter is currently open";
                SetCommandHint(CurrentCommandHint());
                return;
            }
            if (currentChapterIndex < 0 || currentChapterIndex >= chapters.Count)
            {
                bookshelfNotice = chinese ? "请先从目录打开一个 VIP 章节" : "Open a VIP chapter from the catalog first";
                SetCommandHint(CurrentCommandHint());
                return;
            }

            subscribingChapter = true;
            keepBrowserRunningBehindDocument = true;
            if (mode == ReaderMode.Hidden) ApplyLayout();
            SetCommandHint(CurrentCommandHint());
            try
            {
                string inspectScript = "(function(){" +
                    "const visible=e=>!!e&&e.offsetParent!==null;const text=e=>(e&&(e.innerText||e.textContent)||'').trim().replace(/\\s+/g,' ');" +
                    "const controls=Array.from(document.querySelectorAll('button,a,[role=button]'));const target=controls.find(e=>visible(e)&&/^(订阅本章|购买本章|立即订阅)$/.test(text(e)));" +
                    "const reader=document.querySelector('.j_readContent')||document.querySelector('#reader-content')||document.querySelector('#reader');const readerText=reader?(reader.innerText||''):'';const body=document.body?(document.body.innerText||''):'';" +
                    "const locked=/订阅本章|购买本章|本章为VIP/.test(body)&&readerText.length<500;const lines=(target?(target.closest('section,div,li')||document.body).innerText:body).split(/\\n+/).map(x=>x.trim()).filter(x=>x&&/本章|价格|起点币|余额|订阅/.test(x)).slice(0,8);" +
                    "return JSON.stringify({CanSubscribe:!!target,IsLocked:locked,HasReader:readerText.length>500,ConfirmVisible:false,ConfirmClicked:false,Detail:lines.join('；').slice(0,360),Error:''});})()";
                SubscriptionProbe probe = await ExecuteSubscriptionProbe(inspectScript);
                if (probe == null)
                {
                    FailSubscription(chinese ? "无法读取官方订阅信息" : "Could not read the official subscription information");
                    return;
                }
                if (probe.HasReader && !probe.IsLocked)
                {
                    FailSubscription(chinese ? "本章已经拥有阅读权限，无需重复订阅" : "This chapter is already available and does not need another purchase");
                    return;
                }
                if (!probe.CanSubscribe)
                {
                    FailSubscription(chinese ? "官方页面未提供可用的“订阅本章”按钮，可能需要重新登录或完成安全校验" : "The official Subscribe chapter button is unavailable. Sign in again or complete the official security check");
                    return;
                }

                string chapterTitle = chapters[currentChapterIndex].Title;
                string detail = String.IsNullOrWhiteSpace(probe.Detail) ? (chinese ? "价格与余额以起点官方页面最终显示为准" : "The official Qidian page determines the final price and balance") : probe.Detail;
                DialogResult confirmation = MessageBox.Show(
                    chinese
                        ? "将使用起点账户余额购买当前单章。\n\n章节：" + chapterTitle + "\n官方页面信息：" + detail + "\n\n只购买本章，不启用自动订阅，也不会自动充值。是否继续？"
                        : "This will use the Qidian account balance to purchase one chapter.\n\nChapter: " + chapterTitle + "\nOfficial page: " + detail + "\n\nOnly this chapter will be purchased. Auto-subscribe and automatic recharge remain disabled. Continue?",
                    chinese ? "确认订阅本章" : "Confirm chapter subscription",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (confirmation != DialogResult.Yes)
                {
                    FailSubscription(chinese ? "已取消订阅" : "Subscription cancelled");
                    return;
                }

                string clickScript = "(function(){const visible=e=>!!e&&e.offsetParent!==null;const text=e=>(e&&(e.innerText||e.textContent)||'').trim().replace(/\\s+/g,' ');const target=Array.from(document.querySelectorAll('button,a,[role=button]')).find(e=>visible(e)&&/^(订阅本章|购买本章|立即订阅)$/.test(text(e)));if(!target)return false;target.scrollIntoView({block:'center'});target.click();return true;})()";
                string clickEncoded = await browser.ExecuteScriptAsync(clickScript);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                bool clicked = serializer.Deserialize<bool>(clickEncoded);
                if (!clicked)
                {
                    FailSubscription(chinese ? "官方订阅按钮点击失败" : "The official subscription button could not be activated");
                    return;
                }

                for (int attempt = 0; attempt < 24; attempt++)
                {
                    await Task.Delay(500);
                    string confirmationScript = "(function(){" +
                        "const visible=e=>!!e&&e.offsetParent!==null;const text=e=>(e&&(e.innerText||e.textContent)||'').trim().replace(/\\s+/g,' ');" +
                        "const reader=document.querySelector('.j_readContent')||document.querySelector('#reader-content')||document.querySelector('#reader');const readerText=reader?(reader.innerText||''):'';const body=document.body?(document.body.innerText||''):'';" +
                        "const dialogs=Array.from(document.querySelectorAll('[role=dialog],.lbf-panel,[class*=dialog],[class*=popup],[class*=modal]')).filter(visible);const dialog=dialogs.find(e=>/订阅|起点币|余额|支付/.test(text(e)))||null;const dialogText=text(dialog);" +
                        "const error=(dialogText.match(/余额不足|起点币不足|订阅失败|支付失败|请先登录|登录失效|安全验证[^。；\\n]*/)||[])[0]||'';let clicked=false;" +
                        "if(dialog&&!error&&!window.__quietSubscriptionConfirmed){const confirm=Array.from(dialog.querySelectorAll('button,a,[role=button]')).find(e=>visible(e)&&/^(确认订阅|确认支付|立即订阅|订阅|确认)$/.test(text(e)));if(confirm){window.__quietSubscriptionConfirmed=true;confirm.click();clicked=true;}}" +
                        "const locked=/订阅本章|购买本章|本章为VIP/.test(body)&&readerText.length<500;return JSON.stringify({CanSubscribe:false,IsLocked:locked,HasReader:readerText.length>500,ConfirmVisible:!!dialog,ConfirmClicked:clicked||!!window.__quietSubscriptionConfirmed,Detail:dialogText.slice(0,360),Error:error});})()";
                    SubscriptionProbe result = await ExecuteSubscriptionProbe(confirmationScript);
                    if (result == null) continue;
                    if (!String.IsNullOrWhiteSpace(result.Error))
                    {
                        FailSubscription((chinese ? "订阅失败：" : "Subscription failed: ") + result.Error);
                        return;
                    }
                    if (result.HasReader && !result.IsLocked)
                    {
                        subscribingChapter = false;
                        bookshelfNotice = chinese ? "本章订阅成功，正在重新载入正文" : "Chapter subscribed. Reloading the text";
                        OpenChapter(currentChapterIndex + 1);
                        return;
                    }
                    if (result.ConfirmClicked)
                    {
                        await Task.Delay(2500);
                        subscriptionAttemptPending = true;
                        subscribingChapter = false;
                        OpenChapter(currentChapterIndex + 1);
                        return;
                    }
                }
                FailSubscription(chinese ? "订阅失败：未能读取官方确认结果，可能存在验证码或页面结构变化" : "Subscription failed: the official confirmation result was unavailable, possibly because of a security check or page change");
            }
            catch (Exception exception)
            {
                FailSubscription((chinese ? "订阅失败：" : "Subscription failed: ") + exception.Message);
            }
            finally
            {
                subscribingChapter = false;
                SetCommandHint(CurrentCommandHint());
            }
        }

        async Task<SubscriptionProbe> ExecuteSubscriptionProbe(string script)
        {
            string encoded = await browser.ExecuteScriptAsync(script);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Deserialize<string>(encoded);
            return serializer.Deserialize<SubscriptionProbe>(json);
        }

        void FailSubscription(string reason)
        {
            subscribingChapter = false;
            subscriptionAttemptPending = false;
            bookshelfNotice = reason;
            SetCommandHint(CurrentCommandHint());
        }

        void FailChapter(string reason)
        {
            openingChapter = false;
            preparingChapter = false;
            readingActive = false;
            ocrReadingActive = false;
            ocrBusy = false;
            keepBrowserRunningBehindDocument = false;
            ShowCommandPage();
            decoy.Text = (chinese ? "章节阅读失败\n\n原因：" : "Chapter reading failed\n\nReason: ") + reason;
            SetCommandHint(CurrentCommandHint());
        }

        async void BeginOcrReading()
        {
            if (!ready || browser.CoreWebView2 == null || browser.Source == null || !IsChapterAddress(browser.Source))
            {
                bookshelfNotice = chinese ? "请先打开一个可正常阅读的章节" : "Open an available chapter first";
                SetCommandHint(CurrentCommandHint());
                return;
            }
            await ActivateOcrReading();
        }

        async Task ActivateOcrReading()
        {
            if (ocrBusy) return;
            int operationId = ++ocrOperationId;
            ocrBusy = true;
            ocrReadingActive = false;
            readingActive = true;
            chapterEnded = false;
            ocrPageEnded = false;
            ocrPrefetching = false;
            ocrLines.Clear();
            ocrParagraphs.Clear();
            ocrPageMarkers.Clear();
            ocrRevealedLineCount = 0;
            ocrRevealedCharacterCount = 0;
            SetMode(ReaderMode.Line);
            SetCommandHint(CurrentCommandHint());
            await Task.Delay(250);
            bool captured = await CaptureOcrViewport(operationId);
            ocrBusy = false;
            if (!captured)
            {
                ShowOfficialReadingView();
                bookshelfNotice = chinese ? "本地 OCR 不可用，已切回官方网页模式" : "Local OCR is unavailable; returned to the official web view";
                SetCommandHint(CurrentCommandHint());
                return;
            }
            ocrReadingActive = true;
            mode = ReaderMode.Hidden;
            ApplyLayout();
            RenderOcrText();
            decoy.Focus();
            SetCommandHint(CurrentCommandHint());
            StartOcrPrefetch();
        }

        void ShowOfficialReadingView()
        {
            if (browser.Source == null || !IsQidianHost(browser.Source.Host))
            {
                bookshelfNotice = chinese ? "当前没有可显示的起点官方页面" : "No official Qidian page is currently available";
                SetCommandHint(CurrentCommandHint());
                return;
            }
            ocrOperationId++;
            ocrReadingActive = false;
            ocrBusy = false;
            SetMode(IsChapterAddress(browser.Source) ? ReaderMode.Line : ReaderMode.Account);
            browser.Focus();
            SetCommandHint(CurrentCommandHint());
        }

        OcrEngine GetOcrEngine()
        {
            if (ocrEngine != null) return ocrEngine;
            Language language = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(item => item.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
            if (language == null) return null;
            ocrEngine = OcrEngine.TryCreateFromLanguage(language);
            return ocrEngine;
        }

        async Task<OcrViewportGeometry> ReadOcrViewportGeometry()
        {
            string script = "(function(){const reader=document.querySelector('.j_readContent')||document.querySelector('[id^=\"j_\"][class*=\"read-content\"]')||document.querySelector('#reader-content')||document.querySelector('#reader');if(!reader)return JSON.stringify({ViewportWidth:window.innerWidth,ViewportHeight:window.innerHeight,ScrollY:window.scrollY,Ended:false,Paragraphs:[]});const paragraphs=Array.from(reader.querySelectorAll('p')).map((p,index)=>{const r=p.getBoundingClientRect();return {Index:index,Left:r.left,Top:r.top,Right:r.right,Bottom:r.bottom};}).filter(r=>r.Bottom>0&&r.Top<window.innerHeight&&r.Right>0&&r.Left<window.innerWidth);const ended=window.scrollY+window.innerHeight>=document.documentElement.scrollHeight-24;return JSON.stringify({ViewportWidth:window.innerWidth,ViewportHeight:window.innerHeight,ScrollY:window.scrollY,Ended:ended,Paragraphs:paragraphs});})()";
            string encoded = await browser.ExecuteScriptAsync(script);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Deserialize<string>(encoded);
            return serializer.Deserialize<OcrViewportGeometry>(json);
        }

        async Task<bool> CaptureOcrViewport(int operationId)
        {
            if (browser.CoreWebView2 == null || operationId != ocrOperationId) return false;
            try
            {
                OcrViewportGeometry geometry = await ReadOcrViewportGeometry();
                if (geometry == null || geometry.Paragraphs == null || geometry.Paragraphs.Length == 0) return false;
                using (MemoryStream imageStream = new MemoryStream())
                {
                    await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, imageStream);
                    string imagePath = Path.Combine(Path.GetTempPath(), "QuietReader-Ocr-" + Guid.NewGuid().ToString("N") + ".png");
                    File.WriteAllBytes(imagePath, imageStream.ToArray());
                    ExternalOcrResponse response = null;
                    try
                    {
                        response = await RecognizeWithExternalOcr(imagePath);
                    }
                    finally
                    {
                        try { File.Delete(imagePath); } catch { }
                    }

                    if (response != null && response.blocks != null && response.blocks.Length > 0)
                    {
                        ocrProvider = "PP-OCRv6 Small";
                    }
                    else
                    {
                        response = await RecognizeWithWindowsOcr(imageStream);
                        ocrProvider = "Windows OCR";
                    }
                    if (response == null || response.blocks == null || response.blocks.Length == 0) return false;
                    if (operationId != ocrOperationId) return false;
                    bool changed = AppendOcrPage(response, geometry);
                    ocrPageEnded = ocrPageEnded || geometry.Ended;
                    int endCharacterCount = BuildOcrText().Length;
                    if (ocrPageMarkers.Count == 0 || endCharacterCount > ocrPageMarkers[ocrPageMarkers.Count - 1].EndCharacterCount || geometry.Ended)
                        ocrPageMarkers.Add(new OcrPageMarker { EndCharacterCount = endCharacterCount, ScrollY = geometry.ScrollY });
                    return changed || ocrLines.Count > 0;
                }
            }
            catch (Exception exception)
            {
                bookshelfNotice = (chinese ? "OCR 识别失败：" : "OCR failed: ") + exception.GetType().Name;
                return false;
            }
        }

        async Task<ExternalOcrResponse> RecognizeWithExternalOcr(string imagePath)
        {
            string helperPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "ocr-runtime", "OcrHelper.exe"));
            if (!File.Exists(helperPath)) return null;
            await externalOcrSemaphore.WaitAsync();
            try
            {
                Process process;
                lock (externalOcrLock)
                {
                    if (externalOcrProcess == null || externalOcrProcess.HasExited)
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo(helperPath)
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            StandardOutputEncoding = Encoding.UTF8,
                        };
                        externalOcrProcess = Process.Start(startInfo);
                    }
                    process = externalOcrProcess;
                    string encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(imagePath));
                    process.StandardInput.WriteLine(encodedPath);
                    process.StandardInput.Flush();
                }
                Task<string> responseTask = Task.Run(delegate { return process.StandardOutput.ReadLine(); });
                if (await Task.WhenAny(responseTask, Task.Delay(20000)) != responseTask)
                {
                    StopExternalOcr();
                    return null;
                }
                string response = await responseTask;
                if (String.IsNullOrWhiteSpace(response)) return null;
                ExternalOcrResponse result = new JavaScriptSerializer().Deserialize<ExternalOcrResponse>(response);
                if (result == null || result.blocks == null || result.blocks.Length == 0) return null;
                return result;
            }
            finally { externalOcrSemaphore.Release(); }
        }

        async Task<ExternalOcrResponse> RecognizeWithWindowsOcr(MemoryStream imageStream)
        {
            OcrEngine engine = GetOcrEngine();
            if (engine == null) return null;
            imageStream.Position = 0;
            using (var randomAccessStream = imageStream.AsRandomAccessStream())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                using (SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                {
                    OcrResult result = await engine.RecognizeAsync(bitmap);
                    List<ExternalOcrBlock> blocks = new List<ExternalOcrBlock>();
                    foreach (OcrLine line in result.Lines)
                    {
                        if (line.Words == null || line.Words.Count == 0) continue;
                        double left = line.Words.Min(word => word.BoundingRect.Left);
                        double top = line.Words.Min(word => word.BoundingRect.Top);
                        double right = line.Words.Max(word => word.BoundingRect.Right);
                        double bottom = line.Words.Max(word => word.BoundingRect.Bottom);
                        blocks.Add(new ExternalOcrBlock { text = line.Text, left = left, top = top, right = right, bottom = bottom });
                    }
                    return new ExternalOcrResponse { imageWidth = bitmap.PixelWidth, imageHeight = bitmap.PixelHeight, blocks = blocks.ToArray() };
                }
            }
        }

        void StopExternalOcr()
        {
            lock (externalOcrLock)
            {
                if (externalOcrProcess == null) return;
                try
                {
                    if (!externalOcrProcess.HasExited) externalOcrProcess.Kill();
                }
                catch { }
                externalOcrProcess.Dispose();
                externalOcrProcess = null;
            }
        }

        static string NormalizeOcrLine(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            string normalized = value.Replace("　", " ").Trim();
            while (normalized.Contains("  ")) normalized = normalized.Replace("  ", " ");
            return normalized;
        }

        bool AppendOcrPage(ExternalOcrResponse response, OcrViewportGeometry geometry)
        {
            if (response.imageWidth <= 0 || response.imageHeight <= 0 || geometry.ViewportHeight <= 0) return false;
            Dictionary<int, List<ExternalOcrBlock>> grouped = new Dictionary<int, List<ExternalOcrBlock>>();
            foreach (ExternalOcrBlock block in response.blocks.OrderBy(item => item.top).ThenBy(item => item.left))
            {
                double centerY = (block.top + block.bottom) * 0.5 * geometry.ViewportHeight / response.imageHeight;
                OcrParagraphRect paragraph = geometry.Paragraphs
                    .Where(item => centerY >= item.Top - 6 && centerY <= item.Bottom + 6)
                    .OrderBy(item => Math.Abs((item.Top + item.Bottom) * 0.5 - centerY))
                    .FirstOrDefault();
                if (paragraph == null) continue;
                List<ExternalOcrBlock> blocks;
                if (!grouped.TryGetValue(paragraph.Index, out blocks))
                {
                    blocks = new List<ExternalOcrBlock>();
                    grouped.Add(paragraph.Index, blocks);
                }
                blocks.Add(block);
            }

            bool changed = false;
            foreach (KeyValuePair<int, List<ExternalOcrBlock>> entry in grouped.OrderBy(item => item.Key))
            {
                OcrParagraphRect rect = geometry.Paragraphs.First(item => item.Index == entry.Key);
                List<ExternalOcrBlock> blocks = entry.Value.OrderBy(item => item.top).ThenBy(item => item.left).ToList();
                if (rect.Top < -4 && blocks.Count > 1) blocks.RemoveAt(0);
                StringBuilder paragraphText = new StringBuilder();
                foreach (ExternalOcrBlock block in blocks)
                {
                    string line = NormalizeOcrLine(block.text);
                    if (String.IsNullOrWhiteSpace(line)) continue;
                    if (line.Length == 1 && line[0] < 128 && !Char.IsDigit(line[0])) continue;
                    AppendOcrFragment(paragraphText, line);
                }
                string fragment = paragraphText.ToString().Trim();
                if (fragment.Length == 0) continue;
                string existing;
                if (!ocrParagraphs.TryGetValue(entry.Key, out existing))
                {
                    ocrParagraphs.Add(entry.Key, fragment);
                    changed = true;
                }
                else
                {
                    string merged = MergeOcrFragments(existing, fragment);
                    if (!String.Equals(existing, merged, StringComparison.Ordinal))
                    {
                        ocrParagraphs[entry.Key] = merged;
                        changed = true;
                    }
                }
            }
            RebuildOcrLines();
            return changed;
        }

        static void AppendOcrFragment(StringBuilder target, string fragment)
        {
            if (target.Length > 0 && Char.IsLetterOrDigit(target[target.Length - 1]) && fragment.Length > 0 &&
                target[target.Length - 1] < 128 && fragment[0] < 128) target.Append(' ');
            target.Append(fragment);
        }

        static string MergeOcrFragments(string existing, string incoming)
        {
            if (String.IsNullOrWhiteSpace(existing)) return incoming;
            if (String.IsNullOrWhiteSpace(incoming)) return existing;
            if (existing.Contains(incoming)) return existing;
            if (incoming.Contains(existing)) return incoming;
            int maximumOverlap = Math.Min(Math.Min(existing.Length, incoming.Length), 120);
            for (int length = maximumOverlap; length >= 5; length--)
            {
                string suffix = existing.Substring(existing.Length - length);
                string prefix = incoming.Substring(0, length);
                int allowedErrors = Math.Max(1, length / 10);
                if (LevenshteinDistance(suffix, prefix, allowedErrors) <= allowedErrors)
                    return existing + incoming.Substring(length);
            }
            if (TextSimilarity(existing, incoming) >= 0.65) return incoming.Length > existing.Length ? incoming : existing;
            return existing + incoming;
        }

        static double TextSimilarity(string left, string right)
        {
            int maximum = Math.Max(left.Length, right.Length);
            if (maximum == 0) return 1;
            return 1.0 - (double)LevenshteinDistance(left, right, maximum) / maximum;
        }

        static int LevenshteinDistance(string left, string right, int stopAfter)
        {
            if (Math.Abs(left.Length - right.Length) > stopAfter) return stopAfter + 1;
            int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
            int[] current = new int[right.Length + 1];
            for (int row = 1; row <= left.Length; row++)
            {
                current[0] = row;
                int rowMinimum = current[0];
                for (int column = 1; column <= right.Length; column++)
                {
                    int cost = left[row - 1] == right[column - 1] ? 0 : 1;
                    current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1), previous[column - 1] + cost);
                    rowMinimum = Math.Min(rowMinimum, current[column]);
                }
                if (rowMinimum > stopAfter) return stopAfter + 1;
                int[] swap = previous;
                previous = current;
                current = swap;
            }
            return previous[right.Length];
        }

        void RebuildOcrLines()
        {
            ocrLines.Clear();
            foreach (string paragraph in ocrParagraphs.Values)
                if (!String.IsNullOrWhiteSpace(paragraph)) ocrLines.Add(paragraph.Trim());
        }

        void RenderOcrText()
        {
            string heading = String.IsNullOrWhiteSpace(currentChapterTitle) ? (chinese ? "章节正文" : "Chapter") : currentChapterTitle;
            string complete = BuildOcrText();
            int length = Math.Min(GetVisibleOcrCharacterCount(), complete.Length);
            string body = complete.Substring(0, length);
            if (readingViewMode == ReadingViewMode.Scrolling)
            {
                RenderPagedOcrText(heading, body);
                return;
            }
            renderedPageCount = 1;
            decoy.Font = CreateDocumentFont();
            decoy.Text = heading + Environment.NewLine + Environment.NewLine + body;
            decoy.SelectionStart = decoy.TextLength;
            decoy.ScrollToCaret();
            ApplyLayout();
            UpdateModeStatus();
        }

        void RenderPagedOcrText(string heading, string body)
        {
            string text = heading + Environment.NewLine + Environment.NewLine + body;
            Size pageSize = GetScaledPageSize();
            Padding margins = GetScaledPagePadding(pageSize);
            Font font = CreateDocumentFont();
            int contentWidth = Math.Max(120, pageSize.Width - margins.Horizontal);
            int contentHeight = Math.Max(160, pageSize.Height - margins.Vertical);
            List<string> pages = PaginateText(text, font, contentWidth, Math.Max(100, contentHeight - 34), contentHeight);
            EnsureRenderedPageCount(pages.Count);
            renderedPageCount = Math.Max(1, pages.Count);
            for (int index = 0; index < renderedPageEditors.Count; index++)
            {
                RichTextBox editor = renderedPageEditors[index];
                editor.Font = font;
                editor.Text = pages[index];
                editor.SelectionStart = editor.TextLength;
            }
            ApplyLayout();
            FollowLatestRenderedText();
            UpdateModeStatus();
        }

        List<string> PaginateText(string text, Font font, int width, int firstHeight, int otherHeight)
        {
            List<string> pages = new List<string>();
            string remaining = text ?? String.Empty;
            int pageIndex = 0;
            while (remaining.Length > 0)
            {
                int height = pageIndex == 0 ? firstHeight : otherHeight;
                int length = FindFittingTextLength(remaining, font, width, height);
                if (length <= 0) length = Math.Min(1, remaining.Length);
                if (length < remaining.Length)
                {
                    string candidate = remaining.Substring(0, length);
                    int paragraphBreak = candidate.LastIndexOf(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal);
                    if (paragraphBreak > length * 0.72) length = paragraphBreak + Environment.NewLine.Length * 2;
                    if (length > 0 && length < remaining.Length && remaining[length - 1] == '\r' && remaining[length] == '\n') length--;
                }
                pages.Add(remaining.Substring(0, length).TrimEnd('\r', '\n'));
                remaining = remaining.Substring(length).TrimStart('\r', '\n');
                pageIndex++;
            }
            if (pages.Count == 0) pages.Add(String.Empty);
            return pages;
        }

        int FindFittingTextLength(string text, Font font, int width, int height)
        {
            TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;
            int low = 1;
            int high = text.Length;
            int best = 0;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                Size measured = TextRenderer.MeasureText(text.Substring(0, middle), font, new Size(width, 100000), flags);
                if (measured.Height <= height)
                {
                    best = middle;
                    low = middle + 1;
                }
                else high = middle - 1;
            }
            return best;
        }

        void EnsureRenderedPageCount(int count)
        {
            count = Math.Max(1, count);
            while (renderedPagePanels.Count < count)
            {
                Panel panel = new Panel { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
                RichTextBox editor = new RichTextBox
                {
                    BorderStyle = BorderStyle.None,
                    BackColor = Color.White,
                    ReadOnly = true,
                    DetectUrls = false,
                    ScrollBars = RichTextBoxScrollBars.None,
                    TabStop = false
                };
                editor.MouseWheel += OnDocumentMouseWheel;
                panel.Controls.Add(editor);
                pagedDocumentHost.Controls.Add(panel);
                renderedPagePanels.Add(panel);
                renderedPageEditors.Add(editor);
            }
            while (renderedPagePanels.Count > count)
            {
                int index = renderedPagePanels.Count - 1;
                Panel panel = renderedPagePanels[index];
                pagedDocumentHost.Controls.Remove(panel);
                renderedPagePanels.RemoveAt(index);
                renderedPageEditors.RemoveAt(index);
                panel.Dispose();
            }
        }

        void LayoutRenderedPages(Size pageSize, Padding margins, int pageLeft, int pageTop)
        {
            EnsureRenderedPageCount(Math.Max(1, renderedPageCount));
            int gap = Math.Max(14, (int)Math.Round(24 * documentZoom / 100.0));
            int totalHeight = pageTop + renderedPagePanels.Count * pageSize.Height + Math.Max(0, renderedPagePanels.Count - 1) * gap + 24;
            int hostWidth = Math.Max(pagedDocumentHost.ClientSize.Width, pageSize.Width + 48);
            pageLeft = Math.Max(24, (hostWidth - pageSize.Width) / 2);
            pagedDocumentHost.Padding = new Padding(pageLeft, pageTop, 24, 24);
            pagedDocumentHost.AutoScrollMinSize = new Size(hostWidth, totalHeight);
            for (int index = 0; index < renderedPagePanels.Count; index++)
            {
                Panel panel = renderedPagePanels[index];
                panel.Size = pageSize;
                panel.Margin = new Padding(0, 0, 0, index == renderedPagePanels.Count - 1 ? 0 : gap);
                RichTextBox editor = renderedPageEditors[index];
                int reservedCommandHeight = 0;
                editor.Bounds = new Rectangle(margins.Left, margins.Top,
                    Math.Max(80, pageSize.Width - margins.Horizontal),
                    Math.Max(80, pageSize.Height - margins.Vertical - reservedCommandHeight));
            }
        }

        void FollowLatestRenderedText()
        {
            if (renderedPagePanels.Count == 0 || renderedPageEditors.Count == 0) return;
            BeginInvoke((MethodInvoker)delegate
            {
                if (!IsPagedReadingView()) return;
                int index = renderedPagePanels.Count - 1;
                RichTextBox editor = renderedPageEditors[index];
                pagedDocumentHost.ScrollControlIntoView(editor);
                editor.SelectionStart = editor.TextLength;
                editor.ScrollToCaret();
                UpdateModeStatus();
            });
        }
        bool HasUnrevealedOcrText()
        {
            return GetVisibleOcrCharacterCount() < BuildOcrText().Length;
        }

        string BuildOcrText()
        {
            return String.Join(Environment.NewLine + Environment.NewLine, ocrLines);
        }

        int GetVisibleOcrCharacterCount()
        {
            int completeLength = BuildOcrText().Length;
            return charactersPerKey > 0
                ? Math.Min(ocrRevealedCharacterCount, completeLength)
                : Math.Min(ocrRevealedLineCount * EstimatedCharactersPerLine, completeLength);
        }

        int GetUnreadCachedPageCount()
        {
            int visible = GetVisibleOcrCharacterCount();
            return ocrPageMarkers.Count(marker => marker.EndCharacterCount > visible);
        }

        async void StartOcrPrefetch()
        {
            if (!ocrReadingActive || ocrPrefetching || ocrPageEnded || browser.CoreWebView2 == null) return;
            ocrPrefetching = true;
            int operationId = ocrOperationId;
            keepBrowserRunningBehindDocument = true;
            ApplyLayout();
            try
            {
                while (operationId == ocrOperationId && ocrReadingActive && readingActive && !ocrPageEnded && GetUnreadCachedPageCount() < OcrCachePageLimit)
                {
                    bool moved = await MoveToNextOcrViewport();
                    if (!moved) break;
                    bool captured = await CaptureOcrViewport(operationId);
                    if (!captured) break;
                    await Task.Delay(80);
                }
            }
            catch (Exception exception)
            {
                bookshelfNotice = (chinese ? "后台 OCR 缓存失败：" : "Background OCR cache failed: ") + exception.GetType().Name;
            }
            finally
            {
                if (operationId == ocrOperationId)
                {
                    ocrPrefetching = false;
                    keepBrowserRunningBehindDocument = false;
                    ApplyLayout();
                    SetCommandHint(CurrentCommandHint());
                }
            }
        }

        void AdvanceOcrReading()
        {
            if (!ocrReadingActive || ocrBusy || chapterEnded) return;
            if (!HasUnrevealedOcrText())
            {
                if (ocrPageEnded && !ocrPrefetching)
                {
                    chapterEnded = true;
                    bookshelfNotice = chinese ? "本章已结束，可输入 /下一章" : "Chapter finished. Enter /next";
                    SetCommandHint(CurrentCommandHint());
                    return;
                }
                StartOcrPrefetch();
                SetCommandHint(CurrentCommandHint());
                return;
            }
            if (charactersPerKey > 0)
            {
                string complete = BuildOcrText();
                ocrRevealedCharacterCount = Math.Min(complete.Length, ocrRevealedCharacterCount + charactersPerKey);
            }
            else
            {
                int estimatedLineCount = (int)Math.Ceiling((double)BuildOcrText().Length / EstimatedCharactersPerLine);
                ocrRevealedLineCount = Math.Min(estimatedLineCount, ocrRevealedLineCount + Math.Max(1, linesPerKey));
            }
            RenderOcrText();
            SetCommandHint(CurrentCommandHint());
            StartOcrPrefetch();
        }

        async Task<bool> MoveToNextOcrViewport()
        {
            string script = "(function(){const before=window.scrollY;const distance=Math.max(240,window.innerHeight*0.78);window.scrollBy(0,distance);const ended=window.scrollY+window.innerHeight>=document.documentElement.scrollHeight-24;const moved=Math.abs(window.scrollY-before)>2;return JSON.stringify({Ended:ended,Moved:moved,ScrollY:window.scrollY});})()";
            string encoded = await browser.ExecuteScriptAsync(script);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string json = serializer.Deserialize<string>(encoded);
            ReadingViewportResult result = serializer.Deserialize<ReadingViewportResult>(json);
            if (result == null) return false;
            ocrPageEnded = result.Ended;
            await Task.Delay(180);
            return result.Moved || result.Ended;
        }

        async void AdvanceReading()
        {
            if (!readingActive || !ready || browser.CoreWebView2 == null || chapterEnded) return;
            if (ocrReadingActive)
            {
                AdvanceOcrReading();
                return;
            }
            string modeName = charactersPerKey > 0 ? "chars" : "lines";
            int amount = charactersPerKey > 0 ? charactersPerKey : linesPerKey;
            string script = "(function(mode,amount){" +
                "const root=document.querySelector('.chapter-wrapper')||document.querySelector('#reader-content')||document.body;if(!root)return JSON.stringify({Ended:false});" +
                "if(mode==='chars'){const nodes=[];const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT,{acceptNode:n=>{const p=n.parentElement;if(!p||!n.data.trim()||p.closest('script,style,noscript,button,a,[class*=chapter-end]'))return NodeFilter.FILTER_REJECT;return NodeFilter.FILTER_ACCEPT;}});let n,total=0;while(n=walker.nextNode()){nodes.push(n);total+=n.data.length;}let target=Math.min(total,(window.__quietReaderCharOffset||0)+amount);window.__quietReaderCharOffset=target;let passed=0;for(const text of nodes){if(passed+text.data.length>=target){const range=document.createRange();range.setStart(text,Math.max(0,target-passed-1));range.setEnd(text,Math.min(text.data.length,target-passed));const rect=range.getBoundingClientRect();if(rect&&isFinite(rect.top))window.scrollTo(0,Math.max(0,window.scrollY+rect.top-window.innerHeight*0.72));break;}passed+=text.data.length;}if(target>=total)window.scrollTo(0,document.documentElement.scrollHeight);}" +
                "else{const line=parseFloat(getComputedStyle(root).lineHeight)||34;window.scrollBy(0,line*amount);}" +
                "const ended=window.scrollY+window.innerHeight>=document.documentElement.scrollHeight-24;return JSON.stringify({Ended:ended});})(" +
                "'" + modeName + "'," + amount + ")";
            try
            {
                string encoded = await browser.ExecuteScriptAsync(script);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Deserialize<string>(encoded);
                ReadingAdvanceResult result = serializer.Deserialize<ReadingAdvanceResult>(json);
                chapterEnded = result != null && result.Ended;
                if (chapterEnded)
                {
                    bookshelfNotice = chinese ? "本章已结束，可输入 /下一章" : "Chapter finished. Enter /next";
                    SetCommandHint(CurrentCommandHint());
                }
            }
            catch (Exception exception)
            {
                bookshelfNotice = (chinese ? "阅读推进失败：" : "Reading advance failed: ") + exception.GetType().Name;
                SetCommandHint(CurrentCommandHint());
            }
        }
        void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs args)
        {
            Uri target;
            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out target) ||
                (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp))
            {
                args.Cancel = true;
                MessageBox.Show(chinese ? "已阻止非网页跳转请求。" : "Blocked a non-web navigation request.", "Quiet Reader");
                return;
            }
            if (loadingBookshelf && IsBookshelfAddress(target))
                UpdateBookshelfStage(chinese ? "已发起书架导航，等待服务器响应" : "Bookshelf navigation started; waiting for server response");
        }

        void OnContentLoading(object sender, CoreWebView2ContentLoadingEventArgs args)
        {
            if (loadingBookshelf) UpdateBookshelfStage(chinese ? "服务器已响应，正在加载页面内容" : "Server responded; loading page content");
        }

        void OnDomContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs args)
        {
            if (loadingBookshelf) UpdateBookshelfStage(chinese ? "页面 DOM 已生成，准备解析书架" : "Page DOM is ready; preparing bookshelf parsing");
        }

        void OnWebResourceResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
        {
            if (!loadingBookshelf || args.Request == null || args.Response == null) return;
            Uri address;
            if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out address) || !IsBookshelfAddress(address)) return;
            int statusCode = args.Response.StatusCode;
            if (statusCode == 202)
                UpdateBookshelfStage(chinese ? "服务器返回 HTTP 202，正在进行安全验证" : "Server returned HTTP 202; security verification is running");
            else
                UpdateBookshelfStage((chinese ? "书架服务器响应：HTTP " : "Bookshelf server response: HTTP ") + statusCode);
        }

        void OnBrowserProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs args)
        {
            if (loadingBookshelf)
                FailBookshelf(bookshelfOperationId, (chinese ? "浏览器进程异常：" : "Browser process failure: ") + args.ProcessFailedKind);
        }
        void OnBrowserWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string message;
            try { message = args.TryGetWebMessageAsString(); }
            catch { return; }
            if (message == "quiet:command")
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    ClearCommandHint();
                    commandInput.Text = "/";
                    commandInput.SelectionStart = commandInput.Text.Length;
                    commandInput.Focus();
                });
                return;
            }
            if (message == "quiet:advance" && readingActive)
                BeginInvoke((MethodInvoker)delegate { AdvanceReading(); });
        }
        void OnBrowserKeyDown(object sender, KeyEventArgs args)
        {
            if (mode != ReaderMode.Line || !readingActive) return;
            if (browser.Source == null || !IsQidianHost(browser.Source.Host)) return;
            if (Control.ModifierKeys == Keys.None && IsReadingKey(args.KeyCode))
            {
                args.Handled = true;
                args.SuppressKeyPress = true;
                AdvanceReading();
            }
        }

        static bool IsReadingKey(Keys key)
        {
            return (key >= Keys.D0 && key <= Keys.Z) ||
                (key >= Keys.NumPad0 && key <= Keys.Divide) ||
                key == Keys.Space || key == Keys.OemPeriod || key == Keys.Oemcomma ||
                key == Keys.OemSemicolon || key == Keys.OemQuotes || key == Keys.OemOpenBrackets || key == Keys.OemCloseBrackets;
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (guideOverlay.Visible)
            {
                if (keyData == Keys.Escape || keyData == Keys.Back)
                {
                    HideGuide();
                    return true;
                }
                return base.ProcessCmdKey(ref message, keyData);
            }
            if (keyData == Keys.Tab && commandInput.Focused && commandSuggestions.Visible)
            {
                CompleteSelectedCommand();
                return true;
            }
            if (keyData == Keys.F9) { ShowCommandPage(); return true; }
            if (keyData == Keys.F8) { ShowCommandPage(); return true; }
            if (keyData == (Keys.Control | Keys.Alt | Keys.Space)) { ShowCommandPage(); return true; }
            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            bool discoveryShortcutReady = currentPageKind == CommandPageKind.Discovery &&
                (!commandInput.Focused || showingCommandHint || String.IsNullOrWhiteSpace(commandInput.Text));
            if (discoveryShortcutReady && modifiers == Keys.None && key == Keys.N)
            {
                MoveDiscoveryDocumentPage(1);
                return true;
            }
            if (discoveryShortcutReady && modifiers == Keys.None && key == Keys.P)
            {
                MoveDiscoveryDocumentPage(-1);
                return true;
            }
            if (readingActive && !commandInput.Focused && modifiers == Keys.None && (key == Keys.OemQuestion || key == Keys.Divide))
            {
                ClearCommandHint();
                commandInput.Text = "/";
                commandInput.SelectionStart = commandInput.Text.Length;
                commandInput.Focus();
                return true;
            }
            if (readingActive && !commandInput.Focused && modifiers == Keys.None && IsReadingKey(key))
            {
                AdvanceReading();
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }
        void Navigate(string address)
        {
            if (!ready || browser.CoreWebView2 == null) return;
            Uri target = new Uri(address);
            if (browser.Source != null &&
                String.Equals(browser.Source.AbsoluteUri.TrimEnd('/'), target.AbsoluteUri.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                browser.Reload();
                return;
            }
            browser.CoreWebView2.Navigate(address);
        }

        static bool IsQidianHost(string host)
        {
            return String.Equals(host, "qidian.com", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".qidian.com", StringComparison.OrdinalIgnoreCase);
        }

        bool IsPagedReadingView()
        {
            return mode == ReaderMode.Hidden && ocrReadingActive && readingViewMode == ReadingViewMode.Scrolling;
        }

        bool IsPagedDiscoveryView()
        {
            return mode == ReaderMode.Hidden && currentPageKind == CommandPageKind.Discovery && discoveryBooks.Count > 0 &&
                (!loadingDiscovery || appendingDiscoveryPage) && !loadingBookshelf && !loadingCatalog && !loadingBookDetail && !openingChapter;
        }

        bool IsPagedDocumentView()
        {
            return IsPagedReadingView() || IsPagedDiscoveryView();
        }

        Size GetScaledPageSize()
        {
            double scale = documentZoom / 100.0;
            return new Size((int)Math.Round(BasePageWidth * scale), (int)Math.Round(BasePageHeight * scale));
        }

        Padding GetScaledPagePadding(Size pageSize)
        {
            int horizontal = Math.Max(32, (int)Math.Round(64 * documentZoom / 100.0));
            int vertical = Math.Max(28, (int)Math.Round(52 * documentZoom / 100.0));
            horizontal = Math.Min(horizontal, Math.Max(16, pageSize.Width / 4));
            vertical = Math.Min(vertical, Math.Max(16, pageSize.Height / 5));
            return new Padding(horizontal, vertical, horizontal, vertical);
        }

        Font CreateDocumentFont()
        {
            float size = Math.Max(7F, 11F * documentZoom / 100F);
            return chinese ? new Font("Microsoft YaHei", size) : new Font("Calibri", size);
        }

        void SetDocumentZoom(int value)
        {
            int next = Math.Max(50, Math.Min(200, value));
            if (documentZoom == next && zoomSlider != null && zoomSlider.Value == next) return;
            documentZoom = next;
            updatingZoom = true;
            if (zoomSlider != null) zoomSlider.Value = next;
            if (zoomLabel != null) zoomLabel.Text = next + "%";
            updatingZoom = false;
            if (ocrReadingActive) RenderOcrText();
            else if (currentPageKind == CommandPageKind.Discovery) RenderDiscoveryBooks();
            else ApplyLayout();
            UpdateModeStatus();
        }

        void OnDocumentMouseWheel(object sender, MouseEventArgs args)
        {
            if ((Control.ModifierKeys & Keys.Control) != Keys.Control) return;
            SetDocumentZoom(documentZoom + (args.Delta > 0 ? 10 : -10));
            HandledMouseEventArgs handled = args as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;
        }

        void SetMode(ReaderMode next)
        {
            mode = next;
            ApplyLayout();
            UpdateModeStatus();
        }
        void ApplyLayout()
        {
            if (workspace.ClientSize.Width <= 0 || workspace.ClientSize.Height <= 0) return;
            Size pageSize = GetScaledPageSize();
            Padding margins = GetScaledPagePadding(pageSize);
            int pageLeft = Math.Max(24, (workspace.ClientSize.Width - pageSize.Width) / 2);
            int pageTop = 24;

            if (IsPagedDocumentView())
            {
                workspace.AutoScroll = false;
                page.Visible = false;
                pagedDocumentHost.Dock = DockStyle.None;
                pagedDocumentHost.Bounds = new Rectangle(0, 0, workspace.ClientSize.Width, workspace.ClientSize.Height);
                pagedDocumentHost.Visible = true;
                LayoutRenderedPages(pageSize, margins, pageLeft, 24);
                return;
            }

            workspace.AutoScroll = true;
            pagedDocumentHost.Dock = DockStyle.None;
            pagedDocumentHost.Visible = false;
            page.Visible = true;
            page.Bounds = new Rectangle(pageLeft, pageTop, pageSize.Width, pageSize.Height);
            page.Padding = margins;
            workspace.AutoScrollMinSize = new Size(pageSize.Width + 48, pageSize.Height + 48);
            decoy.Font = CreateDocumentFont();

            Rectangle content = page.DisplayRectangle;
            int documentTop = content.Y;
            decoy.Bounds = new Rectangle(content.X, documentTop, content.Width, Math.Max(80, content.Bottom - documentTop));
            if (mode == ReaderMode.Hidden)
            {
                if (keepBrowserRunningBehindDocument && ready)
                {
                    browserHost.Visible = true;
                    browserHost.Bounds = new Rectangle(content.X, documentTop, content.Width, Math.Max(80, content.Bottom - documentTop));
                    browserHost.SendToBack();
                }
                else
                {
                    browserHost.Visible = false;
                }
                decoy.Visible = true;
                decoy.BringToFront();
                return;
            }
            browserHost.Visible = true;
            decoy.Visible = false;
            browserHost.Bounds = new Rectangle(content.X, documentTop, content.Width, Math.Max(80, content.Bottom - documentTop));
            browserHost.BringToFront();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref message);
                if ((int)message.Result == 1)
                {
                    long coordinates = message.LParam.ToInt64();
                    int screenX = unchecked((short)(coordinates & 0xffff));
                    int screenY = unchecked((short)((coordinates >> 16) & 0xffff));
                    Point clientPoint = PointToClient(new Point(screenX, screenY));
                    int grip = 8;
                    bool left = clientPoint.X <= grip;
                    bool right = clientPoint.X >= ClientSize.Width - grip;
                    bool top = clientPoint.Y <= grip;
                    bool bottom = clientPoint.Y >= ClientSize.Height - grip;
                    if (left && top) message.Result = new IntPtr(HtTopLeft);
                    else if (right && top) message.Result = new IntPtr(HtTopRight);
                    else if (left && bottom) message.Result = new IntPtr(HtBottomLeft);
                    else if (right && bottom) message.Result = new IntPtr(HtBottomRight);
                    else if (left) message.Result = new IntPtr(HtLeft);
                    else if (right) message.Result = new IntPtr(HtRight);
                    else if (top) message.Result = new IntPtr(HtTop);
                    else if (bottom) message.Result = new IntPtr(HtBottom);
                }
                return;
            }
            base.WndProc(ref message);
        }

        protected override void OnFormClosed(FormClosedEventArgs args)
        {
            StopExternalOcr();
            base.OnFormClosed(args);
        }

    }
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
