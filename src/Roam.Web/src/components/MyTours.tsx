import React, { useCallback, useEffect, useState } from 'react';

export interface MyTour {
  id: string;
  role: 'Requester' | 'Volunteer';
  status: number;
  statusName: string;
  mode: number;
  visibility: number;
  description: string;
  placeName?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  createdAt: string;
  expiresAt?: string | null;
  hasVolunteer: boolean;
  activeSessionId?: string | null;
  counterpartId?: string | null;
  counterpartName?: string | null;
}

interface MyToursProps {
  apiBase: string;
  authFetch: (url: string, options?: RequestInit) => Promise<Response>;
  role: MyTour['role'];
  title: string;
  /** Change this value to force a reload (e.g. after a hub notification or an accept). */
  refreshKey?: number;
  onJoinSession: (sessionId: string) => void;
}

const STATUS_LABELS: Record<string, { label: string; color: string }> = {
  Published: { label: 'Waiting for a volunteer', color: '#f59e0b' },
  Matching: { label: 'Waiting for a volunteer', color: '#f59e0b' },
  VolunteerInterested: { label: 'Volunteer interested', color: '#60a5fa' },
  Accepted: { label: 'Accepted', color: '#10b981' },
  Confirmed: { label: 'Scheduled', color: '#10b981' },
  Active: { label: 'Live now', color: '#f43f5e' },
};

const buttonStyle = (background: string): React.CSSProperties => ({
  flex: 1,
  padding: '7px 12px',
  backgroundColor: background,
  color: '#ffffff',
  border: 'none',
  borderRadius: '6px',
  fontWeight: 600,
  cursor: 'pointer',
  fontSize: '0.8rem',
});

/** The signed-in user's open tours for one role, with the actions that role can take. */
export const MyTours: React.FC<MyToursProps> = ({ apiBase, authFetch, role, title, refreshKey, onJoinSession }) => {
  const [tours, setTours] = useState<MyTour[]>([]);
  const [error, setError] = useState('');
  const [busyId, setBusyId] = useState<string | null>(null);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);

  // Fetches the list; results are applied only while `isCurrent()` holds, so a slow response from an
  // earlier load (or after unmount) never overwrites newer state.
  const fetchTours = useCallback(async (isCurrent: () => boolean) => {
    try {
      const response = await authFetch(`${apiBase}/api/v1/tour-requests/mine`);
      if (!isCurrent()) return;
      if (!response.ok) {
        setError(`Could not load your tours (HTTP ${response.status}).`);
        return;
      }
      const data: MyTour[] = await response.json();
      if (!isCurrent()) return;
      setTours(data.filter((t) => t.role === role));
      setError('');
    } catch (err) {
      if (isCurrent()) setError(`Could not load your tours: ${err}`);
    }
  }, [apiBase, authFetch, role]);

  const load = useCallback(() => fetchTours(() => true), [fetchTours]);

  useEffect(() => {
    let current = true;
    const run = async () => {
      await fetchTours(() => current);
    };
    run();
    return () => {
      current = false;
    };
  }, [fetchTours, refreshKey]);

  const run = async (tourId: string, action: () => Promise<void>) => {
    setBusyId(tourId);
    setError('');
    try {
      await action();
    } catch (err) {
      setError(String(err instanceof Error ? err.message : err));
    } finally {
      setBusyId(null);
      setConfirmingId(null);
      load();
    }
  };

  const post = async (url: string, body?: unknown) => {
    const response = await authFetch(url, {
      method: 'POST',
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    });
    if (!response.ok) {
      const data = await response.json().catch(() => ({}));
      throw new Error(data.error || data.detail || `Request failed (HTTP ${response.status}).`);
    }
    return response;
  };

  const startTour = (tour: MyTour) => run(tour.id, async () => {
    const response = await post(`${apiBase}/api/v1/tour-requests/${tour.id}/start`);
    const data = await response.json();
    if (data.sessionId) onJoinSession(data.sessionId);
    else throw new Error('Tour started but no session id was returned.');
  });

  const cancelOrWithdraw = (tour: MyTour) => run(tour.id, async () => {
    await post(`${apiBase}/api/v1/tour-requests/${tour.id}/cancel`, {
      reason: role === 'Volunteer' ? 'Volunteer withdrew' : 'Cancelled by requester',
    });
  });

  if (tours.length === 0 && !error) return null;

  return (
    <div>
      <h4 style={{ margin: '0 0 10px 0', fontSize: '0.95rem', color: '#cbd5e1' }}>
        {title} ({tours.length})
      </h4>
      {error && (
        <div style={{ padding: '8px 12px', marginBottom: '8px', backgroundColor: '#450a0a', border: '1px solid #7f1d1d', borderRadius: '8px', color: '#f87171', fontSize: '0.8rem' }}>
          {error}
        </div>
      )}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
        {tours.map((tour) => {
          const status = STATUS_LABELS[tour.statusName] ?? { label: tour.statusName, color: '#94a3b8' };
          const busy = busyId === tour.id;
          const canStart = role === 'Volunteer' && (tour.statusName === 'Accepted' || tour.statusName === 'Confirmed');
          const canRelease = tour.statusName !== 'Active';
          const releaseLabel = role === 'Volunteer' ? 'Withdraw' : 'Cancel Request';

          return (
            <div key={tour.id} style={{ padding: '14px', backgroundColor: '#0f172a', borderRadius: '10px', border: '1px solid #1e293b', display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '8px' }}>
                <div style={{ fontWeight: 600, fontSize: '0.92rem', color: '#f8fafc' }}>
                  {tour.placeName || 'Custom Coordinates Tour'}
                </div>
                <span style={{ fontSize: '0.72rem', padding: '2px 8px', borderRadius: '10px', backgroundColor: '#1e293b', color: status.color, fontWeight: 600, whiteSpace: 'nowrap' }}>
                  {status.label}
                </span>
              </div>
              <div style={{ fontSize: '0.82rem', color: '#cbd5e1', lineHeight: '1.4' }}>
                {tour.description || 'No specific instructions provided.'}
              </div>

              {confirmingId === tour.id ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <span style={{ fontSize: '0.8rem', color: '#fca5a5' }}>
                    {role === 'Volunteer'
                      ? 'Withdraw from this tour? The request reopens for other volunteers.'
                      : 'Cancel this tour request?'}
                  </span>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <button disabled={busy} onClick={() => cancelOrWithdraw(tour)} style={buttonStyle('#dc2626')}>
                      {busy ? 'Working…' : role === 'Volunteer' ? 'Yes, withdraw' : 'Yes, cancel'}
                    </button>
                    <button disabled={busy} onClick={() => setConfirmingId(null)} style={buttonStyle('#334155')}>Keep it</button>
                  </div>
                </div>
              ) : (
                <div style={{ display: 'flex', gap: '8px' }}>
                  {tour.activeSessionId && (
                    <button disabled={busy} onClick={() => onJoinSession(tour.activeSessionId!)} style={buttonStyle('#f43f5e')}>
                      {role === 'Volunteer' ? 'Rejoin Live Tour' : 'Join Live Tour'}
                    </button>
                  )}
                  {canStart && (
                    <button disabled={busy} onClick={() => startTour(tour)} style={buttonStyle('#6366f1')}>
                      {busy ? 'Starting…' : 'Start Live Tour'}
                    </button>
                  )}
                  {canRelease && (
                    <button disabled={busy} onClick={() => setConfirmingId(tour.id)} style={buttonStyle('#334155')}>
                      {releaseLabel}
                    </button>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default MyTours;
