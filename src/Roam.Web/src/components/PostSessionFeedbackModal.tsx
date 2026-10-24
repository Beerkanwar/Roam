import React, { useState } from 'react';

interface PostSessionFeedbackModalProps {
  apiBase: string;
  authFetch: (url: string, options?: RequestInit) => Promise<Response>;
  tourSessionId: string;
  targetUserId: string;
  targetUserName?: string;
  onClose: () => void;
  onSuccess?: () => void;
}

export const PostSessionFeedbackModal: React.FC<PostSessionFeedbackModalProps> = ({
  apiBase,
  authFetch,
  tourSessionId,
  targetUserId,
  targetUserName = 'Participant',
  onClose,
  onSuccess,
}) => {
  const [rating, setRating] = useState<number>(1); // 1 = Positive, 2 = Neutral, 3 = Negative
  const [comments, setComments] = useState<string>('');
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string>('');
  const [submitted, setSubmitted] = useState<boolean>(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!targetUserId) {
      setError('Target user ID is missing.');
      return;
    }

    setSubmitting(true);
    setError('');

    try {
      const response = await authFetch(`${apiBase}/api/v1/trust-safety/feedback`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          tourSessionId,
          targetUserId,
          rating,
          comments: comments.trim() || undefined,
        }),
      });

      if (response.ok) {
        setSubmitted(true);
        if (onSuccess) onSuccess();
      } else {
        const err = await response.json().catch(() => ({}));
        setError(err.error || 'Feedback could not be submitted. Note: feedback is only allowed once per completed session.');
      }
    } catch (err) {
      setError(`Error: ${err}`);
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
      zIndex: 250,
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
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
          <h3 style={{ margin: 0, fontSize: '1.2rem', fontWeight: 700 }}>
            Session Feedback
          </h3>
          <button
            onClick={onClose}
            style={{ background: 'none', border: 'none', color: '#94a3b8', fontSize: '1.4rem', cursor: 'pointer' }}
          >
            ×
          </button>
        </div>

        {submitted ? (
          <div style={{ textAlign: 'center', padding: '20px 0' }}>
            <div style={{ fontSize: '2.5rem', marginBottom: '10px' }}>🌟</div>
            <h4 style={{ margin: '0 0 8px 0', fontSize: '1.1rem', color: '#10b981' }}>Thank You for Your Feedback!</h4>
            <p style={{ fontSize: '0.85rem', color: '#94a3b8', margin: '0 0 16px 0' }}>
              Your feedback helps keep the Roam community safe, reliable, and authentic.
            </p>
            <button
              onClick={onClose}
              style={{
                padding: '9px 20px',
                backgroundColor: '#6366f1',
                color: '#fff',
                border: 'none',
                borderRadius: '8px',
                cursor: 'pointer',
                fontWeight: 600,
              }}
            >
              Done
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            <p style={{ fontSize: '0.85rem', color: '#94a3b8', margin: 0 }}>
              How was your live exploration experience with <strong>{targetUserName}</strong>?
            </p>

            {/* Rating Buttons */}
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                type="button"
                onClick={() => setRating(1)}
                style={{
                  flex: 1,
                  padding: '10px 8px',
                  backgroundColor: rating === 1 ? '#065f46' : '#1e293b',
                  border: `1px solid ${rating === 1 ? '#10b981' : '#334155'}`,
                  borderRadius: '8px',
                  color: rating === 1 ? '#a7f3d0' : '#94a3b8',
                  cursor: 'pointer',
                  fontWeight: 600,
                  fontSize: '0.85rem',
                }}
              >
                👍 Positive
              </button>
              <button
                type="button"
                onClick={() => setRating(2)}
                style={{
                  flex: 1,
                  padding: '10px 8px',
                  backgroundColor: rating === 2 ? '#4338ca' : '#1e293b',
                  border: `1px solid ${rating === 2 ? '#6366f1' : '#334155'}`,
                  borderRadius: '8px',
                  color: rating === 2 ? '#c7d2fe' : '#94a3b8',
                  cursor: 'pointer',
                  fontWeight: 600,
                  fontSize: '0.85rem',
                }}
              >
                😐 Neutral
              </button>
              <button
                type="button"
                onClick={() => setRating(3)}
                style={{
                  flex: 1,
                  padding: '10px 8px',
                  backgroundColor: rating === 3 ? '#7f1d1d' : '#1e293b',
                  border: `1px solid ${rating === 3 ? '#ef4444' : '#334155'}`,
                  borderRadius: '8px',
                  color: rating === 3 ? '#fecaca' : '#94a3b8',
                  cursor: 'pointer',
                  fontWeight: 600,
                  fontSize: '0.85rem',
                }}
              >
                👎 Negative
              </button>
            </div>

            {/* Optional Comments */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
              <label style={{ fontSize: '0.78rem', color: '#94a3b8' }}>
                Comments (Optional):
              </label>
              <textarea
                value={comments}
                onChange={(e) => setComments(e.target.value)}
                placeholder="Share your experience (cultural insights, guidance, audio quality)..."
                rows={3}
                maxLength={500}
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

            {error && (
              <div style={{ color: '#f87171', fontSize: '0.78rem' }}>{error}</div>
            )}

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
                Skip
              </button>
              <button
                type="submit"
                disabled={submitting}
                style={{
                  flex: 2,
                  padding: '9px',
                  backgroundColor: '#6366f1',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '8px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  fontSize: '0.85rem',
                }}
              >
                {submitting ? 'Submitting...' : 'Submit Feedback'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};
export default PostSessionFeedbackModal;
