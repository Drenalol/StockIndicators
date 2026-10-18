using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog;
using StockIndicators.Application.Configuration.Data;
using StockIndicators.Application.Instruments;
using StockIndicators.Application.Instruments.CryptoNames;
using StockIndicators.Application.Instruments.GetInstruments;
using StockIndicators.Components.Utils;

namespace StockIndicators.Infrastructure.Domain.Instruments;

/// <summary>
/// Mirrors the loaded instrument lists into the persistent "Instruments" table:
/// COPY into the UNLOGGED staging table, one merge upsert, then a prune limited to the
/// sources that answered successfully (a failed provider keeps its previous rows) plus the
/// data providers that were not wired at all (their rows are dead links).
/// Runs on the startup background thread once a day; the volume (~tens of thousands of
/// rows) is why it goes through bulk copy instead of row-by-row Dapper.
/// </summary>
public class InstrumentsTableSync : IInstrumentsTableSync
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ICryptoAssetNames _cryptoAssetNames;
  private readonly ILogger _logger;

  public InstrumentsTableSync(
    IServiceScopeFactory scopeFactory,
    ICryptoAssetNames cryptoAssetNames,
    ILogger logger)
  {
    _scopeFactory = scopeFactory;
    _cryptoAssetNames = cryptoAssetNames;
    _logger = logger;
  }

  public async Task SyncAsync(InstrumentsLoadResult result, CancellationToken cancellationToken)
  {
    try
    {
      await SyncCoreAsync(result, cancellationToken);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception ex)
    {
      // The table is a search accelerator: the in-memory cache still answers the request
      // path, so a storage failure here must not break the instruments warm-up.
      _logger.Error(ex, "Instruments table sync failed; search keeps the previous table contents");
    }
  }

  private async Task SyncCoreAsync(InstrumentsLoadResult result, CancellationToken cancellationToken)
  {
    var namesBySymbol = await _cryptoAssetNames.GetNamesBySymbolAsync(cancellationToken);

    var rows = BuildRows(result.Instruments, namesBySymbol);

    using var scope = _scopeFactory.CreateScope();
    var connection = (NpgsqlConnection)await scope.ServiceProvider
      .GetRequiredService<ISqlConnectionFactory>()
      .GetOpenConnectionAsync(cancellationToken);

    await using (var truncate = new NpgsqlCommand("""
        TRUNCATE "InstrumentSyncStaging"
        """, connection))
    {
      await truncate.ExecuteNonQueryAsync(cancellationToken);
    }

    await using (var importer = await connection.BeginBinaryImportAsync("""
        COPY "InstrumentSyncStaging" ("Ticker", "ExchangeName", "ClassCode", "Currency", "Name", "Isin", "Sector", "Type", "Source", "TickerNorm", "NameNorm", "NameTranslit", "Aliases", "AliasesTop") FROM STDIN (FORMAT BINARY)
        """, cancellationToken))
    {
      foreach (var row in rows)
      {
        await importer.StartRowAsync(cancellationToken);

        await importer.WriteAsync(row.Ticker, cancellationToken);
        await importer.WriteAsync(row.ExchangeName, cancellationToken);
        await WriteNullableAsync(importer, row.ClassCode, cancellationToken);
        await WriteNullableAsync(importer, row.Currency, cancellationToken);
        await WriteNullableAsync(importer, row.Name, cancellationToken);
        await WriteNullableAsync(importer, row.Isin, cancellationToken);
        await WriteNullableAsync(importer, row.Sector, cancellationToken);
        await WriteNullableAsync(importer, row.Type, cancellationToken);
        await importer.WriteAsync(row.Source, cancellationToken);
        await importer.WriteAsync(row.TickerNorm, cancellationToken);
        await importer.WriteAsync(row.NameNorm, cancellationToken);
        await WriteNullableAsync(importer, row.NameTranslit, cancellationToken);
        await importer.WriteAsync(row.Aliases, cancellationToken);
        await importer.WriteAsync(row.AliasesTop, cancellationToken);
      }

      await importer.CompleteAsync(cancellationToken);
    }

    var upserted = await connection.ExecuteAsync("""
        INSERT INTO "Instruments" ("Ticker", "ExchangeName", "ClassCode", "Currency", "Name", "Isin", "Sector", "Type", "Source", "TickerNorm", "NameNorm", "NameTranslit", "Aliases", "AliasesTop")
        SELECT s."Ticker", s."ExchangeName", s."ClassCode", s."Currency", s."Name", s."Isin", s."Sector", s."Type", s."Source", s."TickerNorm", s."NameNorm", s."NameTranslit", s."Aliases", s."AliasesTop"
        FROM "InstrumentSyncStaging" s
        ON CONFLICT ("Ticker", "ExchangeName") DO UPDATE SET
          "ClassCode" = EXCLUDED."ClassCode",
          "Currency" = EXCLUDED."Currency",
          "Name" = EXCLUDED."Name",
          "Isin" = EXCLUDED."Isin",
          "Sector" = EXCLUDED."Sector",
          "Type" = EXCLUDED."Type",
          "Source" = EXCLUDED."Source",
          "TickerNorm" = EXCLUDED."TickerNorm",
          "NameNorm" = EXCLUDED."NameNorm",
          "NameTranslit" = EXCLUDED."NameTranslit",
          "Aliases" = EXCLUDED."Aliases",
          "AliasesTop" = EXCLUDED."AliasesTop",
          "UpdatedAt" = CURRENT_TIMESTAMP
        """);

    var pruned = 0;

    // Unwired providers never answer, so their rows never appear in staging and the same
    // NOT EXISTS deletes them all; a wired source's code in the set (however it got there)
    // still only prunes rows the load no longer carries.
    var pruneSources = new HashSet<string>(result.LoadedSources, StringComparer.OrdinalIgnoreCase);
    pruneSources.UnionWith(result.UnwiredSources);

    if (pruneSources.Count > 0)
      pruned = await connection.ExecuteAsync("""
          DELETE FROM "Instruments" i
          WHERE i."Source" = ANY(@Sources)
            AND NOT EXISTS (
              SELECT 1 FROM "InstrumentSyncStaging" s
              WHERE s."Ticker" = i."Ticker" AND s."ExchangeName" = i."ExchangeName"
            )
          """,
        new { Sources = pruneSources.ToArray() });

    _logger.Information(
      "Instruments table synced: {Upserted} upserted, {Pruned} pruned (sources: {Sources}; unwired: {Unwired})",
      upserted, pruned, string.Join(',', result.LoadedSources), string.Join(',', result.UnwiredSources));
  }

  /// <summary>A staging row: the instrument plus its precomputed search columns.</summary>
  internal record StagingRow(
    string Ticker,
    string ExchangeName,
    string? ClassCode,
    string? Currency,
    string? Name,
    string? Isin,
    string? Sector,
    string? Type,
    string Source,
    string TickerNorm,
    string NameNorm,
    string? NameTranslit,
    string[] Aliases,
    string[] AliasesTop);

  internal static IReadOnlyList<StagingRow> BuildRows(
    IReadOnlyList<InstrumentDto> instruments,
    IReadOnlyDictionary<string, AssetNames> namesBySymbol)
  {
    var rows = new List<StagingRow>(instruments.Count);

    foreach (var instrument in instruments)
    {
      var nameNorm = InstrumentText.NormalizeName(instrument.Name);

      var (aliases, aliasesTop) = instrument.Type?.Equals("Crypto", StringComparison.OrdinalIgnoreCase) == true
        ? ResolveAliases(instrument, nameNorm, namesBySymbol)
        : ([], []);

      rows.Add(new StagingRow(
        instrument.Ticker,
        instrument.ExchangeName ?? string.Empty,
        instrument.ClassCode,
        instrument.Currency,
        instrument.Name,
        instrument.Isin,
        instrument.Sector,
        instrument.Type,
        instrument.Source,
        InstrumentText.NormalizeTicker(instrument.Ticker),
        nameNorm,
        Transliteration.RuToLatin(nameNorm),
        aliases,
        aliasesTop));
    }

    return rows;
  }

  private static async Task WriteNullableAsync(NpgsqlBinaryImporter importer, string? value, CancellationToken cancellationToken)
  {
    if (value is null)
      await importer.WriteNullAsync(cancellationToken);
    else
      await importer.WriteAsync(value, cancellationToken);
  }

  // Human asset names of the crypto base ("BTC/USDT" -> CoinGecko names of "btc"),
  // minus the ones that repeat the already-indexed ticker text. The top-market-cap
  // names stay a separate array: the search ranks them above the clone names.
  private static (string[] Aliases, string[] AliasesTop) ResolveAliases(
    InstrumentDto instrument,
    string nameNorm,
    IReadOnlyDictionary<string, AssetNames> namesBySymbol)
  {
    if (namesBySymbol.Count == 0)
      return ([], []);

    var baseAsset = CryptoBaseAsset.Extract(instrument.Name, instrument.Ticker, instrument.Currency);
    var tickerNorm = InstrumentText.NormalizeTicker(instrument.Ticker);

    if (!namesBySymbol.TryGetValue(baseAsset, out var names))
      return ([], []);

    bool Useful(string name) => name.Length > 1 && name != nameNorm && name != tickerNorm;

    return (names.All.Where(Useful).Distinct().ToArray(), names.Top.Where(Useful).Distinct().ToArray());
  }
}
