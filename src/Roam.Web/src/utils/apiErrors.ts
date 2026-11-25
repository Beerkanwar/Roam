/**
 * Turns an API error response body into one readable sentence.
 * Handles the shapes the Roam API returns: { error }, ProblemDetails ({ title, detail }) and
 * ValidationProblemDetails ({ errors: { field: [messages] } }) from ASP.NET Core Identity.
 */
export function formatApiError(body: unknown, status: number, fallback = 'Something went wrong. Please try again.'): string {
  if (status === 429) return 'Too many attempts. Please wait a minute and try again.';

  if (body && typeof body === 'object') {
    const data = body as Record<string, unknown>;

    if (data.errors && typeof data.errors === 'object') {
      const messages = Object.values(data.errors as Record<string, unknown>)
        .flatMap((value) => (Array.isArray(value) ? value : [value]))
        .filter((m): m is string => typeof m === 'string' && m.trim().length > 0);
      if (messages.length > 0) return Array.from(new Set(messages)).join(' ');
    }

    if (typeof data.error === 'string' && data.error.trim()) return data.error;

    if (typeof data.detail === 'string' && data.detail.trim()) {
      // Identity's /login answers 401 with detail "Failed", "LockedOut", "NotAllowed" or "RequiresTwoFactor".
      switch (data.detail) {
        case 'Failed':
          return 'Incorrect email or password.';
        case 'LockedOut':
          return 'Too many failed sign-in attempts. Please try again in 15 minutes.';
        case 'NotAllowed':
          return 'This account is not allowed to sign in yet.';
        default:
          return data.detail;
      }
    }

    if (typeof data.title === 'string' && data.title.trim() && status !== 401) return data.title;
  }

  if (typeof body === 'string' && body.trim() && body.trim().length < 300 && !body.trim().startsWith('<')) {
    return body.trim();
  }

  if (status === 401) return 'Incorrect email or password.';
  if (status >= 500) return 'The server had a problem. Please try again in a moment.';
  return fallback;
}

/** Reads a fetch Response's body (JSON or text) and formats it with {@link formatApiError}. */
export async function readApiError(response: Response, fallback?: string): Promise<string> {
  const text = await response.text().catch(() => '');
  let body: unknown = text;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    // Not JSON: keep the text.
  }
  return formatApiError(body, response.status, fallback);
}
