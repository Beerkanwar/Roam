import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatApiError } from '../src/utils/apiErrors.ts';

test('Identity validation errors become one readable sentence', () => {
  const body = {
    title: 'One or more validation errors occurred.',
    status: 400,
    errors: {
      PasswordRequiresNonAlphanumeric: ['Passwords must have at least one non alphanumeric character.'],
      PasswordRequiresUpper: ["Passwords must have at least one uppercase ('A'-'Z')."],
    },
  };
  assert.equal(
    formatApiError(body, 400),
    "Passwords must have at least one non alphanumeric character. Passwords must have at least one uppercase ('A'-'Z').",
  );
});

test('login failures are explained instead of showing raw JSON', () => {
  assert.equal(formatApiError({ title: 'Unauthorized', status: 401, detail: 'Failed' }, 401), 'Incorrect email or password.');
  assert.match(formatApiError({ status: 401, detail: 'LockedOut' }, 401), /15 minutes/);
  assert.equal(formatApiError(null, 401), 'Incorrect email or password.');
});

test('rate limiting and server errors have friendly messages', () => {
  assert.match(formatApiError('', 429), /wait a minute/);
  assert.match(formatApiError('<html>Bad gateway</html>', 502), /server had a problem/);
});

test('API { error } messages and short text bodies are passed through', () => {
  assert.equal(formatApiError({ error: 'Invalid password for account deletion.' }, 400), 'Invalid password for account deletion.');
  assert.equal(formatApiError('Plain text problem', 400), 'Plain text problem');
  assert.equal(formatApiError({}, 400, 'Fallback'), 'Fallback');
});
