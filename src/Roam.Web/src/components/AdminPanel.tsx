import React, { useCallback, useEffect, useState } from 'react';
import { readApiError } from '../utils/apiErrors';

interface PendingVolunteer {
  id: string;
  email: string;
  displayName: string;
  country: string;
  dateOfBirth?: string | null;
  createdAt: string;
}

interface OpenReport {
  id: string;
  tourSessionId: string;
  reason: string;
  severity: string;
  status: string;
  description: string;
  createdAt: string;
  reporterName?: string | null;
  reporterEmail?: string | null;
  reportedUserId: string;
  reportedUserName?: string | null;
  reportedUserEmail?: string | null;
  reportedUserStatus?: string | null;
}

interface AdminPanelProps {
  apiBase: string;
  authFetch: (url: string, options?: RequestInit) => Promise<Response>;
}

// Matches Roam.Domain.TrustAndSafety.ModerationAction.
const MODERATION_ACTIONS = [
  { value: 0, label: 'Dismiss (no action)' },
  { value: 1, label: 'Warning' },
  { value: 2, label: 'Temporary restriction' },
  { value: 3, label: 'Suspend account' },
  { value: 4, label: 'Ban permanently' },
];

const SEVERITY_COLORS: Record<string, string> = {
  Critical: '#f43f5e',
  High: '#f97316',
  Medium: '#f59e0b',
  Low: '#94a3b8',
};

const card: React.CSSProperties = {
  padding: '12px',
  backgroundColor: '#0f172a',
  borderRadius: '10px',
  border: '1px solid #1e293b',
  display: 'flex',
  flexDirection: 'column',
  gap: '6px',
  fontSize: '0.8rem',
  color: '#cbd5e1',
};

const smallButton = (background: string): React.CSSProperties => ({
  flex: 1,
  padding: '7px 10px',
  backgroundColor: background,
  color: '#fff',
  border: 'none',
  borderRadius: '6px',
  fontWeight: 600,
  cursor: 'pointer',
  fontSize: '0.78rem',
});

const formatDate = (value: string) => new Date(value).toLocaleString();

