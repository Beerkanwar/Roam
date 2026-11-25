import React, { useState } from 'react';
import { AgeCategory } from '../utils/enums';
import { readApiError } from '../utils/apiErrors';

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  country: string;
  profilePhotoUrl?: string | null;
  volunteerStatus: number; // see VolunteerStatus in utils/enums.ts
  ageCategory: number;     // see AgeCategory in utils/enums.ts
  emailConfirmed?: boolean;
  isAdmin?: boolean;
}

interface UserProfileModalProps {
  apiBase: string;
  authFetch: (url: string, options?: RequestInit) => Promise<Response>;
  profile: UserProfile | null;
  onProfileUpdated: () => void;
  /** Called after the account was deleted on the server; the caller signs out. */
  onAccountDeleted: () => void;
  onClose: () => void;
}

export const UserProfileModal: React.FC<UserProfileModalProps> = ({
  apiBase,
  authFetch,
  profile,
  onProfileUpdated,
  onAccountDeleted,
  onClose,
}) => {
  const [showDelete, setShowDelete] = useState(false);
  const [deletePassword, setDeletePassword] = useState('');
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState('');
  const [displayName, setDisplayName] = useState(profile?.displayName || '');
  const [country, setCountry] = useState(profile?.country || '');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [errorMessage, setErrorMessage] = useState('');

  // Handle Onboarding / Age verification (18+ safeguarding)
  const handleOnboardingSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');
    setMessage('');

    if (!displayName.trim() || !country.trim() || !dateOfBirth) {
      setErrorMessage('Please complete all required fields.');
      return;
    }

    setLoading(true);
    try {
      const response = await authFetch(`${apiBase}/api/v1/profile/onboarding`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          displayName: displayName.trim(),
          country: country.trim(),
          dateOfBirth,
        }),
      });

      if (response.ok) {
        setMessage('Safeguarding verification and profile updated successfully!');
        onProfileUpdated();
      } else {
        const err = await response.json().catch(() => ({}));
        setErrorMessage(err.error || 'Failed to complete adult safeguarding verification.');
      }
    } catch (err) {
      setErrorMessage(`Error: ${err}`);
    } finally {
      setLoading(false);
    }
  };

  // Handle volunteer application
  const handleApplyVolunteer = async () => {
    setErrorMessage('');
    setMessage('');
    setLoading(true);
    try {
      const response = await authFetch(`${apiBase}/api/v1/profile/volunteer/apply`, {
        method: 'POST',
      });
      if (response.ok) {
        const data = await response.json().catch(() => ({}));
        setMessage(data.message || 'Volunteer application submitted for administrator review.');
        onProfileUpdated();
      } else {
        const err = await response.json().catch(() => ({}));
        setErrorMessage(err.error || 'Volunteer application could not be submitted. Ensure you are 18+ verified.');
      }
    } catch (err) {
      setErrorMessage(`Error submitting application: ${err}`);
    } finally {
      setLoading(false);
    }
  };

  const getVolunteerStatusBadge = (status: number) => {
    switch (status) {
      case 1:
        return <span style={{ color: '#f59e0b', fontWeight: 600 }}>Application Pending Review</span>;
      case 2:
        return <span style={{ color: '#10b981', fontWeight: 600 }}>Verified Active Volunteer</span>;
      case 3:
        return <span style={{ color: '#ef4444', fontWeight: 600 }}>Status Revoked</span>;
      default:
        return <span style={{ color: '#94a3b8' }}>Not Registered</span>;
    }
  };

  const handleResendConfirmation = async () => {
    if (!profile?.email) return;
    setErrorMessage('');
    setMessage('');
    try {
      const response = await fetch(`${apiBase}/resendConfirmationEmail`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: profile.email }),
      });
      if (response.ok) {
        setMessage(`A confirmation email was sent to ${profile.email}. Check your inbox (and spam folder).`);
      } else {
        setErrorMessage(await readApiError(response, 'Could not send the confirmation email.'));
      }
    } catch {
      setErrorMessage('Could not reach the server.');
    }
  };

  // GDPR: download everything Roam stores about the account as a JSON file.
  const handleExport = async () => {
    setErrorMessage('');
    setMessage('');
    try {
      const response = await authFetch(`${apiBase}/api/v1/profile/me/export`);
      if (!response.ok) {
        setErrorMessage(await readApiError(response, 'Could not export your data.'));
        return;
      }
      const blob = new Blob([JSON.stringify(await response.json(), null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'roam-my-data.json';
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch {
      setErrorMessage('Could not reach the server.');
    }
  };

  const handleDeleteAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    setDeleteError('');
    setDeleting(true);
    try {
      const response = await authFetch(`${apiBase}/api/v1/profile/me`, {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password: deletePassword }),
      });
      if (response.status === 204 || response.ok) {
        onAccountDeleted();
        return;
      }
      setDeleteError(await readApiError(response, 'Your account could not be deleted.'));
    } catch {
      setDeleteError('Could not reach the server.');
    } finally {
      setDeleting(false);
    }
  };

  // Only users whose date of birth was verified as 18+ are Adult; new accounts are Unknown.
  const isAdult = profile?.ageCategory === AgeCategory.Adult;

  return (
    <div style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      bottom: 0,
      backgroundColor: 'rgba(0, 0, 0, 0.75)',
      backdropFilter: 'blur(4px)',
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      zIndex: 200,
      padding: '16px',
    }}>
      <div style={{
        backgroundColor: '#0f172a',
        border: '1px solid #1e293b',
        borderRadius: '16px',
        padding: '24px',
        maxWidth: '460px',
        width: '100%',
        maxHeight: 'calc(100vh - 32px)',
        overflowY: 'auto',
        boxSizing: 'border-box',
        color: '#f8fafc',
        fontFamily: 'system-ui, -apple-system, sans-serif',
        boxShadow: '0 20px 40px rgba(0, 0, 0, 0.5)',
      }}>
        {/* Header */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px' }}>
          <h2 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#f8fafc' }}>
            Account &amp; Explorer Profile
          </h2>
          <button
            onClick={onClose}
            style={{
              background: 'none',
              border: 'none',
              color: '#94a3b8',
              fontSize: '1.4rem',
              cursor: 'pointer',
              lineHeight: 1,
            }}
          >
            ×
          </button>
        </div>

        {/* User Card */}
        <div style={{
          padding: '14px',
          backgroundColor: '#1e293b',
          borderRadius: '10px',
          marginBottom: '16px',
          display: 'flex',
          flexDirection: 'column',
          gap: '6px',
          fontSize: '0.85rem',
        }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', alignItems: 'center' }}>
            <span><strong>Email:</strong> {profile?.email}</span>
            {profile && profile.emailConfirmed === false && (
              <>
                <span style={{ color: '#f59e0b', fontSize: '0.75rem' }}>(not confirmed)</span>
                <button type="button" onClick={handleResendConfirmation}
                  style={{ background: 'none', border: 'none', color: '#a5b4fc', cursor: 'pointer', fontSize: '0.75rem', padding: 0, textDecoration: 'underline' }}>
                  Resend confirmation
                </button>
              </>
            )}
          </div>
          <div><strong>Display Name:</strong> {profile?.displayName || 'Not configured'}</div>
          <div><strong>Country:</strong> {profile?.country || 'Not configured'}</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '4px' }}>
            <strong>Age Safeguarding:</strong>
            {isAdult ? (
              <span style={{
                backgroundColor: '#064e3b',
                color: '#6ee7b7',
                padding: '2px 8px',
                borderRadius: '10px',
                fontSize: '0.75rem',
                fontWeight: 600,
              }}>
                ✓ 18+ Verified Adult
              </span>
            ) : (
              <span style={{
                backgroundColor: '#451a1a',
                color: '#f87171',
                padding: '2px 8px',
                borderRadius: '10px',
                fontSize: '0.75rem',
                fontWeight: 600,
              }}>
                Action Required: 18+ Verification
              </span>
            )}
          </div>
          <div><strong>Volunteer Status:</strong> {getVolunteerStatusBadge(profile?.volunteerStatus ?? 0)}</div>
        </div>

        {/* Status / Error Alerts */}
        {message && (
          <div style={{
            padding: '10px 14px',
            backgroundColor: '#064e3b',
            border: '1px solid #065f46',
            borderRadius: '8px',
            color: '#6ee7b7',
            fontSize: '0.82rem',
            marginBottom: '14px',
          }}>
            {message}
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

        {/* 18+ Safeguarding Onboarding Form (If not verified) */}
        {!isAdult && (
          <form onSubmit={handleOnboardingSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginBottom: '16px' }}>
            <div style={{ fontSize: '0.85rem', fontWeight: 600, color: '#cbd5e1' }}>
              Adult Safeguarding Verification (18+)
            </div>
            <p style={{ fontSize: '0.75rem', color: '#94a3b8', margin: 0 }}>
              Per Roam platform safeguarding policies, all participants submitting requests or volunteering must verify adult status.
            </p>
            <input
              type="text"
              placeholder="Your Display Name"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
              style={{
                padding: '8px 12px',
                borderRadius: '6px',
                border: '1px solid #334155',
                backgroundColor: '#1e293b',
                color: '#f8fafc',
                fontSize: '0.85rem',
              }}
            />
            <input
              type="text"
              placeholder="Country of Residence"
              value={country}
              onChange={(e) => setCountry(e.target.value)}
              required
              style={{
                padding: '8px 12px',
                borderRadius: '6px',
                border: '1px solid #334155',
                backgroundColor: '#1e293b',
                color: '#f8fafc',
                fontSize: '0.85rem',
              }}
            />
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
              <label style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Date of Birth (Must be 18+):</label>
              <input
                type="date"
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
                required
                style={{
                  padding: '8px 12px',
                  borderRadius: '6px',
                  border: '1px solid #334155',
                  backgroundColor: '#1e293b',
                  color: '#f8fafc',
                  fontSize: '0.85rem',
                }}
              />
            </div>
            <button
              type="submit"
              disabled={loading}
              style={{
                padding: '9px 16px',
                backgroundColor: '#6366f1',
                color: '#ffffff',
                border: 'none',
                borderRadius: '6px',
                fontWeight: 600,
                cursor: 'pointer',
                fontSize: '0.85rem',
              }}
            >
              {loading ? 'Verifying...' : 'Verify Age & Complete Profile'}
            </button>
          </form>
        )}

        {/* Volunteer Application Action */}
        {isAdult && profile?.volunteerStatus === 0 && (
          <div style={{
            padding: '14px',
            backgroundColor: '#1e293b',
            borderRadius: '10px',
            marginBottom: '16px',
            textAlign: 'center',
          }}>
            <h4 style={{ margin: '0 0 6px 0', fontSize: '0.95rem', color: '#f8fafc' }}>Volunteer to Host Tours</h4>
            <p style={{ margin: '0 0 12px 0', fontSize: '0.78rem', color: '#94a3b8', lineHeight: '1.4' }}>
              Share historic landmarks and scenic places with travellers worldwide from your smartphone or camera.
            </p>
            <button
              type="button"
              onClick={handleApplyVolunteer}
              disabled={loading}
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
              {loading ? 'Submitting...' : 'Apply to Become a Volunteer'}
            </button>
          </div>
        )}

        {/* Your data: export and deletion */}
        <div style={{ padding: '14px', backgroundColor: '#1e293b', borderRadius: '10px', marginBottom: '16px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
          <div style={{ fontSize: '0.85rem', fontWeight: 600, color: '#cbd5e1' }}>Your data</div>
          <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
            <button type="button" onClick={handleExport}
              style={{ flex: 1, padding: '8px 12px', backgroundColor: '#334155', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '0.8rem', fontWeight: 600 }}>
              Download my data
            </button>
            {!showDelete && (
              <button type="button" onClick={() => setShowDelete(true)}
                style={{ flex: 1, padding: '8px 12px', backgroundColor: 'transparent', color: '#f87171', border: '1px solid #7f1d1d', borderRadius: '6px', cursor: 'pointer', fontSize: '0.8rem', fontWeight: 600 }}>
                Delete my account
              </button>
            )}
          </div>
          {showDelete && (
            <form onSubmit={handleDeleteAccount} style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <p style={{ margin: 0, fontSize: '0.78rem', color: '#fca5a5', lineHeight: 1.4 }}>
                This permanently removes your profile, location and settings, cancels your open requests, releases
                tours you accepted and signs you out. It cannot be undone. Enter your password to confirm.
              </p>
              <input
                type="password"
                placeholder="Your password"
                autoComplete="current-password"
                value={deletePassword}
                onChange={(e) => setDeletePassword(e.target.value)}
                required
                style={{ padding: '8px 12px', borderRadius: '6px', border: '1px solid #7f1d1d', backgroundColor: '#0f172a', color: '#f8fafc', fontSize: '0.85rem' }}
              />
              {deleteError && <div role="alert" style={{ color: '#f87171', fontSize: '0.78rem' }}>{deleteError}</div>}
              <div style={{ display: 'flex', gap: '8px' }}>
                <button type="submit" disabled={deleting || !deletePassword}
                  style={{ flex: 1, padding: '8px 12px', backgroundColor: '#dc2626', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '0.8rem', fontWeight: 600 }}>
                  {deleting ? 'Deleting…' : 'Permanently delete account'}
                </button>
                <button type="button" onClick={() => { setShowDelete(false); setDeletePassword(''); setDeleteError(''); }}
                  style={{ flex: 1, padding: '8px 12px', backgroundColor: '#334155', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '0.8rem' }}>
                  Keep my account
                </button>
              </div>
            </form>
          )}
        </div>

        {/* Close Button */}
        <button
          onClick={onClose}
          style={{
            width: '100%',
            padding: '9px',
            backgroundColor: 'transparent',
            color: '#94a3b8',
            border: '1px solid #334155',
            borderRadius: '8px',
            cursor: 'pointer',
            fontSize: '0.85rem',
          }}
        >
          Close
        </button>
      </div>
    </div>
  );
};
export default UserProfileModal;
