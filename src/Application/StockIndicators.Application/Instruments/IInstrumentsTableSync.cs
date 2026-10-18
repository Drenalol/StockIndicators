namespace StockIndicators.Application.Instruments;

/// <summary>
/// Mirror of the provider instrument lists into the persistent <c>Instruments</c> table
/// (the row set the SQL search and the subscription filters run over).
/// </summary>
public interface IInstrumentsTableSync
{
  /// <summary>
  /// Upserts the loaded instruments, prunes rows of the sources that answered successfully
  /// and drops the rows of the <see cref="InstrumentsLoadResult.UnwiredSources"/> providers
  /// (not wired at all - disabled or unconfigured). Best-effort by contract: implementation
  /// logs and swallows storage failures (the in-memory cache already serves the request
  /// path meanwhile).
  /// </summary>
  Task SyncAsync(InstrumentsLoadResult result, CancellationToken cancellationToken);
}
