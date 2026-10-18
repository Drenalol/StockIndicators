using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;
using StockIndicators.Application.Auth;
using StockIndicators.Application.Configuration.Validation;
using StockIndicators.Envelope;

namespace StockIndicators.Filters;

public class EnvelopeExceptionFilter : IExceptionFilter
{
  private readonly IHostEnvironment _hostEnvironment;
  private readonly Serilog.ILogger _logger;

  public EnvelopeExceptionFilter(IHostEnvironment hostEnvironment, Serilog.ILogger logger)
  {
    _hostEnvironment = hostEnvironment;
    _logger = logger.ForContext<EnvelopeExceptionFilter>();
  }

  public void OnException(ExceptionContext context)
  {
    var (errorCode, error) = context.Exception switch
    {
      NotFoundException notFoundException => (404, notFoundException.Message),
      // Details is null for hand-thrown ValidationExceptions (rule/template validation): the
      // envelope must still carry the human-readable message, otherwise clients get error: null
      // and fall back to the dev-only "exception" field (full stack trace).
      ValidationException validationException => (400, validationException.Details ?? (object)validationException.Message),
      // Wrong password. One code for every failed attempt, no detail to probe against.
      InvalidCredentialsException => (401, "Invalid credentials"),
      _ => (500, "Unhandled exception, please check the application logs")
    };

    if (errorCode == StatusCodes.Status500InternalServerError)
      _logger.Error(
        context.Exception,
        "Unhandled exception on {RequestMethod} {RequestPath} (userId: {UserId})",
        context.HttpContext.Request.Method,
        context.HttpContext.Request.Path,
        context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    else if (context.Exception is NotFoundException)
      // A missing entity is a routine client race (deleted in another window/device, a
      // stale deep link) - and any bugged client can hammer such a request, so it stays
      // informational instead of a warning to keep the log stream readable
      _logger.Information(
        "{ExceptionType} on {RequestMethod} {RequestPath} → {StatusCode}: {Message}",
        context.Exception.GetType().Name,
        context.HttpContext.Request.Method,
        context.HttpContext.Request.Path,
        errorCode,
        context.Exception.Message);
    else
      _logger.Warning(
        "{ExceptionType} on {RequestMethod} {RequestPath} → {StatusCode}: {Message}",
        context.Exception.GetType().Name,
        context.HttpContext.Request.Method,
        context.HttpContext.Request.Path,
        errorCode,
        context.Exception.Message);

    var envelope = ErrorEnvelope.Create(errorCode, error);

    // Structured validation codes ride alongside the unchanged plain error string so clients
    // can localize; the field is omitted entirely for every other exception type.
    if (context.Exception is ValidationException withCodes)
      envelope.ErrorDetails = withCodes.ErrorCodes;

    if (_hostEnvironment.IsDevelopment())
      envelope.Exception = context.Exception.ToString();

    // The envelope carries the logical status code, but the HTTP response must carry it too —
    // otherwise clients treat the failed request as a plain 200 success.
    context.Result = new ObjectResult(envelope) { StatusCode = errorCode };
    context.ExceptionHandled = true;
  }
}