import React, { useState, useCallback, useEffect } from 'react';
import Map from './components/Map';
import LiveSession from './components/LiveSession';
import VolunteerDashboard from './components/VolunteerDashboard';
import UserProfileModal, { type UserProfile } from './components/UserProfileModal';
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

export function App() {
  const [token, setToken] = useState<string>(() => sessionStorage.getItem('roam_token') || '');
  const [refreshToken, setRefreshToken] = useState<string>(() => sessionStorage.getItem('roam_refresh_token') || '');
  const [authEmail, setAuthEmail] = useState('');
  const [authPassword, setAuthPassword] = useState('');
  const [isRegistering, setIsRegistering] = useState(false);
  const [authError, setAuthError] = useState('');

  // User Profile & Safeguarding
  const [userProfile, setUserProfile] = useState<UserProfile | null>(null);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);

  // Active Tab
  const [activeTab, setActiveTab] = useState<'explore' | 'volunteer'>('explore');

  // Search & Places
  const [searchQuery, setSearchQuery] = useState('');
  const [places, setPlaces] = useState<Place[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchError, setSearchError] = useState('');

  // Tour Request Options
  const [selectedCoords, setSelectedCoords] = useState<{ lat: number; lng: number } | null>(null);
  const [requestDescription, setRequestDescription] = useState('');
  const [visibilityMode, setVisibilityMode] = useState<number>(0); // 0 = Private, 1 = Group
  const [tourMode, setTourMode] = useState<number>(0); // 0 = Instant, 1 = Scheduled
  const [submittingRequest, setSubmittingRequest] = useState(false);
  const [requestFeedback, setRequestFeedback] = useState<string | null>(null);

  // Live Session
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null);
  const [joinSessionInput, setJoinSessionInput] = useState('');
  const [incomingSessionAlert, setIncomingSessionAlert] = useState<{ sessionId: string; message: string } | null>(null);

  // Save auth tokens in sessionStorage (avoid long-lived persistent localStorage)
  useEffect(() => {
    // Purge legacy tokens from localStorage if present
    localStorage.removeItem('roam_token');
    localStorage.removeItem('roam_refresh_token');

    if (token) {
      sessionStorage.setItem('roam_token', token);
    } else {
      sessionStorage.removeItem('roam_token');
    }
  }, [token]);

  useEffect(() => {
    if (refreshToken) {
      sessionStorage.setItem('roam_refresh_token', refreshToken);
    } else {
      sessionStorage.removeItem('roam_refresh_token');
    }
  }, [refreshToken]);

  const handleLogout = useCallback(() => {
    setToken('');
    setRefreshToken('');
    setUserProfile(null);
    sessionStorage.removeItem('roam_token');
    sessionStorage.removeItem('roam_refresh_token');
    localStorage.removeItem('roam_token');
    localStorage.removeItem('roam_refresh_token');
    setActiveSessionId(null);
  }, []);

  const refreshAuthToken = useCallback(async (): Promise<string | null> => {
    const currentRefresh = refreshToken || sessionStorage.getItem('roam_refresh_token');
    if (!currentRefresh) {
      handleLogout();
      return null;
    }
    try {
      const response = await fetch(`${API_BASE}/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: currentRefresh }),
      });
      if (response.ok) {
        const data = await response.json();
        const newToken = data.accessToken || data.token;
        const newRefresh = data.refreshToken;
        if (newToken) {
          setToken(newToken);
          sessionStorage.setItem('roam_token', newToken);
        }
        if (newRefresh) {
          setRefreshToken(newRefresh);
          sessionStorage.setItem('roam_refresh_token', newRefresh);
        }
        return newToken || null;
      } else {
        handleLogout();
        return null;
      }
    } catch {
      handleLogout();
      return null;
    }
  }, [refreshToken, handleLogout]);

  const authFetch = useCallback(async (url: string, options: RequestInit = {}): Promise<Response> => {
    const headers = new Headers(options.headers || {});
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
    let response = await fetch(url, { ...options, headers });
    if (response.status === 401 && (refreshToken || sessionStorage.getItem('roam_refresh_token'))) {
      const newToken = await refreshAuthToken();
      if (newToken) {
        headers.set('Authorization', `Bearer ${newToken}`);
        response = await fetch(url, { ...options, headers });
      }
    }
    return response;
  }, [token, refreshToken, refreshAuthToken]);

  // Fetch User Profile
  const fetchUserProfile = useCallback(async () => {
    if (!token) return;
    try {
      const response = await authFetch(`${API_BASE}/api/v1/profile/me`);
      if (response.ok) {
        const profile = await response.json();
        setUserProfile(profile);
      }
    } catch (err) {
      console.error('Failed to load user profile:', err);
    }
  }, [token, authFetch]);

  useEffect(() => {
    if (!token) return;
    let ignore = false;
    const load = async () => {
      try {
        const response = await authFetch(`${API_BASE}/api/v1/profile/me`);
        if (response.ok && !ignore) {
          const profile = await response.json();
          setUserProfile(profile);
        }
      } catch (err) {
        console.error('Failed to load user profile:', err);
      }
    };
    load();
    return () => {
      ignore = true;
    };
  }, [token, authFetch]);

  // SignalR Notification Hub
  useEffect(() => {
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/notifications`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .build();

    connection.on('TourStarting', (sessionId: string) => {
      setIncomingSessionAlert({
        sessionId,
        message: 'A volunteer has launched your tour! The live stream is ready.',
      });
    });

    connection.on('TourEnded', () => {
      setActiveSessionId(null);
      setIncomingSessionAlert(null);
    });

    connection.on('TourRequestAccepted', (requestId: string) => {
      setRequestFeedback(`Tour request (${requestId.substring(0, 8)}...) was accepted! Preparing live session...`);
    });

    connection.start()
      .then(() => {
        connection.invoke('JoinVolunteerGroup').catch(() => {});
      })
      .catch((err) => console.error('Notification hub connection error:', err));

    return () => {
      connection.stop();
    };
  }, [token]);

  // Authentication submission
  const handleAuthSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthError('');
    const endpoint = isRegistering ? '/register' : '/login';

    try {
      const response = await fetch(`${API_BASE}${endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: authEmail, password: authPassword }),
      });

      if (response.ok) {
        if (isRegistering) {
          setIsRegistering(false);
          alert('Registration successful! Please log in to complete adult safeguarding verification.');
        } else {
          const data = await response.json();
          const receivedToken = data.accessToken || data.token;
          if (receivedToken) setToken(receivedToken);
          if (data.refreshToken) setRefreshToken(data.refreshToken);
        }
      } else {
        const errorText = await response.text();
        setAuthError(errorText || 'Authentication failed.');
      }
    } catch (err) {
      setAuthError(`Connection error: ${err}`);
    }
  };

  // Search Places
  const executeSearch = async (query: string) => {
    if (!query.trim()) return;
    setLoading(true);
    setSearchError('');
    try {
      const response = await authFetch(`${API_BASE}/api/v1/places/search?q=${encodeURIComponent(query.trim())}`);
      if (response.ok) {
        const data = await response.json();
        setPlaces(Array.isArray(data) ? data : []);
        if (Array.isArray(data) && data.length === 0) {
          setSearchError(`No places found for '${query}'. Try another search or select map coordinates.`);
        }
      } else {
        setSearchError('Search failed. Please try again.');
      }
    } catch (err) {
      setSearchError(`Search error: ${err}`);
    } finally {
      setLoading(false);
    }
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    executeSearch(searchQuery);
  };

  // Request tour by Place ID
  const handleRequestTourById = async (placeId: string, placeName: string) => {
    if (!token) {
      alert('Please sign in to submit a tour request.');
      return;
    }
    const description = requestDescription.trim() || `Live tour exploration of ${placeName}`;
    setSubmittingRequest(true);
    setRequestFeedback(null);

    try {
      const response = await authFetch(`${API_BASE}/api/v1/tour-requests`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          placeId,
          visibility: visibilityMode,
          mode: tourMode,
          description,
        }),
      });

      if (response.ok) {
        setRequestFeedback(`Tour request for "${placeName}" broadcasted! Nearby volunteers have been notified.`);
        setRequestDescription('');
      } else {
        const err = await response.json().catch(() => ({}));
        alert(err.error || `Failed to submit request (HTTP ${response.status}). Ensure you are age-verified.`);
      }
    } catch (err) {
      alert(`Error submitting tour request: ${err}`);
    } finally {
      setSubmittingRequest(false);
    }
  };

  // Request tour by Map Coordinates
  const handleRequestTourByCoords = async () => {
    if (!token) {
      alert('Please sign in to submit a tour request.');
      return;
    }
    if (!selectedCoords) return;
    const description = requestDescription.trim() || `Live tour exploration at (${selectedCoords.lat.toFixed(4)}, ${selectedCoords.lng.toFixed(4)})`;
    setSubmittingRequest(true);
    setRequestFeedback(null);

    try {
      const response = await authFetch(`${API_BASE}/api/v1/tour-requests/coordinates`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          latitude: selectedCoords.lat,
          longitude: selectedCoords.lng,
          visibility: visibilityMode,
          mode: tourMode,
          description,
        }),
      });

      if (response.ok) {
        setRequestFeedback(`Tour request broadcasted at (${selectedCoords.lat.toFixed(4)}, ${selectedCoords.lng.toFixed(4)})!`);
        setRequestDescription('');
        setSelectedCoords(null);
      } else {
        const err = await response.json().catch(() => ({}));
        alert(err.error || `Failed to submit request (HTTP ${response.status}). Ensure you are age-verified.`);
      }
    } catch (err) {
      alert(`Error submitting tour request: ${err}`);
    } finally {
      setSubmittingRequest(false);
    }
  };

  const handleLocationSelected = useCallback((lat: number, lng: number) => {
    setSelectedCoords({ lat, lng });
    setPlaces([]);
    setRequestFeedback(null);
  }, []);

  const isApprovedVolunteer = userProfile?.volunteerStatus === 2;

  return (
    <div style={{ position: 'relative', width: '100%', height: '100vh', overflow: 'hidden' }}>
      {/* Live Session Full-Screen Mode */}
      {activeSessionId ? (
        <div style={{ position: 'relative', width: '100%', height: '100vh', backgroundColor: '#0b0f19', zIndex: 100 }}>
          <LiveSession
            sessionId={activeSessionId}
            token={token}
            onLeaveSession={() => setActiveSessionId(null)}
          />
        </div>
      ) : (
        <>
          {/* Background Map View */}
          <Map onLocationSelected={handleLocationSelected} />

          {/* Incoming Session Prompt Banner */}
          {incomingSessionAlert && (
            <div style={{
              position: 'absolute',
              top: 16,
              left: '50%',
              transform: 'translateX(-50%)',
              zIndex: 50,
              backgroundColor: '#064e3b',
              border: '1px solid #10b981',
              borderRadius: '12px',
              padding: '12px 20px',
              color: '#ffffff',
              boxShadow: '0 12px 30px rgba(0,0,0,0.4)',
              display: 'flex',
              alignItems: 'center',
              gap: '14px',
              maxWidth: '90%',
            }}>
              <span style={{ fontSize: '1.4rem' }}>⚡</span>
              <div>
                <div style={{ fontWeight: 600, fontSize: '0.9rem' }}>Live Exploration Starting!</div>
                <div style={{ fontSize: '0.78rem', color: '#a7f3d0' }}>{incomingSessionAlert.message}</div>
              </div>
              <button
                onClick={() => {
                  setActiveSessionId(incomingSessionAlert.sessionId);
                  setIncomingSessionAlert(null);
                }}
                style={{
                  padding: '8px 16px',
                  backgroundColor: '#10b981',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  fontSize: '0.85rem',
                }}
              >
                Join Stream
              </button>
            </div>
          )}

          {/* Main Floating Glassmorphic Panel */}
          <div className="roam-panel">
            {/* Header */}
            <div className="roam-panel-header">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <h1 style={{ fontSize: '1.35rem', margin: 0, fontWeight: 800, color: '#f8fafc', letterSpacing: '-0.02em' }}>
                    Roam
                  </h1>
                  <span style={{ fontSize: '0.72rem', color: '#94a3b8' }}>Human-Powered Exploration</span>
                </div>

                {token ? (
                  <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                    <button
                      onClick={() => setIsProfileModalOpen(true)}
                      className="roam-btn-secondary"
                      style={{ fontSize: '0.75rem', padding: '5px 10px' }}
                      title="View Profile & Safeguarding"
                    >
                      👤 {userProfile?.displayName || 'Profile'}
                    </button>
                    <button
                      onClick={handleLogout}
                      className="roam-btn-secondary"
                      style={{ fontSize: '0.75rem', padding: '5px 10px' }}
                    >
                      Logout
                    </button>
                  </div>
                ) : null}
              </div>

              {/* Navigation Tabs */}
              <div className="roam-tabs">
                <button
                  type="button"
                  className={`roam-tab-btn ${activeTab === 'explore' ? 'active' : ''}`}
                  onClick={() => setActiveTab('explore')}
                >
                  🗺️ Explore &amp; Request
                </button>
                <button
                  type="button"
                  className={`roam-tab-btn ${activeTab === 'volunteer' ? 'active' : ''}`}
                  onClick={() => setActiveTab('volunteer')}
                >
                  🚀 Volunteer Hub
                </button>
              </div>
            </div>

            {/* Panel Body */}
            <div className="roam-panel-body">
              {/* Unauthenticated View */}
              {!token && (
                <div style={{
                  padding: '16px',
                  backgroundColor: '#0b0f19',
                  borderRadius: '10px',
                  border: '1px solid #1e293b',
                }}>
                  <div style={{ fontSize: '0.9rem', fontWeight: 600, marginBottom: '10px', color: '#f8fafc' }}>
                    {isRegistering ? 'Create Your Explorer Account' : 'Sign In to Roam'}
                  </div>
                  <form onSubmit={handleAuthSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    <input
                      type="email"
                      placeholder="Email Address"
                      value={authEmail}
                      onChange={(e) => setAuthEmail(e.target.value)}
                      required
                      className="roam-input"
                    />
                    <input
                      type="password"
                      placeholder="Password"
                      value={authPassword}
                      onChange={(e) => setAuthPassword(e.target.value)}
                      required
                      className="roam-input"
                    />
                    {authError && (
                      <div style={{ color: '#f87171', fontSize: '0.75rem', lineHeight: '1.4' }}>{authError}</div>
                    )}
                    <button type="submit" className="roam-btn-primary">
                      {isRegistering ? 'Register' : 'Sign In'}
                    </button>
                    <button
                      type="button"
                      onClick={() => { setIsRegistering(!isRegistering); setAuthError(''); }}
                      style={{ background: 'none', border: 'none', color: '#a5b4fc', cursor: 'pointer', fontSize: '0.78rem', textAlign: 'center' }}
                    >
                      {isRegistering ? 'Already have an account? Sign In' : "Don't have an account? Register"}
                    </button>
                  </form>
                </div>
              )}

              {/* Direct Session Joiner */}
              <div style={{ display: 'flex', gap: '6px' }}>
                <input
                  type="text"
                  placeholder="Direct Session ID..."
                  value={joinSessionInput}
                  onChange={(e) => setJoinSessionInput(e.target.value)}
                  className="roam-input"
                  style={{ flex: 1 }}
                />
                <button
                  type="button"
                  onClick={() => { if (joinSessionInput.trim()) setActiveSessionId(joinSessionInput.trim()); }}
                  className="roam-btn-primary"
                  style={{ whiteSpace: 'nowrap' }}
                >
                  Join
                </button>
              </div>

              {/* TAB 1: EXPLORE & REQUEST */}
              {activeTab === 'explore' && (
                <>
                  {/* Search Form */}
                  <form onSubmit={handleSearchSubmit} style={{ display: 'flex', gap: '6px' }}>
                    <input
                      type="text"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder="Search landmarks, cities, museums..."
                      className="roam-input"
                      style={{ flex: 1 }}
                    />
                    <button
                      type="submit"
                      disabled={loading}
                      className="roam-btn-primary"
                    >
                      {loading ? '...' : 'Search'}
                    </button>
                  </form>

                  {/* Discovery Category Chips */}
                  <div className="roam-chips-scroll">
                    {['Landmarks', 'Parks', 'Museum', 'Beach', 'Castle', 'Historic'].map((category) => (
                      <button
                        key={category}
                        type="button"
                        onClick={() => {
                          setSearchQuery(category);
                          executeSearch(category);
                        }}
                        className="roam-chip-btn"
                      >
                        {category}
                      </button>
                    ))}
                  </div>

                  {searchError && (
                    <div style={{ color: '#f87171', fontSize: '0.78rem' }}>{searchError}</div>
                  )}

                  {/* Tour Request Options (Privacy & Mode) */}
                  <div style={{
                    padding: '12px',
                    backgroundColor: '#0b0f19',
                    borderRadius: '10px',
                    border: '1px solid #1e293b',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '10px',
                  }}>
                    <div style={{ fontSize: '0.8rem', fontWeight: 600, color: '#cbd5e1' }}>
                      Exploration Preferences
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <select
                        value={visibilityMode}
                        onChange={(e) => setVisibilityMode(Number(e.target.value))}
                        className="roam-input"
                        style={{ flex: 1, padding: '7px' }}
                      >
                        <option value={0}>🔒 Private (1-on-1)</option>
                        <option value={1}>👥 Public Group</option>
                      </select>
                      <select
                        value={tourMode}
                        onChange={(e) => setTourMode(Number(e.target.value))}
                        className="roam-input"
                        style={{ flex: 1, padding: '7px' }}
                      >
                        <option value={0}>⚡ Instant Live</option>
                        <option value={1}>📅 Scheduled</option>
                      </select>
                    </div>

                    <input
                      type="text"
                      placeholder="Special instructions (e.g. show the ancient arch)"
                      value={requestDescription}
                      onChange={(e) => setRequestDescription(e.target.value)}
                      className="roam-input"
                      maxLength={2000}
                    />
                  </div>

                  {/* Request Feedback Alert */}
                  {requestFeedback && (
                    <div style={{
                      padding: '10px 14px',
                      backgroundColor: '#064e3b',
                      border: '1px solid #10b981',
                      borderRadius: '8px',
                      color: '#a7f3d0',
                      fontSize: '0.82rem',
                    }}>
                      {requestFeedback}
                    </div>
                  )}

                  {/* Selected Map Coordinates Card */}
                  {selectedCoords && (
                    <div style={{
                      padding: '12px',
                      backgroundColor: '#1e293b',
                      borderRadius: '10px',
                      border: '1px solid #4338ca',
                    }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                        <span style={{ fontWeight: 600, fontSize: '0.85rem', color: '#a5b4fc' }}>📍 Selected Coordinates</span>
                        <button
                          onClick={() => setSelectedCoords(null)}
                          style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', fontSize: '1rem' }}
                        >
                          ×
                        </button>
                      </div>
                      <div style={{ fontSize: '0.78rem', color: '#cbd5e1', marginBottom: '8px' }}>
                        Lat: {selectedCoords.lat.toFixed(4)}, Lng: {selectedCoords.lng.toFixed(4)}
                      </div>
                      <button
                        onClick={handleRequestTourByCoords}
                        disabled={submittingRequest}
                        className="roam-btn-primary"
                        style={{ width: '100%' }}
                      >
                        {submittingRequest ? 'Broadcasting...' : 'Request Tour at Selected Point'}
                      </button>
                    </div>
                  )}

                  {/* Search Results List */}
                  {places.length > 0 && (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
                        Discovered Places ({places.length})
                      </span>
                      {places.map((place, idx) => (
                        <div
                          key={place.id || idx}
                          style={{
                            padding: '12px',
                            backgroundColor: '#0b0f19',
                            borderRadius: '10px',
                            border: '1px solid #1e293b',
                            display: 'flex',
                            flexDirection: 'column',
                            gap: '6px',
                          }}
                        >
                          <div style={{ fontWeight: 600, fontSize: '0.88rem', color: '#f8fafc' }}>
                            {place.name}
                          </div>
                          <div style={{ fontSize: '0.75rem', color: '#94a3b8', lineHeight: '1.4' }}>
                            {place.description}
                          </div>
                          <div style={{ fontSize: '0.72rem', color: '#64748b' }}>
                            Coords: {place.latitude.toFixed(4)}, {place.longitude.toFixed(4)}
                          </div>
                          {place.id && (
                            <button
                              onClick={() => handleRequestTourById(place.id!, place.name)}
                              disabled={submittingRequest}
                              className="roam-btn-primary"
                              style={{ marginTop: '4px', fontSize: '0.8rem', padding: '6px 10px' }}
                            >
                              Request Live Tour
                            </button>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </>
              )}

              {/* TAB 2: VOLUNTEER DISPATCH HUB */}
              {activeTab === 'volunteer' && (
                <VolunteerDashboard
                  apiBase={API_BASE}
                  authFetch={authFetch}
                  isApprovedVolunteer={isApprovedVolunteer}
                  onStartSession={(sessionId) => setActiveSessionId(sessionId)}
                  onApplyVolunteer={() => setIsProfileModalOpen(true)}
                />
              )}
            </div>
          </div>

          {/* User Profile & Safeguarding Modal */}
          {isProfileModalOpen && (
            <UserProfileModal
              apiBase={API_BASE}
              authFetch={authFetch}
              profile={userProfile}
              onProfileUpdated={fetchUserProfile}
              onClose={() => setIsProfileModalOpen(false)}
            />
          )}
        </>
      )}
    </div>
  );
}

export default App;
