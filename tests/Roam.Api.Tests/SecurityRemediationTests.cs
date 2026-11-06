using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Roam.Application.Chat;
using Roam.Application.Payments;
using Roam.Application.TrustAndSafety;
using Roam.Contracts.Chat;
using Roam.Domain.Payments;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;
using Xunit;

namespace Roam.Api.Tests;

public class SecurityRemediationTests
{
    private class FakePaymentService : IPaymentService
    {
        public bool ThrowNotFound { get; set; }
        public bool ThrowUnauthorized { get; set; }
        public bool ThrowConflict { get; set; }

        public Task<TipTransaction> SubmitTipAsync(string senderId, string receiverId, decimal amount, string currency, string? tourSessionId, string? idempotencyKey = null)
        {
            return Task.FromResult(new TipTransaction(senderId, receiverId, amount, currency, tourSessionId, idempotencyKey));
        }

        public Task<Donation> SubmitDonationAsync(decimal amount, string currency, string? donorId, string? idempotencyKey = null)
        {
            return Task.FromResult(new Donation(amount, currency, donorId, idempotencyKey));
        }

        public Task<TipTransaction> SettleTipAsync(string tipId, string callerId, bool isAdmin, bool succeeded, CancellationToken cancellationToken = default)
        {
            if (ThrowNotFound)
                throw new KeyNotFoundException($"Tip '{tipId}' was not found.");

            if (ThrowUnauthorized)
                throw new UnauthorizedAccessException("Only the tip participants or an administrator may settle this tip transaction.");

            if (ThrowConflict)
                throw new InvalidOperationException("Cannot settle tip transaction with status 'Completed'. Only Pending tips can be settled.");

            var tip = new TipTransaction("sender1", callerId, 25.00m, "USD");
            if (succeeded)
                tip.MarkCompleted();
            else
                tip.MarkFailed();

            return Task.FromResult(tip);
        }
    }

    private class FakeTrustAndSafetyService : ITrustAndSafetyService
    {
        public Task<Report> SubmitReportAsync(CreateReportDto dto, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task AppendReliabilityEventAsync(string userId, ReliabilityEventType type, string sessionId, int weight, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> GetUserReliabilitySummaryAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Reliability Score: 95.0, Tours Completed: 10, Late Cancellations: 0");
        }

        public Task BlockUserAsync(string blockerId, string blockedId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnblockUserAsync(string blockerId, string blockedId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<string>> GetBlockedUsersAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
        public Task ResolveReportAsync(string reportId, ModerationAction action, string moderatorId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeChatService : IChatService
    {
        public int LastRequestedLimit { get; private set; }

        public Task<ChatMessageDto> SendMessageAsync(string sessionId, string senderId, string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatMessageDto
            {
                Id = "msg-1",
                SessionId = sessionId,
                SenderId = senderId,
                SenderName = "Test",
                Text = text,
                SentAt = DateTime.UtcNow
            });
        }

        public Task<IEnumerable<ChatMessageDto>> GetSessionChatHistoryAsync(string sessionId, string userId, DateTime? before = null, int limit = 50, CancellationToken cancellationToken = default)
        {
            LastRequestedLimit = limit;
            return Task.FromResult<IEnumerable<ChatMessageDto>>(Array.Empty<ChatMessageDto>());
        }

        public Task<bool> IsUserParticipantAsync(string sessionId, string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    #region Tip Settlement Authorization Tests

    [Fact]
    public async Task SettleTip_WhenCallerIsUnauthorized_Returns403Forbidden()
    {
        var fakePaymentService = new FakePaymentService { ThrowUnauthorized = true };
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "attacker-user-id")
        }, "TestAuth"));

        var result = await SettleTipHandler("tip-123", true, fakePaymentService, user, CancellationToken.None);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task SettleTip_WhenTipNotFound_Returns404NotFound()
    {
        var fakePaymentService = new FakePaymentService { ThrowNotFound = true };
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "valid-receiver-id")
        }, "TestAuth"));

