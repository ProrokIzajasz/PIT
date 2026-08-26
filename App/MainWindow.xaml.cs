using System.Windows;
using PIT.App.ViewModels;

namespace PIT.App;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void SchemePaletteItem_PreviewMouseMove(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.Tag is not string blockKey)
        {
            return;
        }

        System.Windows.DragDrop.DoDragDrop(
            element,
            blockKey,
            System.Windows.DragDropEffects.Copy);
    }

    private void SchemeBlocks_DragOver(
        object sender,
        System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(string))
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;

        e.Handled = true;
    }

    private void SchemeBlocks_Drop(
        object sender,
        System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(string)))
        {
            return;
        }

        var blockKey = e.Data.GetData(typeof(string)) as string;

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.AddSchemeBlockFromPalette(blockKey);
        }

        e.Handled = true;
    }
}