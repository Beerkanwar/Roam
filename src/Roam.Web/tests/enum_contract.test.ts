import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { AgeCategory, VolunteerStatus } from '../src/utils/enums.ts';

/** Parses `Name = 0,` / `Name,` members of a C# enum, numbering implicit members like the compiler does. */
function readCSharpEnum(relativePath: string): Record<string, number> {
  const source = readFileSync(fileURLToPath(new URL(relativePath, import.meta.url)), 'utf-8');
  const body = source.slice(source.indexOf('{') + 1, source.lastIndexOf('}'));
  const members: Record<string, number> = {};
  let next = 0;
  for (const raw of body.split(',')) {
    const entry = raw.replace(/\/\/.*$/gm, '').trim();
    if (!entry) continue;
    const [name, value] = entry.split('=').map((p) => p.trim());
    const resolved = value === undefined ? next : Number.isNaN(Number(value)) ? members[value] : Number(value);
    members[name] = resolved;
    next = resolved + 1;
  }
  return members;
}

describe('Enum contract with the API', () => {
  it('AgeCategory matches Roam.Domain.Users.AgeCategory', () => {
    assert.deepEqual({ ...AgeCategory }, readCSharpEnum('../../Roam.Domain/Users/AgeCategory.cs'));
  });

  it('VolunteerStatus matches Roam.Domain.Users.VolunteerStatus', () => {
    assert.deepEqual({ ...VolunteerStatus }, readCSharpEnum('../../Roam.Domain/Users/VolunteerStatus.cs'));
  });
});
