using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using Microsoft.Web.WebView2.WinForms;
using PdfiumViewer;

namespace PdfReaderLite;

public sealed class MainForm : Form
{
    private enum PdfFormHint
    {
        None,
        AcroForm,
        Xfa
    }

    private const int ThumbnailWidth = 130;
    private const int ThumbnailHeight = 180;
    private const string AppTitle = "PDF Reader Lite";

    private ToolStripButton _openButton = null!;
    private ToolStripButton _togglePreviewButton = null!;
    private ToolStripButton _previousPageButton = null!;
    private ToolStripTextBox _pageTextBox = null!;
    private ToolStripLabel _pageCountLabel = null!;
    private ToolStripButton _nextPageButton = null!;
    private ToolStripButton _zoomOutButton = null!;
    private ToolStripComboBox _zoomComboBox = null!;
    private ToolStripButton _zoomInButton = null!;
    private ToolStripButton _printButton = null!;
    private ToolStripButton _formFillButton = null!;

    private readonly SplitContainer _layoutContainer;
    private readonly ListView _thumbnailListView;
    private readonly ImageList _thumbnailImageList;
    private readonly PdfViewer _pdfViewer;
    private readonly WebView2 _formWebView;

    private readonly Queue<int> _thumbnailRenderQueue = new();
    private readonly System.Windows.Forms.Timer _thumbnailRenderTimer;
    private readonly System.Windows.Forms.Timer _viewStateTimer;

    private PdfDocument? _document;
    private string? _loadedFilePath;
    private bool _syncingUi;
    private bool _isFormFillMode;
    private bool _isSwitchingFormMode;
    private bool _previewWasVisibleBeforeFormMode = true;
    private bool _formFillHintShown;
    private bool _xfaCompatibilityHintShown;
    private PdfFormHint _currentDocumentFormHint = PdfFormHint.None;

    public MainForm(string? startupPath)
    {
        Text = AppTitle;
        Width = 1220;
        Height = 840;
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        AllowDrop = true;

        var toolbar = BuildToolbar();
        _layoutContainer = BuildLayoutContainer();
        _thumbnailImageList = BuildThumbnailImageList();
        _thumbnailListView = BuildThumbnailListView();
        _pdfViewer = BuildPdfViewer();
        _formWebView = BuildFormWebView();

        _layoutContainer.Panel1.Controls.Add(_thumbnailListView);
        _layoutContainer.Panel2.Controls.Add(_formWebView);
        _layoutContainer.Panel2.Controls.Add(_pdfViewer);

        Controls.Add(_layoutContainer);
        Controls.Add(toolbar);

        _thumbnailRenderTimer = new System.Windows.Forms.Timer { Interval = 1 };
        _thumbnailRenderTimer.Tick += (_, _) => RenderNextThumbnail();

        _viewStateTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _viewStateTimer.Tick += (_, _) => UpdateViewState();
        _viewStateTimer.Start();

        HookEvents();
        ApplyEmptyState();

        if (!string.IsNullOrWhiteSpace(startupPath))
        {
            Shown += (_, _) => OpenDocument(startupPath);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnailRenderTimer.Stop();
            _viewStateTimer.Stop();
            _thumbnailRenderTimer.Dispose();
            _viewStateTimer.Dispose();

            _pdfViewer.Document = null;
            _document?.Dispose();
            _document = null;
            _formWebView.Dispose();

            DisposeThumbnailImages();
        }

        base.Dispose(disposing);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.O:
                OpenDocumentFromDialog();
                return true;
            case Keys.Control | Keys.P:
                _ = PrintCurrentDocumentAsync();
                return true;
            case Keys.Control | Keys.E:
                _ = ToggleFormFillModeFromShortcutAsync();
                return true;
        }

