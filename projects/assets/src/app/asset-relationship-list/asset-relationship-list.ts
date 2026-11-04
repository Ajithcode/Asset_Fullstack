import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LucideAngularModule, ArrowLeft, ChevronDown, ChevronRight, Network, Plus, Search, Trash2, X } from 'lucide-angular';
import { Observable } from 'rxjs';
import { apiErrorMessage } from '../services/asset-api';
import { FROM_RELATIONSHIPS } from './user-relationships';
import {
  AssetRelationshipDto,
  AssetRelationshipService,
  RegisteredAsset,
  SubAssetDto,
  subAssetKey,
  toSubAssetRef,
} from './asset-relationship.service';

type Step = 'main' | 'sub';

/** An asset in the register as listed on this page; `tag` is its Asset ID. */
interface PickAsset {
  tag: string;
  name: string;
  category: string;
  location: string;
  status: string;
}

/** One sub asset linked to a main asset. */
interface Link {
  /** Unique within its main asset: "asset:<id>" or "component:<id>". */
  key: string;
  /** Asset ID for a registered asset; component tag for a component. */
  subTag: string;
  subName: string;
  category: string;
  /** Registered asset (its name links to its own Relationships tab) rather than a component. */
  isAsset: boolean;
  createdBy: string;
  createdAt: string;
  sub: SubAssetDto;
}

/** All the sub assets linked to one main asset: one row in the table. */
interface MainGroup {
  /** Saved relationship record id. */
  id: string;
  mainTag: string;
  mainName: string;
  links: Link[];
  createdBy: string;
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
  sub: SubAssetDto;
}

/** One selected asset with everything related to it. */
interface AssetPreview {
  asset: PickAsset;
  subs: SubRow[];
  loading: boolean;
}

function toPickAsset(a: RegisteredAsset): PickAsset {
  return { tag: a.id, name: a.name, category: a.category, location: a.location, status: a.status };
}

function toGroup(r: AssetRelationshipDto): MainGroup {
  const createdBy = r.createdBy ?? '-';
  const at = r.lastUpdated ?? r.createdAt ?? '';
  return {
    id: r.id,
    mainTag: r.mainAssetRegistrationId,
    mainName: r.mainAssetName || r.mainAssetRegistrationId,
    createdBy,
    updatedAt: at,
    links: r.subAssets.map((s) => ({
      key: subAssetKey(s),
      subTag: s.assetRegistrationId ?? (s.tag || s.componentId || ''),
      subName: s.name,
      category: s.category ?? '-',
      isAsset: !!s.assetRegistrationId,
      // The API keeps who / when per main asset, not per sub asset.
      createdBy,
      createdAt: at,
      sub: s,
    })),
  };
}

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
export class AssetRelationshipList implements OnInit, OnDestroy {
  readonly icons = { ArrowLeft, ChevronDown, ChevronRight, Network, Plus, Search, Trash2, X };

  /** Saved relationships, one per main asset. */
  rels: AssetRelationshipDto[] = [];
  /** Assets in the register (main asset table and sub asset picker). */
  assets: PickAsset[] = [];
  /** Opens the Relationships tab on these links rather than the asset's own data. */
  readonly fromRelationships = FROM_RELATIONSHIPS;

