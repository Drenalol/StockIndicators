using System.Security.Cryptography;

namespace StockIndicators.Application.Auth;

/// <summary>
/// PBKDF2-SHA256 for the operator's password, stored as
/// <c>pbkdf2-sha256$iterations$salt$hash</c> with base64 segments. Deliberately the framework
/// primitive and one fixed parameter set: the only hash this project ever needs, so there is
/// no algorithm negotiation to get wrong and old hashes keep verifying after an upgrade.
/// </summary>
public static class LocalPasswordHash
{
  private const string Scheme = "pbkdf2-sha256";
  private const int Iterations = 210_000;
  private const int SaltSize = 16;
  private const int KeySize = 32;

  public static string Create(string password)
  {
    var salt = RandomNumberGenerator.GetBytes(SaltSize);
    var key = Derive(password, salt, Iterations);

    return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
  }

  /// <summary>
  /// Constant-time verification. A stored value that does not parse as our format never
  /// matches - a corrupt hash must lock the door, not degrade into a weaker comparison.
  /// </summary>
  public static bool Verify(string password, string stored)
  {
    if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(stored))
      return false;

    var parts = stored.Split('$');

    if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
      return false;

    byte[] salt, expected;

    try
    {
      salt = Convert.FromBase64String(parts[2]);
      expected = Convert.FromBase64String(parts[3]);
    }
    catch (FormatException)
    {
      return false;
    }

    var actual = Derive(password, salt, iterations);

    return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
  }

  private static byte[] Derive(string password, byte[] salt, int iterations) =>
    Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
}
