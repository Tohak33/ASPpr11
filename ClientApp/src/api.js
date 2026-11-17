export const apiBase = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5134').replace(/\/$/, '');
export const frontendOrigin = import.meta.env.VITE_FRONTEND_ORIGIN || 'http://localhost:5173';
export const blockedOrigin = 'http://127.0.0.1:5173';

const sessionKey = 'para-api-session';
export function restoreSession() {
  try {
    const session = JSON.parse(sessionStorage.getItem(sessionKey));
    if (session?.accessToken && session.user && Date.parse(session.expiresAt) > Date.now()) return session;
    sessionStorage.removeItem(sessionKey);
  } catch {
    sessionStorage.removeItem(sessionKey);
  }
  return null;
}

export function saveSession(session) {
  if (session) sessionStorage.setItem(sessionKey, JSON.stringify(session));
  else sessionStorage.removeItem(sessionKey);
}

export async function callApi(method, path, token, body) {
  const headers = { Accept: 'application/json' };
  if (token) headers.Authorization = 'Bearer ' + token;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  try {
    const response = await fetch(apiBase + path, {
      method, headers, body: body === undefined ? undefined : JSON.stringify(body),
      credentials: 'omit', cache: 'no-store',
    });
    let data = null;
    if (response.headers.get('Content-Type')?.includes('json')) {
      try { data = await response.json(); } catch { /* No readable JSON body. */ }
    }
    return {
      method, path, status: response.status, ok: response.ok, data,
      retryAfter: response.headers.get('Retry-After'),
      totalCount: response.headers.get('X-Total-Count'),
    };
  } catch {
    return { method, path, networkError: true };
  }
}
