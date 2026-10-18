namespace StockIndicators.Application.Configuration.Data;

public interface ISerializer
{
  string Serialize<TModel>(TModel model) where TModel : class;

  TModel Deserialize<TModel>(string str) where TModel : class;

  TModel? DeserializeOrNull<TModel>(string str) where TModel : class;

  object Deserialize(string str, Type? type);
  
  object? DeserializeOrNull(string str, Type? type);
}