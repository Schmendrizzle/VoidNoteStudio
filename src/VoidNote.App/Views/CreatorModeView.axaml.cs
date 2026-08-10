using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Input.Platform;
using VoidNote.App.ViewModels;

namespace VoidNote.App.Views;

public sealed partial class CreatorModeView : UserControl
{
    public CreatorModeView() => InitializeComponent();
    private CreatorModeViewModel ViewModel => (CreatorModeViewModel)DataContext!;
    private void CreateSession_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.CreateSession();
    private void DryRun_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.DryRun();
    private void Complete_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.MarkComplete();
    private async void StartTake_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.StartTakeAsync();
    private async void EmergencyStop_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.EmergencyStopAsync();
    private void NeedsRetake_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.MarkNeedsRetake();
    private void Retake_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.CreateRetake();
    private async void CopyCode_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    { var clipboard = TopLevel.GetTopLevel(this)?.Clipboard; if (clipboard is not null && ViewModel.SelectedSongCode.Length > 0) await clipboard.SetTextAsync(ViewModel.SelectedSongCode); }
    private async void ExportJson_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => await SaveTextAsync("creator-sync.json", "JSON", "*.json", ViewModel.ExportJson());
    private async void ExportCsv_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e) => await SaveTextAsync("creator-markers.csv", "CSV", "*.csv", ViewModel.ExportCsv());
    private async void ExportWave_Click(object? s, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await PickAsync("creator-sync.wav", "Wave audio", "*.wav"); if (file is not null) await File.WriteAllBytesAsync(file.Path.LocalPath, ViewModel.ExportWave());
    }
    private async Task SaveTextAsync(string name, string label, string pattern, string content) { var file = await PickAsync(name, label, pattern); if (file is not null) await File.WriteAllTextAsync(file.Path.LocalPath, content); }
    private async Task<IStorageFile?> PickAsync(string name, string label, string pattern)
    {
        var provider = TopLevel.GetTopLevel(this)?.StorageProvider; if (provider is null) return null;
        return await provider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = name, FileTypeChoices = [new(label) { Patterns = [pattern] }] });
    }
}
