using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using FactForge.Views;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}

public class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null) return false;

        var dialog = new ConfirmDialog(title, message)
        {
            Width = owner.Bounds.Width,
            Height = owner.Bounds.Height,
            Position = owner.Position
        };

        return await dialog.ShowDialog<bool>(owner);
    }
}