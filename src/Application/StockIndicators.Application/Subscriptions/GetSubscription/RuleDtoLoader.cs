using System.Data.Common;
using Dapper;
using Newtonsoft.Json;
using StockIndicators.Application.Rules;
using StockIndicators.Contracts.Rules;
using StockIndicators.Domain.Models.Rules;

namespace StockIndicators.Application.Subscriptions.GetSubscription;

/// <summary>
/// Loads the full rule tree for rule-based subscriptions (shared by the single/list queries).
/// </summary>
public static class RuleDtoLoader
{
  public static async Task<RuleDto?> LoadRuleAsync(DbConnection connection, Guid ruleId, CancellationToken cancellationToken)
  {
    var row = (
      await connection.QueryAsync<RuleRow>(
        "SELECT \"Id\", \"Name\", \"RuleData\" FROM \"Rules\" WHERE \"Id\" = @RuleId",
        new { RuleId = ruleId }
      )
    ).SingleOrDefault();

    if (row == null || string.IsNullOrEmpty(row.RuleData))
      return null;

    if (JsonConvert.DeserializeObject<RuleDataPayload>(row.RuleData) is not { } payload)
      return null;

    var rule = new Rule
    {
      Id = row.Id,
      Name = row.Name,
      When = payload.When,
      ThenTrue = payload.ThenTrue,
      ThenFalse = payload.ThenFalse
    };

    return RuleMappers.ToDto(rule);
  }

  public static async Task<Dictionary<Guid, RuleDto>> LoadRulesAsync(DbConnection connection, IEnumerable<Guid> ruleIds, CancellationToken cancellationToken)
  {
    var ids = ruleIds.Distinct().ToArray();

    if (ids.Length == 0)
      return [];

    var rows = (
      await connection.QueryAsync<RuleRow>(
        "SELECT \"Id\", \"Name\", \"RuleData\" FROM \"Rules\" WHERE \"Id\" = ANY(@RuleIds)",
        new { RuleIds = ids }
      )
    ).AsList();

    var result = new Dictionary<Guid, RuleDto>(rows.Count);

    foreach (var row in rows)
    {
      if (string.IsNullOrEmpty(row.RuleData))
        continue;

      if (JsonConvert.DeserializeObject<RuleDataPayload>(row.RuleData) is not { } payload)
        continue;

      result[row.Id] = RuleMappers.ToDto(new Rule
      {
        Id = row.Id,
        Name = row.Name,
        When = payload.When,
        ThenTrue = payload.ThenTrue,
        ThenFalse = payload.ThenFalse
      });
    }

    return result;
  }

  public class RuleRow
  {
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string RuleData { get; set; } = null!;
  }
}
