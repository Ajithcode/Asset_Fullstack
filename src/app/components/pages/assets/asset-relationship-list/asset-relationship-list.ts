import { Component, HostListener, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LucideAngularModule, ArrowLeft, ChevronDown, ChevronRight, Network, Plus, Search, Trash2, X } from 'lucide-angular';
import { ASSET_RECORDS, AssetRecord, assetLocation, findAsset } from '../asset-data';
import { FROM_RELATIONSHIPS, UserRelationship, nowLocal, readUserRelationships, writeUserRelationships } from './user-relationships';
import { loadAssetDetail } from '../asset-details/asset-details';

type Step = 'main' | 'sub';

/** All the sub assets linked to one main asset: one row in the table. */
interface MainGroup {
  mainTag: string;
  links: UserRelationship[];
  /** Newest link, yyyy-MM-ddTHH:mm. */
  updatedAt: string;
}

/** A sub asset of a selected asset, as shown in the review popup. */
interface SubRow {
  tag: string;
  name: string;
  relation: string;
  category: string;
  location: string;
  /** In the asset register (its name links to its own details). */
  inRegister: boolean;
}

/** One selected asset with everything related to it. */
interface AssetPreview {
  asset: AssetRecord;
  subs: SubRow[];
  loading: boolean;
}

const CURRENT_USER = 'Rex Kumar';

@Component({
  selector: 'app-asset-relationship-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-relationship-list.html',
  // Shares the asset pages' card, button, table and modal styles
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    './asset-relationship-list.css',
  ],
})
export class AssetRelationshipList implements OnDestroy {
  readonly icons = { ArrowLeft, ChevronDown, ChevronRight, Network, Plus, Search, Trash2, X };

  relationships: UserRelationship[] = readUserRelationships();
  /** Opens the Relationships tab on these links rather than the asset's own data. */
  readonly fromRelationships = FROM_RELATIONSHIPS;

  /** Which picker is open; null when none is. */
  step: Step | null = null;
  /** Main assets ticked in the table on the page, in the order they were picked. */
  selectedMains: string[] = [];
  mainSearch = '';
  /** Main assets the open sub asset popup links to (the page selection, or one row's main asset). */
  mainTags: string[] = [];
  /** Open the last picked main asset's details page after saving (the "+ Asset Relationship" flow). */
  private openAfterSave = false;
  pickerSearch = '';
  /** Sub assets ticked in the sub picker. */
  readonly selectedSubs = new Set<string>();

  search = '';
  deleteId: number | null = null;
  /** Main asset whose links are all being removed. */
  deleteMain: string | null = null;
  /** Main assets whose sub asset list is open. */
  readonly expanded = new Set<string>();

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  private write(): void {
    writeUserRelationships(this.relationships);
  }

  // ===== Lookups =====

  asset(tag: string): AssetRecord | undefined {
    return findAsset(tag);
  }

  /** The most recently picked main asset. */
  get mainTag(): string {
    return this.mainTags[this.mainTags.length - 1] ?? '';
  }

  get mainAsset(): AssetRecord | undefined {
    return findAsset(this.mainTag);
  }

  location(a: AssetRecord): string {
    return assetLocation(a);
  }

  /** Assets offered in the open popup: the sub asset popup leaves out the chosen main assets. */
  get pickerAssets(): AssetRecord[] {
    const q = this.pickerSearch.trim().toLowerCase();
    return ASSET_RECORDS.filter(
      (a) =>
        (this.step !== 'sub' || !this.mainTags.includes(a.tag)) &&
        (!q || [a.name, a.tag, a.category, assetLocation(a)].some((v) => v.toLowerCase().includes(q))),
    );
  }

  /** Chosen main assets this sub asset is already linked to. */
  linkedMains(subTag: string): string[] {
    return this.mainTags.filter((m) => this.relationships.some((r) => r.mainTag === m && r.subTag === subTag));
  }

  /** True when the sub asset is already linked to every chosen main asset (nothing left to add). */
  isLinked(subTag: string): boolean {
    return this.step === 'sub' && this.mainTags.length > 0 && this.linkedMains(subTag).length === this.mainTags.length;
  }

  get filtered(): UserRelationship[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.relationships;
    return this.relationships.filter((r) =>
      [r.mainTag, r.subTag, findAsset(r.mainTag)?.name ?? '', findAsset(r.subTag)?.name ?? ''].some((v) => v.toLowerCase().includes(q)),
    );
  }

