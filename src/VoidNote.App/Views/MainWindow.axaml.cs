using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using VoidNote.App.ViewModels;
using Avalonia.Input;

namespace VoidNote.App.Views;

/// <summary>The shell window; all application behavior is delegated to its view model and services.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) => await ViewModel.InitializeAsync();
        KeyDown += MainWindow_KeyDown;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void NewProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.NewProject();
    private void RenameProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.RenameProject();
    private void Undo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Undo();
    private void Redo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Redo();

    private async void OpenProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var startLocation = await StorageProvider.TryGetFolderFromPathAsync(ViewModel.ProjectDialogDirectory);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open VoidNote project", AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter = [new FilePickerFileType("VoidNote project") { Patterns = ["*.vns"] }],
        });
        if (files.Count == 1) try { await ViewModel.OpenProjectAsync(files[0].Path.LocalPath); } catch (Exception exception) { await ShowErrorAsync("The project could not be opened.", exception); }
    }

    private async void SaveProject_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await SaveProjectAsync();

    private async Task SaveProjectAsync()
    {
        try
        {
            if (Path.GetExtension(ViewModel.ProjectPath).Equals(".vns", StringComparison.OrdinalIgnoreCase)) { await ViewModel.SaveProjectAsync(); return; }
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save VoidNote project", SuggestedFileName = ViewModel.SuggestedProjectFileName, DefaultExtension = "vns",
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(ViewModel.ProjectDialogDirectory),
                FileTypeChoices = [new FilePickerFileType("VoidNote project") { Patterns = ["*.vns"] }],
            });
            if (file is not null) await ViewModel.SaveProjectAsync(file.Path.LocalPath);
        }
        catch (Exception exception) { await ShowErrorAsync("The project could not be saved. The existing file was not replaced.", exception); }
    }

    private async void OpenRecent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { try { await ViewModel.OpenSelectedRecentProjectAsync(); } catch (Exception exception) { await ShowErrorAsync("The recent project could not be opened.", exception); } }
    private async void Recover_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { try { await ViewModel.RecoverAsync(); } catch (Exception exception) { await ShowErrorAsync("The recovery snapshot could not be opened.", exception); } }
    private async void DiscardRecovery_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DiscardRecoveryAsync();
    private async void SaveSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.SaveSettingsAsync();
    private async void Diagnostics_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.RunDiagnosticsAsync();
    private async void ExportDiagnostics_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export VoidNote Diagnostics", SuggestedFileName = "voidnote-diagnostics.json", DefaultExtension = "json", FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }] });
        if (file is not null) await File.WriteAllTextAsync(file.Path.LocalPath, await ViewModel.ExportDiagnosticsJsonAsync());
    }
    private void ValidateCode_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.ValidateShawzinCode();
    private void GenerateMapping_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.GenerateMappingSequence();
    private async void ConfirmMapping_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.SaveMappingValidationAsync(true);
    private async void SaveUnconfirmedMapping_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.SaveMappingValidationAsync(false);

    private async void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.S) { e.Handled = true; await SaveProjectAsync(); }
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.O) { e.Handled = true; OpenProject_Click(sender, new Avalonia.Interactivity.RoutedEventArgs()); }
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Z) { e.Handled = true; ViewModel.Undo(); }
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Y) { e.Handled = true; ViewModel.Redo(); }
    }

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
    private void SplitEnsemble_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.SplitEnsemble();

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
        try { await ViewModel.ArmAsync(true); } catch (Exception exception) { await ShowErrorAsync("GameBridge could not be armed.", exception); }
    }

    private async void Disarm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DisarmAsync();
    private async void DryRun_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DryRunAsync();
    private async void TestInput_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.TestInputAsync();
    private async void SaveProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.SaveProfileAsync();
    private async void DuplicateProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DuplicateProfileAsync();
    private async void DeleteProfile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.DeleteProfileAsync();
    private async void StartIngame_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.StartIngameAsync();
    private async void StartDelay_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0)
        {
            ViewModel.SelectedStartDelayIndex = comboBox.SelectedIndex;
            if (ViewModel.IsInitialized)
            {
                try { await ViewModel.SaveStartDelayAsync(); }
                catch (Exception exception) { await ShowErrorAsync("The GameBridge start delay could not be saved.", exception); }
            }
        }
    }
    private async void EmergencyStop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await ViewModel.EmergencyStopAsync();

    private async Task ShowErrorAsync(string message, Exception exception)
    {
        var details = exception.ToString();
        var dialog = new Window { Title = "VoidNote Studio", Width = 600, Height = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var close = new Button { Content = "Close", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var copy = new Button { Content = "Copy Details", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        copy.Click += async (_, _) => { if (Clipboard is not null) await Clipboard.SetTextAsync(details); };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 16, Children = { new TextBlock { Text = message, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, new TextBlock { Text = exception.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, new Expander { Header = "Technical details", Content = new TextBox { Text = details, IsReadOnly = true, AcceptsReturn = true, MaxHeight = 130 } }, new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Children = { copy, close } } } };
        await dialog.ShowDialog(this);
    }
}
