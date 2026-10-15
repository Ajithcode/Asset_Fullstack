import { findAsset } from '../asset-data';

/**
 * Main asset → sub asset links that users create on the Asset Relationship page (menu).
 * DEMO MODE: saved in this browser until the Assets API is wired up. The asset Relationships tab shows them
 * when it is opened from that page (?from=relationships).
 */
export interface UserRelationship {
  id: number;
  mainTag: string;
  subTag: string;
  createdBy: string;
  /** Local date-time, yyyy-MM-ddTHH:mm. */
  createdAt: string;
}

/** Query param value that opens the Relationships tab on these links instead of the asset's own data. */
export const FROM_RELATIONSHIPS = 'relationships';

const STORAGE_KEY = 'piq.assetRelationships';

export function readUserRelationships(): UserRelationship[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const list = raw ? (JSON.parse(raw) as UserRelationship[]) : [];
    // Drop links to assets no longer in the register
    return Array.isArray(list) ? list.filter((r) => findAsset(r.mainTag) && findAsset(r.subTag)) : [];
  } catch {
    return [];
  }
}

export function writeUserRelationships(list: UserRelationship[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
  } catch {
    // Storage blocked (private window etc.): changes last for this visit only
  }
}

export function nowLocal(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}
