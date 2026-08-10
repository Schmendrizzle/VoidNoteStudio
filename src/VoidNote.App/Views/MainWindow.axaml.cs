using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using VoidNote.App.ViewModels;

namespace VoidNote.App.Views;

/// <summary>The shell window; all application behavior is delegated to its view model and services.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the main window.</summary>
    public MainWindow() => InitializeComponent();

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OpenMidi_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open MIDI file",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("MIDI") { Patterns = ["*.mid", "*.midi"] }],
        });
        if (files.Count == 1) await ViewModel.LoadMidiFileAsync(files[0].Path.LocalPath);
    }

    private void Analyze_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Analyze();
    private void Arrange_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Arrange();

    private async void SavePreview_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Shawzin preview",
            SuggestedFileName = "shawzin-preview.wav",
            DefaultExtension = "wav",
            FileTypeChoices = [new FilePickerFileType("Wave audio") { Patterns = ["*.wav"] }],
        });
        if (file is not null) await ViewModel.SavePreviewAsync(file.Path.LocalPath);
    }

    private async void CopyCode_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Clipboard is not null && !string.IsNullOrEmpty(ViewModel.SongCode))
            await Clipboard.SetTextAsync(ViewModel.SongCode);
    }
}
