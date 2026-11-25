/**
 * Numeric enum values as serialized by the API (System.Text.Json writes C# enums as numbers).
 * Must mirror src/Roam.Domain/Users/*.cs exactly; tests/enum_contract.test.ts enforces this.
 */
export const AgeCategory = {
  Adult: 0,
  Minor: 1,
  Unknown: 2,
} as const;

export const VolunteerStatus = {
  None: 0,
  Pending: 1,
  Approved: 2,
  Revoked: 3,
} as const;
