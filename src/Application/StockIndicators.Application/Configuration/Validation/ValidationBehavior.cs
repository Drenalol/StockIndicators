using FluentValidation;
using MediatR;
using StockIndicators.Components.Utils;

namespace StockIndicators.Application.Configuration.Validation;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
  private readonly IValidator<TRequest>[] _validators;

  public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators.AsArray();

  public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
  {
    if (_validators.Length == 0)
      return next();

    var errors = _validators
      .Select(v => v.Validate(request))
      .SelectMany(result => result.Errors)
      .Where(error => error != null)
      .GroupBy(error => error.PropertyName, error => error.ErrorMessage)
      .ToDictionary(grouping => grouping.Key, grouping => grouping);

    if (errors.Count == 0)
      return next();

    throw new ValidationException("Validation failed", errors);
  }
}