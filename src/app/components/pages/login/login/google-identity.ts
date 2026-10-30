/** The parts of Google Identity Services (accounts.google.com/gsi/client) the login page uses. */
interface GoogleAccountsId {
  initialize(config: { client_id: string; callback: (res: { credential: string }) => void; ux_mode?: 'popup'; auto_select?: boolean }): void;
  renderButton(parent: HTMLElement, options: Record<string, unknown>): void;
}

declare global {
  interface Window {
    google?: { accounts: { id: GoogleAccountsId } };
  }
}

const GIS_SRC = 'https://accounts.google.com/gsi/client';
let loading: Promise<GoogleAccountsId> | null = null;

/** Loads the Google Identity Services script once and resolves with `google.accounts.id`. */
export function loadGoogleIdentity(): Promise<GoogleAccountsId> {
  loading ??= new Promise<GoogleAccountsId>((resolve, reject) => {
    if (window.google?.accounts?.id) return resolve(window.google.accounts.id);
    const script = document.createElement('script');
    script.src = GIS_SRC;
    script.async = true;
    script.defer = true;
    script.onload = () => (window.google?.accounts?.id ? resolve(window.google.accounts.id) : reject(new Error('Google sign-in did not load.')));
    script.onerror = () => {
      loading = null;
      reject(new Error('Google sign-in could not be loaded. Check your connection.'));
    };
    document.head.appendChild(script);
  });
  return loading;
}
