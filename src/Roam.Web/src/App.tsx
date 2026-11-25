import React, { useState, useCallback, useEffect } from 'react';
import Map from './components/Map';
import LiveSession from './components/LiveSession';
import VolunteerDashboard from './components/VolunteerDashboard';
import MyTours from './components/MyTours';
import UserProfileModal, { type UserProfile } from './components/UserProfileModal';
import AuthPanel from './components/AuthPanel';
import AdminPanel from './components/AdminPanel';
import { API_BASE } from './config';
import * as signalR from '@microsoft/signalr';
import { TokenManager } from './auth/tokenManager';
import { hubConnectionOptions } from './auth/hubConnection';
import { AgeCategory, VolunteerStatus } from './utils/enums';
import './App.css';

interface Place {
  id?: string;
  name: string;
  description: string;
  latitude: number;
  longitude: number;
}

const tokenManager = new TokenManager(API_BASE, sessionStorage, undefined, undefined, localStorage);

export function App() {
  // Auth state is owned by tokenManager; this mirrors "signed in or not" for rendering.
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => tokenManager.isAuthenticated);

  // User Profile & Safeguarding
  const [userProfile, setUserProfile] = useState<UserProfile | null>(null);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);
  const [profileError, setProfileError] = useState('');
  const [profileReloadKey, setProfileReloadKey] = useState(0);

  // Active Tab
  const [activeTab, setActiveTab] = useState<'explore' | 'volunteer' | 'admin'>('explore');

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

  // Bumped whenever tour state may have changed (hub notifications, new requests) so tour lists reload.
  const [toursRefreshKey, setToursRefreshKey] = useState(0);
  const refreshTours = useCallback(() => setToursRefreshKey((k) => k + 1), []);

  // Live Session
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null);
  const [joinSessionInput, setJoinSessionInput] = useState('');
  const [incomingSessionAlert, setIncomingSessionAlert] = useState<{ sessionId: string; message: string } | null>(null);

  useEffect(() => {
    return tokenManager.subscribe(() => {
      const signedIn = tokenManager.isAuthenticated;
      setIsAuthenticated(signedIn);
      if (!signedIn) {
        // Signed out explicitly, or the refresh token was rejected (expired / account suspended).
        setUserProfile(null);
        setActiveSessionId(null);
      }
    });
  }, []);

  const handleLogout = useCallback(() => {
    tokenManager.clear();
  }, []);

  const authFetch = useCallback(
    (url: string, options: RequestInit = {}): Promise<Response> => tokenManager.authFetch(url, options),
    [],
  );

  const getAccessToken = useCallback(() => tokenManager.getValidAccessToken(), []);

  // Reload the profile (e.g. after onboarding or a volunteer application).
  const fetchUserProfile = useCallback(() => setProfileReloadKey((k) => k + 1), []);

  // Load the profile, retrying transient failures (network errors, 5xx, rate limiting) with backoff so a
  // brief server restart does not leave the app without a profile until the page is reloaded.
  useEffect(() => {
    if (!isAuthenticated) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    // About a minute in total: long enough to ride out an API restart during an update.
    const delays = [1000, 2000, 4000, 8000, 15000, 30000];

    const attempt = async (n: number) => {
      try {
        const response = await authFetch(`${API_BASE}/api/v1/profile/me`);
        if (cancelled) return;
        if (response.ok) {
          setUserProfile(await response.json());
          setProfileError('');
          return;
        }
        if (response.status < 500 && response.status !== 429) {
          setProfileError(`Could not load your profile (HTTP ${response.status}).`);
          return;
        }
      } catch {
        if (cancelled) return;
      }
      if (n < delays.length) {
        timer = setTimeout(() => attempt(n + 1), delays[n]);
      } else {
        setProfileError('Could not load your profile. The server may be restarting.');
      }
    };

    attempt(0);
    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [isAuthenticated, authFetch, profileReloadKey]);

  // SignalR Notification Hub. Lives for the whole signed-in period: the token factory asks for a fresh
  // token on every (re)connect, so routine token refreshes do not tear the connection down.
  useEffect(() => {
    if (!isAuthenticated) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/notifications`, hubConnectionOptions(getAccessToken))
      .withAutomaticReconnect()
      .build();

    connection.on('TourStarting', (sessionId: string) => {
      refreshTours();
      setIncomingSessionAlert({
        sessionId,
        message: 'A volunteer has launched your tour! The live stream is ready.',
      });
    });

    // The live session view handles its own end-of-tour flow (feedback); here just update lists/alerts.
    connection.on('TourEnded', () => {
      refreshTours();
      setIncomingSessionAlert(null);
    });

    connection.on('TourRequestAccepted', (requestId: string) => {
      refreshTours();
      setRequestFeedback(`Tour request (${requestId.substring(0, 8)}...) was accepted! Preparing live session...`);
    });

    connection.on('TourRequestVolunteerWithdrawn', (requestId: string) => {
      refreshTours();
      setRequestFeedback(`The volunteer for request ${requestId.substring(0, 8)}... is no longer available. Your request is open to other volunteers again.`);
    });

    connection.on('TourRequestCancelled', (requestId: string) => {
      refreshTours();
      setRequestFeedback(`Tour request ${requestId.substring(0, 8)}... was cancelled.`);
    });

    connection.start()
      .then(() => {
        connection.invoke('JoinVolunteerGroup').catch(() => {});
      })
      .catch((err) => console.error('Notification hub connection error:', err));

    return () => {
      connection.stop();
    };
  }, [isAuthenticated, getAccessToken, refreshTours]);

  const handleSignedIn = useCallback(
    (tokens: unknown) => tokenManager.setTokens(tokens as Parameters<typeof tokenManager.setTokens>[0]),
    [],
  );

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
    if (!isAuthenticated) {
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
        refreshTours();
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
    if (!isAuthenticated) {
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
        refreshTours();
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

  const isApprovedVolunteer = userProfile?.volunteerStatus === VolunteerStatus.Approved;
  const isAdmin = !!userProfile?.isAdmin;
  // Never leave a signed-out (or demoted) user on the admin tab.
  const visibleTab = activeTab === 'admin' && !isAdmin ? 'explore' : activeTab;

  return (
    <div style={{ position: 'relative', width: '100%', height: '100vh', overflow: 'hidden' }}>
      {/* Live Session Full-Screen Mode */}
      {activeSessionId ? (
        <div style={{ position: 'relative', width: '100%', height: '100vh', backgroundColor: '#0b0f19', zIndex: 100 }}>
          <LiveSession
            sessionId={activeSessionId}
            getAccessToken={getAccessToken}
            authFetch={authFetch}
            currentUserId={userProfile?.id}
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

                {isAuthenticated ? (
                  <div style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
                    <button
                      onClick={() => setIsProfileModalOpen(true)}
                      className="roam-btn-secondary"
                      style={{ fontSize: '0.75rem', padding: '5px 10px' }}
                      title="View Profile & Safeguarding"
                    >
                      <span style={{ display: 'inline-block', maxWidth: '120px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', verticalAlign: 'bottom' }}>
                        👤 {userProfile?.displayName || 'Profile'}
                      </span>
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
                  className={`roam-tab-btn ${visibleTab === 'explore' ? 'active' : ''}`}
                  onClick={() => setActiveTab('explore')}
                >
                  🗺️ Explore
                </button>
                <button
                  type="button"
                  className={`roam-tab-btn ${visibleTab === 'volunteer' ? 'active' : ''}`}
                  onClick={() => setActiveTab('volunteer')}
                >
                  🚀 Volunteer
                </button>
                {isAdmin && (
                  <button
                    type="button"
                    className={`roam-tab-btn ${visibleTab === 'admin' ? 'active' : ''}`}
                    onClick={() => setActiveTab('admin')}
                  >
                    🛡️ Admin
                  </button>
                )}
              </div>
            </div>

            {/* Panel Body */}
            <div className="roam-panel-body">
              {/* Unauthenticated View */}
              {!isAuthenticated && <AuthPanel apiBase={API_BASE} onSignedIn={handleSignedIn} />}

              {/* Profile could not be loaded (after retries) */}
              {isAuthenticated && profileError && (
                <div role="alert" style={{ padding: '10px 12px', backgroundColor: '#450a0a', border: '1px solid #7f1d1d', borderRadius: '8px', color: '#fca5a5', fontSize: '0.8rem', display: 'flex', gap: '8px', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span>{profileError}</span>
                  <button type="button" onClick={() => { setProfileError(''); fetchUserProfile(); }} className="roam-btn-secondary" style={{ fontSize: '0.75rem', padding: '4px 10px' }}>
                    Retry
                  </button>
                </div>
              )}

              {/* Signed in but not age-verified yet: point to the one step that unlocks the app */}
              {isAuthenticated && userProfile && userProfile.ageCategory !== AgeCategory.Adult && (
                <div style={{ padding: '10px 12px', backgroundColor: '#1e1b4b', border: '1px solid #4338ca', borderRadius: '8px', color: '#c7d2fe', fontSize: '0.8rem', display: 'flex', gap: '8px', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span>Finish your profile (18+ check) to request or host tours.</span>
                  <button type="button" onClick={() => setIsProfileModalOpen(true)} className="roam-btn-primary" style={{ fontSize: '0.75rem', padding: '5px 10px', whiteSpace: 'nowrap' }}>
                    Finish profile
                  </button>
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
              {visibleTab === 'explore' && (
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

                  {/* The requester's own open requests: cancel, or join once live */}
                  {isAuthenticated && (
                    <MyTours
                      apiBase={API_BASE}
                      authFetch={authFetch}
                      role="Requester"
                      title="Your Tour Requests"
                      refreshKey={toursRefreshKey}
                      onJoinSession={(sessionId) => setActiveSessionId(sessionId)}
                    />
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
              {visibleTab === 'volunteer' && (
                <VolunteerDashboard
                  apiBase={API_BASE}
                  authFetch={authFetch}
                  isApprovedVolunteer={isApprovedVolunteer}
                  onStartSession={(sessionId) => setActiveSessionId(sessionId)}
                  onApplyVolunteer={() => setIsProfileModalOpen(true)}
                  refreshKey={toursRefreshKey}
                />
              )}

              {/* TAB 3: ADMIN (administrators only; the API enforces this too) */}
              {visibleTab === 'admin' && <AdminPanel apiBase={API_BASE} authFetch={authFetch} />}

              <div style={{ marginTop: 'auto', paddingTop: '8px', fontSize: '0.7rem', color: '#64748b', textAlign: 'center' }}>
                <a href="/privacy.html" target="_blank" rel="noopener" style={{ color: '#94a3b8' }}>Privacy</a>
                {' · '}
                <a href="/terms.html" target="_blank" rel="noopener" style={{ color: '#94a3b8' }}>Terms</a>
              </div>
            </div>
          </div>

          {/* User Profile & Safeguarding Modal */}
          {isProfileModalOpen && (
            <UserProfileModal
              apiBase={API_BASE}
              authFetch={authFetch}
              profile={userProfile}
              onProfileUpdated={fetchUserProfile}
              onAccountDeleted={() => { setIsProfileModalOpen(false); handleLogout(); }}
              onClose={() => setIsProfileModalOpen(false)}
            />
          )}
        </>
      )}
    </div>
  );
}

export default App;
