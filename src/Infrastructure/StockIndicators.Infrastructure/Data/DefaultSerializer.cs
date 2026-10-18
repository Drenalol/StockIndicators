using Newtonsoft.Json;
using Serilog;
using StockIndicators.Application.Configuration.Data;

namespace StockIndicators.Infrastructure.Data;

public class DefaultSerializer : ISerializer
{
  private readonly JsonSerializerSettings? _settings;
  private readonly ILogger _logger;

  public DefaultSerializer(JsonSerializerSettings? settings, ILogger logger)
  {
    _settings = settings;
    _logger = logger;
  }

  public string Serialize<TModel>(TModel model) where TModel : class => JsonConvert.SerializeObject(model, _settings);

  public TModel Deserialize<TModel>(string str) where TModel : class => DeserializeOrNull<TModel>(str) ?? throw new InvalidOperationException("Deserialize failed");

  public TModel? DeserializeOrNull<TModel>(string str) where TModel : class => (TModel?)DeserializeOrNull(str, typeof(TModel));

  public object Deserialize(string str, Type? type) => DeserializeOrNull(str, type) ?? throw new InvalidOperationException("Deserialize failed");

  public object? DeserializeOrNull(string str, Type? type)
  {
    try
    {
      return JsonConvert.DeserializeObject(str, type, _settings);
    }
    catch (Exception e)
    {
      _logger.Debug(e, "Deserialize failed");
      return null;
    }
  }
}