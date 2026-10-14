using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FactForge.ViewModels;
using FactForge.ViewModels.Presentation;
using FactForge.Views;

namespace FactForge;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        Type? type;

        if (param is PresentViewModel)
        {
            type = typeof(PresentView);
        }
        else
        {
            var name = param.GetType().FullName!
                .Replace("ViewModel", "View", StringComparison.Ordinal);

            type = Type.GetType(name);
        }

        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        var expectedName = param is PresentViewModel
            ? typeof(PresentView).FullName
            : param.GetType().FullName!
                .Replace("ViewModel", "View", StringComparison.Ordinal);

        return new TextBlock { Text = "Not Found: " + expectedName };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}