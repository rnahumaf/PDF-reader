using System.Globalization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PdfReaderLite;

public sealed class MainForm : Form
{
    private const string AppTitle = "PDF Reader Lite";

    private readonly WebView2 _pdfWebView;
    private string? _loadedFilePath;
    private bool _isOpeningDocument;
    private bool _isWindowInWebViewFullscreen;
    private bool _isFullscreenOwnedByWebViewElement;
    private FormBorderStyle _windowBorderStyleBeforeWebViewFullscreen;
    private FormWindowState _windowStateBeforeWebViewFullscreen;
    private Rectangle _windowBoundsBeforeWebViewFullscreen;
    private bool _windowTopMostBeforeWebViewFullscreen;

    public MainForm(string? startupPath)
    {
        Text = AppTitle;
        Width = 1220;
        Height = 840;
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        AllowDrop = true;

        _windowBorderStyleBeforeWebViewFullscreen = FormBorderStyle;
        _windowStateBeforeWebViewFullscreen = WindowState;
        _windowBoundsBeforeWebViewFullscreen = Bounds;
        _windowTopMostBeforeWebViewFullscreen = TopMost;

        _pdfWebView = BuildPdfWebView();
        Controls.Add(_pdfWebView);

        HookEvents();

        Shown += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(startupPath))
            {
                OpenDocument(startupPath);
                return;
            }

            OpenDocumentFromDialog();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pdfWebView.Dispose();
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
            case Keys.Control | Keys.S:
            case Keys.Control | Keys.Shift | Keys.S:
                _ = ShowNativeSaveUiAsync();
                return true;
            case Keys.Control | Keys.I:
                ShowCurrentDocumentInfo();
                return true;
            case Keys.Control | Keys.P:
                _ = PrintCurrentDocumentAsync();
                return true;
            case Keys.F11:
                ToggleHostFullscreen();
                return true;
            case Keys.Escape:
                if (_isWindowInWebViewFullscreen || _pdfWebView.CoreWebView2?.ContainsFullScreenElement == true)
                {
                    ExitWebViewFullscreenIfNeeded();
                    _pdfWebView.Focus();
                    return true;
                }

                break;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static WebView2 BuildPdfWebView()
    {
        return new WebView2
        {
            Dock = DockStyle.Fill
        };
    }

