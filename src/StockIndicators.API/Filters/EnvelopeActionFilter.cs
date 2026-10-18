using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StockIndicators.Envelope;

namespace StockIndicators.Filters;

public class EnvelopeActionFilter : IAsyncResultFilter
{
  public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
  {
    if (context.Result is ObjectResult result)
      result.Value = ResultEnvelope.Create(result.StatusCode, result.Value);

    return next.Invoke();
  }
}