using System.Windows;

using TestConnectorLib.Connector.Implementations;

namespace TestConnectorUI;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var t = new TestConnectorBitfinex();
        t.SubscribeTrades("tBTCUSD", 110);
    }
}