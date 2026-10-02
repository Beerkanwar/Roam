using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Roam.Contracts.Chat;

namespace Roam.App.Services;

public class SignalRService
{
    private HubConnection? _tourHubConnection;
    private HubConnection? _chatHubConnection;
    private readonly string _baseApiUrl = "http://localhost:5000"; // Update with actual API URL

    public event Action<string, string, string>? OnParticipantJoined;
    public event Action<string, string>? OnParticipantLeft;
    public event Action<ChatMessageDto>? OnChatMessageReceived;

    public async Task InitializeAsync(string token)
    {
        _tourHubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseApiUrl}/hubs/tour", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        _chatHubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseApiUrl}/hubs/chat", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .WithAutomaticReconnect()
            .Build();

        // Tour Hub Events
        _tourHubConnection.On<string, string, string>("ParticipantJoined", (userId, role, timestamp) =>
        {
            OnParticipantJoined?.Invoke(userId, role, timestamp);
        });

        _tourHubConnection.On<string, string>("ParticipantLeft", (userId, timestamp) =>
        {
            OnParticipantLeft?.Invoke(userId, timestamp);
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
}
