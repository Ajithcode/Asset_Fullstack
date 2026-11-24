import { AssetRelationshipDto, RegisteredAsset, SubAssetDto, subAssetKey } from './asset-relationship.service';

/** One end of a relationship: a registered asset, or a component of the source asset. */
export interface RelEnd {
  /** Graph key: the Asset ID for a registered asset, "component:<id>" for a component. */
  key: string;
  /** Asset ID (registered asset) or null (component). */
  assetId: string | null;
  name: string;
  tag: string;
  isComponent: boolean;
  site: string;
  location: string;
  criticality: string;
}

/**
 * One relationship as the page lists it: a link stored in the API record of the source (main) asset.
 * The API keeps one record per main asset with its linked (sub) assets; each link is one relationship here.
 */
export interface RelRow {
  /** Unique on the page: "<record id>|<link key>". */
  id: string;
  /** API record (the source asset's relationship record). */
  recordId: string;
  /** Link key within the record ("asset:<id>" / "component:<id>"). */
  linkKey: string;
  source: RelEnd;
  target: RelEnd;
  type: string;
  /** Site of the source asset, else of the target. */
  site: string;
  /** The higher criticality of the two assets ('' when neither has one). */
  criticality: string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  sub: SubAssetDto;
}

/** Criticality levels from lowest to highest (Asset Registration values). */
export const CRITICALITY_ORDER = ['Low', 'Medium', 'High', 'Critical'];

export function criticalityRank(level: string): number {
  return CRITICALITY_ORDER.findIndex((c) => c.toLowerCase() === (level || '').toLowerCase());
}

/** A dependency is critical when either asset is High or Critical. */
export function isCriticalDependency(row: RelRow): boolean {
  return criticalityRank(row.criticality) >= criticalityRank('High');
}

function higher(a: string, b: string): string {
  return criticalityRank(a) >= criticalityRank(b) ? a : b;
}

function endFromAsset(id: string, asset: RegisteredAsset | undefined, fallbackName: string, fallbackLocation: string): RelEnd {
  return {
    key: id,
    assetId: id,
    name: asset?.name || fallbackName || id,
    tag: asset?.assetTag || '',
    isComponent: false,
    site: asset?.site || '',
    location: asset?.location || fallbackLocation || '',
    criticality: asset?.criticality || '',
  };
}

/** Flattens the API records into one row per link, joined with the asset register for site and criticality. */
export function toRows(records: AssetRelationshipDto[], assets: Map<string, RegisteredAsset>): RelRow[] {
  const rows: RelRow[] = [];
  for (const r of records) {
    const source = endFromAsset(r.mainAssetRegistrationId, assets.get(r.mainAssetRegistrationId), r.mainAssetName ?? '', r.mainLocation ?? '');
    for (const s of r.subAssets) {
      const linkKey = subAssetKey(s);
      const target: RelEnd = s.componentId
        ? {
            key: `component:${s.componentId}`,
            assetId: null,
            name: s.name || s.tag || s.componentId,
            tag: s.tag ?? '',
            isComponent: true,
            site: source.site,
            location: s.location ?? source.location,
            criticality: '',
          }
        : endFromAsset(s.assetRegistrationId ?? '', assets.get(s.assetRegistrationId ?? ''), s.name, s.location ?? '');
      rows.push({
        id: `${r.id}|${linkKey}`,
        recordId: r.id,
        linkKey,
        source,
        target,
        type: s.relationshipType,
        site: source.site || target.site,
        criticality: higher(source.criticality, target.criticality),
        createdBy: r.createdBy ?? '',
        createdAt: r.createdAt ?? '',
        updatedAt: r.lastUpdated ?? r.updatedAt ?? r.createdAt ?? '',
        sub: s,
      });
    }
  }
  return rows;
}

/** An asset reached from the failing asset, with the chain that leads to it. */
export interface ImpactHit {
  end: RelEnd;
  /** Hops from the failing asset (1 = directly related). */
  depth: number;
  /** The relationship that reaches it. */
  via: RelRow;
  /** Asset names from the failing asset to this one. */
  path: string[];
}

/**
 * Assets that may be affected when `startKey` fails: everything reachable downstream (source -> target) through the
 * relationships, breadth first, each asset once (cycle-safe).
 */
export function impactOf(startKey: string, rows: RelRow[]): ImpactHit[] {
  const out = new Map<string, RelRow[]>();
  for (const r of rows) out.set(r.source.key, [...(out.get(r.source.key) ?? []), r]);
  const seen = new Set<string>([startKey]);
  const hits: ImpactHit[] = [];
  const start = rows.find((r) => r.source.key === startKey)?.source ?? rows.find((r) => r.target.key === startKey)?.target;
  let frontier: { key: string; path: string[] }[] = [{ key: startKey, path: [start?.name ?? startKey] }];
  for (let depth = 1; frontier.length; depth++) {
    const next: { key: string; path: string[] }[] = [];
    for (const f of frontier) {
      for (const r of out.get(f.key) ?? []) {
        if (seen.has(r.target.key)) continue;
        seen.add(r.target.key);
        const path = [...f.path, r.target.name];
        hits.push({ end: r.target, depth, via: r, path });
        next.push({ key: r.target.key, path });
      }
    }
    frontier = next;
  }
  return hits;
}

export interface DiagramNode {
  end: RelEnd;
  x: number;
  y: number;
}

export interface DiagramEdge {
  row: RelRow;
  from: DiagramNode;
  to: DiagramNode;
}

export const NODE_W = 184;
export const NODE_H = 52;
const COL_GAP = 96;
const ROW_GAP = 26;

/**
 * Layered left-to-right layout: an asset's column is the longest chain of relationships leading to it (assets with
 * none start at column 0); cycles are cut so every asset gets a column.
 */
export function layout(rows: RelRow[]): { nodes: DiagramNode[]; edges: DiagramEdge[]; width: number; height: number } {
  const ends = new Map<string, RelEnd>();
  const incoming = new Map<string, string[]>();
  for (const r of rows) {
    ends.set(r.source.key, r.source);
    ends.set(r.target.key, r.target);
    incoming.set(r.target.key, [...(incoming.get(r.target.key) ?? []), r.source.key]);
  }
  const level = new Map<string, number>();
  const visiting = new Set<string>();
  const levelOf = (key: string): number => {
    if (level.has(key)) return level.get(key)!;
    if (visiting.has(key)) return 0; // cycle: cut here
    visiting.add(key);
    const parents = incoming.get(key) ?? [];
    const l = parents.length ? Math.max(...parents.map((p) => levelOf(p) + 1)) : 0;
    visiting.delete(key);
    level.set(key, l);
    return l;
  };
  const columns: RelEnd[][] = [];
  for (const [key, end] of ends) {
    const l = levelOf(key);
    (columns[l] ??= []).push(end);
  }
  const nodes = new Map<string, DiagramNode>();
  let height = 0;
  columns.forEach((col, c) => {
    col.sort((a, b) => a.name.localeCompare(b.name));
    col.forEach((end, i) => {
      const node = { end, x: c * (NODE_W + COL_GAP), y: i * (NODE_H + ROW_GAP) };
      nodes.set(end.key, node);
      height = Math.max(height, node.y + NODE_H);
    });
  });
  const edges = rows.map((row) => ({ row, from: nodes.get(row.source.key)!, to: nodes.get(row.target.key)! }));
  return { nodes: [...nodes.values()], edges, width: Math.max(0, columns.length * (NODE_W + COL_GAP) - COL_GAP), height };
}
