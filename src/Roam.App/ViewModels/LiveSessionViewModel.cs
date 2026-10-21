using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Roam.App.Services;
using Roam.Contracts.Chat;

namespace Roam.App.ViewModels;

public class LiveSessionViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly SignalRService _signalRService;
    private string _sessionId = string.Empty;
    private string _messageText = string.Empty;
    private string _statusText = "Not connected";
    private string _authToken = string.Empty;
    
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

    public string AuthToken
    {
        get => _authToken;
        set => SetProperty(ref _authToken, value);
    }
    
    public ICommand JoinCommand { get; }
    public ICommand LeaveCommand { get; }
    public ICommand SendCommand { get; }

    public LiveSessionViewModel() : this(null)
    {
    }

    public LiveSessionViewModel(SignalRService? signalRService)
    {
        _signalRService = signalRService ?? new SignalRService();
        
        _signalRService.OnParticipantJoined += (connectionId) => 
        {
            MainThread.BeginInvokeOnMainThread(() => StatusText = $"Participant {connectionId} joined");
        };
        
        _signalRService.OnParticipantLeft += (connectionId) => 
        {
            MainThread.BeginInvokeOnMainThread(() => StatusText = $"Participant {connectionId} left");
        };

        _signalRService.OnChatMessageReceived += (msg) => 
        {
            MainThread.BeginInvokeOnMainThread(() => ChatMessages.Add(msg));
        };

        JoinCommand = new Command(async () => await JoinSessionAsync());
        LeaveCommand = new Command(async () => await LeaveSessionAsync());
        SendCommand = new Command(async () => await SendMessageAsync());
    }

    private async Task JoinSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId)) return;
        
        StatusText = "Connecting...";
        
        var token = !string.IsNullOrWhiteSpace(AuthToken) ? AuthToken : string.Empty;
        await _signalRService.InitializeAsync(token);
        await _signalRService.JoinSessionAsync(SessionId);
        
        StatusText = $"Connected to {SessionId}";
    }

    private async Task LeaveSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId)) return;
        
        await _signalRService.LeaveSessionAsync(SessionId);
        StatusText = "Disconnected";
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(MessageText) || string.IsNullOrWhiteSpace(SessionId)) return;
        
        await _signalRService.SendChatMessageAsync(SessionId, MessageText);
        MessageText = string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        await _signalRService.DisposeAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
