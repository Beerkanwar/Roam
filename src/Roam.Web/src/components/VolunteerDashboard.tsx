import React, { useState, useEffect, useCallback } from 'react';

interface PlaceInfo {
  id: string;
  name: string;
  description: string;
  latitude: number;
  longitude: number;
}

interface IncomingTourRequest {
  id: string;
  description: string;
  mode: number; // 0 = Instant, 1 = Scheduled
  visibility: number; // 0 = Private, 1 = Group
  place: PlaceInfo | null;
}

interface VolunteerDashboardProps {
  apiBase: string;
  authFetch: (url: string, options?: RequestInit) => Promise<Response>;
  isApprovedVolunteer: boolean;
  onStartSession: (sessionId: string) => void;
  onApplyVolunteer: () => void;
}

export const VolunteerDashboard: React.FC<VolunteerDashboardProps> = ({
  apiBase,
  authFetch,
  isApprovedVolunteer,
  onStartSession,
  onApplyVolunteer,
}) => {
  const [availableForRequests, setAvailableForRequests] = useState<boolean>(true);
  const [radiusKm, setRadiusKm] = useState<number>(10);
  const [gpsLocation, setGpsLocation] = useState<{ lat: number; lng: number } | null>(null);
  const [gpsStatus, setGpsStatus] = useState<string>('Location not synced');
  const [requests, setRequests] = useState<IncomingTourRequest[]>([]);
  const [loadingRequests, setLoadingRequests] = useState<boolean>(false);
  const [actionError, setActionError] = useState<string>('');
  const [actionSuccess, setActionSuccess] = useState<string>('');

  // 1. Sync GPS location to backend
  const syncLocation = useCallback(async (lat: number, lng: number) => {
    try {
      setGpsStatus(`Syncing (${lat.toFixed(4)}, ${lng.toFixed(4)})...`);
      const response = await authFetch(`${apiBase}/api/v1/profile/location`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ latitude: lat, longitude: lng }),
      });
      if (response.ok) {
        setGpsLocation({ lat, lng });
        setGpsStatus(`Synced: ${lat.toFixed(4)}, ${lng.toFixed(4)}`);
      } else {
        setGpsStatus('Failed to sync location to server');
      }
    } catch (err) {
      setGpsStatus(`Location sync error: ${err}`);
    }
  }, [apiBase, authFetch]);

  // Request browser geolocation
  const handleGetLocation = useCallback(() => {
    if (!navigator.geolocation) {
      setGpsStatus('Geolocation not supported by your browser');
      return;
    }
    setGpsStatus('Acquiring device GPS...');
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        syncLocation(pos.coords.latitude, pos.coords.longitude);
      },
      (err) => {
        setGpsStatus(`GPS error: ${err.message}`);
      },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }, [syncLocation]);

  // 2. Toggle volunteer availability
  const handleToggleAvailability = async (newVal: boolean) => {
    try {
      setAvailableForRequests(newVal);
      const payload = {
        enabled: true,
        availableForRequests: newVal,
        acceptsPrivateTours: true,
        acceptsGroupTours: true,
        maxConcurrentViewers: 10,
        languages: 'English',
        tipEnabled: false,
      };
      const response = await authFetch(`${apiBase}/api/v1/profile/volunteer-settings`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!response.ok) {
        setAvailableForRequests(!newVal);
        setActionError('Failed to update availability status.');
      } else {
        setActionError('');
      }
    } catch (err) {
      setAvailableForRequests(!newVal);
      setActionError(`Error updating availability: ${err}`);
    }
  };

  // 3. Poll nearby tour requests
  const fetchNearbyRequests = useCallback(async () => {
    if (!isApprovedVolunteer) return;
    setLoadingRequests(true);
    setActionError('');
    try {
      const radiusMeters = radiusKm * 1000;
      const response = await authFetch(`${apiBase}/api/v1/tour-requests?radius=${radiusMeters}`);
      if (response.ok) {
        const data = await response.json();
        setRequests(Array.isArray(data) ? data : []);
      } else if (response.status === 403) {
        setActionError('Only approved volunteers can query dispatch requests.');
      } else {
        const text = await response.text();
        setActionError(`Error fetching requests: ${text}`);
      }
    } catch (err) {
      setActionError(`Connection error: ${err}`);
    } finally {
      setLoadingRequests(false);
    }
  }, [apiBase, authFetch, isApprovedVolunteer, radiusKm]);

  // Initial load
  useEffect(() => {
    if (!isApprovedVolunteer) return;
    let ignore = false;
    const init = async () => {
      if (!ignore) {
        await fetchNearbyRequests();
      }
    };
    init();
    return () => {
      ignore = true;
    };
  }, [isApprovedVolunteer, fetchNearbyRequests]);

  // 4. Accept a request
  const handleAcceptRequest = async (requestId: string) => {
    setActionError('');
    setActionSuccess('');
    try {
      const response = await authFetch(`${apiBase}/api/v1/tour-requests/${requestId}/accept`, {
        method: 'POST',
      });
      if (response.ok) {
        setActionSuccess(`Request ${requestId} accepted! You can now start the live broadcast.`);
        fetchNearbyRequests();
      } else {
        const errData = await response.json().catch(() => ({}));
        setActionError(errData.error || 'Failed to accept request.');
      }
    } catch (err) {
      setActionError(`Error accepting request: ${err}`);
    }
  };

  // 5. Start tour broadcast
  const handleStartTour = async (requestId: string) => {
    setActionError('');
    setActionSuccess('');
    try {
      const response = await authFetch(`${apiBase}/api/v1/tour-requests/${requestId}/start`, {
        method: 'POST',
      });
      if (response.ok) {
        const data = await response.json();
        if (data.sessionId) {
          onStartSession(data.sessionId);
        } else {
          setActionError('Tour started but no sessionId was returned.');
        }
      } else {
        const errData = await response.json().catch(() => ({}));
        setActionError(errData.error || 'Failed to start tour session.');
      }
    } catch (err) {
      setActionError(`Error starting tour: ${err}`);
    }
  };

  if (!isApprovedVolunteer) {
    return (
      <div style={{
        padding: '24px',
        backgroundColor: '#0f172a',
        borderRadius: '12px',
        border: '1px solid #1e293b',
        color: '#f8fafc',
        textAlign: 'center',
      }}>
        <div style={{ fontSize: '2.5rem', marginBottom: '12px' }}>🌍</div>
        <h3 style={{ margin: '0 0 8px 0', fontSize: '1.2rem', color: '#f8fafc' }}>
          Become a Roam Volunteer Explorer
        </h3>
        <p style={{ fontSize: '0.88rem', color: '#94a3b8', maxWidth: '400px', margin: '0 auto 18px auto', lineHeight: '1.5' }}>
          Roam volunteers connect travellers with local historic places, natural wonders, and culture.
          All volunteers must be age-verified adults (18+) and approved by platform administrators.
        </p>
        <button
          onClick={onApplyVolunteer}
          style={{
            padding: '10px 20px',
            backgroundColor: '#6366f1',
            color: '#ffffff',
            border: 'none',
            borderRadius: '8px',
            fontWeight: 600,
            cursor: 'pointer',
            fontSize: '0.9rem',
          }}
        >
          Apply to Volunteer
        </button>
      </div>
    );
  }

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      gap: '16px',
      color: '#f8fafc',
      fontFamily: 'system-ui, -apple-system, sans-serif',
    }}>
      {/* Dispatch Controls Header */}
      <div style={{
        padding: '16px',
        backgroundColor: '#0f172a',
        borderRadius: '12px',
        border: '1px solid #1e293b',
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '1.1rem', color: '#f8fafc' }}>Volunteer Dispatch Hub</h3>
            <span style={{ fontSize: '0.78rem', color: '#10b981', fontWeight: 600 }}>● Active Volunteer Status</span>
          </div>
          <button
            onClick={fetchNearbyRequests}
            disabled={loadingRequests}
            style={{
              padding: '6px 12px',
              backgroundColor: '#1e293b',
              color: '#94a3b8',
              border: '1px solid #334155',
              borderRadius: '6px',
              cursor: 'pointer',
              fontSize: '0.8rem',
            }}
          >
            {loadingRequests ? 'Refreshing...' : '🔄 Refresh'}
          </button>
        </div>

        {/* Availability Toggle */}
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '10px 14px',
          backgroundColor: '#1e293b',
          borderRadius: '8px',
          marginBottom: '12px',
        }}>
          <div>
            <div style={{ fontSize: '0.88rem', fontWeight: 600 }}>Available for Requests</div>
            <div style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Receive notifications when travellers request tours nearby</div>
          </div>
          <label style={{ display: 'flex', alignItems: 'center', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={availableForRequests}
              onChange={(e) => handleToggleAvailability(e.target.checked)}
              style={{ width: '18px', height: '18px', accentColor: '#10b981', cursor: 'pointer' }}
            />
          </label>
        </div>

        {/* GPS Sync & Radius Control */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
            <button
              onClick={handleGetLocation}
              style={{
                flex: 1,
                padding: '8px 12px',
                backgroundColor: '#1e293b',
                color: '#e2e8f0',
                border: '1px solid #334155',
                borderRadius: '6px',
                cursor: 'pointer',
                fontSize: '0.8rem',
                textAlign: 'left',
              }}
            >
              📍 {gpsLocation ? `${gpsLocation.lat.toFixed(4)}, ${gpsLocation.lng.toFixed(4)}` : gpsStatus}
            </button>
            <div style={{
              padding: '8px 12px',
              backgroundColor: '#1e293b',
              borderRadius: '6px',
              fontSize: '0.8rem',
              color: '#a5b4fc',
              fontWeight: 600,
              whiteSpace: 'nowrap',
            }}>
              {radiusKm} km radius
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Radius:</span>
            <input
              type="range"
              min="1"
              max="50"
              value={radiusKm}
              onChange={(e) => setRadiusKm(Number(e.target.value))}
              onMouseUp={fetchNearbyRequests}
              onTouchEnd={fetchNearbyRequests}
              style={{ flex: 1, accentColor: '#6366f1', cursor: 'pointer' }}
            />
            <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>50 km</span>
          </div>
        </div>
      </div>

      {/* Notifications / Errors */}
      {actionError && (
        <div style={{
          padding: '10px 14px',
          backgroundColor: '#451a1a',
          border: '1px solid #7f1d1d',
          borderRadius: '8px',
          color: '#f87171',
          fontSize: '0.82rem',
        }}>
          {actionError}
        </div>
      )}
      {actionSuccess && (
        <div style={{
          padding: '10px 14px',
          backgroundColor: '#064e3b',
          border: '1px solid #065f46',
          borderRadius: '8px',
          color: '#6ee7b7',
          fontSize: '0.82rem',
        }}>
          {actionSuccess}
        </div>
      )}

      {/* Incoming Requests List */}
      <div>
        <h4 style={{ margin: '0 0 10px 0', fontSize: '0.95rem', color: '#cbd5e1' }}>
          Incoming Exploration Requests ({requests.length})
        </h4>

        {requests.length === 0 ? (
          <div style={{
            padding: '30px 20px',
            backgroundColor: '#0f172a',
            borderRadius: '12px',
            border: '1px dashed #334155',
            textAlign: 'center',
          }}>
            <div style={{ fontSize: '2rem', marginBottom: '8px' }}>📡</div>
            <div style={{ fontSize: '0.9rem', fontWeight: 600, color: '#e2e8f0' }}>Radar Active</div>
            <div style={{ fontSize: '0.8rem', color: '#64748b', marginTop: '4px' }}>
              No open requests found within {radiusKm} km of your position. Keep this open to receive live requests.
            </div>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {requests.map((req) => (
              <div
                key={req.id}
                style={{
                  padding: '14px',
                  backgroundColor: '#0f172a',
                  borderRadius: '10px',
                  border: '1px solid #1e293b',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '8px',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div style={{ fontWeight: 600, fontSize: '0.92rem', color: '#f8fafc' }}>
                    {req.place?.name || 'Custom Coordinates Tour'}
                  </div>
                  <span style={{
                    fontSize: '0.72rem',
                    padding: '2px 8px',
                    borderRadius: '10px',
                    backgroundColor: '#1e293b',
                    color: req.mode === 0 ? '#60a5fa' : '#a78bfa',
                    fontWeight: 600,
                  }}>
                    {req.mode === 0 ? '⚡ Instant' : '📅 Scheduled'}
                  </span>
                </div>

                <div style={{ fontSize: '0.75rem', color: '#94a3b8', display: 'flex', gap: '12px' }}>
                  <span>{req.visibility === 0 ? '🔒 Private (1-on-1)' : '👥 Public Group'}</span>
                  {req.place && (
                    <span>📍 {req.place.latitude.toFixed(4)}, {req.place.longitude.toFixed(4)}</span>
                  )}
                </div>

                <div style={{ fontSize: '0.82rem', color: '#cbd5e1', lineHeight: '1.4' }}>
                  {req.description || 'No specific instructions provided.'}
                </div>

                <div style={{ display: 'flex', gap: '8px', marginTop: '4px' }}>
                  <button
                    onClick={() => handleAcceptRequest(req.id)}
                    style={{
                      flex: 1,
                      padding: '7px 12px',
                      backgroundColor: '#10b981',
                      color: '#ffffff',
                      border: 'none',
                      borderRadius: '6px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      fontSize: '0.8rem',
                    }}
                  >
                    Accept Request
                  </button>
                  <button
                    onClick={() => handleStartTour(req.id)}
                    style={{
                      flex: 1,
                      padding: '7px 12px',
                      backgroundColor: '#6366f1',
                      color: '#ffffff',
                      border: 'none',
                      borderRadius: '6px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      fontSize: '0.8rem',
                    }}
                  >
                    Start Live Tour
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
export default VolunteerDashboard;
