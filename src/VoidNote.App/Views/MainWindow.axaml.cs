using Avalonia.Controls;

namespace VoidNote.App.Views;

/// <summary>The shell window; all application behavior is delegated to its view model and services.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the main window.</summary>
    public MainWindow() => InitializeComponent();
}
