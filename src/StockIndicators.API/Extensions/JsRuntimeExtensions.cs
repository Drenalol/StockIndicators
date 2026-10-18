using Microsoft.JSInterop;

namespace StockIndicators.Extensions;

public static class JsRuntimeExtensions
{
  public static async Task<IJSObjectReference> LoadModuleAsync<TModule>(this IJSRuntime js)
  {
    var type = typeof(TModule);
    
    var paths = type.FullName!
      .Split('.')
      .Skip(1)
      .Prepend("..")
      .ToArray();
    
    var path = $"{Path.Combine(paths)}.razor.js";

    return await js.InvokeAsync<IJSObjectReference>(
      "import",
      path
    );
  }

  public static async Task<TValue> InvokeAsync<TModule, TValue>(
    this IJSRuntime js,
    string identifier,
    object?[]? args = null,
    CancellationToken cancellationToken = default
  )
  {
    await using var module = await js.LoadModuleAsync<TModule>();

    return await module.InvokeAsync<TValue>(identifier, cancellationToken, args);
  }
}