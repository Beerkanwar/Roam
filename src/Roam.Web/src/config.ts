/**
 * Base URL of the Roam API, without a trailing slash.
 * - Production (Docker image): VITE_API_BASE_URL is empty, so the API is called on the same origin
 *   (the web server proxies /api, /hubs and the sign-in endpoints to the API).
 * - Development: set VITE_API_BASE_URL in .env.local, or the local API's default HTTPS port is used.
 */
const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;

export const API_BASE = (configured ?? (import.meta.env.DEV ? 'https://localhost:7217' : '')).replace(/\/+$/, '');
