using System.Collections.ObjectModel;
using System.Windows;

using TestConnectorLib.Connector.Interfaces;
using TestConnectorLib.Converter.Interfaces;
using TestConnectorLib.Model;

using TestConnectorUI.Model;
using TestConnectorUI.ViewModels.Base;

namespace TestConnectorUI.ViewModels;
public class ConverterViewModel : BaseViewModel
{
    private decimal _btcCount = 1;
    public decimal BtcCount
    {
        get => _btcCount;
        set
        {
            _ = SetProperty(ref _btcCount, value);
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await UpdateCurrencyConverionAsync();
            });
        }
    }

    private decimal _xrpCount = 15000;
    public decimal XrpCount
    {
        get => _xrpCount;
        set
        {
            _ = SetProperty(ref _xrpCount, value);
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await UpdateCurrencyConverionAsync();
            });
        }
    }

    private decimal _xmrCount = 50;
    public decimal XmrCount
    {
        get => _xmrCount;
        set
        {
            _ = SetProperty(ref _xmrCount, value);
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await UpdateCurrencyConverionAsync();
            });
        }
    }

    private decimal _dashCount = 30;
    public decimal DashCount
    {
        get => _dashCount; set
        {
            _ = SetProperty(ref _dashCount, value);
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await UpdateCurrencyConverionAsync();
            });
        }
    }

    public ObservableCollection<CurrencyConvertedBag> Currencies { get; set; } = [];
    public ObservableCollection<Candle> Candles { get; set; } = null!;
    public ObservableCollection<Trade> Trades { get; set; } = [];

    private readonly ITestConnector _testConnector;
    private readonly ICurrencyConverter _currencyConverter;

    public ConverterViewModel(ITestConnector testConnector, ICurrencyConverter currencyConverter)
    {
        _testConnector = testConnector;
        _currencyConverter = currencyConverter;

        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await UpdateCurrencyConverionAsync();
        });

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

    private async Task UpdateCurrencyConverionAsync()
    {
        Task<CurrencyConvertedBag> btcBagTask = GetBagItemAsync("BTC");
        Task<CurrencyConvertedBag> xmrBagTask = GetBagItemAsync("XMR");
        Task<CurrencyConvertedBag> xrpBagTask = GetBagItemAsync("XRP");
        Task<CurrencyConvertedBag> dashBagTask = GetBagItemAsync("DSH");

        CurrencyConvertedBag[] bags = await Task.WhenAll(btcBagTask, xmrBagTask, xrpBagTask, dashBagTask);

        Application.Current.Dispatcher.Invoke(() =>
        {
            Currencies.Clear();
            foreach (CurrencyConvertedBag? bag in bags)
            {
                Currencies.Add(bag);
            }
        });
    }

    private async Task<CurrencyConvertedBag> GetBagItemAsync(string targetCurrency)
    {
        (Task<decimal> xrpTask, Task<decimal> xmrTask, Task<decimal> dashTask, Task<decimal> btcTask) = (
            _currencyConverter.ConvertAsync("XRP", targetCurrency, XrpCount),
            _currencyConverter.ConvertAsync("XMR", targetCurrency, XmrCount),
            _currencyConverter.ConvertAsync("DSH", targetCurrency, DashCount),
            _currencyConverter.ConvertAsync("BTC", targetCurrency, BtcCount)
        );

        decimal[] results = await Task.WhenAll(xrpTask, xmrTask, dashTask, btcTask);
        (decimal xrp, decimal xmr, decimal dash, decimal btc) = (results[0], results[1], results[2], results[3]);

        decimal total = xmr + xrp + btc + dash;

        var btcBag = new CurrencyConvertedBag()
        {
            CurrencyType = targetCurrency,
            Total = total,
            Btc = btc,
            Dash = dash,
            Xmr = xmr,
            Xrp = xrp,
        };

        return btcBag;
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
