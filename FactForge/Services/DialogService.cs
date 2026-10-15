using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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