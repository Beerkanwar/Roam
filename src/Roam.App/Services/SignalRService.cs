using System;
using System.Threading.Tasks;
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
        return DeviceInfo.DeviceType == DeviceType.Virtual ? "http://10.0.2.2:5000" : "https://localhost:7217";
#else
        return "https://localhost:7217";
#endif
    }

    public SignalRService(string? baseApiUrl = null)
    {
        _baseApiUrl = !string.IsNullOrWhiteSpace(baseApiUrl) ? baseApiUrl : GetDefaultBaseUrl();
    }

    public async Task InitializeAsync(string token)
    {
        await DisposeAsync();

        _tourHubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseApiUrl}/hubs/tour", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _chatHubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseApiUrl}/hubs/chat", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
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