  /** Links grouped by main asset, most recently changed first; a search keeps a group when its main or any sub asset matches. */
  get groups(): MainGroup[] {
    const q = this.search.trim().toLowerCase();
    const map = new Map<string, MainGroup>();
    for (const r of this.relationships) {
      let g = map.get(r.mainTag);
      if (!g) map.set(r.mainTag, (g = { mainTag: r.mainTag, links: [], updatedAt: r.createdAt }));
      g.links.push(r);
      if (r.createdAt > g.updatedAt) g.updatedAt = r.createdAt;
    }
    const matches = (tag: string) => !q || tag.toLowerCase().includes(q) || (findAsset(tag)?.name ?? '').toLowerCase().includes(q);
    return [...map.values()]
      .filter((g) => matches(g.mainTag) || g.links.some((l) => matches(l.subTag)))
      .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  }

  /** Sub asset names that fit in the row before "+N more". */
  readonly previewCount = 2;

  toggleExpand(tag: string): void {
    if (this.expanded.has(tag)) this.expanded.delete(tag);
    else this.expanded.add(tag);
  }

  /** Opens the sub asset picker for a main asset that already has links. */
  addSubsTo(mainTag: string): void {
    this.mainTags = [mainTag];
    this.openAfterSave = false;
    this.pickerSearch = '';
    this.selectedSubs.clear();
    this.step = 'sub';
  }

  get deletingMain(): MainGroup | undefined {
    return this.deleteMain ? this.groups.find((g) => g.mainTag === this.deleteMain) ?? {
      mainTag: this.deleteMain, links: this.relationships.filter((r) => r.mainTag === this.deleteMain), updatedAt: '',
    } : undefined;
  }

  confirmDeleteMain(): void {
    const tag = this.deleteMain;
    if (!tag) return;
    const n = this.relationships.filter((r) => r.mainTag === tag).length;
    this.relationships = this.relationships.filter((r) => r.mainTag !== tag);
    this.write();
    this.expanded.delete(tag);
    this.deleteMain = null;
    this.showToast(`${n} sub asset${n === 1 ? '' : 's'} unlinked from ${tag}.`);
  }

  get deleting(): UserRelationship | undefined {
    return this.relationships.find((r) => r.id === this.deleteId);
  }

  // ===== Flow =====

  /** Assets listed in the main asset table on the page. */
  get mainList(): AssetRecord[] {
    const q = this.mainSearch.trim().toLowerCase();
    return ASSET_RECORDS.filter((a) => !q || a.name.toLowerCase().includes(q) || a.tag.toLowerCase().includes(q));
  }

  isMain(tag: string): boolean {
    return this.selectedMains.includes(tag);
  }

  toggleMain(a: AssetRecord): void {
    this.selectedMains = this.isMain(a.tag) ? this.selectedMains.filter((t) => t !== a.tag) : [...this.selectedMains, a.tag];
  }

  get allMainsChecked(): boolean {
    return this.mainList.length > 0 && this.mainList.every((a) => this.isMain(a.tag));
  }

  toggleAllMains(on: boolean): void {
    const tags = this.mainList.map((a) => a.tag);
    this.selectedMains = on ? [...this.selectedMains, ...tags.filter((t) => !this.isMain(t))] : this.selectedMains.filter((t) => !tags.includes(t));
  }

  clearMains(): void {
    this.selectedMains = [];
  }

  /** Review popup: each selected asset with its sub assets, category and location. */
  previews: AssetPreview[] | null = null;

