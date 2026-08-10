using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VoidNote.App.ViewModels;

namespace VoidNote.App.Views;

public sealed partial class AudioLabView : UserControl
{
    private readonly Avalonia.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    public AudioLabView() { InitializeComponent(); _timer.Tick += (_, _) => ViewModel?.RefreshPlaybackPosition(); AttachedToVisualTree += (_, _) => _timer.Start(); DetachedFromVisualTree += (_, _) => _timer.Stop(); }
    private AudioLabViewModel? ViewModel => DataContext as AudioLabViewModel;
    private async void ImportAudio_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this); if (top is null || ViewModel is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Import audio", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("Audio") { Patterns = ["*.wav", "*.flac", "*.mp3"] }] });
        if (files.Count == 1) await ViewModel.ImportAsync(files[0].Path.LocalPath);
    }
    private void ZoomIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.ZoomIn();
    private void ZoomOut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.ZoomOut();
    private async void ClearCache_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.ClearCacheAsync(); }
    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.CancelOperation();
    private async void Play_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.StartPlaybackAsync(); }
    private async void Pause_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.PauseAsync(); }
    private async void Stop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.StopAsync(); }
    private async void Seek_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.SeekAsync(); }
    private void RemoveTrack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if ((sender as Control)?.DataContext is AudioTrackRowViewModel track) track.Remove(); }
    private async void DiscoverEngines_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.DiscoverEnginesAsync(); }
    private async void Separate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.SeparateAsync(); }
    private async void Transcribe_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.TranscribeAsync(); }
    private void Original_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.CompareOriginal();
    private void Stem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.CompareStem();
    private async void StemMix_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.PlayStemMixAsync(); }
    private void RemoveStemSet_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.RemoveSelectedStemSet();
}