        if (_isFormFillMode)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        switch (keyData)
        {
            case Keys.Control | Keys.Oemplus:
            case Keys.Control | Keys.Add:
                ZoomIn();
                return true;
            case Keys.Control | Keys.OemMinus:
            case Keys.Control | Keys.Subtract:
                ZoomOut();
                return true;
            case Keys.Control | Keys.D0:
            case Keys.Control | Keys.NumPad0:
                SetZoomMode(PdfViewerZoomMode.FitBest);
                return true;
            case Keys.Control | Keys.D1:
            case Keys.Control | Keys.NumPad1:
                SetZoomMode(PdfViewerZoomMode.FitWidth);
                return true;
            case Keys.PageUp:
            case Keys.Left:
            case Keys.Up:
                GoToPage((_pdfViewer.Document == null ? 1 : _pdfViewer.Renderer.Page + 1) - 1);
                return true;
            case Keys.PageDown:
            case Keys.Right:
            case Keys.Down:
                GoToPage((_pdfViewer.Document == null ? 1 : _pdfViewer.Renderer.Page + 1) + 1);
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private ToolStrip BuildToolbar()
    {
        var toolbarHeight = Math.Max(52, (int)Math.Ceiling(Font.GetHeight() + 30));

        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = toolbarHeight,
            Padding = new Padding(8, 8, 8, 8),
            BackColor = Color.FromArgb(245, 245, 245)
        };

        _openButton = CreateTextButton("Abrir", "Ctrl+O");
        _formFillButton = CreateTextButton("Formulario", "Ctrl+E");
        _formFillButton.CheckOnClick = true;
        _togglePreviewButton = CreateTextButton("Preview", "Mostrar/Ocultar miniaturas");
        _togglePreviewButton.CheckOnClick = true;
        _togglePreviewButton.Checked = true;

        _previousPageButton = CreateTextButton("Anterior", "PgUp");
        _pageTextBox = new ToolStripTextBox
        {
            AutoSize = false,
            Size = new Size(56, 28),
            Text = "0",
            TextBoxTextAlign = HorizontalAlignment.Center
        };
        _pageCountLabel = new ToolStripLabel("/ 0");
        _nextPageButton = CreateTextButton("Proxima", "PgDn");

        _zoomOutButton = CreateTextButton("-", "Ctrl+-");
        _zoomComboBox = new ToolStripComboBox
        {
            AutoSize = false,
            Size = new Size(132, 28),
            DropDownStyle = ComboBoxStyle.DropDown
        };
        _zoomComboBox.Items.AddRange(
        [
            "Ajustar largura",
            "Ajustar pagina",
            "50%",
            "75%",
            "100%",
            "125%",
            "150%",
            "200%"
        ]);
        _zoomComboBox.Text = "Ajustar largura";
        _zoomInButton = CreateTextButton("+", "Ctrl++");
        _printButton = CreateTextButton("Imprimir", "Ctrl+P");

        toolbar.Items.AddRange(
        [
            _openButton,
            new ToolStripSeparator(),
            _formFillButton,
            new ToolStripSeparator(),
            _togglePreviewButton,
            new ToolStripSeparator(),
            _previousPageButton,
            _pageTextBox,
            _pageCountLabel,
            _nextPageButton,
            new ToolStripSeparator(),
            _zoomOutButton,
            _zoomComboBox,
            _zoomInButton,
            new ToolStripSeparator(),
            _printButton
        ]);

        return toolbar;
    }