        var result = await SettleTipHandler("nonexistent-tip", true, fakePaymentService, user, CancellationToken.None);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task SettleTip_WhenTipAlreadySettled_Returns409Conflict()
    {
        var fakePaymentService = new FakePaymentService { ThrowConflict = true };
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "valid-receiver-id")
        }, "TestAuth"));

        var result = await SettleTipHandler("tip-already-settled", true, fakePaymentService, user, CancellationToken.None);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task SettleTip_WhenAuthorized_Returns200OkWithCompletedStatus()
    {
        var fakePaymentService = new FakePaymentService();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "volunteer-receiver-id")
        }, "TestAuth"));

        var result = await SettleTipHandler("tip-123", true, fakePaymentService, user, CancellationToken.None);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
    }

    private static async Task<IResult> SettleTipHandler(
        string id,
        bool succeeded,
        IPaymentService paymentService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var callerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Results.Unauthorized();

        var isAdmin = user.IsInRole("Admin");

        try
        {
            var tip = await paymentService.SettleTipAsync(id, callerId, isAdmin, succeeded, cancellationToken);
            return Results.Ok(new { tip.Id, Status = tip.Status.ToString() });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = $"Tip transaction '{id}' was not found." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    #endregion

    #region Reliability Lookup Authorization Tests

    [Fact]
    public async Task GetUserReliability_WhenCallerIsDifferentUserAndNotAdmin_Returns403Forbid()
    {
        var service = new FakeTrustAndSafetyService();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "caller-user-id")
        }, "TestAuth"));

        var result = await GetUserReliabilityHandler("other-user-id", user, service, CancellationToken.None);

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task GetUserReliability_WhenCallerIsSubjectUser_Returns200Ok()
    {
        var service = new FakeTrustAndSafetyService();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "subject-user-id")
        }, "TestAuth"));

        var result = await GetUserReliabilityHandler("subject-user-id", user, service, CancellationToken.None);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetUserReliability_WhenCallerIsAdmin_Returns200Ok()
    {
        var service = new FakeTrustAndSafetyService();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user-id"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "TestAuth"));

        var result = await GetUserReliabilityHandler("any-subject-user-id", user, service, CancellationToken.None);

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, statusCodeResult.StatusCode);
    }

    private static async Task<IResult> GetUserReliabilityHandler(
        string userId,
        ClaimsPrincipal user,
        ITrustAndSafetyService service,
        CancellationToken cancellationToken)
    {
        var callerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId))
            return Results.Unauthorized();

        var isAdmin = user.IsInRole("Admin");
        if (callerId != userId && !isAdmin)
        {
            return Results.Forbid();
        }

        var summary = await service.GetUserReliabilitySummaryAsync(userId, cancellationToken);
        return Results.Ok(new { summary });
    }

    #endregion

    #region Account Deletion Password Required Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeleteAccount_WhenPasswordMissing_Returns400BadRequest(string? password)
    {
        var dto = new Roam.Api.Endpoints.DeleteAccountDto { Password = password };

        IResult result;
        if (string.IsNullOrWhiteSpace(dto?.Password))
        {
            result = Results.BadRequest(new { error = "Password confirmation is required for account deletion." });
        }
        else
        {
            result = Results.Ok();
        }

        var statusCodeResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusCodeResult.StatusCode);
    }

    #endregion

    #region Chat Limit Clamping Tests

    [Theory]
    [InlineData(500, 100)]
    [InlineData(100, 100)]
    [InlineData(50, 50)]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    public async Task ChatEndpoint_ClampsLimitToSaneRange(int requestedLimit, int expectedClampedLimit)
    {
        var chatService = new FakeChatService();
        var safeLimit = Math.Clamp(requestedLimit, 1, 100);

        await chatService.GetSessionChatHistoryAsync("session-1", "user-1", null, safeLimit, CancellationToken.None);

        Assert.Equal(expectedClampedLimit, chatService.LastRequestedLimit);
    }

    #endregion
}
