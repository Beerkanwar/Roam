import React, { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

interface LiveSessionProps {
    sessionId: string;
}

const LiveSession: React.FC<LiveSessionProps> = ({ sessionId }) => {
    const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
    const [logs, setLogs] = useState<string[]>([]);

    useEffect(() => {
        const newConnection = new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:7154/hubs/tour")
            .withAutomaticReconnect()
            .build();

        setConnection(newConnection);
    }, []);

    useEffect(() => {
        if (connection) {
            connection.start()
                .then(() => {
                    setLogs(prev => [...prev, 'Connected to SignalR!']);
                    
                    // Join the specific session room
                    connection.invoke('JoinSession', sessionId)
                        .then(() => setLogs(prev => [...prev, `Joined session room: ${sessionId}`]))
                        .catch(err => console.error(err));
                    
                    // Register handlers for WebRTC signaling
                    connection.on('ParticipantJoined', (connectionId: string) => {
                        setLogs(prev => [...prev, `Participant joined: ${connectionId}`]);
                    });

                    connection.on('ParticipantLeft', (connectionId: string) => {
                        setLogs(prev => [...prev, `Participant left: ${connectionId}`]);
                    });

                    connection.on('ReceiveOffer', (senderId: string, sdp: string) => {
                        setLogs(prev => [...prev, `Received SDP Offer from ${senderId}`]);
                    });

                    connection.on('ReceiveAnswer', (senderId: string, sdp: string) => {
                        setLogs(prev => [...prev, `Received SDP Answer from ${senderId}`]);
                    });

                    connection.on('ReceiveIceCandidate', (senderId: string, candidate: string) => {
                        setLogs(prev => [...prev, `Received ICE Candidate from ${senderId}`]);
                    });
                })
                .catch(e => console.log('Connection failed: ', e));
        }
        
        return () => {
            if (connection) {
                connection.stop();
            }
        };
    }, [connection, sessionId]);

    const sendMockOffer = () => {
        if (connection) {
            connection.invoke('SendOffer', sessionId, "MOCK_SDP_OFFER_DATA")
                .catch(err => console.error(err));
        }
    };

    return (
        <div style={{ padding: '20px', backgroundColor: '#f0f0f0', borderRadius: '8px', color: '#333', marginTop: '15px' }}>
            <h3>Live Session: {sessionId}</h3>
            <button onClick={sendMockOffer} style={{ padding: '8px 16px', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', marginBottom: '15px' }}>
                Broadcast WebRTC Offer
            </button>
            <div style={{ backgroundColor: 'black', color: 'lime', padding: '10px', height: '150px', overflowY: 'auto', fontSize: '12px', fontFamily: 'monospace' }}>
                {logs.map((log, index) => <div key={index}>{log}</div>)}
            </div>
        </div>
    );
};

export default LiveSession;
