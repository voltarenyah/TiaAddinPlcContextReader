using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using PlcSourceExporter.Core;

namespace PlcSourceExporter.AddInShared;

internal sealed class ExportProgressForm : Form
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Label _statusLabel = new();
    private readonly Label _percentLabel = new();
    private readonly Label _currentItemLabel = new();
    private readonly Label _countsLabel = new();
    private readonly Label _elapsedLabel = new();
    private readonly TextBox _detailsTextBox = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Button _cancelButton = new();
    private bool _terminal;
    private readonly Queue<string> _details = new();

    public ExportProgressForm(string title, string exportRoot, string logFile)
    {
        Text = title;
        Width = 620;
        Height = 360;
        MinimumSize = new Size(560, 320);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        TopMost = true;

        var rootLabel = new Label
        {
            AutoSize = false,
            Left = 16,
            Top = 16,
            Width = 560,
            Height = 36,
            Text = $"Export folder: {exportRoot}"
        };

        _statusLabel.AutoSize = false;
        _statusLabel.Left = 16;
        _statusLabel.Top = 60;
        _statusLabel.Width = 450;
        _statusLabel.Height = 24;
        _statusLabel.Text = "Starting export";

        _percentLabel.AutoSize = false;
        _percentLabel.Left = 480;
        _percentLabel.Top = 60;
        _percentLabel.Width = 90;
        _percentLabel.Height = 24;
        _percentLabel.TextAlign = ContentAlignment.MiddleRight;
        _percentLabel.Text = "0%";

        _progressBar.Left = 16;
        _progressBar.Top = 90;
        _progressBar.Width = 560;
        _progressBar.Height = 24;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Style = ProgressBarStyle.Continuous;

        _currentItemLabel.AutoSize = false;
        _currentItemLabel.Left = 16;
        _currentItemLabel.Top = 124;
        _currentItemLabel.Width = 560;
        _currentItemLabel.Height = 40;
        _currentItemLabel.Text = "Current item: -";

        _countsLabel.AutoSize = false;
        _countsLabel.Left = 16;
        _countsLabel.Top = 166;
        _countsLabel.Width = 300;
        _countsLabel.Height = 22;
        _countsLabel.Text = "Items: -";

        _elapsedLabel.AutoSize = false;
        _elapsedLabel.Left = 330;
        _elapsedLabel.Top = 166;
        _elapsedLabel.Width = 246;
        _elapsedLabel.Height = 22;
        _elapsedLabel.TextAlign = ContentAlignment.MiddleRight;
        _elapsedLabel.Text = "Elapsed: 00:00";

        _detailsTextBox.Left = 16;
        _detailsTextBox.Top = 196;
        _detailsTextBox.Width = 560;
        _detailsTextBox.Height = 76;
        _detailsTextBox.Multiline = true;
        _detailsTextBox.ReadOnly = true;
        _detailsTextBox.ScrollBars = ScrollBars.Vertical;
        AddDetail($"Log file: {logFile}");

        _cancelButton.Left = 456;
        _cancelButton.Top = 282;
        _cancelButton.Width = 120;
        _cancelButton.Height = 30;
        _cancelButton.Text = "Cancel";
        _cancelButton.Click += CancelButtonClicked;

        Controls.Add(rootLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_percentLabel);
        Controls.Add(_progressBar);
        Controls.Add(_currentItemLabel);
        Controls.Add(_countsLabel);
        Controls.Add(_elapsedLabel);
        Controls.Add(_detailsTextBox);
        Controls.Add(_cancelButton);

        _timer.Interval = 1000;
        _timer.Tick += (_, _) => UpdateElapsed();
        _timer.Start();
    }

    public event EventHandler? CancelRequested;

    public void UpdateProgress(ExportProgress progress)
    {
        if (progress.Phase == ExportPhase.EnumeratingObjects && progress.TotalItems == 0)
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
            _progressBar.MarqueeAnimationSpeed = 30;
            _percentLabel.Text = "Scanning";
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.MarqueeAnimationSpeed = 0;
            _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, progress.PercentComplete));
            _percentLabel.Text = $"{progress.PercentComplete}%";
        }

        _statusLabel.Text = progress.Message;
        _currentItemLabel.Text = string.IsNullOrWhiteSpace(progress.CurrentItem)
            ? "Current item: -"
            : $"Current item: {progress.CurrentItem}";
        _countsLabel.Text = progress.TotalItems > 0
            ? $"Items: {progress.CompletedItems} / {progress.TotalItems}"
            : progress.CompletedItems > 0
                ? $"Discovered: {progress.CompletedItems}"
                : "Items: -";
        AddDetail(progress.Message);
    }

    public void MarkCompleted(ExportSummary summary)
    {
        _terminal = true;
        _timer.Stop();
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = 100;
        _percentLabel.Text = "100%";
        _statusLabel.Text = "Export finished. Click Close to return to TIA Portal.";
        _currentItemLabel.Text = "Current item: -";
        _countsLabel.Text = $"{summary.SuccessCount} exported, {summary.SkippedCount} skipped, {summary.FailureCount} failed";
        _cancelButton.Text = "Close";
        _cancelButton.Enabled = true;
        _cancelButton.Click -= CancelButtonClicked;
        _cancelButton.Click += CloseButtonClicked;
        AddDetail("Export finished. Click Close to return to TIA Portal.");
        AddDetail(_countsLabel.Text);
        UpdateElapsed();
    }

    public void MarkCanceled()
    {
        MarkTerminal("Export canceled", "The export was canceled between export items.");
    }

    public void MarkFailed(Exception ex)
    {
        MarkTerminal("Export failed", ex.Message);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_terminal)
        {
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            AddDetail("Export is still running. Use Cancel to request cancellation.");
            return;
        }

        base.OnFormClosing(e);
    }

    private void MarkTerminal(string status, string detail)
    {
        _terminal = true;
        _timer.Stop();
        _statusLabel.Text = status;
        _cancelButton.Text = "Close";
        _cancelButton.Enabled = true;
        _cancelButton.Click -= CancelButtonClicked;
        _cancelButton.Click += CloseButtonClicked;
        AddDetail(detail);
        UpdateElapsed();
    }

    private void AddDetail(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_details.Count > 0 && _details.Last() == text)
        {
            return;
        }

        _details.Enqueue(text);
        while (_details.Count > 200)
        {
            _details.Dequeue();
        }

        _detailsTextBox.Lines = _details.ToArray();
        _detailsTextBox.SelectionStart = _detailsTextBox.TextLength;
        _detailsTextBox.ScrollToCaret();
    }

    private void UpdateElapsed()
    {
        _elapsedLabel.Text = $"Elapsed: {_elapsed.Elapsed:mm\\:ss}";
    }

    private void CancelButtonClicked(object? sender, EventArgs e)
    {
        _cancelButton.Enabled = false;
        _statusLabel.Text = "Cancellation requested";
        AddDetail("Cancellation requested. Waiting for the current export item to finish.");
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseButtonClicked(object? sender, EventArgs e)
    {
        Close();
    }
}
