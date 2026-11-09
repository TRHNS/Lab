// ЛР 6: клієнтський antiforgery helper. Підключається на кожній сторінці з mutation
(() => {
  const TOKEN_URL = "/api/auth/csrf-token";
  const HEADER = "X-CSRF-TOKEN";
  let token = null;
  let pending = null;
  let generation = 0;

  // Забути токен (після login/logout або відмови)
  function reset() {
    token = null;
    pending = null;
    generation++;
  }

  // Lazy: токен запитується лише перед першою mutation на цій сторінці
  async function ensureToken() {
    if (token) return token;
    if (pending) return pending;
    const current = generation;
    const operation = (async () => {
      const response = await fetch(TOKEN_URL, { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) throw new Error(`Token request failed: ${response.status}`);
      const data = await response.json();
      if (current !== generation) throw new Error("Session changed; request a fresh token.");
      if (typeof data.token !== "string" || !data.token) throw new Error("Invalid token response.");
      token = data.token;
      return token;
    })();
    pending = operation;
    try { return await operation; }
    finally { if (pending === operation) pending = null; }
  }

  // Змінювальний запит із header; лише same-origin; без автоматичного повтору
  async function mutate(url, options = {}) {
    const method = (options.method || "POST").toUpperCase();
    if (!["POST", "PUT", "PATCH", "DELETE"].includes(method)) {
      throw new Error("Use normal fetch for read requests.");
    }
    const target = new URL(url, location.href);
    if (target.origin !== location.origin) throw new Error("Token must stay on the same origin.");
    const headers = new Headers(options.headers);
    headers.set(HEADER, await ensureToken());
    const response = await fetch(target, { ...options, method, headers, credentials: "same-origin" });
    if (response.status === 400 || response.status === 401) reset();
    return response;
  }

  window.SecureLabCsrf = { reset, ensureToken, mutate };
  window.addEventListener("pageshow", reset);
})();