using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Roam.Application.Payments;
using System.Threading.Tasks;
using System;

namespace Roam.Api.Endpoints;

public static class PaymentsEndpoints
{
    public static void MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/payments")
            .WithApiVersionSet(app.NewApiVersionSet().Build())
            .HasApiVersion(1, 0)
            .RequireAuthorization()
            .WithTags("Payments");

        group.MapPost("/tips", async (
            [FromBody] SubmitTipRequest request,
            [FromServices] IPaymentService paymentService,
            ClaimsPrincipal user) =>
        {
            var senderIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(senderIdStr, out var senderId))
                return Results.Unauthorized();

            var tip = await paymentService.SubmitTipAsync(senderId, request.ReceiverId, request.Amount, request.Currency, request.TourSessionId);
            return Results.Ok(new { tip.Id, tip.Status });
        })
        .WithName("SubmitTip")
        .WithSummary("Submit a tip to a volunteer");

        group.MapPost("/donations", async (
            [FromBody] SubmitDonationRequest request,
            [FromServices] IPaymentService paymentService,
            ClaimsPrincipal user) =>
        {
            var donorIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? donorId = Guid.TryParse(donorIdStr, out var id) ? id : null;

            var donation = await paymentService.SubmitDonationAsync(request.Amount, request.Currency, donorId);
            return Results.Ok(new { donation.Id, donation.Status });
        })
        .WithName("SubmitDonation")
        .WithSummary("Submit a donation to the platform");
    }
}

public class SubmitTipRequest
{
    public Guid ReceiverId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? TourSessionId { get; set; }
}

public class SubmitDonationRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
}
