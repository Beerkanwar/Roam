import React, { useState, useCallback, useEffect } from 'react';
import Map from './components/Map';
import LiveSession from './components/LiveSession';
import * as signalR from '@microsoft/signalr';
import './App.css';

interface Place {
  id?: string;
  name: string;
  description: string;
  latitude: number;
  longitude: number;
}

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'https://localhost:7217';

function App() {
  const [token, setToken] = useState<string>(() => localStorage.getItem('roam_token') || '');
  const [authEmail, setAuthEmail] = useState('');
  const [authPassword, setAuthPassword] = useState('');
  const [isRegistering, setIsRegistering] = useState(false);
  const [authError, setAuthError] = useState('');
  
  const [searchQuery, setSearchQuery] = useState('');
  const [places, setPlaces] = useState<Place[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedCoords, setSelectedCoords] = useState<{lat: number, lng: number} | null>(null);
  const [visibilityMode, setVisibilityMode] = useState<number>(1); // 1 = Group, 0 = Private
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null);
  const [joinSessionInput, setJoinSessionInput] = useState('');

  useEffect(() => {
    if (token) {
      localStorage.setItem('roam_token', token);
    } else {
      localStorage.removeItem('roam_token');
    }
  }, [token]);

  useEffect(() => {
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/notifications`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build();

    connection.on('TourStarting', (sessionId: string) => {
      setActiveSessionId(sessionId);
    });

    connection.on('TourEnded', () => {
      setActiveSessionId(null);
    });

    connection.on('TourRequestAccepted', (requestId: string, volunteerId: string) => {
      alert(`Tour request (${requestId}) accepted by volunteer ${volunteerId}!`);
    });

    connection.start()
      .then(() => {
        connection.invoke('JoinVolunteerGroup').catch(() => {});
      })
      .catch((err) => console.error("Notification hub connection error:", err));

    return () => {
      connection.stop();
    };
  }, [token]);

  const handleAuthSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthError('');
    const endpoint = isRegistering ? '/register' : '/login';

    try {
      const response = await fetch(`${API_BASE}${endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: authEmail, password: authPassword })
      });

      if (response.ok) {
        if (isRegistering) {
          setIsRegistering(false);
          alert('Registration successful! Please log in.');
        } else {
          const data = await response.json();
          if (data.accessToken) {
            setToken(data.accessToken);
          } else if (data.token) {
            setToken(data.token);
          }
        }
      } else {
        const errorText = await response.text();
        setAuthError(errorText || 'Authentication failed.');
      }
    } catch (err) {
      setAuthError(`Connection error: ${err}`);
    }
  };

  const handleLogout = () => {
    setToken('');
    setActiveSessionId(null);
  };

  const getAuthHeaders = (): HeadersInit => {
    return token ? { 'Authorization': `Bearer ${token}` } : {};
  };

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;

    setLoading(true);
    try {
      const response = await fetch(`${API_BASE}/api/v1/places/search?q=${encodeURIComponent(searchQuery)}`, {
        headers: getAuthHeaders()
      });
      if (response.ok) {
        const data = await response.json();
        setPlaces(data);
        setSelectedCoords(null);
      } else {
        console.error("Failed to fetch places");
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleRequestTourById = async (placeId: string) => {
    try {
      const response = await fetch(`${API_BASE}/api/v1/tour-requests`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...getAuthHeaders()
        },
        body: JSON.stringify({
          placeId: placeId,
          visibility: visibilityMode,
          mode: 0, // Immediate
          description: "Live tour request"
        })
      });

      if (response.ok) {
        const data = await response.json();
        alert("Tour requested successfully!");
        if (data && data.id) {
          // tour request created
        }
      } else {
        alert("Failed to request tour. Status: " + response.status);
      }
    } catch (err) {
      console.error(err);
      alert("Error requesting tour.");
    }
  };

  const handleRequestTourByCoords = async () => {
    if (!selectedCoords) return;
    try {
      const response = await fetch(`${API_BASE}/api/v1/tour-requests/coordinates`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...getAuthHeaders()
        },
        body: JSON.stringify({
          latitude: selectedCoords.lat,
          longitude: selectedCoords.lng,
          visibility: visibilityMode,
          mode: 0, // Immediate
          description: "Live tour request at selected coordinates."
        })
      });

      if (response.ok) {
        alert("Tour requested successfully at coordinates!");
        setSelectedCoords(null);
      } else {
        alert("Failed to request tour. Status: " + response.status);
      }
    } catch (err) {
      console.error(err);
      alert("Error requesting tour.");
    }
  };

  const handleLocationSelected = useCallback((lat: number, lng: number) => {
    setSelectedCoords({ lat, lng });
    setPlaces([]);
  }, []);

  return (
    <div style={{ position: 'relative', width: '100%', height: '100vh', overflow: 'hidden' }}>
      {activeSessionId ? (
        <div style={{ position: 'relative', width: '100%', height: '100vh', backgroundColor: '#fff', zIndex: 100 }}>
          <LiveSession 
            sessionId={activeSessionId} 
            token={token} 
            onLeaveSession={() => setActiveSessionId(null)} 
          />
        </div>
      ) : (
        <>
          <Map onLocationSelected={handleLocationSelected} />
          
          <div style={{
            position: 'absolute', top: 20, left: 20, zIndex: 10,
            backgroundColor: 'rgba(255, 255, 255, 0.95)', backdropFilter: 'blur(8px)',
            padding: '18px', borderRadius: '12px',
            boxShadow: '0 8px 24px rgba(0,0,0,0.15)', width: '320px', color: '#1a1a1a',
            fontFamily: 'system-ui, -apple-system, sans-serif'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
              <h1 style={{ fontSize: '1.4rem', margin: 0, fontWeight: 700, color: '#111' }}>Roam</h1>
              {token ? (
                <button 
                  onClick={handleLogout}
                  style={{ fontSize: '0.8rem', padding: '4px 8px', borderRadius: '4px', border: '1px solid #ccc', background: '#f5f5f5', cursor: 'pointer' }}
                >
                  Log Out
                </button>
              ) : null}
            </div>

            {!token ? (
              <div style={{ marginBottom: '15px', padding: '12px', background: '#f8f9fa', borderRadius: '8px', border: '1px solid #e9ecef' }}>
                <div style={{ fontSize: '0.9rem', fontWeight: 600, marginBottom: '8px' }}>
                  {isRegistering ? 'Create Account' : 'Sign In'}
                </div>
                <form onSubmit={handleAuthSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <input
                    type="email"
                    placeholder="Email"
                    value={authEmail}
                    onChange={(e) => setAuthEmail(e.target.value)}
                    required
                    style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontSize: '0.85rem' }}
                  />
                  <input
                    type="password"
                    placeholder="Password"
                    value={authPassword}
                    onChange={(e) => setAuthPassword(e.target.value)}
                    required
                    style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontSize: '0.85rem' }}
                  />
                  {authError && (
                    <div style={{ color: '#d13438', fontSize: '0.75rem' }}>{authError}</div>
                  )}
                  <button
                    type="submit"
                    style={{ padding: '8px', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 600 }}
                  >
                    {isRegistering ? 'Register' : 'Log In'}
                  </button>
                  <button
                    type="button"
                    onClick={() => { setIsRegistering(!isRegistering); setAuthError(''); }}
                    style={{ background: 'none', border: 'none', color: '#512BD4', cursor: 'pointer', fontSize: '0.75rem', textAlign: 'center' }}
                  >
                    {isRegistering ? 'Already have an account? Log In' : "Don't have an account? Register"}
                  </button>
                </form>
              </div>
            ) : null}

            {/* Session Join Control */}
            <div style={{ marginBottom: '15px', display: 'flex', gap: '6px' }}>
              <input
                type="text"
                placeholder="Live Session ID..."
                value={joinSessionInput}
                onChange={(e) => setJoinSessionInput(e.target.value)}
                style={{ flex: 1, padding: '7px', border: '1px solid #ccc', borderRadius: '4px', fontSize: '0.85rem' }}
              />
              <button
                type="button"
                onClick={() => { if (joinSessionInput.trim()) setActiveSessionId(joinSessionInput.trim()); }}
                style={{ padding: '7px 12px', backgroundColor: '#0f766e', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem' }}
              >
                Join
              </button>
            </div>

            <form onSubmit={handleSearch} style={{ display: 'flex', gap: '6px' }}>
              <input 
                type="text" 
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search for a place..."
                style={{ flex: 1, padding: '8px', border: '1px solid #ccc', borderRadius: '4px', fontSize: '0.85rem' }}
              />
              <button 
                type="submit" 
                disabled={loading} 
                style={{ padding: '8px 12px', cursor: 'pointer', backgroundColor: '#333', color: 'white', border: 'none', borderRadius: '4px', fontSize: '0.85rem' }}
              >
                {loading ? '...' : 'Search'}
              </button>
            </form>

            <div style={{ marginTop: '10px', display: 'flex', alignItems: 'center', gap: '8px', fontSize: '0.8rem' }}>
              <label>Visibility:</label>
              <select 
                value={visibilityMode} 
                onChange={(e) => setVisibilityMode(Number(e.target.value))}
                style={{ padding: '4px', borderRadius: '4px', border: '1px solid #ccc' }}
              >
                <option value={1}>Group (Public)</option>
                <option value={0}>Private (1-on-1)</option>
              </select>
            </div>

            {places.length > 0 && (
              <ul style={{ listStyle: 'none', padding: 0, margin: '12px 0 0 0', maxHeight: '250px', overflowY: 'auto' }}>
                {places.map((place, i) => (
                  <li key={i} style={{ padding: '10px', borderBottom: '1px solid #eee', color: '#333' }}>
                    <strong style={{ display: 'block', fontSize: '0.9rem' }}>{place.name}</strong>
                    <small style={{ color: '#666' }}>{place.description}</small>
                    {place.id && (
                      <button 
                        onClick={() => handleRequestTourById(place.id!)} 
                        style={{ marginTop: '6px', display: 'block', width: '100%', padding: '6px 10px', cursor: 'pointer', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px', fontSize: '0.8rem' }}
                      >
                        Request Tour
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            )}

            {selectedCoords && (
              <div style={{ marginTop: '12px', padding: '10px', backgroundColor: '#f9f9f9', borderRadius: '6px', border: '1px solid #eee' }}>
                <strong style={{ fontSize: '0.85rem' }}>Selected Location</strong>
                <p style={{ margin: '4px 0', fontSize: '0.8rem', color: '#555' }}>
                  Lat: {selectedCoords.lat.toFixed(4)}<br/>
                  Lng: {selectedCoords.lng.toFixed(4)}
                </p>
                <button 
                  onClick={handleRequestTourByCoords} 
                  style={{ width: '100%', padding: '7px', cursor: 'pointer', backgroundColor: '#512BD4', color: 'white', border: 'none', borderRadius: '4px', fontSize: '0.85rem' }}
                >
                  Request Tour Here
                </button>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}

export default App;
