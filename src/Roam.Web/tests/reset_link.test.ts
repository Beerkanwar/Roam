import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readResetLink } from '../src/auth/resetLink.ts';

test('reads email and code from the password-reset email link', () => {
  const code = 'Q2ZESjhBTS9-_abc==';
  const hash = `#reset-password?email=${encodeURIComponent('a+b@example.com')}&code=${encodeURIComponent(code)}`;
  assert.deepEqual(readResetLink(hash), { email: 'a+b@example.com', code });
});

test('ignores other fragments and incomplete links', () => {
  assert.equal(readResetLink(''), null);
  assert.equal(readResetLink('#something-else'), null);
  assert.equal(readResetLink('#reset-password?email=a%40b.com'), null);
});
