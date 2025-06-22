using System.Net.WebSockets;
using System.Text;
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

    private static readonly Dictionary<int, string> _availablePeriods = new()
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
                Candle candle = GetCandleFromJson(item, pair);

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
                Trade trade = GetTradeFromJson(item, pair);

                res.Add(trade);

                return res;
            }
        }

        throw new RequestFailedException($"Failed with {response.StatusCode} {response.ErrorMessage} {response.ErrorException}");
    }

    #endregion

    #region WebSocket

    private readonly Dictionary<string, ClientWebSocket> _subscribedCandlePairs = [];

    // async void is unsafe. Interface should be updated to async Task
    public async void SubscribeCandles(string pair, int periodInSec, DateTimeOffset? from = null, DateTimeOffset? to = null,
        long? count = 0)
    {
        if (_subscribedCandlePairs.ContainsKey(pair))
        {
            return;
        }

        if (!_availablePeriods.TryGetValue(periodInSec, out string? period))
        {
            throw new ArgumentException($"Invalid period passed ({periodInSec})");
        }

        var message = new
        {
            @event = "subscribe",
            channel = "candles",
            key = $"trade:{period}:{pair}",
        };
        var wsUri = new Uri("wss://api-pub.bitfinex.com/ws/2");

        var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(wsUri, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            ws.Dispose();
            return;
        }

        string jsonMessage = JsonSerializer.Serialize(message);
        byte[] sendBuffer = Encoding.UTF8.GetBytes(jsonMessage);
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true,
    CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            ws.Dispose();
            return;
        }

        byte[] buffer = new byte[1024];
        var messageBuilder = new StringBuilder();

        _ = Task.Run(async () =>
        {
            _subscribedCandlePairs.Add(pair, ws);
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(chunk);

                    if (result.EndOfMessage)
                    {
                        string fullMessage = messageBuilder.ToString();
                        messageBuilder.Clear();

                        JsonNode node = JsonSerializer.Deserialize<JsonNode>(fullMessage)!;

                        if (node is JsonArray response)
                        {
                            if (response.Count >= 2 && response[1] is JsonArray candlesArray)
                            {
                                foreach (JsonNode? candleNode in candlesArray)
                                {
                                    if (candleNode is JsonArray candleItem)
                                    {
                                        Candle candle = GetCandleFromJson(candleItem, pair);

                                        CandleSeriesProcessing?.Invoke(candle);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    _subscribedCandlePairs.Remove(pair);
                }
                catch (InvalidOperationException)
                {
                    ws.Dispose();
                    _subscribedCandlePairs.Remove(pair);
                }
            }
        });
    }

    public void UnsubscribeCandles(string pair)
    {
        if (_subscribedCandlePairs.TryGetValue(pair, out ClientWebSocket? ws))
        {
            ws.Dispose();
            _subscribedCandlePairs.Remove(pair);
        }
    }

    private readonly Dictionary<string, ClientWebSocket> _subscribedTradePairs = [];
    public async void SubscribeTrades(string pair, int maxCount = 100)
    {
        if (_subscribedTradePairs.ContainsKey(pair))
        {
            return;
        }

        var message = new
        {
            @event = "subscribe",
            channel = "trades",
            symbol = pair,
        };
        var wsUri = new Uri("wss://api-pub.bitfinex.com/ws/2");

        var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(wsUri, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            ws.Dispose();
            return;
        }

        string jsonMessage = JsonSerializer.Serialize(message);
        byte[] sendBuffer = Encoding.UTF8.GetBytes(jsonMessage);
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true,
    CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            ws.Dispose();
            return;
        }

        byte[] buffer = new byte[1024];
        var messageBuilder = new StringBuilder();

        _ = Task.Run(async () =>
        {
            _subscribedTradePairs.Add(pair, ws);
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(chunk);

                    if (result.EndOfMessage)
                    {
                        string fullMessage = messageBuilder.ToString();
                        messageBuilder.Clear();

                        JsonNode node = JsonSerializer.Deserialize<JsonNode>(fullMessage)!;

                        if (node is JsonArray response)
                        {
                            if (response.Count >= 2 && response[1] is JsonArray tradesArray)
                            {
                                foreach (JsonNode? tradeNode in tradesArray)
                                {
                                    if (tradeNode is JsonArray tradeItem)
                                    {
                                        Trade trade = GetTradeFromJson(tradeItem, pair);
                                        if (trade.Side == BuySide)
                                        {
                                            NewBuyTrade?.Invoke(trade);
                                        }
                                        else if (trade.Side == SellSide)
                                        {
                                            NewSellTrade?.Invoke(trade);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    _subscribedTradePairs.Remove(pair);
                }
                catch (InvalidOperationException)
                {
                    ws.Dispose();
                    _subscribedTradePairs.Remove(pair);
                }
            }
        });
    }

    public void UnsubscribeTrades(string pair)
    {
        if (_subscribedTradePairs.TryGetValue(pair, out ClientWebSocket? ws))
        {
            ws.Dispose();
            _subscribedTradePairs.Remove(pair);
        }
    }

    #endregion

    private static Candle GetCandleFromJson(JsonArray json, string pair)
    {
        int openPrice = json[1]!.GetValue<int>();
        int closePrice = json[2]!.GetValue<int>();
        int highPrice = json[3]!.GetValue<int>();
        int lowPrice = json[4]!.GetValue<int>();
        decimal volume = json[5]!.GetValue<decimal>();

        var candle = new Candle()
        {
            OpenPrice = openPrice,
            ClosePrice = closePrice,
            HighPrice = highPrice,
            LowPrice = lowPrice,
            OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(json[0]!.GetValue<long>()),
            Pair = pair,
            TotalVolume = volume,
            // OCHL average
            TotalPrice = volume * (openPrice + closePrice + highPrice + lowPrice) / 4,
        };

        return candle;
    }

    public const string BuySide = "BUY";
    public const string SellSide = "SELL";

    private static Trade GetTradeFromJson(JsonArray json, string pair)
    {
        long id = json[0]!.GetValue<long>();
        long mts = json[1]!.GetValue<long>();
        double amount = json[2]!.GetValue<double>();
        double price = json[3]!.GetValue<double>();
        string side = amount >= 0 ? BuySide : SellSide;

        var trade = new Trade()
        {
            Amount = new decimal(amount),
            Id = id.ToString(),
            Pair = pair,
            Price = new decimal(price),
            Side = side,
            Time = DateTimeOffset.FromUnixTimeMilliseconds(mts),
        };

        return trade;
    }
}
