using System.Windows;

namespace Knode;

public partial class CorpusHelpWindow : Window
{
    public CorpusHelpWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
