using System.Windows.Forms;
using PlcSourceExporter.Core;

namespace PlcSourceExporter.AddInShared;

internal sealed class ExportProgressWindow : IProgress<ExportProgress>
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly ManualResetEventSlim _closed = new();
    private readonly Thread _uiThread;
    private ExportProgressForm? _form;

    public ExportProgressWindow(string title, string exportRoot, string logFile)
    {
        _uiThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new ExportProgressForm(title, exportRoot, logFile);
            form.CancelRequested += (_, _) => _cancellationTokenSource.Cancel();
            form.FormClosed += (_, _) => _closed.Set();
            _form = form;
            _ready.Set();
            Application.Run(form);
            _closed.Set();
        });

        _uiThread.IsBackground = true;
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public void Report(ExportProgress value)
    {
        Invoke(form => form.UpdateProgress(value));
    }

    public void Complete(ExportSummary summary)
    {
        Invoke(form => form.MarkCompleted(summary), waitForCompletion: true);
        WaitForClose();
    }

    public void Canceled()
    {
        Invoke(form => form.MarkCanceled(), waitForCompletion: true);
        WaitForClose();
    }

    public void Failed(Exception ex)
    {
        Invoke(form => form.MarkFailed(ex), waitForCompletion: true);
        WaitForClose();
    }

    private void Invoke(Action<ExportProgressForm> action, bool waitForCompletion = false)
    {
        var form = _form;
        if (form == null || form.IsDisposed)
        {
            return;
        }

        try
        {
            if (form.InvokeRequired)
            {
                var callback = (Action)(() => action(form));
                if (waitForCompletion)
                {
                    form.Invoke(callback);
                }
                else
                {
                    form.BeginInvoke(callback);
                }
            }
            else
            {
                action(form);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void WaitForClose()
    {
        var form = _form;
        if (form == null || form.IsDisposed)
        {
            _closed.Set();
            return;
        }

        _closed.Wait();
        if (_uiThread.IsAlive)
        {
            _uiThread.Join(TimeSpan.FromSeconds(2));
        }
    }
}
