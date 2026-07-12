using TiHiY.StreamControlCenter.Models;
using TiHiY.StreamControlCenter.UI;

namespace TiHiY.StreamControlCenter.Services;

public sealed class OverlayController : IDisposable
{
    private OverlayForm? _overlay;
    private bool _disposed;

    public bool IsVisible => _overlay is { IsDisposed: false, Visible: true };

    public event EventHandler? VisibilityChanged;

    public void Show(AppSettings settings)
    {
        ThrowIfDisposed();

        if (_overlay is null || _overlay.IsDisposed)
        {
            _overlay = new OverlayForm();
            _overlay.FormClosed += OverlayOnFormClosed;
        }

        ApplySettings(settings);

        if (!_overlay.Visible)
        {
            _overlay.Show();
        }

        _overlay.BringToFrontWithoutActivation();
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Hide()
    {
        if (_overlay is { IsDisposed: false })
        {
            _overlay.Hide();
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Close()
    {
        if (_overlay is { IsDisposed: false })
        {
            _overlay.FormClosed -= OverlayOnFormClosed;
            _overlay.Close();
            _overlay.Dispose();
        }

        _overlay = null;
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplySettings(AppSettings settings)
    {
        if (_overlay is null || _overlay.IsDisposed)
        {
            return;
        }

        _overlay.TopMost = settings.OverlayTopMost;
        _overlay.SetBackgroundOpacity(settings.OverlayBackgroundOpacity);
        _overlay.SetClickThrough(settings.OverlayClickThrough);
        _overlay.SetStatistics(settings.ViewerCount, settings.LikeCount);

        var width = Math.Clamp(settings.OverlayWidth, 320, 1600);
        var height = Math.Clamp(settings.OverlayHeight, 300, 1400);
        var bounds = new Rectangle(settings.OverlayX, settings.OverlayY, width, height);

        if (_overlay.Bounds != bounds)
        {
            _overlay.Bounds = bounds;
        }
    }

    public void AddMessage(ChatMessage message)
    {
        if (_overlay is { IsDisposed: false })
        {
            _overlay.AddMessage(message);
        }
    }

    public Rectangle? GetBounds()
    {
        return _overlay is { IsDisposed: false } ? _overlay.Bounds : null;
    }

    private void OverlayOnFormClosed(object? sender, FormClosedEventArgs e)
    {
        if (_overlay is not null)
        {
            _overlay.FormClosed -= OverlayOnFormClosed;
            _overlay.Dispose();
            _overlay = null;
        }

        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Close();
        GC.SuppressFinalize(this);
    }
}
