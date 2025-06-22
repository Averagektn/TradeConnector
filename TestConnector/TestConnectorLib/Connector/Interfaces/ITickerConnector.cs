using TestConnectorLib.Model;

namespace TestConnectorLib.Connector.Interfaces;
public interface ITickerConnector
{
    Task<Ticker> GetTickerInfoAsync(string pair);
}
