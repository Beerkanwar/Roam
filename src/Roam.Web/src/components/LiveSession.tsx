import React, { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import PostSessionFeedbackModal from './PostSessionFeedbackModal';
import ReportAndBlockModal from './ReportAndBlockModal';
import { sanitizeMessageText } from '../utils/sanitize';
import { hubConnectionOptions } from '../auth/hubConnection';

interface LiveSessionProps {
    sessionId: string;
    /** Returns a current access token (refreshing if needed); called on every hub (re)connect. */
    getAccessToken: () => Promise<string | null>;
    /** Authenticated fetch with refresh-on-401, shared with the rest of the app. */
    authFetch: (url: string, options?: RequestInit) => Promise<Response>;
    targetUserId?: string;
    /** The signed-in user's id; used so the user is never picked as their own feedback/report counterpart. */
    currentUserId?: string;
    onLeaveSession?: () => void;
}

interface ChatMessage {
    id: string;
    senderId: string;
    senderName: string;
    text: string;
    timestamp: string;
}

import { API_BASE } from '../config';

const LiveSession: React.FC<LiveSessionProps> = ({ sessionId, getAccessToken, authFetch, targetUserId, currentUserId, onLeaveSession }) => {
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
    const [counterpartName, setCounterpartName] = useState<string>('');
    // Leave flow: choose between ending the tour for everyone and stepping out (rejoin later).
    const [showLeaveChoice, setShowLeaveChoice] = useState(false);
    const [tourEnded, setTourEnded] = useState(false);
    const [endingTour, setEndingTour] = useState(false);
    // Where the tour is, shown under the title (from the user's own tour list).
    const [tourPlace, setTourPlace] = useState('');

    // WebRTC refs
    const peerConnection = useRef<RTCPeerConnection | null>(null);
    const localVideoRef = useRef<HTMLVideoElement>(null);
    const remoteVideoRef = useRef<HTMLVideoElement>(null);
    const localStreamRef = useRef<MediaStream | null>(null);
    const pendingIceCandidates = useRef<RTCIceCandidate[]>([]);
    const currentUserIdRef = useRef<string | undefined>(currentUserId);
    useEffect(() => {
        currentUserIdRef.current = currentUserId;
    }, [currentUserId]);

    const log = (msg: string) => setLogs(prev => [...prev, msg]);

    useEffect(() => {
        let current = true;
        authFetch(`${API_BASE}/api/v1/tour-requests/mine`)
            .then(response => (response.ok ? response.json() : []))
            .then((tours: { activeSessionId?: string | null; placeName?: string | null; description?: string; counterpartId?: string | null; counterpartName?: string | null }[]) => {
                const tour = tours.find(t => t.activeSessionId === sessionId);
                if (!current || !tour) return;
                setTourPlace(tour.placeName || tour.description || '');
                // Know who the other person is from the start (not only after they chat), so the title,
                // feedback and Safety & Report all name the right person.
                if (tour.counterpartId) setCounterpartUserId(prev => prev || tour.counterpartId!);
                if (tour.counterpartName) setCounterpartName(prev => prev || sanitizeMessageText(tour.counterpartName!));
            })
            .catch(() => { /* The title simply stays generic. */ });
        return () => {
            current = false;
        };
    }, [sessionId, authFetch]);


    // Initialize WebRTC and SignalR
    useEffect(() => {
        let isMounted = true;
        let tConnection: signalR.HubConnection | undefined;
        let cConnection: signalR.HubConnection | undefined;
        let ownPeerConnection: RTCPeerConnection | null = null;
        let ownStream: MediaStream | null = null;

        // Releases everything THIS effect run acquired. Safe to call more than once, and called again by
        // setup() if the component unmounted while an await was pending (otherwise the camera/mic
        // stream and hub connections created after unmount would leak). Shared refs are only cleared
        // if they still point at this run's objects, so a stale run never tears down a newer one.
        const teardown = () => {
            ownStream?.getTracks().forEach(track => track.stop());
            if (localStreamRef.current === ownStream) localStreamRef.current = null;
            ownStream = null;
            tConnection?.stop();
            cConnection?.stop();
            ownPeerConnection?.close();
            if (peerConnection.current === ownPeerConnection) {
                peerConnection.current = null;
                pendingIceCandidates.current = [];
            }
            ownPeerConnection = null;
        };

        const setup = async () => {
            try {
                // 1. Fetch ICE Servers (TURN credentials) - no third-party fallback per privacy requirements
                let iceServers: RTCIceServer[] = [];
                try {
                    const turnResponse = await authFetch(`${API_BASE}/api/v1/ice-servers`);
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
                ownPeerConnection = new RTCPeerConnection({
                    iceServers: iceServers
                });
                peerConnection.current = ownPeerConnection;

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
                    if (!isMounted) {
                        stream.getTracks().forEach(track => track.stop());
                        return;
                    }
                    ownStream = stream;
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
                if (!isMounted) return;

                // 3. Initialize Tour Hub (Signaling)
                tConnection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_BASE}/hubs/tour`, hubConnectionOptions(getAccessToken))
                    .withAutomaticReconnect()
                    .build();

                tConnection.on('ParticipantJoined', (connectionId: string) => {
                    log(`Participant joined: ${connectionId}`);
                });

                // The other participant (or the server timeout) ended the tour: stop media and offer feedback.
                tConnection.on('TourEnded', () => {
                    log('The tour has ended.');
                    setTourEnded(true);
                    setShowLeaveChoice(false);
                    localStreamRef.current?.getTracks().forEach(track => track.stop());
                    setShowFeedbackModal(true);
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
                        
                        if (tConnection?.state === signalR.HubConnectionState.Connected) {
                            tConnection.invoke('SendAnswer', sessionId, JSON.stringify(answer), senderId)
                                .catch(err => console.error(err));
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
                if (!isMounted) { teardown(); return; }
                await tConnection.invoke('JoinSession', sessionId);
                if (!isMounted) { teardown(); return; }
                setTourConnection(tConnection);
                log('Tour Hub connected and joined session');

                // 4. Initialize Chat Hub
                cConnection = new signalR.HubConnectionBuilder()
                    .withUrl(`${API_BASE}/hubs/chat`, hubConnectionOptions(getAccessToken))
                    .withAutomaticReconnect()
                    .build();

                cConnection.on('ChatMessageReceived', (message: ChatMessage) => {
                    const sanitizedMessage: ChatMessage = {
                        ...message,
                        text: sanitizeMessageText(message.text),
                        senderName: sanitizeMessageText(message.senderName)
                    };
                    setChatMessages(prev => [...prev, sanitizedMessage]);
                    // The hub echoes our own messages back to us; only another participant can be the counterpart.
                    if (message.senderId && message.senderId !== currentUserIdRef.current) {
                        setCounterpartUserId(prev => prev || message.senderId);
                        setCounterpartName(prev => prev || sanitizeMessageText(message.senderName));
                    }
                });

                await cConnection.start();
                if (!isMounted) { teardown(); return; }
                await cConnection.invoke('JoinSessionChat', sessionId);
                if (!isMounted) { teardown(); return; }
                setChatConnection(cConnection);

                // Load earlier messages so a rejoin or page reload doesn't show an empty chat. Live messages
                // that arrived meanwhile are kept; duplicates are dropped by id.
                try {
                    const historyResponse = await authFetch(`${API_BASE}/api/v1/sessions/${encodeURIComponent(sessionId)}/chat?limit=100`);
                    if (historyResponse.ok && isMounted) {
                        const history: ChatMessage[] = await historyResponse.json();
                        const sanitized = history.map(m => ({
                            ...m,
                            text: sanitizeMessageText(m.text),
                            senderName: sanitizeMessageText(m.senderName),
                        }));
                        setChatMessages(prev => {
                            const known = new Set(sanitized.map(m => m.id));
                            return [...sanitized, ...prev.filter(m => !known.has(m.id))];
                        });
                        const other = sanitized.find(m => m.senderId && m.senderId !== currentUserIdRef.current);
                        if (other) {
                            setCounterpartUserId(prev => prev || other.senderId);
                            setCounterpartName(prev => prev || other.senderName);
                        }
                    }
                } catch {
                    log('Could not load earlier chat messages.');
                }
                log('Chat Hub connected and joined session');

            } catch (err) {
                if (isMounted) log(`Setup Error: ${err}`);
            }
        };

        setup();

        return () => {
            isMounted = false;
            teardown();
        };
        // Deliberately not keyed on the access token: a routine token refresh must not tear down the
        // live WebRTC session. The hub token factory fetches a fresh token whenever it reconnects.
    }, [sessionId, getAccessToken, authFetch]);

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

    const finishAndReview = () => {
        if (counterpartUserId) {
            setShowFeedbackModal(true);
        } else if (onLeaveSession) {
            onLeaveSession();
        }
    };

    const handleLeaveClick = () => {
        if (tourEnded) {
            finishAndReview();
        } else {
            setShowLeaveChoice(true);
        }
    };

    const handleEndTour = async () => {
        setEndingTour(true);
        try {
            const response = await authFetch(`${API_BASE}/api/v1/sessions/${encodeURIComponent(sessionId)}/end`, { method: 'POST' });
            if (!response.ok) {
                const data = await response.json().catch(() => ({}));
                log(`Could not end the tour: ${data.error || data.detail || response.status}`);
                return;
            }
            setTourEnded(true);
            setShowLeaveChoice(false);
            finishAndReview();
        } catch (e) {
            log(`Could not end the tour: ${e}`);
        } finally {
            setEndingTour(false);
        }
    };

    return (
        <div className="roam-live" style={{ display: 'flex', flexWrap: 'wrap', gap: '20px', padding: '20px', fontFamily: 'sans-serif', height: '100%', overflowY: 'auto', boxSizing: 'border-box' }}>
            
            {/* Left Pane: Video and Controls */}
            <div style={{ flex: '1 1 340px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: '15px' }}>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ minWidth: 0 }}>
                        <h2 style={{ margin: 0, color: '#f8fafc', fontSize: '1.3rem' }}>
                            {tourEnded ? 'Tour ended' : 'Live tour'}{counterpartName ? ` with ${counterpartName}` : ''}
                        </h2>
                        {tourPlace && (
                            <div style={{ fontSize: '0.85rem', color: '#94a3b8', marginTop: '2px' }}>📍 {tourPlace}</div>
                        )}
                    </div>
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
                    <div style={{ width: 'min(200px, 35%)', backgroundColor: '#000', borderRadius: '8px', overflow: 'hidden', aspectRatio: '16/9', position: 'relative' }}>
                        <video ref={localVideoRef} autoPlay playsInline muted style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                        <span style={{ position: 'absolute', top: 5, left: 10, color: 'white', background: 'rgba(0,0,0,0.5)', padding: '2px 6px', borderRadius: '4px' }}>Local</span>
                    </div>
                </div>

                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px' }}>
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

            {/* Right Pane: Chat (dark theme, matching the rest of the app; own messages right-aligned) */}
            <div style={{ flex: '1 1 280px', maxWidth: '100%', minHeight: '360px', maxHeight: 'calc(100vh - 40px)', backgroundColor: '#0f172a', borderRadius: '8px', display: 'flex', flexDirection: 'column', border: '1px solid #1e293b', color: '#e2e8f0' }}>
                <div style={{ padding: '15px', borderBottom: '1px solid #1e293b', backgroundColor: '#1e293b', fontWeight: 700, color: '#f8fafc', borderRadius: '8px 8px 0 0' }}>
                    Session Chat
                </div>

                <div style={{ flex: '1', padding: '15px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {chatMessages.map(msg => {
                        const isOwn = !!currentUserId && msg.senderId === currentUserId;
                        return (
                            <div key={msg.id} style={{ display: 'flex', flexDirection: 'column', alignItems: isOwn ? 'flex-end' : 'flex-start' }}>
                                <span style={{ fontSize: '11px', color: '#94a3b8', fontWeight: 600 }}>
                                    {isOwn ? 'You' : sanitizeMessageText(msg.senderName)}
                                </span>
                                <span style={{
                                    backgroundColor: isOwn ? '#4f46e5' : '#1e293b',
                                    color: isOwn ? '#ffffff' : '#e2e8f0',
                                    padding: '8px 12px',
                                    borderRadius: '12px',
                                    border: isOwn ? 'none' : '1px solid #334155',
                                    marginTop: '2px',
                                    maxWidth: '85%',
                                    overflowWrap: 'anywhere',
                                    fontSize: '0.88rem',
                                    lineHeight: 1.35,
                                }}>
                                    {sanitizeMessageText(msg.text)}
                                </span>
                            </div>
                        );
                    })}
                    {chatMessages.length === 0 && (
                        <div style={{ color: '#64748b', textAlign: 'center', marginTop: '50px' }}>No messages yet</div>
                    )}
                </div>

                <form onSubmit={handleSendMessage} style={{ display: 'flex', padding: '10px', borderTop: '1px solid #1e293b' }}>
                    <input
                        type="text"
                        value={chatInput}
                        onChange={(e) => setChatInput(e.target.value)}
                        placeholder="Type a message..."
                        maxLength={2000}
                        aria-label="Chat message"
                        style={{ flex: '1', padding: '8px', border: '1px solid #334155', borderRadius: '6px', outline: 'none', backgroundColor: '#1e293b', color: '#f8fafc' }}
                    />
                    <button type="submit" style={{ marginLeft: '10px', padding: '8px 16px', backgroundColor: '#6366f1', color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontWeight: 600 }}>
                        Send
                    </button>
                </form>
            </div>

            {/* Leave choice: end the tour for everyone, or step out and rejoin later */}
            {showLeaveChoice && (
                <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(2, 6, 23, 0.7)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 200 }}>
                    <div role="dialog" aria-label="Leave live tour" style={{ width: 'min(380px, 92vw)', backgroundColor: '#0f172a', border: '1px solid #1e293b', borderRadius: '12px', padding: '20px', color: '#e2e8f0', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                        <h3 style={{ margin: 0, color: '#f8fafc', fontSize: '1.1rem' }}>Leave the live tour?</h3>
                        <p style={{ margin: 0, fontSize: '0.85rem', color: '#94a3b8' }}>
                            Ending the tour finishes it for everyone and lets you both leave feedback. If you just step out, you can rejoin from your tour list.
                        </p>
                        <button disabled={endingTour} onClick={handleEndTour} style={{ padding: '10px', backgroundColor: '#dc2626', color: '#fff', border: 'none', borderRadius: '8px', fontWeight: 600, cursor: 'pointer' }}>
                            {endingTour ? 'Ending…' : 'End tour for everyone'}
                        </button>
                        <button disabled={endingTour} onClick={() => { setShowLeaveChoice(false); onLeaveSession?.(); }} style={{ padding: '10px', backgroundColor: '#334155', color: '#fff', border: 'none', borderRadius: '8px', fontWeight: 600, cursor: 'pointer' }}>
                            Leave for now (rejoin later)
                        </button>
                        <button disabled={endingTour} onClick={() => setShowLeaveChoice(false)} style={{ padding: '8px', backgroundColor: 'transparent', color: '#94a3b8', border: '1px solid #334155', borderRadius: '8px', cursor: 'pointer' }}>
                            Stay in the tour
                        </button>
                    </div>
                </div>
            )}

            {/* Post-Session Feedback Modal */}
            {showFeedbackModal && (
                <PostSessionFeedbackModal
                    apiBase={API_BASE}
                    authFetch={authFetch}
                    tourSessionId={sessionId}
                    targetUserId={counterpartUserId}
                    targetUserName={counterpartName || 'your tour partner'}
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
                    targetUserName={counterpartName || 'your tour partner'}
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
