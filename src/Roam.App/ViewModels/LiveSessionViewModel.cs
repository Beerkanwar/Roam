using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Roam.App.Services;
using Roam.Contracts.Chat;

namespace Roam.App.ViewModels;

public class LiveSessionViewModel : BaseViewModel, IQueryAttributable, IAsyncDisposable
{
    private readonly SignalRService _signalRService;
    private readonly IAuthService _authService;
    private readonly IApiClient _apiClient;

    private string _sessionId = string.Empty;
    private string _messageText = string.Empty;
    private string _statusText = "Disconnected";
    private bool _isConnected;
    private string _counterpartUserId = string.Empty;
    private string _counterpartName = "Explorer";

    // Post-Session Feedback
    private bool _isFeedbackVisible;
    private int _selectedRating = 1; // 1: Positive, 2: Neutral, 3: Negative
    private string _feedbackComments = string.Empty;

    // Safety Report & Block
    private bool _isReportVisible;
    private int _selectedReportReasonIndex = 0;
    private string _reportDescription = string.Empty;

    public ObservableCollection<ChatMessageDto> ChatMessages { get; } = new();

    public string SessionId
    {
        get => _sessionId;
        set => SetProperty(ref _sessionId, value);
    }

    public string MessageText
    {
        get => _messageText;
        set => SetProperty(ref _messageText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string CounterpartUserId
    {
        get => _counterpartUserId;
        set => SetProperty(ref _counterpartUserId, value);
    }

    public string CounterpartName
    {
        get => _counterpartName;
        set => SetProperty(ref _counterpartName, value);
    }

    public bool IsFeedbackVisible
    {
        get => _isFeedbackVisible;
        set => SetProperty(ref _isFeedbackVisible, value);
    }

    public int SelectedRating
    {
        get => _selectedRating;
        set => SetProperty(ref _selectedRating, value);
    }

    public string FeedbackComments
    {
        get => _feedbackComments;
        set => SetProperty(ref _feedbackComments, value);
    }

    public bool IsReportVisible
    {
        get => _isReportVisible;
        set => SetProperty(ref _isReportVisible, value);
    }

    public int SelectedReportReasonIndex
    {
        get => _selectedReportReasonIndex;
        set => SetProperty(ref _selectedReportReasonIndex, value);
    }

    public string ReportDescription
    {
        get => _reportDescription;
        set => SetProperty(ref _reportDescription, value);
    }

    public ICommand JoinCommand { get; }
    public ICommand LeaveCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand OpenFeedbackCommand { get; }
    public ICommand CloseFeedbackCommand { get; }
    public ICommand SubmitFeedbackCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand CloseReportCommand { get; }
    public ICommand SubmitReportCommand { get; }
    public ICommand BlockUserCommand { get; }

    public LiveSessionViewModel(SignalRService signalRService, IAuthService authService, IApiClient apiClient)
    {
        _signalRService = signalRService;
        _authService = authService;
        _apiClient = apiClient;
        Title = "Live Tour Session";

        _signalRService.OnParticipantJoined += (connectionId) =>
        {
            MainThread.BeginInvokeOnMainThread(() => StatusText = "Participant connected");
        };

        _signalRService.OnParticipantLeft += (connectionId) =>
        {
            MainThread.BeginInvokeOnMainThread(() => StatusText = "Participant departed");
        };

        _signalRService.OnChatMessageReceived += (msg) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ChatMessages.Add(msg);
                if (string.IsNullOrEmpty(CounterpartUserId) && msg.SenderId != _authService.CurrentUserId)
                {
                    CounterpartUserId = msg.SenderId;
                    CounterpartName = msg.SenderName;
                }
            });
        };

        JoinCommand = new Command(async () => await JoinSessionAsync());
        LeaveCommand = new Command(async () => await LeaveSessionAsync());
        SendCommand = new Command(async () => await SendMessageAsync());

        OpenFeedbackCommand = new Command(() => IsFeedbackVisible = true);
        CloseFeedbackCommand = new Command(() => IsFeedbackVisible = false);
        SubmitFeedbackCommand = new Command(async () => await SubmitFeedbackAsync());