    private void HookEvents()
    {
        DragEnter += MainFormOnDragEnter;
        DragDrop += MainFormOnDragDrop;
        _pdfWebView.KeyDown += PdfWebViewOnKeyDown;
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

        var firstPdf = files.FirstOrDefault(path =>
            string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase));

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
        _ = OpenDocumentAsync(filePath);
    }

    private async Task OpenDocumentAsync(string? filePath)
    {
        if (_isOpeningDocument || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(filePath);
        if (!File.Exists(normalizedPath))
        {
            MessageBox.Show(this, $"Arquivo nao encontrado:\n{normalizedPath}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isOpeningDocument = true;

        try
        {
            UseWaitCursor = true;

            if (!await EnsurePdfWebViewReadyAsync() || _pdfWebView.CoreWebView2 == null)
            {
                return;
            }

            _loadedFilePath = normalizedPath;
            Text = $"{Path.GetFileName(normalizedPath)} - {AppTitle}";

            _pdfWebView.CoreWebView2.Navigate(BuildDocumentViewerUrl(1));
            _pdfWebView.BringToFront();
            _pdfWebView.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nao foi possivel abrir o PDF.\n\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _isOpeningDocument = false;
        }
    }

    private async Task ShowNativeSaveUiAsync()
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath))
        {
            return;
        }

        if (!await EnsurePdfWebViewReadyAsync() || _pdfWebView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            await _pdfWebView.CoreWebView2.ShowSaveAsUIAsync();
        }
        catch
        {
            await SaveDocumentAsAsync();
        }
    }

    private async Task SaveDocumentAsAsync()
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath))
        {
            MessageBox.Show(this, "Nenhum PDF aberto para salvar.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var defaultName = $"{Path.GetFileNameWithoutExtension(_loadedFilePath)}-copia.pdf";

        using var dialog = new SaveFileDialog
        {
            Title = "Salvar PDF como",
            Filter = "Arquivos PDF (*.pdf)|*.pdf|Todos os arquivos (*.*)|*.*",
            CheckPathExists = true,
            AddExtension = true,
            DefaultExt = "pdf",
            OverwritePrompt = true,
            FileName = defaultName
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var destinationPath = Path.GetFullPath(dialog.FileName);
        if (string.Equals(destinationPath, _loadedFilePath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Escolha outro nome para criar um novo arquivo.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;

            if (await TrySaveFromWebViewAsync(destinationPath))
            {
                return;
            }

            File.Copy(_loadedFilePath, destinationPath, overwrite: true);
            MessageBox.Show(
                this,
                "Nao foi possivel exportar a versao atual da visualizacao.\nFoi criada uma copia do arquivo original.",
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nao foi possivel salvar o PDF.\n\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task<bool> TrySaveFromWebViewAsync(string destinationPath)
    {
        if (!await EnsurePdfWebViewReadyAsync() || _pdfWebView.CoreWebView2 == null)
        {
            return false;
        }

        try
        {
            var saved = await _pdfWebView.CoreWebView2.PrintToPdfAsync(destinationPath);
            return saved && File.Exists(destinationPath);
        }
        catch
        {
            return false;
        }
    }

    private void ShowCurrentDocumentInfo()
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath))
        {
            MessageBox.Show(this, "Nenhum PDF aberto.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var fileInfo = new FileInfo(_loadedFilePath);
        var modifiedAt = fileInfo.Exists
            ? fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "desconhecido";
        var fileSize = fileInfo.Exists ? FormatFileSize(fileInfo.Length) : "desconhecido";

        var details =
            $"Arquivo: {Path.GetFileName(_loadedFilePath)}\n" +
            $"Tamanho: {fileSize}\n" +
            $"Modificado: {modifiedAt}\n" +
            "Modo: WebView2 nativo\n\n" +
            $"Caminho:\n{_loadedFilePath}";

        MessageBox.Show(this, details, "Informacoes do arquivo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string FormatFileSize(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        double value = Math.Max(0, bytes);
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private async Task PrintCurrentDocumentAsync()
    {
        if (string.IsNullOrWhiteSpace(_loadedFilePath))
        {
            return;
        }

        if (!await EnsurePdfWebViewReadyAsync() || _pdfWebView.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            await _pdfWebView.CoreWebView2.ExecuteScriptAsync("window.print();");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Falha ao imprimir.\n\n{ex.Message}", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task<bool> EnsurePdfWebViewReadyAsync()
    {
        if (_pdfWebView.CoreWebView2 != null)
        {
            ConfigurePdfWebView();
            return true;
        }

        try
        {
            await _pdfWebView.EnsureCoreWebView2Async();

            if (_pdfWebView.CoreWebView2 != null)
            {
                ConfigurePdfWebView();
            }

            return _pdfWebView.CoreWebView2 != null;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Nao foi possivel iniciar o visualizador PDF.\nInstale/atualize o Microsoft Edge WebView2 Runtime.\n\n" + ex.Message,
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return false;
        }
    }

    private void ConfigurePdfWebView()
    {
        if (_pdfWebView.CoreWebView2 == null)
        {
            return;
        }

        _pdfWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _pdfWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
        _pdfWebView.CoreWebView2.ContainsFullScreenElementChanged -= PdfWebViewOnContainsFullScreenElementChanged;
        _pdfWebView.CoreWebView2.ContainsFullScreenElementChanged += PdfWebViewOnContainsFullScreenElementChanged;
    }

    private void PdfWebViewOnContainsFullScreenElementChanged(object? sender, object e)
    {
        if (_pdfWebView.CoreWebView2?.ContainsFullScreenElement == true)
        {
            _isFullscreenOwnedByWebViewElement = true;
            EnterNativeFullscreenForWebView();
            return;
        }

        if (!_isFullscreenOwnedByWebViewElement)
        {
            return;
        }

        _isFullscreenOwnedByWebViewElement = false;
        ExitNativeFullscreenForWebView();
    }

    private void PdfWebViewOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F11)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ToggleHostFullscreen();
            return;
        }

        if (e.KeyCode != Keys.Escape ||
            (!_isWindowInWebViewFullscreen && _pdfWebView.CoreWebView2?.ContainsFullScreenElement != true))
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        ExitWebViewFullscreenIfNeeded();
        _pdfWebView.Focus();
    }

    private void ToggleHostFullscreen()
    {
        _isFullscreenOwnedByWebViewElement = false;

        if (_isWindowInWebViewFullscreen)
        {
            ExitWebViewFullscreenIfNeeded();
            return;
        }

        EnterNativeFullscreenForWebView();
    }

    private void EnterNativeFullscreenForWebView()
    {
        if (_isWindowInWebViewFullscreen)
        {
            return;
        }

        _windowBorderStyleBeforeWebViewFullscreen = FormBorderStyle;
        _windowStateBeforeWebViewFullscreen = WindowState;
        _windowBoundsBeforeWebViewFullscreen = Bounds;
        _windowTopMostBeforeWebViewFullscreen = TopMost;

        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        Bounds = Screen.FromControl(this).Bounds;
        TopMost = true;
        WindowState = FormWindowState.Maximized;
        _isWindowInWebViewFullscreen = true;
    }

    private void ExitNativeFullscreenForWebView()
    {
        if (!_isWindowInWebViewFullscreen)
        {
            return;
        }

        TopMost = _windowTopMostBeforeWebViewFullscreen;
        FormBorderStyle = _windowBorderStyleBeforeWebViewFullscreen;

        if (_windowStateBeforeWebViewFullscreen == FormWindowState.Normal)
        {
            WindowState = FormWindowState.Normal;
            Bounds = _windowBoundsBeforeWebViewFullscreen;
        }
        else
        {
            WindowState = _windowStateBeforeWebViewFullscreen;
        }

        _isWindowInWebViewFullscreen = false;
    }

    private void ExitWebViewFullscreenIfNeeded()
    {
        _isFullscreenOwnedByWebViewElement = false;
        var core = _pdfWebView.CoreWebView2;

        if (core?.ContainsFullScreenElement == true)
        {
            _ = core.ExecuteScriptAsync("if (document.fullscreenElement) { document.exitFullscreen(); }");
        }

        if (core != null)
        {
            _ = core.ExecuteScriptAsync(
                "try { if (typeof PDFViewerApplication !== 'undefined' && PDFViewerApplication.pdfPresentationMode?.active) { PDFViewerApplication.pdfPresentationMode.exit(); } } catch (_) { }"
            );
        }

        ExitNativeFullscreenForWebView();
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
}
