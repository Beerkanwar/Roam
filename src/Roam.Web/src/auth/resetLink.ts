/** Reads the "#reset-password?email=...&code=..." link from the password-reset email, if present. */
export function readResetLink(hash: string): { email: string; code: string } | null {
  if (!hash.startsWith('#reset-password?')) return null;
  const params = new URLSearchParams(hash.slice('#reset-password?'.length));
  const email = params.get('email') ?? '';
  const code = params.get('code') ?? '';
  return email && code ? { email, code } : null;
}
