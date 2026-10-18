using System.Data;
using System.Data.Common;
using Dapper;
using Npgsql;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Contracts.Subscriptions;

namespace StockIndicators.Infrastructure.Database;

public sealed class NpgsqlConnectionFactory : ISqlConnectionFactory, IDisposable
{
  private readonly string _connectionString;
  private NpgsqlConnection? _connection;

  // Dapper reads text columns into enums only through a registered handler (its default
  // conversion goes via Convert.ChangeType, which cannot parse strings into enums); the
  // subscription status is stored as text exactly like the billing enums in EF
  static NpgsqlConnectionFactory() => SqlMapper.AddTypeHandler(new SubscriptionStatusTypeHandler());

  public NpgsqlConnectionFactory(string connectionString) => _connectionString = connectionString;

  public async Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
  {
    if (_connection is { State: ConnectionState.Open })
      return _connection;

    _connection = new NpgsqlConnection(_connectionString);
    await _connection.OpenAsync(cancellationToken);

    return _connection;
  }

  public void Dispose()
  {
    if (_connection is { State: ConnectionState.Open })
      _connection.Dispose();
  }

  private sealed class SubscriptionStatusTypeHandler : SqlMapper.TypeHandler<SubscriptionStatusDto>
  {
    public override SubscriptionStatusDto Parse(object value) => value switch
    {
      string text when Enum.TryParse<SubscriptionStatusDto>(text, out var parsed) => parsed,
      _ => throw new DataException($"Cannot map '{value}' to {nameof(SubscriptionStatusDto)}"),
    };

    public override void SetValue(IDbDataParameter parameter, SubscriptionStatusDto value) => parameter.Value = value.ToString();
  }
}