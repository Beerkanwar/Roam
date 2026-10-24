import React, { useState } from 'react';

interface ReportAndBlockModalProps {
  apiBase: string;
  authFetch: (url: string, options?: RequestInit) => Promise<Response>;
  targetUserId: string;
  targetUserName?: string;
  tourSessionId?: string;
  onClose: () => void;
  onUserBlocked?: () => void;
}

export const ReportAndBlockModal: React.FC<ReportAndBlockModalProps> = ({
  apiBase,
  authFetch,
  targetUserId,
  targetUserName = 'User',
  tourSessionId,
  onClose,
  onUserBlocked,
}) => {
  const [activeTab, setActiveTab] = useState<'report' | 'block'>('report');
  const [reason, setReason] = useState<number>(0); // 0 = Harassment
  const [description, setDescription] = useState<string>('');
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string>('');

  const reportReasons = [
    { value: 0, label: 'Harassment or Bullying' },
    { value: 1, label: 'Abusive or Hateful Behavior' },
    { value: 2, label: 'Inappropriate or Explicit Content' },
    { value: 3, label: 'Dangerous or Trespassing Request' },
    { value: 4, label: 'Illegal Activity' },
    { value: 5, label: 'Recording Without Consent' },
    { value: 6, label: 'Spam or Fraud' },
    { value: 7, label: 'Immediate Safety Concern' },
    { value: 8, label: 'Other Safety Violation' },
  ];

  const handleReportSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description.trim() || description.length < 5) {
      setErrorMessage('Please provide a description (minimum 5 characters).');
      return;
    }

    setSubmitting(true);
    setErrorMessage('');
    setStatusMessage('');

    try {
      const response = await authFetch(`${apiBase}/api/v1/trust-safety/reports`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          reportedUserId: targetUserId,
          tourSessionId: tourSessionId || undefined,
          reason,
          description: description.trim(),
          severity: 1, // Medium
        }),
      });

      if (response.ok) {
        setStatusMessage('Your safety report has been submitted to platform administrators for priority review.');
      } else {
        const err = await response.json().catch(() => ({}));
        setErrorMessage(err.error || 'Failed to submit safety report.');
      }
    } catch (err) {
      setErrorMessage(`Error: ${err}`);
    } finally {
      setSubmitting(false);
    }
  };

  const handleBlockUser = async () => {
    setSubmitting(true);
    setErrorMessage('');
    setStatusMessage('');

    try {
      const response = await authFetch(`${apiBase}/api/v1/trust-safety/blocks/${targetUserId}`, {
        method: 'POST',
      });

      if (response.ok) {
        setStatusMessage(`User has been blocked. They can no longer contact you, view your requests, or join your sessions.`);
        if (onUserBlocked) onUserBlocked();
      } else {
        const err = await response.json().catch(() => ({}));
        setErrorMessage(err.error || 'Failed to block user.');
      }
    } catch (err) {
      setErrorMessage(`Error: ${err}`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      bottom: 0,
      backgroundColor: 'rgba(0,0,0,0.8)',
      backdropFilter: 'blur(5px)',
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      zIndex: 300,
      padding: '16px',
    }}>
      <div style={{
        backgroundColor: '#0f172a',
        border: '1px solid #1e293b',
        borderRadius: '16px',
        padding: '24px',
        maxWidth: '440px',
        width: '100%',
        color: '#f8fafc',
        fontFamily: 'system-ui, -apple-system, sans-serif',
        boxShadow: '0 20px 40px rgba(0,0,0,0.5)',
      }}>
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
          <h3 style={{ margin: 0, fontSize: '1.2rem', fontWeight: 700, color: '#f87171' }}>
            🛡️ Safety &amp; Moderation
          </h3>
          <button
            onClick={onClose}
            style={{ background: 'none', border: 'none', color: '#94a3b8', fontSize: '1.4rem', cursor: 'pointer' }}
          >
            ×
          </button>
        </div>

        {/* Tab switch: Report vs Block */}
        <div style={{ display: 'flex', gap: '4px', backgroundColor: '#0b0f19', padding: '4px', borderRadius: '8px', marginBottom: '16px' }}>
          <button
            type="button"
            onClick={() => setActiveTab('report')}
            style={{
              flex: 1,
              padding: '8px',
              backgroundColor: activeTab === 'report' ? '#1e293b' : 'transparent',
              color: activeTab === 'report' ? '#f8fafc' : '#94a3b8',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 600,
              cursor: 'pointer',
              fontSize: '0.85rem',
            }}
          >
            🚩 File Safety Report
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('block')}
            style={{
              flex: 1,
              padding: '8px',
              backgroundColor: activeTab === 'block' ? '#1e293b' : 'transparent',
              color: activeTab === 'block' ? '#f87171' : '#94a3b8',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 600,
              cursor: 'pointer',
              fontSize: '0.85rem',
            }}
          >
            🚫 Block User
          </button>
        </div>

        {/* Alerts */}
        {statusMessage && (
          <div style={{
            padding: '10px 14px',
            backgroundColor: '#064e3b',
            border: '1px solid #10b981',
            borderRadius: '8px',
            color: '#a7f3d0',
            fontSize: '0.82rem',
            marginBottom: '14px',
          }}>
            {statusMessage}
          </div>
        )}
        {errorMessage && (
          <div style={{
            padding: '10px 14px',
            backgroundColor: '#451a1a',
            border: '1px solid #7f1d1d',
            borderRadius: '8px',
            color: '#f87171',
            fontSize: '0.82rem',
            marginBottom: '14px',
          }}>
            {errorMessage}
          </div>
        )}

        {/* REPORT TAB */}
        {activeTab === 'report' && (
          <form onSubmit={handleReportSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <div style={{ fontSize: '0.85rem', color: '#94a3b8' }}>
              Report <strong>{targetUserName}</strong> for violations of Roam community safety standards.
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <label style={{ fontSize: '0.78rem', color: '#94a3b8' }}>Violation Reason:</label>
              <select
                value={reason}
                onChange={(e) => setReason(Number(e.target.value))}
                style={{
                  padding: '9px 12px',
                  backgroundColor: '#1e293b',
                  border: '1px solid #334155',
                  borderRadius: '8px',
                  color: '#f8fafc',
                  fontSize: '0.85rem',
                }}
              >
                {reportReasons.map(r => (
                  <option key={r.value} value={r.value}>{r.label}</option>
                ))}
              </select>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <label style={{ fontSize: '0.78rem', color: '#94a3b8' }}>Description / Evidence:</label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Describe what occurred with timestamps if applicable..."
                rows={4}
                maxLength={2000}
                required
                style={{
                  padding: '10px',
                  backgroundColor: '#1e293b',
                  border: '1px solid #334155',
                  borderRadius: '8px',
                  color: '#f8fafc',
                  fontSize: '0.85rem',
                  resize: 'none',
                  outline: 'none',
                  fontFamily: 'inherit',
                }}
              />
            </div>

            <div style={{ display: 'flex', gap: '8px', marginTop: '4px' }}>
              <button
                type="button"
                onClick={onClose}
                style={{
                  flex: 1,
                  padding: '9px',
                  backgroundColor: '#1e293b',
                  color: '#94a3b8',
                  border: '1px solid #334155',
                  borderRadius: '8px',
                  cursor: 'pointer',
                  fontSize: '0.85rem',
                }}
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={submitting}
                style={{
                  flex: 2,
                  padding: '9px',
                  backgroundColor: '#ef4444',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '8px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  fontSize: '0.85rem',
                }}
              >
                {submitting ? 'Submitting...' : 'Submit Safety Report'}
              </button>
            </div>
          </form>
        )}

        {/* BLOCK TAB */}
        {activeTab === 'block' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            <p style={{ fontSize: '0.88rem', color: '#cbd5e1', lineHeight: '1.5', margin: 0 }}>
              Blocking <strong>{targetUserName}</strong> will prevent all communication, hide your tour requests from their dispatch view, and immediately disconnect mutual live sessions.
            </p>
            <p style={{ fontSize: '0.78rem', color: '#94a3b8', margin: 0 }}>
              This action is strictly enforced across both real-time SignalR hubs and spatial discovery queries.
            </p>

            <div style={{ display: 'flex', gap: '8px', marginTop: '6px' }}>
              <button
                type="button"
                onClick={onClose}
                style={{
                  flex: 1,
                  padding: '9px',
                  backgroundColor: '#1e293b',
                  color: '#94a3b8',
                  border: '1px solid #334155',
                  borderRadius: '8px',
                  cursor: 'pointer',
                  fontSize: '0.85rem',
                }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleBlockUser}
                disabled={submitting}
                style={{
                  flex: 2,
                  padding: '9px',
                  backgroundColor: '#dc2626',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '8px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  fontSize: '0.85rem',
                }}
              >
                {submitting ? 'Blocking...' : `Block ${targetUserName}`}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
export default ReportAndBlockModal;
