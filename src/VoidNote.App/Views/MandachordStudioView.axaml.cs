using Avalonia.Controls;
using Avalonia.Platform.Storage;
using VoidNote.App.ViewModels;
using VoidNote.Domain.Mandachord;

namespace VoidNote.App.Views;

public partial class MandachordStudioView : UserControl
{
    public MandachordStudioView() => InitializeComponent();
    private MandachordStudioViewModel? ViewModel => DataContext as MandachordStudioViewModel;
    private void Generate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.Generate();
    private void Accept_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.Accept();
    private void Preview_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.RenderPreview();
    private void Clear_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.Clear();
    private void Reset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.Reset();
    private void SetKick_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is { } vm) vm.AddStep(MandachordLayer.Percussion, vm.EditStep, percussion: MandachordPercussionCategory.Kick); }
    private void SetBass_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is { } vm) vm.AddStep(MandachordLayer.Bass, vm.EditStep, vm.EditPitch); }
    private void SetMelody_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if (ViewModel is { } vm) vm.AddStep(MandachordLayer.Melody, vm.EditStep, vm.EditPitch); }
    private void DeleteDrums_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.DeleteCell(MandachordLayer.Percussion);
    private void DeleteBass_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.DeleteCell(MandachordLayer.Bass);
    private void DeleteMelody_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel?.DeleteCell(MandachordLayer.Melody);
    private async void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null || TopLevel.GetTopLevel(this) is not { } top) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new() { SuggestedFileName = "mandachord-preview.wav", FileTypeChoices = [new("WAV") { Patterns = ["*.wav"] }] });
        if (file is not null) await ViewModel.SavePreviewAsync(file.Path.LocalPath);
    }
}