        OpenReportCommand = new Command(() => IsReportVisible = true);
        CloseReportCommand = new Command(() => IsReportVisible = false);
        SubmitReportCommand = new Command(async () => await SubmitReportAsync());
        BlockUserCommand = new Command(async () => await BlockUserAsync());
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("sessionId", out var sId) && sId is string sessionId && !string.IsNullOrWhiteSpace(sessionId))
        {
            SessionId = sessionId.Trim();
            MainThread.BeginInvokeOnMainThread(async () => await JoinSessionAsync());
        }
    }

    public async Task JoinSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            ErrorMessage = "Please enter a valid session ID.";
            return;
        }

        IsBusy = true;
        StatusText = "Connecting...";
        ErrorMessage = null;

        try
        {
            var token = await _authService.GetAccessTokenAsync() ?? string.Empty;
            await _signalRService.InitializeAsync(token);
            await _signalRService.JoinSessionAsync(SessionId);

            IsConnected = true;
            StatusText = $"Connected to live broadcast";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusText = "Connection error";
            ErrorMessage = $"Unable to join session: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LeaveSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId)) return;

        try
        {
            await _signalRService.LeaveSessionAsync(SessionId);
            IsConnected = false;
            StatusText = "Disconnected";

            // If counterpart exists, prompt for post-session feedback
            if (!string.IsNullOrEmpty(CounterpartUserId))
            {
                IsFeedbackVisible = true;
            }
            else
            {
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error leaving session: {ex.Message}";
        }
    }

    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || string.IsNullOrWhiteSpace(SessionId)) return;

        var text = MessageText.Trim();
        MessageText = string.Empty;

        try
        {
            await _signalRService.SendChatMessageAsync(SessionId, text);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to send message: {ex.Message}";
        }
    }

    public async Task SubmitFeedbackAsync()
    {
        if (string.IsNullOrEmpty(CounterpartUserId))
        {
            IsFeedbackVisible = false;
            await Shell.Current.GoToAsync("..");
            return;
        }

        IsBusy = true;
        try
        {
            var payload = new
            {
                tourSessionId = SessionId,
                targetUserId = CounterpartUserId,
                rating = SelectedRating,
                comments = string.IsNullOrWhiteSpace(FeedbackComments) ? null : FeedbackComments.Trim()
            };

            var success = await _apiClient.PostAsync("/api/v1/trust-safety/feedback", payload, requireAuth: true);
            if (success)
            {
                await Shell.Current.DisplayAlertAsync("Feedback Recorded", "Thank you for rating your live tour experience!", "OK");
            }
            IsFeedbackVisible = false;
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error submitting feedback: {ex.Message}";
            IsFeedbackVisible = false;
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SubmitReportAsync()
    {
        if (string.IsNullOrEmpty(CounterpartUserId))
        {
            ErrorMessage = "Target user ID is unavailable to report.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReportDescription) || ReportDescription.Length < 5)
        {
            ErrorMessage = "Please describe the safety violation (minimum 5 characters).";
            return;
        }

        IsBusy = true;
        try
        {
            var payload = new
            {
                reportedUserId = CounterpartUserId,
                tourSessionId = SessionId,
                reason = SelectedReportReasonIndex,
                description = ReportDescription.Trim(),
                severity = 1 // Medium
            };

            var success = await _apiClient.PostAsync("/api/v1/trust-safety/reports", payload, requireAuth: true);
            if (success)
            {
                await Shell.Current.DisplayAlertAsync("Report Filed", "Your report has been submitted to platform administrators for priority review.", "OK");
                IsReportVisible = false;
                ReportDescription = string.Empty;
            }
            else
            {
                ErrorMessage = "Failed to file report. Please try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error filing report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task BlockUserAsync()
    {
        if (string.IsNullOrEmpty(CounterpartUserId))
        {
            ErrorMessage = "No target user available to block.";
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync("Block User", 
            $"Are you sure you want to block this user? They will be immediately disconnected, excluded from your chat, and prevented from viewing your tour requests.", 
            "Block", "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            var success = await _apiClient.PostAsync<object>($"/api/v1/trust-safety/blocks/{CounterpartUserId}", new { });
            if (success)
            {
                await Shell.Current.DisplayAlertAsync("User Blocked", "User has been blocked. Disconnecting from live session...", "OK");
                IsReportVisible = false;
                await LeaveSessionAsync();
            }
            else
            {
                ErrorMessage = "Unable to block user at this time.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error blocking user: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _signalRService.DisposeAsync();
    }
}
