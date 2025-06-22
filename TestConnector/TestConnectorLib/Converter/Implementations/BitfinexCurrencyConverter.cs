using System.Text.Json;
using System.Text.Json.Nodes;

using RestSharp;

using TestConnectorLib.Converter.Interfaces;
using TestConnectorLib.Exceptions;

namespace TestConnectorLib.Converter.Implementations;
public class BitfinexCurrencyConverter : ICurrencyConverter
{
    public async Task<decimal> ConvertAsync(string from, string to, decimal amount)
    {
        if (from == to)
        {
            return amount;
        }

        using var client = new RestClient("https://api-pub.bitfinex.com/v2");

        RestRequest request = new RestRequest($"calc/fx", Method.Post)
            .AddHeader("Accept", "application/json")
            .AddHeader("Content-Type", "application/json")
            .AddBody(new { Ccy1 = from, Ccy2 = to });

        RestResponse response = await client.ExecuteAsync(request);

        if (response.IsSuccessStatusCode)
        {
            string json = response.Content!;

            JsonArray data = JsonSerializer.Deserialize<JsonArray>(json)!;

            decimal rate = data[0]!.GetValue<decimal>();

            return rate * amount;
        }

        throw new RequestFailedException($"Failed with {response.StatusCode} {response.ErrorMessage} {response.ErrorException}");
    }
}
