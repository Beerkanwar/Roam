/**
 * Sanitizes chat messages and display names against HTML/script injection vectors
 * while preserving valid plain-text formatting and mathematical symbols.
 */
export function sanitizeMessageText(raw: string | null | undefined): string {
    if (!raw) return '';
    return raw.replace(/<\/?[a-zA-Z!][^>]*>/g, '').trim();
}
