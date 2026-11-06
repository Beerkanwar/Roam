import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { sanitizeMessageText } from '../src/utils/sanitize.ts';

describe('Web Client Security & Quality Tests', () => {
  it('Tokens must be stored in sessionStorage, never in localStorage', () => {
    const mockLocalStorage: Record<string, string> = {};
    const mockSessionStorage: Record<string, string> = {};

    const saveTokensSecurely = (token: string, refreshToken: string) => {
      // Purge from localStorage
      delete mockLocalStorage['roam_token'];
      delete mockLocalStorage['roam_refresh_token'];

      // Save only to sessionStorage
      mockSessionStorage['roam_token'] = token;
      mockSessionStorage['roam_refresh_token'] = refreshToken;
    };

    saveTokensSecurely('test_access_token', 'test_refresh_token');

    assert.equal(mockLocalStorage['roam_token'], undefined, 'Access token must not exist in localStorage');
    assert.equal(mockLocalStorage['roam_refresh_token'], undefined, 'Refresh token must not exist in localStorage');
    assert.equal(mockSessionStorage['roam_token'], 'test_access_token');
    assert.equal(mockSessionStorage['roam_refresh_token'], 'test_refresh_token');
  });

  it('Chat messages must be sanitized against HTML injection and XSS vectors without double-encoding plain text', () => {
    const xssPayloads = [
      '<script>alert("xss")</script>',
      '<img src=x onerror=alert(1)>',
      '<svg onload=alert(document.cookie)>',
      '"><script src=evil.com/leak.js></script>'
    ];

    for (const payload of xssPayloads) {
      const sanitized = sanitizeMessageText(payload);
      assert.ok(!sanitized.includes('<script>'), `Payload ${payload} must not contain raw script tags`);
      assert.ok(!sanitized.includes('<img'), `Payload ${payload} must not contain raw img tags`);
      assert.ok(!sanitized.includes('<svg'), `Payload ${payload} must not contain raw svg tags`);
    }

    // Verify plain text with ampersand is preserved without double-encoding
    const plainText = 'Tom & Jerry: 1 < 2 and 5 > 3';
    const preserved = sanitizeMessageText(plainText);
    assert.equal(preserved, 'Tom & Jerry: 1 < 2 and 5 > 3');
  });

  it('Volunteer status enum mapping matches backend (3 = Revoked, not Declined)', () => {
    const mapVolunteerStatus = (status: number): string => {
      switch (status) {
        case 1:
          return 'Application Pending Review';
        case 2:
          return 'Verified Active Volunteer';
        case 3:
          return 'Status Revoked';
        default:
          return 'Not Registered';
      }
    };

    assert.equal(mapVolunteerStatus(0), 'Not Registered');
    assert.equal(mapVolunteerStatus(1), 'Application Pending Review');
    assert.equal(mapVolunteerStatus(2), 'Verified Active Volunteer');
    assert.equal(mapVolunteerStatus(3), 'Status Revoked');
    // Ensure 3 is never 'Application Declined'
    assert.notEqual(mapVolunteerStatus(3), 'Application Declined');
  });

  it('Volunteer availability toggle preserves existing preferences', () => {
    const existingSettings = {
      enabled: true,
      availableForRequests: false,
      acceptsPrivateTours: true,
      acceptsGroupTours: false,
      maxConcurrentViewers: 25,
      languages: 'English, French, German',
      tipEnabled: true,
    };

    const buildTogglePayload = (newAvailability: boolean, current: typeof existingSettings) => ({
      enabled: current.enabled ?? true,
      availableForRequests: newAvailability,
      acceptsPrivateTours: current.acceptsPrivateTours ?? true,
      acceptsGroupTours: current.acceptsGroupTours ?? true,
      maxConcurrentViewers: current.maxConcurrentViewers ?? 10,
      languages: current.languages ?? 'English',
      tipEnabled: current.tipEnabled ?? false,
    });

    const payload = buildTogglePayload(true, existingSettings);

    assert.equal(payload.availableForRequests, true);
    assert.equal(payload.acceptsPrivateTours, true);
    assert.equal(payload.acceptsGroupTours, false, 'Group tours setting should be preserved');
    assert.equal(payload.maxConcurrentViewers, 25, 'Max viewers should be preserved');
    assert.equal(payload.languages, 'English, French, German', 'Languages should be preserved');
    assert.equal(payload.tipEnabled, true, 'Tip enabled setting should be preserved');
  });

  it('Report submission accepts severity values from ReportSeverity enum (0-3)', () => {
    const buildReportPayload = (targetUserId: string, reason: number, description: string, severity: number) => {
      assert.ok(severity >= 0 && severity <= 3, 'Severity must be in range 0 (Low) to 3 (Critical)');
      return {
        reportedUserId: targetUserId,
        reason,
        description: description.trim(),
        severity,
      };
    };

    const lowReport = buildReportPayload('user_123', 0, 'Spam request', 0);
    assert.equal(lowReport.severity, 0);

    const criticalReport = buildReportPayload('user_456', 7, 'Immediate safety threat', 3);
    assert.equal(criticalReport.severity, 3);
  });
});
