using MediatR;
using Microsoft.AspNetCore.JsonPatch;
using StockIndicators.Application.Configuration.Commands;
using StockIndicators.Application.Users;

namespace StockIndicators.Application.Subscriptions.PatchSubscription;

public class PatchSubscriptionCommand : ICommand<Unit>, IHasUserId
{
  public Guid SubscriptionId { get; set; }
  public Guid UserId { get; set; }
  public JsonPatchDocument PatchDocument { get; set; }

  public PatchSubscriptionCommand(Guid subscriptionId, Guid userId, JsonPatchDocument patchDocument)
  {
    SubscriptionId = subscriptionId;
    UserId = userId;
    PatchDocument = patchDocument;
  }
}