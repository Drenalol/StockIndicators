using System.Data.Common;

namespace StockIndicators.Application.Configuration.Data;

public interface ISqlConnectionFactory
{
  Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default);
}