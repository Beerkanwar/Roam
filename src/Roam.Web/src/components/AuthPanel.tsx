import React, { useEffect, useState } from 'react';
import { readApiError } from '../utils/apiErrors';
import { readResetLink } from '../auth/resetLink';

type Mode = 'signin' | 'register' | 'forgot' | 'reset';

interface AuthPanelProps {
  apiBase: string;
  /** Called with the /login response body; returns false if it held no usable token. */
  onSignedIn: (tokens: unknown) => boolean;
}

const linkButton: React.CSSProperties = {
  background: 'none',
  border: 'none',
  color: '#a5b4fc',
  cursor: 'pointer',
  fontSize: '0.78rem',
  textAlign: 'center',
  padding: 0,
};

const TITLES: Record<Mode, string> = {
  signin: 'Sign In to Roam',
  register: 'Create Your Explorer Account',
  forgot: 'Reset Your Password',
  reset: 'Choose a New Password',
};

/** Sign in, registration and the emailed-code password reset, all against ASP.NET Core Identity's endpoints. */
export const AuthPanel: React.FC<AuthPanelProps> = ({ apiBase, onSignedIn }) => {
  const [resetLink] = useState(() => readResetLink(window.location.hash));
  const [mode, setMode] = useState<Mode>(resetLink ? 'reset' : 'signin');
  const [email, setEmail] = useState(resetLink?.email ?? '');
  const [password, setPassword] = useState('');
  const [resetCode, setResetCode] = useState(resetLink?.code ?? '');
  const [acceptedTerms, setAcceptedTerms] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  // Don't leave the reset code in the address bar (or browser history) once it has been read.
  useEffect(() => {
    if (resetLink) window.history.replaceState(null, '', window.location.pathname + window.location.search);
  }, [resetLink]);

  const switchMode = (next: Mode) => {
    setMode(next);
    setError('');
    setNotice('');
    setPassword('');
  };

  const post = (path: string, body: unknown) =>
    fetch(`${apiBase}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });

  const signIn = async (): Promise<boolean> => {
    const response = await post('/login', { email: email.trim(), password });
    if (!response.ok) {
      setError(await readApiError(response, 'Sign-in failed.'));
      return false;
    }
    if (!onSignedIn(await response.json())) {
      setError('Sign-in response did not include an access token.');
      return false;
    }
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setNotice('');
    setBusy(true);
    try {
      if (mode === 'signin') {
        await signIn();
      } else if (mode === 'register') {
        const response = await post('/register', { email: email.trim(), password });
        if (!response.ok) {
          setError(await readApiError(response, 'Registration failed.'));
          return;
        }
        // Registration succeeded: sign straight in so the user can finish their profile.
        await signIn();
      } else if (mode === 'forgot') {
        const response = await post('/forgotPassword', { email: email.trim() });
        if (!response.ok) {
          setError(await readApiError(response, 'Could not request a reset code.'));
          return;
        }
        setMode('reset');
        setNotice('If this email belongs to an account with a confirmed email address, a reset code is on its way. Enter it below.');
      } else {
        const response = await post('/resetPassword', { email: email.trim(), resetCode: resetCode.trim(), newPassword: password });
        if (!response.ok) {
          const message = await readApiError(response, 'Could not reset the password.');
          setError(/invalid token/i.test(message) ? 'That reset code is invalid or has expired. Request a new one.' : message);
          return;
        }
        switchMode('signin');
        setNotice('Your password was changed. Sign in with your new password.');
      }
    } catch {
      setError('Could not reach the server. Check your connection and try again.');
    } finally {
      setBusy(false);
    }
  };

  const resendConfirmation = async () => {
    if (!email.trim()) {
      setError('Enter your email address first.');
      return;
    }
    setError('');
    try {
      await post('/resendConfirmationEmail', { email: email.trim() });
      setNotice('If that account exists and is not confirmed yet, a new confirmation email has been sent.');
    } catch {
      setError('Could not reach the server. Check your connection and try again.');
    }
  };

  const needsPassword = mode !== 'forgot';
  const submitLabel = {
    signin: busy ? 'Signing in…' : 'Sign In',
    register: busy ? 'Creating account…' : 'Create Account',
    forgot: busy ? 'Sending…' : 'Email Me a Reset Code',
    reset: busy ? 'Saving…' : 'Set New Password',
  }[mode];

  return (
    <div style={{ padding: '16px', backgroundColor: '#0b0f19', borderRadius: '10px', border: '1px solid #1e293b' }}>
      <div style={{ fontSize: '0.9rem', fontWeight: 600, marginBottom: '10px', color: '#f8fafc' }}>{TITLES[mode]}</div>
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
        <input
          type="email"
          placeholder="Email Address"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
          className="roam-input"
        />
        {mode === 'reset' && resetLink && (
          <div style={{ fontSize: '0.75rem', color: '#6ee7b7', lineHeight: 1.4 }}>Your reset code was filled in from the email. Choose a new password.</div>
        )}
        {mode === 'reset' && (
          <input
            type="text"
            placeholder="Reset code from the email"
            autoComplete="one-time-code"
            value={resetCode}
            onChange={(e) => setResetCode(e.target.value)}
            required
            className="roam-input"
          />
        )}
        {needsPassword && (
          <input
            type="password"
            placeholder={mode === 'reset' ? 'New password' : 'Password'}
            autoComplete={mode === 'signin' ? 'current-password' : 'new-password'}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={mode === 'signin' ? undefined : 8}
            className="roam-input"
          />
        )}
        {(mode === 'register' || mode === 'reset') && (
          <div style={{ fontSize: '0.72rem', color: '#94a3b8', lineHeight: 1.4 }}>
            At least 8 characters, with an uppercase letter, a lowercase letter, a number and a symbol.
          </div>
        )}
        {mode === 'register' && (
          <label style={{ display: 'flex', gap: '8px', alignItems: 'flex-start', fontSize: '0.75rem', color: '#cbd5e1', lineHeight: 1.4 }}>
            <input type="checkbox" checked={acceptedTerms} onChange={(e) => setAcceptedTerms(e.target.checked)} required style={{ marginTop: '2px' }} />
            <span>
              I am 18 or older and agree to the{' '}
              <a href="/terms.html" target="_blank" rel="noopener" style={{ color: '#a5b4fc' }}>Terms</a> and{' '}
              <a href="/privacy.html" target="_blank" rel="noopener" style={{ color: '#a5b4fc' }}>Privacy Policy</a>.
            </span>
          </label>
        )}

        {notice && <div role="status" style={{ color: '#6ee7b7', fontSize: '0.75rem', lineHeight: 1.4 }}>{notice}</div>}
        {error && <div role="alert" style={{ color: '#f87171', fontSize: '0.75rem', lineHeight: 1.4 }}>{error}</div>}

        <button type="submit" className="roam-btn-primary" disabled={busy}>{submitLabel}</button>

        {mode === 'signin' && (
          <>
            <button type="button" onClick={() => switchMode('register')} style={linkButton}>Don't have an account? Register</button>
            <button type="button" onClick={() => switchMode('forgot')} style={linkButton}>Forgot your password?</button>
          </>
        )}
        {mode === 'register' && (
          <button type="button" onClick={() => switchMode('signin')} style={linkButton}>Already have an account? Sign In</button>
        )}
        {(mode === 'forgot' || mode === 'reset') && (
          <>
            {mode === 'reset' && (
              <button type="button" onClick={() => switchMode('forgot')} style={linkButton}>Didn't get a code? Send another</button>
            )}
            <button type="button" onClick={resendConfirmation} style={linkButton}>Never confirmed your email? Resend the confirmation email</button>
            <button type="button" onClick={() => switchMode('signin')} style={linkButton}>Back to Sign In</button>
          </>
        )}
      </form>
    </div>
  );
};

export default AuthPanel;
