using DapperExtensions.Mapper;

namespace StockIndicators.Infrastructure.Database;

public class CustomAutoClassMapper<T> : PluralizedAutoClassMapper<T> where T : class
{
  public override void Table(string tableName)
  {
    const string dto = "dto";

    if (tableName.EndsWith(dto, StringComparison.OrdinalIgnoreCase))
      tableName = tableName.Replace(dto, string.Empty, StringComparison.OrdinalIgnoreCase);

    base.Table(tableName);
  }

  protected override void AutoMap()
  {
    base.AutoMap();

    foreach (var memberMap in Properties.OfType<MemberMap>())
    {
      if (          memberMap.Name.Equals("UserId", StringComparison.OrdinalIgnoreCase) ||
          memberMap.Name.Equals("DomainEvents", StringComparison.OrdinalIgnoreCase) ||
          memberMap.Name.Equals("CooldownSeconds", StringComparison.OrdinalIgnoreCase) ||
          memberMap.Name.Equals("IntervalSeconds", StringComparison.OrdinalIgnoreCase))
        memberMap.Ignore();
    }
  }
}
