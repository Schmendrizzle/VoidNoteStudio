using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using VoidNote.App.ViewModels;

namespace VoidNote.App.Views;

/// <summary>The shell window; all application behavior is delegated to its view model and services.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) => await ViewModel.InitializeAsync();
    }

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

    private async void Arm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!ViewModel.DisclaimerAcknowledged)
        {
            var dialog = new Window { Title = "Third-party software notice", Width = 560, Height = 250, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var accept = new Button { Content = "I understand and want to arm", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            accept.Click += (_, _) => dialog.Close(true);
            dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 18, Children = { new TextBlock { Text = "Using external software with Warframe is at your own risk. VoidNote is independent and is not affiliated with or endorsed by Digital Extremes. This is not a statement that use is safe, approved, or free of account risk.", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, accept } };
            if (await dialog.ShowDialog<bool>(this) is false) return;
        }
        try { await ViewModel.ArmAsync(true); } catch (Exception exception) { await ShowErrorAsync(exception.Message); }
    }

    private async void Disarm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DisarmAsync();
    private async void DryRun_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DryRunAsync();
    private async void TestInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.TestInputAsync();
    private async void SaveProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.SaveProfileAsync();
    private async void DuplicateProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DuplicateProfileAsync();
    private async void DeleteProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DeleteProfileAsync();
    private async void StartIngame_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.StartIngameAsync();
    private async void EmergencyStop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.EmergencyStopAsync();

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window { Title = "GameBridge", Width = 480, Height = 180, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var close = new Button { Content = "Close", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 16, Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, close } };
        await dialog.ShowDialog(this);
    }
}