  /** Opens the review popup for the assets ticked on the page and loads what is related to them. */
  nextToSubs(): void {
    const assets = this.selectedMains.map((t) => findAsset(t)).filter((a): a is AssetRecord => !!a);
    if (!assets.length) return;
    const previews: AssetPreview[] = assets.map((asset) => ({ asset, subs: [], loading: true }));
    this.previews = previews;
    for (const p of previews) {
      loadAssetDetail(p.asset.tag).then((detail) => {
        const rows: SubRow[] = [];
        const parts = detail?.relationships.parts ?? [];
        // Sub assets: the asset's own child links (components, connected and supporting equipment)...
        for (const l of detail?.relationships.links ?? []) {
          if (l.direction !== 'Child' || l.status !== 'Active') continue;
          const reg = findAsset(l.tag);
          const part = parts.find((x) => x.tag === l.tag);
          rows.push({
            tag: l.tag, name: reg?.name ?? part?.name ?? l.tag, relation: l.type,
            category: reg?.category ?? part?.category ?? '-', location: reg ? assetLocation(reg) : part?.location ?? '-', inRegister: !!reg,
          });
        }
        // ...and sub assets linked on this page
        for (const r of this.relationships) {
          const reg = findAsset(r.subTag);
          if (r.mainTag !== p.asset.tag || !reg || rows.some((x) => x.tag === r.subTag)) continue;
          rows.push({ tag: reg.tag, name: reg.name, relation: 'Sub Asset', category: reg.category, location: assetLocation(reg), inRegister: true });
        }
        p.subs = rows;
        p.loading = false;
      });
    }
  }

  closePreview(): void {
    this.previews = null;
  }

  /** Submit: open the details page of the most recently selected asset. */
  submitPreview(): void {
    const last = this.previews?.[this.previews.length - 1]?.asset.tag;
    if (!last) return;
    this.previews = null;
    this.selectedMains = [];
    this.router.navigate(['/assets/relationships'], { queryParams: { tag: last } });
  }

  /** "Change" in the popup: back to the table on the page, keeping the ticked main assets. */
  backToMain(): void {
    this.closePicker();
  }

  toggleSub(a: AssetRecord): void {
    if (this.isLinked(a.tag)) return;
    if (this.selectedSubs.has(a.tag)) this.selectedSubs.delete(a.tag);
    else this.selectedSubs.add(a.tag);
  }

  /** Sub assets in the current (searched) list that can still be linked. */
  private get selectable(): AssetRecord[] {
    return this.pickerAssets.filter((a) => !this.isLinked(a.tag));
  }

  get allSelected(): boolean {
    return this.selectable.length > 0 && this.selectable.every((a) => this.selectedSubs.has(a.tag));
  }

  toggleAll(on: boolean): void {
    this.selectable.forEach((a) => (on ? this.selectedSubs.add(a.tag) : this.selectedSubs.delete(a.tag)));
  }

  /** Links every ticked sub asset to every chosen main asset (pairs that already exist are skipped). */
  addSubs(): void {
    const subs = ASSET_RECORDS.filter((a) => this.selectedSubs.has(a.tag) && !this.isLinked(a.tag));
    if (!subs.length || !this.mainTags.length) return;
    let id = Math.max(0, ...this.relationships.map((r) => r.id));
    const at = nowLocal();
    const added: UserRelationship[] = [];
    for (const main of this.mainTags) {
      for (const sub of subs) {
        if (sub.tag === main || this.relationships.some((r) => r.mainTag === main && r.subTag === sub.tag)) continue;
        added.push({ id: ++id, mainTag: main, subTag: sub.tag, createdBy: CURRENT_USER, createdAt: at });
      }
    }
    this.relationships = [...added.reverse(), ...this.relationships];
    this.write();
    this.mainTags.forEach((m) => this.expanded.add(m));
    const mains = this.mainTags.length;
    this.showToast(
      mains === 1
        ? `${subs.length} sub asset${subs.length === 1 ? '' : 's'} linked to ${this.mainAsset?.name} (${this.mainTag}).`
        : `${added.length} links saved for ${mains} main assets.`,
    );
    const open = this.openAfterSave ? this.mainTag : '';
    if (this.openAfterSave) this.selectedMains = [];
    this.closePicker();
    // Show the most recently picked main asset with its new sub assets
    if (open) this.router.navigate(['/assets/relationships'], { queryParams: { tag: open, from: FROM_RELATIONSHIPS } });
  }

  closePicker(): void {
    this.step = null;
    this.pickerSearch = '';
    this.selectedSubs.clear();
    this.mainTags = [];
    this.openAfterSave = false;
  }

  confirmDelete(): void {
    const r = this.deleting;
    if (!r) return;
    this.relationships = this.relationships.filter((x) => x.id !== r.id);
    this.write();
    this.deleteId = null;
    this.showToast('Relationship removed.');
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closePreview();
    this.closePicker();
    this.deleteId = null;
    this.deleteMain = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
