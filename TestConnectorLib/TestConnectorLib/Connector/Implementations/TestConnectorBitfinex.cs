using System.Text.Json;
using System.Text.Json.Nodes;

using RestSharp;

using TestConnectorLib.Connector.Interfaces;
using TestConnectorLib.Exceptions;
using TestConnectorLib.Model;

namespace TestConnectorLib.Connector.Implementations;

public class TestConnectorBitfinex : ITestConnector
{
    public event Action<Trade>? NewBuyTrade;
    public event Action<Trade>? NewSellTrade;
    public event Action<Candle>? CandleSeriesProcessing;

    private readonly Dictionary<int, string> _availablePeriods = new()
    {
        { 1 * 60, "1m" },
        { 5 * 60, "5m" },
        { 15 * 60, "15m" },
        { 30 * 60, "30m" },
        { 1 * 60 * 60, "1h" },
        { 3 * 60 * 60, "3h" },
        { 6 * 60 * 60, "6h" },
        { 12 * 60 * 60, "12h" },
        { 1 * 24 * 60 * 60, "1D" },
        { 1 * 7 * 24 * 60 * 60, "1W" },
        { 2 * 7 * 24 * 60 * 60, "14D" },
        { 30 * 24 * 60 * 60, "1M" }
    };

    #region REST

    public async Task<IEnumerable<Candle>> GetCandleSeriesAsync(string pair, int periodInSec, DateTimeOffset? from,
        DateTimeOffset? to = null, long? count = 0)
    {
        using var client = new RestClient("https://api-pub.bitfinex.com/v2");
        if (!_availablePeriods.TryGetValue(periodInSec, out string? period))
        {
            throw new ArgumentException($"Invalid period passed ({periodInSec})");
        }

        RestRequest request = new RestRequest($"candles/trade:{period}:{pair}/hist", Method.Get)
            .AddHeader("Accept", "application/json");

        if (from.HasValue)
        {
            request.AddQueryParameter("start", from.Value.ToUnixTimeMilliseconds());
        }

        if (to.HasValue)
        {
            request.AddQueryParameter("end", to.Value.ToUnixTimeMilliseconds());
        }

        if (count.HasValue)
        {
            request.AddQueryParameter("limit", count.Value);
        }

        RestResponse response = await client.ExecuteAsync(request);

        if (response.IsSuccessStatusCode)
        {
            string json = response.Content!;

            List<JsonArray> data = JsonSerializer.Deserialize<List<JsonArray>>(json)!;
            List<Candle> res = new(data.Count);

            foreach (JsonArray item in data)
            {
                int openPrice = item[1]!.GetValue<int>();
                int closePrice = item[2]!.GetValue<int>();
                int highPrice = item[3]!.GetValue<int>();
                int lowPrice = item[4]!.GetValue<int>();
                decimal volume = item[5]!.GetValue<decimal>();

                var candle = new Candle()
                {
                    OpenPrice = openPrice,
                    ClosePrice = closePrice,
                    HighPrice = highPrice,
                    LowPrice = lowPrice,
                    OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(item[0]!.GetValue<long>()),
                    Pair = pair,
                    TotalVolume = volume,
                    // OCHL average
                    TotalPrice = volume * (openPrice + closePrice + highPrice + lowPrice) / 4,
                };

                res.Add(candle);

                return res;
            }
        }

        throw new RequestFailedException($"Failed with {response.StatusCode} {response.ErrorMessage} {response.ErrorException}");
    }

    public async Task<IEnumerable<Trade>> GetNewTradesAsync(string pair, int maxCount)
    {
        using var client = new RestClient("https://api-pub.bitfinex.com/v2");
        RestRequest request = new RestRequest($"trades/{pair}/hist", Method.Get)
            .AddHeader("Accept", "application/json")
            .AddQueryParameter("limit", maxCount);

        RestResponse response = await client.ExecuteAsync(request);

        if (response.IsSuccessStatusCode)
        {
            string json = response.Content!;

            List<JsonArray> data = JsonSerializer.Deserialize<List<JsonArray>>(json)!;
            List<Trade> res = new(data.Count);

            foreach (JsonArray item in data)
            {
                long id = item[0]!.GetValue<long>();
                long mts = item[1]!.GetValue<long>();
                double amount = item[2]!.GetValue<double>();
                double price = item[3]!.GetValue<double>();
                string side = amount >= 0 ? "BUY" : "SELL";

                var trade = new Trade()
                {
                    Amount = new decimal(amount),
                    Id = id.ToString(),
                    Pair = pair,
                    Price = new decimal(price),
                    Side = side,
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(mts),
                };

                res.Add(trade);

                return res;
            }
        }

        throw new RequestFailedException($"Failed with {response.StatusCode} {response.ErrorMessage} {response.ErrorException}");
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
