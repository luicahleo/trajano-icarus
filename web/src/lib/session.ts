// El access token vive solo en memoria: nunca en localStorage ni sessionStorage
// (anti-PII). La sesión se restaura por cookie HttpOnly vía renovarSesion().
let token: string | null = null;

export function getAccessToken(): string | null {
  return token;
}

export function setAccessToken(nuevo: string | null): void {
  token = nuevo;
}

export function clearAccessToken(): void {
  token = null;
}
