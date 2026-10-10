using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Uttu.ViewModels;

namespace Uttu;

public partial class MainWindow : Window
{
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();

        Application.Current.Exit += (s, e) => _isExiting = true;

        Loaded += async (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.RequestShowWindow = ShowAndActivate;
                await vm.InitializeAsync();
            }
        };

        // Atalhos de teclado globais da janela
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Ctrl+Enter: Aprova o primeiro rascunho visível
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            var firstDraft = vm.Drafts.FirstOrDefault();
            if (firstDraft != null)
            {
                e.Handled = true;
                vm.ApproveDraftCommand.Execute(firstDraft);
            }
        }
    }

    private void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isExiting)
        {
            base.OnClosing(e);
            return;
        }

        // Ao fechar pelo 'X', oculta na bandeja do sistema (System Tray) para manter a rotina matinal ativa
        if (DataContext is MainViewModel vm && vm.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        MyTaskbarIcon?.Dispose();
        base.OnClosed(e);
    }
}