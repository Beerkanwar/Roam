using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Maui.Devices;
using Roam.Contracts.Chat;

namespace Roam.App.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _tourHubConnection;
    private HubConnection? _chatHubConnection;
    private readonly string _baseApiUrl;

    public event Action<string>? OnParticipantJoined;
    public event Action<string>? OnParticipantLeft;
    public event Action<ChatMessageDto>? OnChatMessageReceived;

    public static string GetDefaultBaseUrl()
    {
#if ANDROID
        return DeviceInfo.DeviceType == DeviceType.Virtual ? "http://10.0.2.2:5147" : "https://localhost:7217";
#else
        return "https://localhost:7217";
#endif
    }

    public SignalRService(string? baseApiUrl = null)
    {
        _baseApiUrl = !string.IsNullOrWhiteSpace(baseApiUrl) ? baseApiUrl : GetDefaultBaseUrl();
    }

    /// <param name="accessTokenProvider">
    /// Called on every connect and automatic reconnect, so a reconnect after the access token expired
    /// uses a refreshed token instead of the one captured when the session was joined.
    /// </param>
    public async Task InitializeAsync(Func<Task<string?>> accessTokenProvider)
    {
        await DisposeAsync();

        _tourHubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseApiUrl}/hubs/tour", options => ConfigureConnection(options, accessTokenProvider))
            .WithAutomaticReconnect()
            .Build();

        _chatHubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseApiUrl}/hubs/chat", options => ConfigureConnection(options, accessTokenProvider))
            .WithAutomaticReconnect()
            .Build();

        // Tour Hub Events
        _tourHubConnection.On<string>("ParticipantJoined", (connectionId) =>
        {
            OnParticipantJoined?.Invoke(connectionId);
        });

        _tourHubConnection.On<string>("ParticipantLeft", (connectionId) =>
        {
            OnParticipantLeft?.Invoke(connectionId);
        });

        // Chat Hub Events
        _chatHubConnection.On<ChatMessageDto>("ChatMessageReceived", (message) =>
        {
            OnChatMessageReceived?.Invoke(message);
        });

        await _tourHubConnection.StartAsync();
        await _chatHubConnection.StartAsync();
    }

    private static void ConfigureConnection(HttpConnectionOptions options, Func<Task<string?>> accessTokenProvider)
    {
        options.AccessTokenProvider = accessTokenProvider;
        // WebSockets without the negotiate round-trip, so no sticky sessions are needed when several
        // API servers sit behind a round-robin load balancer.
        options.Transports = HttpTransportType.WebSockets;
        options.SkipNegotiation = true;
    }

    public async Task JoinSessionAsync(string sessionId)
    {
        if (_tourHubConnection?.State == HubConnectionState.Connected)
        {
            await _tourHubConnection.InvokeAsync("JoinSession", sessionId);
        }

        if (_chatHubConnection?.State == HubConnectionState.Connected)
        {
            await _chatHubConnection.InvokeAsync("JoinSessionChat", sessionId);
        }
    }

    public async Task LeaveSessionAsync(string sessionId)
    {
        if (_tourHubConnection?.State == HubConnectionState.Connected)
        {
            await _tourHubConnection.InvokeAsync("LeaveSession", sessionId);
        }

        if (_chatHubConnection?.State == HubConnectionState.Connected)
        {
            await _chatHubConnection.InvokeAsync("LeaveSessionChat", sessionId);
        }
    }

    public async Task SendChatMessageAsync(string sessionId, string message)
    {
        if (_chatHubConnection?.State == HubConnectionState.Connected)
        {
            await _chatHubConnection.InvokeAsync("SendMessage", sessionId, message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_tourHubConnection != null)
        {
            await _tourHubConnection.DisposeAsync();
            _tourHubConnection = null;
        }

        if (_chatHubConnection != null)
        {
            await _chatHubConnection.DisposeAsync();
            _chatHubConnection = null;
        }
    }
}