  loadingAssets = false;
  loadingRels = false;
  /** Shown above the tables when a list could not be loaded. */
  loadError = '';
  /** True while a save / delete request is running. */
  saving = false;
  /** Error from the open popup's request. */
  popupError = '';

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
  /** Link being removed: its main asset and key. */
  deleteId: { mainTag: string; key: string } | null = null;
  /** Main asset whose links are all being removed. */
  deleteMain: string | null = null;
  /** Main assets whose sub asset list is open. */
  readonly expanded = new Set<string>();

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);
  private readonly api = inject(AssetRelationshipService);

  ngOnInit(): void {
    this.loadAssets();
    this.loadRelationships();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  loadAssets(): void {
    this.loadingAssets = true;
    this.api.getAssets().subscribe({
      next: (list) => {
        this.assets = list.map(toPickAsset);
        this.loadingAssets = false;
      },
      error: (err) => {
        this.loadingAssets = false;
        this.loadError = apiErrorMessage(err, 'Could not load the asset list.');
      },
    });
  }

  loadRelationships(): void {
    this.loadingRels = true;
    this.api.getRelationships().subscribe({
      next: (list) => {
        this.rels = list;
        this.loadingRels = false;
      },
      error: (err) => {
        this.loadingRels = false;
        this.loadError = apiErrorMessage(err, 'Could not load the asset relationships.');
      },
    });
  }

  retry(): void {
    this.loadError = '';
    this.loadAssets();
    this.loadRelationships();
  }

  // ===== Lookups =====

  asset(tag: string): PickAsset | undefined {
    return this.assets.find((a) => a.tag === tag);
  }

  /** Name of an asset for messages and chips: from the register, else from a saved relationship. */
  nameOf(tag: string): string {
    return this.asset(tag)?.name ?? this.rels.find((r) => r.mainAssetRegistrationId === tag)?.mainAssetName ?? tag;
  }

  /** The most recently picked main asset. */
  get mainTag(): string {
    return this.mainTags[this.mainTags.length - 1] ?? '';
  }

  get mainAsset(): PickAsset | undefined {
    return this.asset(this.mainTag);
  }

  location(a: PickAsset): string {
    return a.location || '-';
  }

  /** Total sub asset links across all main assets. */
  get linkCount(): number {
    return this.rels.reduce((sum, r) => sum + r.subAssets.length, 0);
  }

  /** Assets offered in the open popup: the sub asset popup leaves out the chosen main assets. */
  get pickerAssets(): PickAsset[] {
    const q = this.pickerSearch.trim().toLowerCase();
    return this.assets.filter(
      (a) =>
        (this.step !== 'sub' || !this.mainTags.includes(a.tag)) &&
        (!q || [a.name, a.tag, a.category, a.location].some((v) => v.toLowerCase().includes(q))),
    );
  }

  private savedFor(mainTag: string): AssetRelationshipDto | undefined {
    return this.rels.find((r) => r.mainAssetRegistrationId === mainTag);
  }

  /** Chosen main assets this sub asset is already linked to. */
  linkedMains(subTag: string): string[] {
    return this.mainTags.filter((m) => this.savedFor(m)?.subAssets.some((s) => s.assetRegistrationId === subTag));
  }

  /** True when the sub asset is already linked to every chosen main asset (nothing left to add). */
  isLinked(subTag: string): boolean {
    return this.step === 'sub' && this.mainTags.length > 0 && this.linkedMains(subTag).length === this.mainTags.length;
  }

  /** Links grouped by main asset, most recently changed first; a search keeps a group when its main or any sub asset matches. */
  get groups(): MainGroup[] {
    const q = this.search.trim().toLowerCase();
    const matches = (...values: string[]) => !q || values.some((v) => v.toLowerCase().includes(q));
    return this.rels
      .map(toGroup)
      .filter((g) => matches(g.mainTag, g.mainName) || g.links.some((l) => matches(l.subTag, l.subName)))
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
    this.popupError = '';
    this.selectedSubs.clear();
    this.step = 'sub';
  }

  get deletingMain(): MainGroup | undefined {
    const r = this.deleteMain ? this.savedFor(this.deleteMain) : undefined;
    return r ? toGroup(r) : undefined;
  }

  confirmDeleteMain(): void {
    const g = this.deletingMain;
    if (!g || this.saving) return;
    const n = g.links.length;
    this.mutate(this.api.delete(g.id), `${n} sub asset${n === 1 ? '' : 's'} unlinked from ${g.mainTag}.`, () => {
      this.expanded.delete(g.mainTag);
      this.deleteMain = null;
    });
  }

  get deleting(): { group: MainGroup; link: Link } | undefined {
    const d = this.deleteId;
    const r = d ? this.savedFor(d.mainTag) : undefined;
    if (!d || !r) return undefined;
    const group = toGroup(r);
    const link = group.links.find((l) => l.key === d.key);
    return link ? { group, link } : undefined;
  }

  // ===== Flow =====

  /** Assets listed in the main asset table on the page. */
  get mainList(): PickAsset[] {
    const q = this.mainSearch.trim().toLowerCase();
    return this.assets.filter((a) => !q || a.name.toLowerCase().includes(q) || a.tag.toLowerCase().includes(q));
  }

  isMain(tag: string): boolean {
    return this.selectedMains.includes(tag);
  }

  toggleMain(a: PickAsset): void {
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
    const assets = this.selectedMains.map((t) => this.asset(t)).filter((a): a is PickAsset => !!a);
    if (!assets.length) return;
    const previews: AssetPreview[] = assets.map((asset) => ({ asset, subs: [], loading: true }));
    this.previews = previews;
    this.popupError = '';
    this.api.getRelated(assets.map((a) => a.tag)).subscribe({
      next: (sections) => {
        if (this.previews !== previews) return;
        for (const p of previews) {
          const section = sections.find((s) => s.mainAssetRegistrationId === p.asset.tag);
          p.subs = (section?.subAssets ?? []).map((s) => ({
            tag: s.assetRegistrationId ?? (s.tag || s.componentId || ''),
            name: s.name,
            relation: s.relationshipType,
            category: s.category ?? '-',
            location: s.location ?? '-',
            inRegister: !!s.assetRegistrationId,
            sub: s,
          }));
          p.loading = false;
        }
      },
      error: (err) => {
        if (this.previews !== previews) return;
        previews.forEach((p) => (p.loading = false));
        this.popupError = apiErrorMessage(err, 'Could not load the related assets.');
      },
    });
  }

  closePreview(): void {
    this.previews = null;
    this.popupError = '';
  }

  /**
   * Submit: save what the popup lists for each asset (its components become linked sub assets), then open the
   * details page with one diagram per selected asset; the most recently selected one is active.
   */
  submitPreview(): void {
    const list = this.previews ?? [];
    const tags = list.map((p) => p.asset.tag);
    const last = tags[tags.length - 1];
    if (!last || this.saving || list.some((p) => p.loading)) return;
    const open = () => {
      this.previews = null;
      this.selectedMains = [];
      this.router.navigate(['/assets/relationships'], { queryParams: { tag: last, tags: tags.join(','), from: FROM_RELATIONSHIPS } });
    };
    const toSave = list
      .filter((p) => p.subs.length)
      .map((p) => ({ mainAssetRegistrationId: p.asset.tag, subAssets: p.subs.map((s) => toSubAssetRef(s.sub)) }));
    if (!toSave.length) {
      open();
      return;
    }
    this.saving = true;
    this.popupError = '';
    this.api.save(toSave).subscribe({
      next: () => {
        this.saving = false;
        this.loadRelationships();
        open();
      },
      error: (err) => {
        this.saving = false;
        this.popupError = apiErrorMessage(err);
      },
    });
  }

  /** "Change" in the popup: back to the table on the page, keeping the ticked main assets. */
  backToMain(): void {
    this.closePicker();
  }

  toggleSub(a: PickAsset): void {
    if (this.isLinked(a.tag)) return;
    if (this.selectedSubs.has(a.tag)) this.selectedSubs.delete(a.tag);
    else this.selectedSubs.add(a.tag);
  }

  /** Sub assets in the current (searched) list that can still be linked. */
  private get selectable(): PickAsset[] {
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
    const subs = this.assets.filter((a) => this.selectedSubs.has(a.tag) && !this.isLinked(a.tag));
    if (!subs.length || !this.mainTags.length || this.saving) return;
    const mainTags = [...this.mainTags];
    const request = mainTags
      .map((main) => ({
        mainAssetRegistrationId: main,
        subAssets: subs
          .filter((sub) => sub.tag !== main && !this.savedFor(main)?.subAssets.some((s) => s.assetRegistrationId === sub.tag))
          .map((sub) => ({ assetRegistrationId: sub.tag, relationshipType: 'Sub Asset' })),
      }))
      .filter((r) => r.subAssets.length);
    if (!request.length) {
      this.closePicker();
      return;
    }
    const added = request.reduce((n, r) => n + r.subAssets.length, 0);
    const mainName = this.mainAsset?.name ?? this.nameOf(this.mainTag);
    const message =
      mainTags.length === 1
        ? `${subs.length} sub asset${subs.length === 1 ? '' : 's'} linked to ${mainName} (${this.mainTag}).`
        : `${added} links saved for ${mainTags.length} main assets.`;
    const open = this.openAfterSave ? this.mainTag : '';
    this.mutate(this.api.save(request), message, () => {
      mainTags.forEach((m) => this.expanded.add(m));
      if (this.openAfterSave) this.selectedMains = [];
      this.closePicker();
      // Show the most recently picked main asset with its new sub assets
      if (open) this.router.navigate(['/assets/relationships'], { queryParams: { tag: open, from: FROM_RELATIONSHIPS } });
    });
  }

  closePicker(): void {
    this.step = null;
    this.pickerSearch = '';
    this.popupError = '';
    this.selectedSubs.clear();
    this.mainTags = [];
    this.openAfterSave = false;
  }

  confirmDelete(): void {
    const d = this.deleting;
    if (!d || this.saving) return;
    const r = this.savedFor(d.group.mainTag)!;
    const remaining = r.subAssets.filter((s) => subAssetKey(s) !== d.link.key).map((s) => toSubAssetRef(s));
    this.mutate(this.api.replaceSubAssets(r.id, remaining), 'Relationship removed.', () => (this.deleteId = null));
  }

  /** Runs a save / delete, then reloads the relationships and shows the message; errors stay in the open popup. */
  private mutate(request: Observable<unknown>, success: string, done: () => void): void {
    this.saving = true;
    this.popupError = '';
    request.subscribe({
      next: () => {
        this.saving = false;
        done();
        this.showToast(success);
        this.loadRelationships();
      },
      error: (err) => {
        this.saving = false;
        this.popupError = apiErrorMessage(err);
      },
    });
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
