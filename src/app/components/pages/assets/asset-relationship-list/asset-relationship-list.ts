import { Component, HostListener, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LucideAngularModule, ArrowLeft, Network, Plus, Search, Trash2, X } from 'lucide-angular';
import { ASSET_RECORDS, AssetRecord, findAsset } from '../asset-data';
import { FROM_RELATIONSHIPS, UserRelationship, nowLocal, readUserRelationships, writeUserRelationships } from './user-relationships';

type Step = 'main' | 'sub';

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
  readonly icons = { ArrowLeft, Network, Plus, Search, Trash2, X };

  relationships: UserRelationship[] = readUserRelationships();
  /** Opens the Relationships tab on these links rather than the asset's own data. */
  readonly fromRelationships = FROM_RELATIONSHIPS;

  /** Which picker is open; null when none is. */
  step: Step | null = null;
  mainTag = '';
  pickerSearch = '';
  /** Sub assets ticked in the sub picker. */
  readonly selectedSubs = new Set<string>();

  search = '';
  deleteId: number | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

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

  get mainAsset(): AssetRecord | undefined {
    return findAsset(this.mainTag);
  }

  /** Assets offered in the open picker: the sub picker leaves out the chosen main asset. */
  get pickerAssets(): AssetRecord[] {
    const q = this.pickerSearch.trim().toLowerCase();
    return ASSET_RECORDS.filter(
      (a) => (this.step !== 'sub' || a.tag !== this.mainTag) && (!q || a.name.toLowerCase().includes(q) || a.tag.toLowerCase().includes(q)),
    );
  }

  /** True when this sub asset is already linked to the chosen main asset. */
  isLinked(subTag: string): boolean {
    return this.step === 'sub' && this.relationships.some((r) => r.mainTag === this.mainTag && r.subTag === subTag);
  }

  get filtered(): UserRelationship[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.relationships;
    return this.relationships.filter((r) =>
      [r.mainTag, r.subTag, findAsset(r.mainTag)?.name ?? '', findAsset(r.subTag)?.name ?? ''].some((v) => v.toLowerCase().includes(q)),
    );
  }

  get deleting(): UserRelationship | undefined {
    return this.relationships.find((r) => r.id === this.deleteId);
  }

  // ===== Flow =====

  start(): void {
    this.mainTag = '';
    this.pickerSearch = '';
    this.step = 'main';
  }

  pickMain(a: AssetRecord): void {
    this.mainTag = a.tag;
    this.pickerSearch = '';
    this.selectedSubs.clear();
    this.step = 'sub';
  }

  backToMain(): void {
    this.pickerSearch = '';
    this.step = 'main';
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

  /** Links every ticked sub asset to the main asset, in list order. */
  addSubs(): void {
    const subs = ASSET_RECORDS.filter((a) => this.selectedSubs.has(a.tag) && !this.isLinked(a.tag));
    if (!subs.length) return;
    let id = Math.max(0, ...this.relationships.map((r) => r.id));
    const at = nowLocal();
    const added = subs.map((a) => ({ id: ++id, mainTag: this.mainTag, subTag: a.tag, createdBy: CURRENT_USER, createdAt: at }));
    this.relationships = [...added.reverse(), ...this.relationships];
    this.write();
    this.showToast(
      subs.length === 1
        ? `${this.mainAsset?.name} (${this.mainTag}) linked to ${subs[0].name} (${subs[0].tag}).`
        : `${subs.length} sub assets linked to ${this.mainAsset?.name} (${this.mainTag}).`,
    );
    this.closePicker();
  }

  closePicker(): void {
    this.step = null;
    this.pickerSearch = '';
    this.selectedSubs.clear();
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
    this.closePicker();
    this.deleteId = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
