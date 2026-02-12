namespace PdfReaderLite;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) => ShowFatalError(eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => ShowFatalError(eventArgs.ExceptionObject as Exception);

        var startupPath = args.FirstOrDefault(arg => !string.IsNullOrWhiteSpace(arg));

        try
        {
            Application.Run(new MainForm(startupPath));
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private static void ShowFatalError(Exception? ex)
    {
        var details = ex?.ToString() ?? "Erro desconhecido.";

        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PDFReaderLite"
            );
            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(logDirectory, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}]{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Ignore log write failures.
        }

        MessageBox.Show(
            "PDF Reader Lite encontrou um erro inesperado e sera fechado.\n\n" + details,
            "PDF Reader Lite",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        );
    }
}
