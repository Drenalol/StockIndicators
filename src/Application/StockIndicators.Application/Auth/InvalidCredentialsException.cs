namespace StockIndicators.Application.Auth;

/// <summary>
/// The password did not match. Deliberately one error for "no such user" and "wrong password"
/// (there is one user anyway) and never more specific than that.
/// </summary>
public class InvalidCredentialsException : Exception
{
  public InvalidCredentialsException()
    : base("Invalid credentials")
  {
  }
}
