using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FactForge.ViewModels.Presentation;
using FactForge.Views;
using FactForge.Views.Dialogs;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
    Task<AppMenuResult> ShowMenuAsync();
    Task<string?> ShowCreateQuizAsync();
    Task<string?> ShowRenameQuizAsync(string currentTitle);
    Task ShowRejoinQrAsync(PresentViewModel viewModel);
    Task<string?> ShowImportQuizFileAsync();
    Task<string?> ShowExportQuizFileAsync(string suggestedFileName);
}

public class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = GetMainWindow();
        if (owner is null) return false;

        var dialog = new ConfirmDialog(title, message);
        SizeToOwner(dialog, owner);

        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<AppMenuResult> ShowMenuAsync()
    {
        var owner = GetMainWindow();
        if (owner is null) return AppMenuResult.None;

        var dialog = new AppMenuDialog();
        SizeToOwner(dialog, owner);

        return await dialog.ShowDialog<AppMenuResult>(owner);
    }

    public async Task<string?> ShowCreateQuizAsync()
    {
        var owner = GetMainWindow();
        if (owner is null) return null;

        var dialog = new CreateQuizDialog();
        SizeToOwner(dialog, owner);

        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task<string?> ShowRenameQuizAsync(string currentTitle)
    {
        var owner = GetMainWindow();
        if (owner is null) return null;

        var dialog = new RenameQuizDialog(currentTitle);
        SizeToOwner(dialog, owner);

        return await dialog.ShowDialog<string?>(owner);
    }

    public async Task ShowRejoinQrAsync(PresentViewModel viewModel)
    {
        var owner = GetMainWindow();
        if (owner is null) return;

        var dialog = new RejoinQrDialog(viewModel);
        SizeToOwner(dialog, owner);

        await dialog.ShowDialog(owner);
    }

    public async Task<string?> ShowImportQuizFileAsync()
    {
        var owner = GetMainWindow();
        if (owner is null) return null;

        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Import quiz",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("FactForge quiz archive")
                    {
                        Patterns = new[] { "*.zip" }
                    }
                }
            });

        return files.FirstOrDefault()?.Path.LocalPath;
    }

    public async Task<string?> ShowExportQuizFileAsync(string suggestedFileName)
    {
        var owner = GetMainWindow();
        if (owner is null) return null;

        var file = await owner.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export quiz",
                SuggestedFileName = $"{suggestedFileName}.zip",
                DefaultExtension = "zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("ZIP archive")
                    {
                        Patterns = new[] { "*.zip" }
                    }
                }
            });

        return file?.Path.LocalPath;
    }

    private static Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }

    private static void SizeToOwner(Window dialog, Window owner)
    {
        var frameSize = owner.FrameSize ?? owner.Bounds.Size;
        dialog.Width = frameSize.Width;
        dialog.Height = frameSize.Height;
        dialog.Position = owner.Position;
    }
}