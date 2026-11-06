import React, { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import PostSessionFeedbackModal from './PostSessionFeedbackModal';
import ReportAndBlockModal from './ReportAndBlockModal';
import { sanitizeMessageText } from '../utils/sanitize';

interface LiveSessionProps {
    sessionId: string;
    token?: string;
    targetUserId?: string;
    onLeaveSession?: () => void;
}

interface ChatMessage {
    id: string;
    senderId: string;
    senderName: string;
    text: string;
    timestamp: string;
}

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7217';

const LiveSession: React.FC<LiveSessionProps> = ({ sessionId, token, targetUserId, onLeaveSession }) => {
    const [tourConnection, setTourConnection] = useState<signalR.HubConnection | null>(null);
    const [chatConnection, setChatConnection] = useState<signalR.HubConnection | null>(null);
    const [logs, setLogs] = useState<string[]>([]);
    
    // Chat state
    const [chatMessages, setChatMessages] = useState<ChatMessage[]>([]);
    const [chatInput, setChatInput] = useState('');

    // Media controls state
    const [isAudioMuted, setIsAudioMuted] = useState(false);
    const [isVideoMuted, setIsVideoMuted] = useState(true);

    // Safety and Feedback state
    const [showFeedbackModal, setShowFeedbackModal] = useState(false);
    const [showReportModal, setShowReportModal] = useState(false);
    const [counterpartUserId, setCounterpartUserId] = useState<string>(targetUserId || '');

    // WebRTC refs
    const peerConnection = useRef<RTCPeerConnection | null>(null);
    const localVideoRef = useRef<HTMLVideoElement>(null);
    const remoteVideoRef = useRef<HTMLVideoElement>(null);
    const localStreamRef = useRef<MediaStream | null>(null);
    const pendingIceCandidates = useRef<RTCIceCandidate[]>([]);

    const log = (msg: string) => setLogs(prev => [...prev, msg]);

    const authToken = token || sessionStorage.getItem('roam_token') || '';

    // Initialize WebRTC and SignalR
    useEffect(() => {
        let isMounted = true;
        let tConnection: signalR.HubConnection;
        let cConnection: signalR.HubConnection;

        const setup = async () => {
            try {
                // 1. Fetch ICE Servers (TURN credentials) - no third-party fallback per privacy requirements
                const headers: HeadersInit = authToken ? { 'Authorization': `Bearer ${authToken}` } : {};
                let iceServers: RTCIceServer[] = [];
                try {
                    const turnResponse = await fetch(`${API_BASE}/api/v1/ice-servers`, { headers });
                    if (turnResponse.ok) {
                        const turnData = await turnResponse.json();
                        const retrieved = Array.isArray(turnData) ? turnData : (turnData.servers || []);
                        if (retrieved.length > 0) {
                            iceServers = retrieved;
                        }
                    } else {
                        log(`ICE endpoint status: ${turnResponse.status}`);
                    }
                } catch (iceErr) {
                    log(`Failed to fetch ICE credentials (${iceErr})`);
                }
                
                if (!isMounted) return;

                // 2. Initialize RTCPeerConnection safely handling array or object
                peerConnection.current = new RTCPeerConnection({
                    iceServers: iceServers
                });

                peerConnection.current.onicecandidate = (event) => {
                    if (event.candidate && tConnection?.state === signalR.HubConnectionState.Connected) {
                        tConnection.invoke('SendIceCandidate', sessionId, JSON.stringify(event.candidate), null)
                            .catch(err => console.error(err));
                    }
                };

                peerConnection.current.ontrack = (event) => {
                    if (remoteVideoRef.current && event.streams && event.streams[0]) {
                        remoteVideoRef.current.srcObject = event.streams[0];
                        log('Received remote video track');
                    }
                };

                // Get local camera - camera off/muted by default per privacy requirements
                try {
                    const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
                    localStreamRef.current = stream;
                    if (localVideoRef.current) {
                        localVideoRef.current.srcObject = stream;
                    }
                    stream.getVideoTracks().forEach(track => {
                        track.enabled = false;
                    });
                    setIsVideoMuted(true);
                    stream.getTracks().forEach(track => peerConnection.current?.addTrack(track, stream));
                } catch {
                    log('Could not get local camera access. You will be view-only.');
                }

                // 3. Initialize Tour Hub (Signaling)
                tConnection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_BASE}/hubs/tour`, {
                        accessTokenFactory: () => authToken
                    })
                    .withAutomaticReconnect()
                    .build();

                tConnection.on('ParticipantJoined', (connectionId: string) => {
                    log(`Participant joined: ${connectionId}`);
                });

                tConnection.on('ParticipantLeft', (connectionId: string) => {
                    log(`Participant left: ${connectionId}`);
                });

                tConnection.on('ReceiveOffer', async (senderId: string, sdp: string) => {
                    log(`Received SDP Offer from ${senderId}`);
                    try {
                        const offer = JSON.parse(sdp);
                        await peerConnection.current?.setRemoteDescription(new RTCSessionDescription(offer));
                        
                        // Flush any pending buffered ICE candidates
                        while (pendingIceCandidates.current.length > 0) {
                            const pendingCandidate = pendingIceCandidates.current.shift();
                            if (pendingCandidate) {
                                await peerConnection.current?.addIceCandidate(pendingCandidate);
                            }
                        }

                        const answer = await peerConnection.current?.createAnswer();
                        await peerConnection.current?.setLocalDescription(answer);
                        
                        if (tConnection.state === signalR.HubConnectionState.Connected) {
                            tConnection.invoke('SendAnswer', sessionId, JSON.stringify(answer), senderId);
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

                        // Flush any pending buffered ICE candidates
                        while (pendingIceCandidates.current.length > 0) {
                            const pendingCandidate = pendingIceCandidates.current.shift();
                            if (pendingCandidate) {
                                await peerConnection.current?.addIceCandidate(pendingCandidate);
                            }
                        }
                    } catch (e) {
                        log(`Error handling answer: ${e}`);
                    }
                });

                tConnection.on('ReceiveIceCandidate', async (_senderId: string, candidateJson: string) => {
                    try {
                        const candidateData = JSON.parse(candidateJson);
                        const candidate = new RTCIceCandidate(candidateData);
                        if (!peerConnection.current || !peerConnection.current.remoteDescription || !peerConnection.current.remoteDescription.type) {
                            pendingIceCandidates.current.push(candidate);
                        } else {
                            await peerConnection.current.addIceCandidate(candidate);
                        }
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
                    .withUrl(`${API_BASE}/hubs/chat`, {
                        accessTokenFactory: () => authToken
                    })
                    .withAutomaticReconnect()
                    .build();

                cConnection.on('ChatMessageReceived', (message: ChatMessage) => {
                    const sanitizedMessage: ChatMessage = {
                        ...message,
                        text: sanitizeMessageText(message.text),
                        senderName: sanitizeMessageText(message.senderName)
                    };
                    setChatMessages(prev => [...prev, sanitizedMessage]);
                    if (message.senderId) {
                        setCounterpartUserId(prev => prev || message.senderId);
                    }
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
            if (localStreamRef.current) {
                localStreamRef.current.getTracks().forEach(track => track.stop());
                localStreamRef.current = null;
            }
            tConnection?.stop();
            cConnection?.stop();
            peerConnection.current?.close();
        };
    }, [sessionId, authToken]);

    const handleCall = async () => {
        try {
            const offer = await peerConnection.current?.createOffer();
            await peerConnection.current?.setLocalDescription(offer);
            
            if (tourConnection?.state === signalR.HubConnectionState.Connected) {
                await tourConnection.invoke('SendOffer', sessionId, JSON.stringify(offer), null);
                log('Sent WebRTC Offer');
            }
        } catch (e) {
            log(`Call Error: ${e}`);
        }
    };

    const toggleAudio = () => {
        if (localStreamRef.current) {
            const audioTracks = localStreamRef.current.getAudioTracks();
            audioTracks.forEach(t => {
                t.enabled = !t.enabled;
            });
            setIsAudioMuted(prev => !prev);
        }
    };

    const toggleVideo = () => {
        if (localStreamRef.current) {
            const videoTracks = localStreamRef.current.getVideoTracks();
            videoTracks.forEach(t => {
                t.enabled = !t.enabled;
            });
            setIsVideoMuted(prev => !prev);
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

    const authFetch = async (url: string, options: RequestInit = {}) => {
        const headers = new Headers(options.headers || {});
        if (authToken) {
            headers.set('Authorization', `Bearer ${authToken}`);
        }
        return fetch(url, { ...options, headers });
    };

    const handleLeaveClick = () => {
        if (counterpartUserId) {
            setShowFeedbackModal(true);
        } else if (onLeaveSession) {
            onLeaveSession();
        }
    };

    return (
        <div style={{ display: 'flex', gap: '20px', padding: '20px', fontFamily: 'sans-serif' }}>
            
            {/* Left Pane: Video and Controls */}
            <div style={{ flex: '1', display: 'flex', flexDirection: 'column', gap: '15px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <h2 style={{ margin: 0 }}>Live Session: {sessionId}</h2>
                    <div style={{ display: 'flex', gap: '10px' }}>
                        <button 
                            onClick={() => setShowReportModal(true)} 
                            style={{ padding: '8px 16px', backgroundColor: '#dc2626', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 600 }}
                        >
                            🛡️ Safety &amp; Report
                        </button>
                        {onLeaveSession && (
                            <button 
                                onClick={handleLeaveClick} 
                                style={{ padding: '8px 16px', backgroundColor: '#475569', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                            >
                                Leave Session
                            </button>
                        )}
                    </div>
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
                    <button onClick={handleCall} style={{ padding: '10px 20px', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '14px' }}>
                        Start Call
                    </button>
                    <button onClick={toggleAudio} style={{ padding: '10px 20px', backgroundColor: isAudioMuted ? '#d13438' : '#28a745', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '14px' }}>
                        {isAudioMuted ? 'Unmute Mic' : 'Mute Mic'}
                    </button>
                    <button onClick={toggleVideo} style={{ padding: '10px 20px', backgroundColor: isVideoMuted ? '#d13438' : '#28a745', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '14px' }}>
                        {isVideoMuted ? 'Start Video' : 'Stop Video'}
                    </button>
                </div>

                <div style={{ backgroundColor: '#0b0f19', color: '#e5e7eb', padding: '10px', height: '150px', overflowY: 'auto', fontSize: '12px', fontFamily: 'monospace', borderRadius: '8px' }}>
                    {logs.map((item, index) => <div key={index}>{item}</div>)}
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
                            <span style={{ fontSize: '11px', color: '#666', fontWeight: 'bold' }}>{sanitizeMessageText(msg.senderName)}</span>
                            <span style={{ backgroundColor: '#fff', padding: '8px 12px', borderRadius: '12px', border: '1px solid #eee', marginTop: '2px' }}>{sanitizeMessageText(msg.text)}</span>
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

            {/* Post-Session Feedback Modal */}
            {showFeedbackModal && (
                <PostSessionFeedbackModal
                    apiBase={API_BASE}
                    authFetch={authFetch}
                    tourSessionId={sessionId}
                    targetUserId={counterpartUserId}
                    onClose={() => {
                        setShowFeedbackModal(false);
                        if (onLeaveSession) onLeaveSession();
                    }}
                    onSuccess={() => {
                        setShowFeedbackModal(false);
                        if (onLeaveSession) onLeaveSession();
                    }}
                />
            )}

            {/* Safety Reporting & Blocking Modal */}
            {showReportModal && (
                <ReportAndBlockModal
                    apiBase={API_BASE}
                    authFetch={authFetch}
                    targetUserId={counterpartUserId}
                    tourSessionId={sessionId}
                    onClose={() => setShowReportModal(false)}
                    onUserBlocked={() => {
                        setShowReportModal(false);
                        if (onLeaveSession) onLeaveSession();
                    }}
                />
            )}

        </div>
    );
};

export default LiveSession;
