import React, { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

interface LiveSessionProps {
    sessionId: string;
}

interface ChatMessage {
    id: string;
    senderId: string;
    senderName: string;
    text: string;
    timestamp: string;
}

const API_BASE = 'https://localhost:7217'; // Update to the correct port if different

const LiveSession: React.FC<LiveSessionProps> = ({ sessionId }) => {
    const [tourConnection, setTourConnection] = useState<signalR.HubConnection | null>(null);
    const [chatConnection, setChatConnection] = useState<signalR.HubConnection | null>(null);
    const [logs, setLogs] = useState<string[]>([]);
    
    // Chat state
    const [chatMessages, setChatMessages] = useState<ChatMessage[]>([]);
    const [chatInput, setChatInput] = useState('');

    // WebRTC refs
    const peerConnection = useRef<RTCPeerConnection | null>(null);
    const localVideoRef = useRef<HTMLVideoElement>(null);
    const remoteVideoRef = useRef<HTMLVideoElement>(null);

    const log = (msg: string) => setLogs(prev => [...prev, msg]);

    // Initialize WebRTC and SignalR
    useEffect(() => {
        let isMounted = true;
        let tConnection: signalR.HubConnection;
        let cConnection: signalR.HubConnection;

        const setup = async () => {
            try {
                // 1. Fetch ICE Servers (TURN credentials)
                const turnResponse = await fetch(`${API_BASE}/api/v1/ice-servers`);
                const turnData = await turnResponse.json();
                
                if (!isMounted) return;

                // 2. Initialize RTCPeerConnection
                peerConnection.current = new RTCPeerConnection({
                    iceServers: turnData.servers || []
                });

                peerConnection.current.onicecandidate = (event) => {
                    if (event.candidate && tConnection?.state === signalR.HubConnectionState.Connected) {
                        tConnection.invoke('SendIceCandidate', sessionId, JSON.stringify(event.candidate))
                            .catch(err => console.error(err));
                    }
                };

                peerConnection.current.ontrack = (event) => {
                    if (remoteVideoRef.current && event.streams && event.streams[0]) {
                        remoteVideoRef.current.srcObject = event.streams[0];
                        log('Received remote video track');
                    }
                };

                // Get local camera
                try {
                    const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
                    if (localVideoRef.current) {
                        localVideoRef.current.srcObject = stream;
                    }
                    stream.getTracks().forEach(track => peerConnection.current?.addTrack(track, stream));
                } catch (e) {
                    log('Could not get local camera access. You will be view-only.');
                }

                // 3. Initialize Tour Hub (Signaling)
                tConnection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_BASE}/hubs/tour`)
                    .withAutomaticReconnect()
                    .build();

                tConnection.on('ParticipantJoined', (connectionId: string) => {
                    log(`Participant joined: ${connectionId}`);
                });

                tConnection.on('ReceiveOffer', async (senderId: string, sdp: string) => {
                    log(`Received SDP Offer from ${senderId}`);
                    try {
                        const offer = JSON.parse(sdp);
                        await peerConnection.current?.setRemoteDescription(new RTCSessionDescription(offer));
                        const answer = await peerConnection.current?.createAnswer();
                        await peerConnection.current?.setLocalDescription(answer);
                        
                        if (tConnection.state === signalR.HubConnectionState.Connected) {
                            tConnection.invoke('SendAnswer', sessionId, JSON.stringify(answer));
                        }
                    } catch (e) {
                        log(`Error handling offer: ${e}`);
                    }
                });

                tConnection.on('ReceiveAnswer', async (_senderId: string, sdp: string) => {
                    log(`Received SDP Answer from ${_senderId}`);
                    try {
                        const answer = JSON.parse(sdp);
                        await peerConnection.current?.setRemoteDescription(new RTCSessionDescription(answer));
                    } catch (e) {
                        log(`Error handling answer: ${e}`);
                    }
                });

                tConnection.on('ReceiveIceCandidate', async (_senderId: string, candidateJson: string) => {
                    try {
                        const candidate = JSON.parse(candidateJson);
                        await peerConnection.current?.addIceCandidate(new RTCIceCandidate(candidate));
                    } catch (e) {
                        log(`Error handling ICE candidate: ${e}`);
                    }
                });

                await tConnection.start();
                await tConnection.invoke('JoinSession', sessionId);
                setTourConnection(tConnection);
                log('Tour Hub connected and joined session');

                // 4. Initialize Chat Hub
                cConnection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_BASE}/hubs/chat`)
                    .withAutomaticReconnect()
                    .build();

                cConnection.on('ChatMessageReceived', (message: ChatMessage) => {
                    setChatMessages(prev => [...prev, message]);
                });

                await cConnection.start();
                await cConnection.invoke('JoinSessionChat', sessionId);
                setChatConnection(cConnection);
                log('Chat Hub connected and joined session');

            } catch (err) {
                if (isMounted) log(`Setup Error: ${err}`);
            }
        };

        setup();

        return () => {
            isMounted = false;
            tConnection?.stop();
            cConnection?.stop();
            peerConnection.current?.close();
        };
    }, [sessionId]);

    const handleCall = async () => {
        try {
            const offer = await peerConnection.current?.createOffer();
            await peerConnection.current?.setLocalDescription(offer);
            
            if (tourConnection?.state === signalR.HubConnectionState.Connected) {
                await tourConnection.invoke('SendOffer', sessionId, JSON.stringify(offer));
                log('Sent WebRTC Offer');
            }
        } catch (e) {
            log(`Call Error: ${e}`);
        }
    };

    const handleSendMessage = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!chatInput.trim() || chatConnection?.state !== signalR.HubConnectionState.Connected) return;
        
        try {
            await chatConnection.invoke('SendMessage', sessionId, chatInput);
            setChatInput('');
        } catch (e) {
            log(`Chat Error: ${e}`);
        }
    };

    const handleReport = async () => {
        try {
            const response = await fetch(`${API_BASE}/api/v1/trust-safety/reports`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    reportedUserId: 'mock_volunteer_123',
                    tourSessionId: sessionId,
                    reason: 2, // InappropriateContent
                    description: 'Mock report submitted from UI.',
                    severity: 1 // Medium
                })
            });
            if (response.ok) {
                alert('Report submitted successfully.');
            } else {
                alert('Failed to submit report.');
            }
        } catch (error) {
            console.error(error);
        }
    };

    return (
        <div style={{ display: 'flex', gap: '20px', padding: '20px', fontFamily: 'sans-serif' }}>
            
            {/* Left Pane: Video and Controls */}
            <div style={{ flex: '1', display: 'flex', flexDirection: 'column', gap: '15px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <h2>Live Session: {sessionId}</h2>
                    <button onClick={handleReport} style={{ padding: '8px 16px', backgroundColor: '#d13438', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
                        Report User
                    </button>
                </div>

                <div style={{ display: 'flex', gap: '10px' }}>
                    <div style={{ flex: '1', backgroundColor: '#000', borderRadius: '8px', overflow: 'hidden', aspectRatio: '16/9', position: 'relative' }}>
                        <video ref={remoteVideoRef} autoPlay playsInline style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                        <span style={{ position: 'absolute', top: 5, left: 10, color: 'white', background: 'rgba(0,0,0,0.5)', padding: '2px 6px', borderRadius: '4px' }}>Remote</span>
                    </div>
                    <div style={{ width: '200px', backgroundColor: '#000', borderRadius: '8px', overflow: 'hidden', aspectRatio: '16/9', position: 'relative' }}>
                        <video ref={localVideoRef} autoPlay playsInline muted style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                        <span style={{ position: 'absolute', top: 5, left: 10, color: 'white', background: 'rgba(0,0,0,0.5)', padding: '2px 6px', borderRadius: '4px' }}>Local</span>
                    </div>
                </div>

                <div style={{ display: 'flex', gap: '10px' }}>
                    <button onClick={handleCall} style={{ padding: '12px 24px', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '16px' }}>
                        Start Call
                    </button>
                </div>

                <div style={{ backgroundColor: 'black', color: 'lime', padding: '10px', height: '150px', overflowY: 'auto', fontSize: '12px', fontFamily: 'monospace', borderRadius: '8px' }}>
                    {logs.map((log, index) => <div key={index}>{log}</div>)}
                </div>
            </div>

            {/* Right Pane: Chat */}
            <div style={{ width: '300px', backgroundColor: '#F5F5F5', borderRadius: '8px', display: 'flex', flexDirection: 'column', border: '1px solid #ddd' }}>
                <div style={{ padding: '15px', borderBottom: '1px solid #ddd', backgroundColor: '#E0E0E0', fontWeight: 'bold' }}>
                    Session Chat
                </div>
                
                <div style={{ flex: '1', padding: '15px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {chatMessages.map(msg => (
                        <div key={msg.id} style={{ display: 'flex', flexDirection: 'column' }}>
                            <span style={{ fontSize: '11px', color: '#666', fontWeight: 'bold' }}>{msg.senderName}</span>
                            <span style={{ backgroundColor: '#fff', padding: '8px 12px', borderRadius: '12px', border: '1px solid #eee', marginTop: '2px' }}>{msg.text}</span>
                        </div>
                    ))}
                    {chatMessages.length === 0 && (
                        <div style={{ color: '#999', textAlign: 'center', marginTop: '50px' }}>No messages yet</div>
                    )}
                </div>

                <form onSubmit={handleSendMessage} style={{ display: 'flex', padding: '10px', borderTop: '1px solid #ddd', backgroundColor: '#fff' }}>
                    <input 
                        type="text" 
                        value={chatInput}
                        onChange={(e) => setChatInput(e.target.value)}
                        placeholder="Type a message..." 
                        style={{ flex: '1', padding: '8px', border: '1px solid #ccc', borderRadius: '4px', outline: 'none' }} 
                    />
                    <button type="submit" style={{ marginLeft: '10px', padding: '8px 16px', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}>
                        Send
                    </button>
                </form>
            </div>

        </div>
    );
};

export default LiveSession;
