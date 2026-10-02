using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Roam.App.Services;
using Roam.Contracts.Chat;

namespace Roam.App.ViewModels;

public class LiveSessionViewModel : INotifyPropertyChanged
{
    private readonly SignalRService _signalRService;
    private string _sessionId = string.Empty;
    private string _messageText = string.Empty;
    private string _statusText = "Not connected";
    
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
    
    public ICommand JoinCommand { get; }
    public ICommand LeaveCommand { get; }
    public ICommand SendCommand { get; }

    public LiveSessionViewModel()
    {
        // Use dependency injection in a real app, instantiate directly for MVP
        _signalRService = new SignalRService();
        
        _signalRService.OnParticipantJoined += (userId, role, time) => 
        {
            MainThread.BeginInvokeOnMainThread(() => StatusText = $"User {userId} joined as {role}");
        };
        
        _signalRService.OnParticipantLeft += (userId, time) => 
        {
            MainThread.BeginInvokeOnMainThread(() => StatusText = $"User {userId} left");
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
        
        // In a real application, you get the token from your auth service
        string dummyToken = "your_jwt_token_here";
        
        await _signalRService.InitializeAsync(dummyToken);
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
