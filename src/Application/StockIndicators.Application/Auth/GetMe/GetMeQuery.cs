using StockIndicators.Application.Configuration.Queries;
using StockIndicators.Contracts.Auth;

namespace StockIndicators.Application.Auth.GetMe;

public record GetMeQuery(Guid UserId) : IQuery<AuthProfileDto>;
