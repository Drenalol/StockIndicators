using System.Text;
using DapperExtensions.Sql;

namespace StockIndicators.Infrastructure.Database;

public class CustomPostgreSqlDialect : PostgreSqlDialect
{
  public override string GetColumnName(string prefix, string columnName, string alias)
  {
    if (string.IsNullOrEmpty(columnName))
      throw new ArgumentNullException(nameof(columnName), "columnName cannot be null or empty.");

    if (string.IsNullOrWhiteSpace(columnName))
      throw new ArgumentNullException(nameof(columnName), "columnName cannot be null or empty.");

    var stringBuilder = new StringBuilder();

    if (!string.IsNullOrWhiteSpace(prefix))
      stringBuilder.AppendFormat((IsQuoted(prefix) ? prefix : QuoteString(prefix)) + ".");

    stringBuilder.AppendFormat(QuoteString(columnName));

    if (!string.IsNullOrWhiteSpace(alias))
      stringBuilder.Append($" AS {QuoteString(alias)}");

    return stringBuilder.ToString();
  }

  public override string GetTableName(string schemaName, string tableName, string alias)
  {
    if (string.IsNullOrWhiteSpace(tableName))
      throw new ArgumentNullException(nameof(tableName), "tableName cannot be null or empty.");

    var stringBuilder = new StringBuilder();

    if (!string.IsNullOrWhiteSpace(schemaName))
      stringBuilder.AppendFormat(QuoteString(schemaName) + ".");

    stringBuilder.AppendFormat(QuoteString(tableName));

    if (!string.IsNullOrWhiteSpace(alias))
      stringBuilder.Append($" {QuoteString(alias)}");

    return stringBuilder.ToString();
  }
}