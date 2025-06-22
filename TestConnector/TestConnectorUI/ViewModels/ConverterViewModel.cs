using System.Collections.ObjectModel;
using System.Windows;

using TestConnectorLib.Connector.Interfaces;
using TestConnectorLib.Model;

using TestConnectorUI.Model;
using TestConnectorUI.ViewModels.Base;

namespace TestConnectorUI.ViewModels;
public class ConverterViewModel : BaseViewModel
{
    public decimal BtcCount { get; set; } = 1;
    public decimal XrpCount { get; set; } = 15000;
    public decimal XmrCount { get; set; } = 50;
    public decimal DashCount { get; set; } = 30;

    public decimal TotalBtc { get; set; }
    public decimal TotalXrp { get; set; }
    public decimal TotalXmr { get; set; }
    public decimal TotalDash { get; set; }

    public ObservableCollection<CurrencyConvertedBag> Currencies { get; set; } = [];
    public ObservableCollection<Candle> Candles { get; set; } = null!;
    public ObservableCollection<Trade> Trades { get; set; } = [];

    private readonly ITestConnector _testConnector;

    public ConverterViewModel(ITestConnector testConnector)
    {
        _testConnector = testConnector;

        _ = Task.Run(async () =>
        {
            IEnumerable<Candle> candleSeries = await _testConnector.GetCandleSeriesAsync("tBTCUSD", 60, null);
            Candles = new ObservableCollection<Candle>([.. candleSeries]);
        });

        _testConnector.NewBuyTrade += UpdateTrades;
        _testConnector.NewSellTrade += UpdateTrades;
        _testConnector.SubscribeTrades("tBTCUSD");
    }

    private void UpdateTrades(Trade trade)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Trades.Add(trade);
        });
    }

    private bool _isDisposed = false;
    public override void Dispose()
    {
        if (!_isDisposed)
        {
            base.Dispose();

            GC.SuppressFinalize(this);

            _testConnector.NewBuyTrade -= UpdateTrades;
            _testConnector.NewSellTrade -= UpdateTrades;
            _testConnector.UnsubscribeTrades("tBTCUSD");

            _isDisposed = true;
        }
    }
}
