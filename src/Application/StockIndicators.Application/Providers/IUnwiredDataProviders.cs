namespace StockIndicators.Application.Providers;

/// <summary>
/// The data provider codes that were not wired into the process at composition (disabled or
/// left unconfigured - the same decision <c>Program.cs</c> makes once per start). Their rows
/// in the persistent Instruments table are dead catalog links: no source answers for them
/// anymore, so the warm-up's table sync prunes them. A provider that <em>is</em> wired but
/// temporarily failing is not here and keeps its rows.
/// </summary>
public interface IUnwiredDataProviders
{
  IReadOnlySet<string> Codes { get; }
}

/// <summary>Frozen view built by the composition root after the provider wiring decisions.</summary>
public sealed class UnwiredDataProviders(IEnumerable<string> codes) : IUnwiredDataProviders
{
  public IReadOnlySet<string> Codes { get; } =
    codes.Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