/** Administrator tools: approve volunteer applications and act on safety reports. */
export const AdminPanel: React.FC<AdminPanelProps> = ({ apiBase, authFetch }) => {
  const [volunteers, setVolunteers] = useState<PendingVolunteer[]>([]);
  const [reports, setReports] = useState<OpenReport[]>([]);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busyId, setBusyId] = useState<string | null>(null);
  const [decisions, setDecisions] = useState<Record<string, { action: number; reason: string }>>({});
  const [reloadKey, setReloadKey] = useState(0);

  const reload = useCallback(() => setReloadKey((k) => k + 1), []);

  useEffect(() => {
    let current = true;
    const load = async () => {
      try {
        const [volunteerResponse, reportResponse] = await Promise.all([
          authFetch(`${apiBase}/api/v1/admin/volunteers/pending`),
          authFetch(`${apiBase}/api/v1/admin/reports`),
        ]);
        if (!current) return;
        if (!volunteerResponse.ok || !reportResponse.ok) {
          setError(await readApiError(volunteerResponse.ok ? reportResponse : volunteerResponse, 'Could not load admin data.'));
          return;
        }
        const [volunteerData, reportData] = await Promise.all([volunteerResponse.json(), reportResponse.json()]);
        if (!current) return;
        setVolunteers(volunteerData);
        setReports(reportData);
        setError('');
      } catch {
        if (current) setError('Could not reach the server.');
      }
    };
    load();
    return () => {
      current = false;
    };
  }, [apiBase, authFetch, reloadKey]);

  const act = async (id: string, url: string, body: unknown, success: string) => {
    setBusyId(id);
    setError('');
    setNotice('');
    try {
      const response = await authFetch(url, {
        method: 'POST',
        headers: body ? { 'Content-Type': 'application/json' } : undefined,
        body: body ? JSON.stringify(body) : undefined,
      });
      if (!response.ok) {
        setError(await readApiError(response, 'The action failed.'));
        return;
      }
      setNotice(success);
      reload();
    } catch {
      setError('Could not reach the server.');
    } finally {
      setBusyId(null);
    }
  };

  const decisionFor = (id: string) => decisions[id] ?? { action: 0, reason: '' };
  const setDecision = (id: string, patch: Partial<{ action: number; reason: string }>) =>
    setDecisions((prev) => ({ ...prev, [id]: { ...decisionFor(id), ...patch } }));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h3 style={{ margin: 0, fontSize: '1rem', color: '#f8fafc' }}>Admin</h3>
        <button type="button" onClick={reload} className="roam-btn-secondary" style={{ fontSize: '0.75rem', padding: '4px 10px' }}>
          Refresh
        </button>
      </div>

      {notice && <div role="status" style={{ padding: '8px 12px', backgroundColor: '#064e3b', border: '1px solid #10b981', borderRadius: '8px', color: '#a7f3d0', fontSize: '0.8rem' }}>{notice}</div>}
      {error && <div role="alert" style={{ padding: '8px 12px', backgroundColor: '#450a0a', border: '1px solid #7f1d1d', borderRadius: '8px', color: '#f87171', fontSize: '0.8rem' }}>{error}</div>}

      <section style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
        <h4 style={{ margin: 0, fontSize: '0.9rem', color: '#cbd5e1' }}>Volunteer applications ({volunteers.length})</h4>
        {volunteers.length === 0 && <div style={{ fontSize: '0.8rem', color: '#64748b' }}>No applications waiting.</div>}
        {volunteers.map((v) => (
          <div key={v.id} style={card}>
            <div style={{ fontWeight: 600, color: '#f8fafc' }}>{v.displayName || 'No name yet'}</div>
            <div>{v.email}</div>
            <div style={{ color: '#94a3b8' }}>
              {v.country || 'Country not set'} · Account created {formatDate(v.createdAt)}
            </div>
            <div style={{ display: 'flex', gap: '8px', marginTop: '4px' }}>
              <button type="button" disabled={busyId === v.id} style={smallButton('#10b981')}
                onClick={() => act(v.id, `${apiBase}/api/v1/admin/volunteers/${encodeURIComponent(v.id)}/approve`, null, `${v.email} is now an approved volunteer.`)}>
                Approve
              </button>
              <button type="button" disabled={busyId === v.id} style={smallButton('#475569')}
                onClick={() => act(v.id, `${apiBase}/api/v1/admin/volunteers/${encodeURIComponent(v.id)}/reject`, null, `${v.email}'s application was rejected.`)}>
                Reject
              </button>
            </div>
          </div>
        ))}
      </section>

      <section style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
        <h4 style={{ margin: 0, fontSize: '0.9rem', color: '#cbd5e1' }}>Open safety reports ({reports.length})</h4>
        {reports.length === 0 && <div style={{ fontSize: '0.8rem', color: '#64748b' }}>No open reports.</div>}
        {reports.map((r) => {
          const decision = decisionFor(r.id);
          return (
            <div key={r.id} style={card}>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: '8px' }}>
                <span style={{ fontWeight: 600, color: '#f8fafc' }}>{r.reason.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>
                <span style={{ color: SEVERITY_COLORS[r.severity] ?? '#94a3b8', fontWeight: 600 }}>{r.severity}</span>
              </div>
              <div>
                <strong>Reported:</strong> {r.reportedUserName || 'Unknown'} ({r.reportedUserEmail || r.reportedUserId})
                {r.reportedUserStatus && r.reportedUserStatus !== 'Active' ? ` · ${r.reportedUserStatus}` : ''}
              </div>
              <div><strong>By:</strong> {r.reporterName || 'Unknown'} ({r.reporterEmail || 'no email'})</div>
              <div style={{ whiteSpace: 'pre-wrap', color: '#e2e8f0' }}>{r.description || 'No description.'}</div>
              <div style={{ color: '#64748b', fontSize: '0.72rem' }}>{formatDate(r.createdAt)} · Session {r.tourSessionId.substring(0, 8)}…</div>
              <select
                className="roam-input"
                value={decision.action}
                onChange={(e) => setDecision(r.id, { action: Number(e.target.value) })}
                style={{ padding: '6px' }}
                aria-label="Moderation action"
              >
                {MODERATION_ACTIONS.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
              </select>
              <input
                className="roam-input"
                placeholder="Note for the audit log (optional)"
                value={decision.reason}
                maxLength={1900}
                onChange={(e) => setDecision(r.id, { reason: e.target.value })}
              />
              <button type="button" disabled={busyId === r.id} style={smallButton(decision.action >= 3 ? '#dc2626' : '#6366f1')}
                onClick={() => act(r.id, `${apiBase}/api/v1/trust-safety/moderation/resolve/${encodeURIComponent(r.id)}`,
                  { action: decision.action, reason: decision.reason.trim() || null },
                  'Report resolved.')}>
                {busyId === r.id ? 'Saving…' : 'Resolve report'}
              </button>
            </div>
          );
        })}
      </section>
    </div>
  );
};

export default AdminPanel;
