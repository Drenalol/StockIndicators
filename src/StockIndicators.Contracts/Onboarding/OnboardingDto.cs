namespace StockIndicators.Contracts.Onboarding;

/// <summary>
/// The settings-page onboarding state. The guide itself lives in the frontend (it is
/// locale-bound copy); the database only remembers that the operator dismissed it, so the
/// guide never comes back on another device or after a reinstall.
/// </summary>
public record OnboardingDto(bool Hidden);
