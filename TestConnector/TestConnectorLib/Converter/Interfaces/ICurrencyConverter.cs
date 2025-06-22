namespace TestConnectorLib.Converter.Interfaces;
public interface ICurrencyConverter
{
    Task<decimal> ConvertAsync(string from, string to, decimal amount);
}
