using TestConnectorLib.Connector.Interfaces;
using TestConnectorLib.Model;

namespace TestConnectorLib.Connector.Implementations;

public class TestConnector : ITestConnector
{
    public event Action<Trade>? NewBuyTrade;
    public event Action<Trade>? NewSellTrade;
    public event Action<Candle>? CandleSeriesProcessing;

    #region REST

    public Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from,
        DateTimeOffset? to = null, long? count = 0)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region WebSocket

    public void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null,
        long? count = 0)
    {
        throw new NotImplementedException();
    }

    public void SubscribeTrades(string pair, int maxCount = 100)
    {
        throw new NotImplementedException();
    }

    public void UnsubscribeCandles(string pair)
    {
        throw new NotImplementedException();
    }

    public void UnsubscribeTrades(string pair)
    {
        throw new NotImplementedException();
    }

    #endregion
}