    private static ToolStripButton CreateTextButton(string text, string tooltip)
    {
        return new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = true,
            Margin = new Padding(2, 1, 2, 1),
            ToolTipText = tooltip
        };
    }

    private static SplitContainer BuildLayoutContainer()
    {
        return new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 230,
            Panel1MinSize = 190,
            SplitterWidth = 6
        };
    }

    private static ImageList BuildThumbnailImageList()
    {
        return new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(ThumbnailWidth, ThumbnailHeight)
        };
    }

    private ListView BuildThumbnailListView()
    {
        var listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.LargeIcon,
            LargeImageList = _thumbnailImageList,
            MultiSelect = false,
            HideSelection = false,
            UseCompatibleStateImageBehavior = false,
            BackColor = Color.FromArgb(250, 250, 250)
        };

        return listView;
    }

    private static PdfViewer BuildPdfViewer()
    {
        var viewer = new PdfViewer
        {
            Dock = DockStyle.Fill,
            ShowToolbar = false,
            ShowBookmarks = false
        };

        viewer.Renderer.BackColor = Color.FromArgb(45, 45, 45);

        return viewer;
    }

    private static WebView2 BuildFormWebView()
    {
        return new WebView2
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
    }

    private void HookEvents()
    {
        _openButton.Click += (_, _) => OpenDocumentFromDialog();
        _formFillButton.Click += async (_, _) => await ToggleFormFillModeAsync();
        _togglePreviewButton.Click += (_, _) => TogglePreviewPanel();
        _previousPageButton.Click += (_, _) => GoToPage((_pdfViewer.Document == null ? 1 : _pdfViewer.Renderer.Page + 1) - 1);
        _nextPageButton.Click += (_, _) => GoToPage((_pdfViewer.Document == null ? 1 : _pdfViewer.Renderer.Page + 1) + 1);
        _zoomOutButton.Click += (_, _) => ZoomOut();
        _zoomInButton.Click += (_, _) => ZoomIn();
        _printButton.Click += async (_, _) => await PrintCurrentDocumentAsync();

        _pageTextBox.KeyDown += PageTextBoxOnKeyDown;
        _pageTextBox.Leave += (_, _) => GoToTypedPage();

        _zoomComboBox.SelectedIndexChanged += (_, _) => ApplyZoomFromComboBox();
        _zoomComboBox.KeyDown += ZoomComboBoxOnKeyDown;
        _zoomComboBox.Leave += (_, _) => ApplyZoomFromComboBox();

        _thumbnailListView.SelectedIndexChanged += ThumbnailListViewOnSelectedIndexChanged;

        _pdfViewer.Renderer.Scroll += (_, _) => UpdateViewState();
        _pdfViewer.Renderer.DisplayRectangleChanged += (_, _) => UpdateViewState();
        _pdfViewer.Renderer.ZoomChanged += (_, _) => UpdateViewState(force: true);

        DragEnter += MainFormOnDragEnter;
        DragDrop += MainFormOnDragDrop;
    }

    private void MainFormOnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            return;
        }

        e.Effect = DragDropEffects.None;
    }

    private void MainFormOnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var firstPdf = files.FirstOrDefault(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase));

        if (firstPdf != null)
        {
            OpenDocument(firstPdf);
        }
    }

    private void OpenDocumentFromDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Abrir PDF",
            Filter = "Arquivos PDF (*.pdf)|*.pdf|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenDocument(dialog.FileName);
        }
    }

    private void OpenDocument(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(filePath);

        if (!File.Exists(normalizedPath))
        {
            MessageBox.Show(this, $"Arquivo nao encontrado:\n{normalizedPath}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        PdfDocument? loadedDocument = null;

        try
        {
            UseWaitCursor = true;
            loadedDocument = PdfDocument.Load(normalizedPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nao foi possivel abrir o PDF.\n\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            loadedDocument?.Dispose();
            return;
        }
        finally
        {
            UseWaitCursor = false;
        }

        var oldDocument = _document;
        _document = loadedDocument;
        _pdfViewer.Document = loadedDocument;
        oldDocument?.Dispose();

        _loadedFilePath = normalizedPath;
        Text = $"{Path.GetFileName(normalizedPath)} - {AppTitle}";
        _currentDocumentFormHint = DetectFormHint(normalizedPath);

        if (_document.PageCount > 0)
        {
            SetZoomMode(PdfViewerZoomMode.FitWidth);
            _pdfViewer.Renderer.Page = 0;
        }

        RebuildThumbnailList();

        if (_isFormFillMode)
        {
            _ = ReloadFormModeDocumentAsync();
        }
        else
        {
            _ = AutoEnableFormFillModeIfNeededAsync();
        }

        UpdateViewState(force: true);
    }

    private void RebuildThumbnailList()
    {
        _thumbnailRenderTimer.Stop();
        _thumbnailRenderQueue.Clear();

        _thumbnailListView.BeginUpdate();
        _thumbnailListView.Items.Clear();
        ResetThumbnailImageList();

        if (_document != null)
        {
            for (var i = 0; i < _document.PageCount; i++)
            {
                var listViewItem = new ListViewItem((i + 1).ToString(CultureInfo.InvariantCulture), 0);
                _thumbnailListView.Items.Add(listViewItem);
                _thumbnailRenderQueue.Enqueue(i);
            }
        }

        _thumbnailListView.EndUpdate();

        if (_thumbnailRenderQueue.Count > 0)
        {
            _thumbnailRenderTimer.Start();
        }
    }

    private void ResetThumbnailImageList()
    {
        DisposeThumbnailImages();
        _thumbnailImageList.Images.Add(CreatePlaceholderThumbnail());
    }

    private void DisposeThumbnailImages()
    {
        foreach (Image image in _thumbnailImageList.Images)
        {
            image.Dispose();
        }

        _thumbnailImageList.Images.Clear();
    }

    private static Bitmap CreatePlaceholderThumbnail()
    {
        var bitmap = new Bitmap(ThumbnailWidth, ThumbnailHeight);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(242, 242, 242));
        graphics.DrawRectangle(Pens.Gainsboro, 0, 0, ThumbnailWidth - 1, ThumbnailHeight - 1);

        return bitmap;
    }

    private void RenderNextThumbnail()
    {
        if (_document == null || _thumbnailRenderQueue.Count == 0)
        {
            _thumbnailRenderTimer.Stop();
            return;
        }

        var pageIndex = _thumbnailRenderQueue.Dequeue();

        try
        {
            var thumbnail = CreateThumbnailForPage(pageIndex);
            _thumbnailImageList.Images.Add(thumbnail);

            var imageIndex = _thumbnailImageList.Images.Count - 1;
            if (pageIndex >= 0 && pageIndex < _thumbnailListView.Items.Count)
            {
                _thumbnailListView.Items[pageIndex].ImageIndex = imageIndex;
            }
        }
        catch
        {
            // Ignore thumbnail render failures to keep navigation available.
        }
    }

    private Bitmap CreateThumbnailForPage(int pageIndex)
    {
        if (_document == null)
        {
            return CreatePlaceholderThumbnail();
        }

        var pageSize = _document.PageSizes[pageIndex];
        var pageRatio = pageSize.Height <= 0 ? 1f : pageSize.Width / pageSize.Height;

        var viewportWidth = ThumbnailWidth - 10;
        var viewportHeight = ThumbnailHeight - 10;

        int renderedWidth;
        int renderedHeight;

        if (pageRatio >= viewportWidth / (float)viewportHeight)
        {
            renderedWidth = viewportWidth;
            renderedHeight = Math.Max(1, (int)Math.Round(renderedWidth / pageRatio));
        }
        else
        {
            renderedHeight = viewportHeight;
            renderedWidth = Math.Max(1, (int)Math.Round(renderedHeight * pageRatio));
        }

        using var renderedPage = _document.Render(
            pageIndex,
            renderedWidth,
            renderedHeight,
            96,
            96,
            PdfRenderFlags.Annotations | PdfRenderFlags.CorrectFromDpi
        );

        var canvas = new Bitmap(ThumbnailWidth, ThumbnailHeight);

        using var graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var drawX = (ThumbnailWidth - renderedWidth) / 2;
        var drawY = (ThumbnailHeight - renderedHeight) / 2;
        graphics.DrawImage(renderedPage, drawX, drawY, renderedWidth, renderedHeight);
        graphics.DrawRectangle(Pens.Gainsboro, 0, 0, ThumbnailWidth - 1, ThumbnailHeight - 1);

        return canvas;
    }

    private void ThumbnailListViewOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncingUi || _document == null || _thumbnailListView.SelectedIndices.Count == 0)
        {
            return;
        }

        var selectedPage = _thumbnailListView.SelectedIndices[0] + 1;
        GoToPage(selectedPage);
    }

    private void GoToPage(int pageNumber)
    {
        if (_isFormFillMode || _document == null || _document.PageCount == 0)
        {
            return;
        }

        var clampedPage = Math.Clamp(pageNumber, 1, _document.PageCount);
        _pdfViewer.Renderer.Page = clampedPage - 1;
        UpdateViewState(force: true);
    }

    private void PageTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        GoToTypedPage();
    }

    private void GoToTypedPage()
    {
        if (_document == null)
        {
            return;
        }

        if (!int.TryParse(_pageTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageNumber))
        {
            UpdateViewState(force: true);
            return;
        }

        GoToPage(pageNumber);
    }

    private void ZoomComboBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        ApplyZoomFromComboBox();
    }

    private void ApplyZoomFromComboBox()
    {
        if (_isFormFillMode || _document == null || _syncingUi)
        {
            return;
        }

        var input = _zoomComboBox.Text.Trim();

        if (string.Equals(input, "Ajustar largura", StringComparison.OrdinalIgnoreCase))
        {
            SetZoomMode(PdfViewerZoomMode.FitWidth);
            return;
        }

        if (string.Equals(input, "Ajustar pagina", StringComparison.OrdinalIgnoreCase))
        {
            SetZoomMode(PdfViewerZoomMode.FitBest);
            return;
        }

        if (!TryParseZoom(input, out var zoom))
        {
            UpdateViewState(force: true);
            return;
        }

        _pdfViewer.Renderer.Zoom = zoom;
        UpdateViewState(force: true);
    }

    private void SetZoomMode(PdfViewerZoomMode mode)
    {
        if (_isFormFillMode || _document == null)
        {
            return;
        }

        _pdfViewer.ZoomMode = mode;

        _syncingUi = true;
        _zoomComboBox.Text = mode == PdfViewerZoomMode.FitWidth ? "Ajustar largura" : "Ajustar pagina";
        _syncingUi = false;

        UpdateViewState(force: true);
    }

    private void ZoomIn()
    {
        if (_isFormFillMode || _document == null)
        {
            return;
        }

        _pdfViewer.Renderer.ZoomIn();
        UpdateViewState(force: true);
    }

    private void ZoomOut()
    {
        if (_isFormFillMode || _document == null)
        {
            return;
        }

        _pdfViewer.Renderer.ZoomOut();
        UpdateViewState(force: true);
    }

    private static bool TryParseZoom(string input, out double zoom)
    {
        zoom = 1.0;
        var normalized = input.Trim();

        if (normalized.EndsWith('%'))
        {
            normalized = normalized[..^1];
        }

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
        {
            return false;
        }

        zoom = Math.Clamp(percentage / 100.0, 0.2, 6.0);
        return true;
    }

    private void TogglePreviewPanel()
    {
        if (_isFormFillMode)
        {
            _togglePreviewButton.Checked = false;
            _layoutContainer.Panel1Collapsed = true;
            return;
        }

        var showPreview = _togglePreviewButton.Checked;
        _layoutContainer.Panel1Collapsed = !showPreview;
    }

    private async Task PrintCurrentDocumentAsync()
    {
        if (_document == null)
        {
            return;
        }

        if (_isFormFillMode)
        {
            await PrintFromFormWebViewAsync();
            return;
        }

        using var printDocument = _document.CreatePrintDocument(PdfPrintMode.ShrinkToMargin);
        printDocument.DocumentName = Path.GetFileName(_loadedFilePath ?? "documento.pdf");

        using var printDialog = new PrintDialog
        {
            Document = printDocument,
            AllowSomePages = true,
            UseEXDialog = false
        };

        if (printDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            printDocument.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Falha ao imprimir.\n\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task PrintFromFormWebViewAsync()
    {
        if (!await EnsureFormWebViewReadyAsync() || _formWebView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            await _formWebView.CoreWebView2.ExecuteScriptAsync("window.print();");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Falha ao imprimir no modo de formulario.\n\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ToggleFormFillModeFromShortcutAsync()
    {
        if (_document == null)
        {
            return;
        }

        _formFillButton.Checked = !_formFillButton.Checked;
        await ToggleFormFillModeAsync();
    }

    private async Task ToggleFormFillModeAsync()
    {
        if (_isSwitchingFormMode)
        {
            return;
        }

        _isSwitchingFormMode = true;

        try
        {
            if (!_formFillButton.Checked)
            {
                ExitFormFillMode();
                return;
            }

            if (_document == null)
            {
                _formFillButton.Checked = false;
                return;
            }

            if (!await LoadCurrentDocumentInFormModeAsync())
            {
                _formFillButton.Checked = false;
                ExitFormFillMode();
                return;
            }

            EnterFormFillMode();
        }
        finally
        {
            _isSwitchingFormMode = false;
        }
    }

    private void EnterFormFillMode()
    {
        if (_isFormFillMode)
        {
            return;
        }

        _isFormFillMode = true;
        _previewWasVisibleBeforeFormMode = _togglePreviewButton.Checked;
        _togglePreviewButton.Checked = false;
        _layoutContainer.Panel1Collapsed = true;
        _thumbnailListView.Enabled = false;

        _pdfViewer.Visible = false;
        _formWebView.Visible = true;
        _formWebView.BringToFront();
        _formWebView.Focus();

        if (!_formFillHintShown)
        {
            _formFillHintShown = true;
            MessageBox.Show(
                this,
                "Modo de formulario ativo.\nUse a barra nativa do visualizador para salvar o PDF preenchido.",
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        if (_currentDocumentFormHint == PdfFormHint.Xfa && !_xfaCompatibilityHintShown)
        {
            _xfaCompatibilityHintShown = true;
            MessageBox.Show(
                this,
                "Este PDF usa formulario XFA. Alguns arquivos XFA podem abrir apenas em modo leitura no motor do Edge/WebView2.\nSe os campos seguirem bloqueados, abra no Adobe Acrobat Reader.",
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }

        UpdateViewState(force: true);
    }

    private void ExitFormFillMode()
    {
        if (!_isFormFillMode)
        {
            return;
        }

        _isFormFillMode = false;
        _thumbnailListView.Enabled = true;

        _formWebView.Visible = false;
        _pdfViewer.Visible = true;
        _pdfViewer.BringToFront();
        _pdfViewer.Focus();

        _togglePreviewButton.Checked = _previewWasVisibleBeforeFormMode;
        _layoutContainer.Panel1Collapsed = !_previewWasVisibleBeforeFormMode;

        UpdateViewState(force: true);
    }

    private async Task<bool> LoadCurrentDocumentInFormModeAsync()
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath))
        {
            return false;
        }

        if (!await EnsureFormWebViewReadyAsync() || _formWebView.CoreWebView2 == null)
        {
            return false;
        }

        var currentPage = _pdfViewer.Document == null ? 1 : _pdfViewer.Renderer.Page + 1;

        try
        {
            _formWebView.CoreWebView2.Navigate(BuildDocumentViewerUrl(currentPage));
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nao foi possivel abrir o modo de formulario.\n\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private async Task AutoEnableFormFillModeIfNeededAsync()
    {
        if (_document == null || _isFormFillMode || _currentDocumentFormHint == PdfFormHint.None)
        {
            return;
        }

        _formFillButton.Checked = true;
        await ToggleFormFillModeAsync();
    }

    private async Task ReloadFormModeDocumentAsync()
    {
        if (await LoadCurrentDocumentInFormModeAsync())
        {
            return;
        }

        _formFillButton.Checked = false;
        ExitFormFillMode();
    }

    private async Task<bool> EnsureFormWebViewReadyAsync()
    {
        if (_formWebView.CoreWebView2 != null)
        {
            return true;
        }

        try
        {
            UseWaitCursor = true;
            await _formWebView.EnsureCoreWebView2Async();

            if (_formWebView.CoreWebView2 != null)
            {
                _formWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            }

            return _formWebView.CoreWebView2 != null;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Nao foi possivel iniciar o modo de formulario.\nInstale/atualize o Microsoft Edge WebView2 Runtime.\n\n" + ex.Message,
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return false;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private string BuildDocumentViewerUrl(int pageNumber)
    {
        var documentUri = new Uri(_loadedFilePath!);
        var page = Math.Max(1, pageNumber);
        var builder = new UriBuilder(documentUri)
        {
            Fragment = $"page={page}"
        };

        return builder.Uri.AbsoluteUri;
    }

    private static PdfFormHint DetectFormHint(string filePath)
    {
        const int maxBytesToScan = 2 * 1024 * 1024;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bytesToRead = (int)Math.Min(stream.Length, maxBytesToScan);
            if (bytesToRead <= 0)
            {
                return PdfFormHint.None;
            }

            var buffer = new byte[bytesToRead];
            _ = stream.Read(buffer, 0, bytesToRead);

            var text = Encoding.ASCII.GetString(buffer);
            if (text.Contains("/XFA", StringComparison.Ordinal))
            {
                return PdfFormHint.Xfa;
            }

            if (text.Contains("/AcroForm", StringComparison.Ordinal))
            {
                return PdfFormHint.AcroForm;
            }
        }
        catch
        {
            // If probing fails, keep default reader mode.
        }

        return PdfFormHint.None;
    }

    private void ApplyEmptyState()
    {
        _formFillButton.Checked = false;
        ExitFormFillMode();

        _syncingUi = true;
        _pageTextBox.Text = "0";
        _pageCountLabel.Text = "/ 0";
        _zoomComboBox.Text = "100%";
        _syncingUi = false;

        _previousPageButton.Enabled = false;
        _nextPageButton.Enabled = false;
        _pageTextBox.Enabled = false;
        _zoomOutButton.Enabled = false;
        _zoomInButton.Enabled = false;
        _zoomComboBox.Enabled = false;
        _togglePreviewButton.Enabled = false;
        _printButton.Enabled = false;
        _formFillButton.Enabled = false;
        _thumbnailListView.Enabled = false;
    }

    private void UpdateViewState(bool force = false)
    {
        if (_syncingUi)
        {
            return;
        }

        if (_document == null || _document.PageCount == 0)
        {
            ApplyEmptyState();
            return;
        }

        var page = _pdfViewer.Renderer.Page + 1;
        var zoomPercent = (int)Math.Round(_pdfViewer.Renderer.Zoom * 100);

        _syncingUi = true;
        _pageTextBox.Text = page.ToString(CultureInfo.InvariantCulture);
        _pageCountLabel.Text = $"/ {_document.PageCount}";
        _zoomComboBox.Text = _isFormFillMode ? "Modo formulario" : $"{zoomPercent}%";
        _syncingUi = false;

        if (!_isFormFillMode && (force || _thumbnailListView.SelectedIndices.Count == 0 || _thumbnailListView.SelectedIndices[0] != page - 1))
        {
            SelectThumbnail(page - 1);
        }

        _previousPageButton.Enabled = !_isFormFillMode && page > 1;
        _nextPageButton.Enabled = !_isFormFillMode && page < _document.PageCount;
        _pageTextBox.Enabled = !_isFormFillMode;
        _zoomOutButton.Enabled = !_isFormFillMode;
        _zoomInButton.Enabled = !_isFormFillMode;
        _zoomComboBox.Enabled = !_isFormFillMode;
        _togglePreviewButton.Enabled = !_isFormFillMode;
        _printButton.Enabled = true;
        _formFillButton.Enabled = true;
        _thumbnailListView.Enabled = !_isFormFillMode;
    }

    private void SelectThumbnail(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _thumbnailListView.Items.Count)
        {
            return;
        }

        _syncingUi = true;
        _thumbnailListView.SelectedIndices.Clear();
        _thumbnailListView.Items[pageIndex].Selected = true;
        _thumbnailListView.EnsureVisible(pageIndex);
        _syncingUi = false;
    }
}
