using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Roam.Application.Payments;

namespace Roam.Api.Endpoints;

public static class PaymentsEndpoints
{
    public static void MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payments")
            .RequireAuthorization()
            .RequireRateLimiting("UserPolicy")
            .WithTags("Payments");

        group.MapPost("/tips", async (
            [FromBody] SubmitTipRequest request,
            [FromServices] IPaymentService paymentService,
            ClaimsPrincipal user) =>
        {
            var senderId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(senderId))
                return Results.Unauthorized();

            try
            {
                var tip = await paymentService.SubmitTipAsync(
                    senderId, 
                    request.ReceiverId, 
                    request.Amount, 
                    request.Currency, 
                    request.TourSessionId, 
                    request.IdempotencyKey);

                return Results.Ok(new { tip.Id, tip.Status, tip.Amount, tip.Currency });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SubmitTip")
        .WithSummary("Submit a tip to a volunteer");

        group.MapPost("/tips/{id}/settle", async (
            string id,
            [FromQuery] bool succeeded,
            Roam.Infrastructure.Persistence.ApplicationDbContext dbContext) =>
        {
            var tip = await dbContext.TipTransactions.FindAsync(id);
            if (tip == null) return Results.NotFound();

            if (succeeded)
                tip.MarkCompleted();
            else
                tip.MarkFailed();

            await dbContext.SaveChangesAsync();
            return Results.Ok(new { tip.Id, Status = tip.Status.ToString() });
        })
        .WithName("SettleTip")
        .WithSummary("Settle a tip transaction upon PSP confirmation");

        group.MapPost("/donations", async (
            [FromBody] SubmitDonationRequest request,
            [FromServices] IPaymentService paymentService,
            ClaimsPrincipal user) =>
        {
            var donorId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                var donation = await paymentService.SubmitDonationAsync(
                    request.Amount, 
                    request.Currency, 
                    donorId, 
                    request.IdempotencyKey);

                return Results.Ok(new { donation.Id, donation.Status, donation.Amount, donation.Currency });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SubmitDonation")
        .WithSummary("Submit a donation to the platform");
    }
}

public class SubmitTipRequest
{
    public string ReceiverId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? TourSessionId { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class SubmitDonationRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? IdempotencyKey { get; set; }
}
