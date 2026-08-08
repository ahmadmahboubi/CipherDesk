using System;
using System.Threading;
using System.Windows.Forms;
using CipherDesk.App.Dialogs;
using CipherDesk.App.Forms;
using CipherDesk.App.Services;
using CipherDesk.App.Theming;

namespace CipherDesk.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Emits the visual style, DPI and default font configuration declared in the .csproj.
        ApplicationConfiguration.Initialize();

        // Catch what escapes so the user gets an explanation instead of a stack trace,
        // and so an unexpected error can never leave a half-finished file behind unreported.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;

        AppSettings settings = AppSettings.Load();
        ThemeManager.Initialize(settings.Theme);

        using var mainForm = new MainForm(settings);
        Application.Run(mainForm);
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e) =>
        ReportFatal(e.Exception);

    private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception) ReportFatal(exception);
    }

    private static void ReportFatal(Exception exception)
    {
        UserFacingError error = ErrorPresenter.Describe(exception);

        ModernMessageBox.Error(null, error.Title, error.Message);
    }
}
