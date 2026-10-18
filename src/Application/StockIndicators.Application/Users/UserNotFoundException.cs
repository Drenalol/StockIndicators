using StockIndicators.Application.Configuration.Validation;

namespace StockIndicators.Application.Users;

public class UserNotFoundException : NotFoundException
{
  public UserNotFoundException() : base("User not found")
  {
  }
}
