using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using VoidNote.Audio.Waveforms;

namespace VoidNote.App.Controls;

/// <summary>Efficiently renders cached min/max peaks and exposes pointer selection/seek values.</summary>
public sealed class WaveformControl : Control
{
    public static readonly StyledProperty<WaveformData?> DataProperty = AvaloniaProperty.Register<WaveformControl, WaveformData?>(nameof(Data));
    public static readonly StyledProperty<double> PlayheadSecondsProperty = AvaloniaProperty.Register<WaveformControl, double>(nameof(PlayheadSeconds), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> SelectionStartSecondsProperty = AvaloniaProperty.Register<WaveformControl, double>(nameof(SelectionStartSeconds), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> SelectionEndSecondsProperty = AvaloniaProperty.Register<WaveformControl, double>(nameof(SelectionEndSeconds), defaultBindingMode: BindingMode.TwoWay);
    private double _dragStart; private bool _dragging;

    static WaveformControl() => AffectsRender<WaveformControl>(DataProperty, PlayheadSecondsProperty, SelectionStartSecondsProperty, SelectionEndSecondsProperty);
    public WaveformData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public double PlayheadSeconds { get => GetValue(PlayheadSecondsProperty); set => SetValue(PlayheadSecondsProperty, value); }
    public double SelectionStartSeconds { get => GetValue(SelectionStartSecondsProperty); set => SetValue(SelectionStartSecondsProperty, value); }
    public double SelectionEndSeconds { get => GetValue(SelectionEndSecondsProperty); set => SetValue(SelectionEndSecondsProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context); context.DrawRectangle(new SolidColorBrush(Color.Parse("#151821")), null, Bounds);
        var data = Data; if (data is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var duration = data.TotalFrames / (double)data.SampleRate; var desired = Math.Max(1, (int)Bounds.Width); var level = data.SelectLevel(desired);
        var selectionStart = X(Math.Min(SelectionStartSeconds, SelectionEndSeconds), duration); var selectionEnd = X(Math.Max(SelectionStartSeconds, SelectionEndSeconds), duration);
        if (selectionEnd > selectionStart) context.DrawRectangle(new SolidColorBrush(Color.Parse("#334F86C6")), null, new Rect(selectionStart, 0, selectionEnd - selectionStart, Bounds.Height));
        var peakPen = new Pen(new SolidColorBrush(Color.Parse("#66D9EF")), 1); var channels = Math.Max(1, level.ChannelCount); var channelHeight = Bounds.Height / channels;
        for (var x = 0; x < (int)Bounds.Width; x++)
        {
            var frame = Math.Min(level.PeakFrameCount - 1, (int)(x / Bounds.Width * level.PeakFrameCount));
            for (var channel = 0; channel < channels; channel++)
            {
                var peak = level.Peaks[frame * channels + channel]; var center = channelHeight * (channel + 0.5);
                context.DrawLine(peakPen, new Point(x, center - peak.Maximum * channelHeight * 0.45), new Point(x, center - peak.Minimum * channelHeight * 0.45));
            }
        }
        var playhead = X(PlayheadSeconds, duration); context.DrawLine(new Pen(Brushes.OrangeRed, 2), new Point(playhead, 0), new Point(playhead, Bounds.Height));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); _dragStart = Seconds(e.GetPosition(this).X); _dragging = true; SelectionStartSeconds = _dragStart; SelectionEndSeconds = _dragStart; PlayheadSeconds = _dragStart; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (!_dragging) return; SelectionStartSeconds = _dragStart; SelectionEndSeconds = Seconds(e.GetPosition(this).X); PlayheadSeconds = SelectionEndSeconds; e.Handled = true; }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); _dragging = false; e.Pointer.Capture(null); e.Handled = true; }
    private double Seconds(double x) { var data = Data; return data is null ? 0 : Math.Clamp(x / Math.Max(1, Bounds.Width) * data.TotalFrames / data.SampleRate, 0, data.TotalFrames / (double)data.SampleRate); }
    private double X(double seconds, double duration) => duration <= 0 ? 0 : Math.Clamp(seconds / duration * Bounds.Width, 0, Bounds.Width);
}
