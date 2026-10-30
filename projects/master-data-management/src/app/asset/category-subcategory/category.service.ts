import { Injectable } from '@angular/core';
import { initialCategories } from './category.data';
import { CategoryFilters, CategoryRecord, SubcategoryRecord } from './category.model';

/** DEMO MODE: in-memory categories and subcategories; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class CategoryService {
  private records: CategoryRecord[] = initialCategories();

  all(): CategoryRecord[] {
    return this.records;
  }

  find(code: string | null): CategoryRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  /** A category's assets are the sum of its subcategories. */
  assetsOf(category: CategoryRecord): number {
    return category.subcategories.reduce((sum, s) => sum + s.assets, 0);
  }

  children(code: string): CategoryRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  /** Parents from the top down, not including `code` itself. */
  ancestry(code: string | null): CategoryRecord[] {
    const chain: CategoryRecord[] = [];
    for (let c = this.find(code); c && chain.length < 20; c = this.find(c.parentCode)) chain.unshift(c);
    return chain;
  }

  /** `code` and every category under it, so none can become its own parent. */
  descendantCodes(code: string): Set<string> {
    const found = new Set([code]);
    for (let added = true; added; ) {
      added = false;
      for (const r of this.records) {
        if (r.parentCode && found.has(r.parentCode) && !found.has(r.code)) {
          found.add(r.code);
          added = true;
        }
      }
    }
    return found;
  }

  assetTypesInUse(): string[] {
    return [...new Set(this.records.map((r) => r.assetType))];
  }

  filter(f: CategoryFilters): CategoryRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.assetType || r.assetType === f.assetType) &&
        (!term || [r.name, r.code, r.description, ...r.subcategories.map((s) => s.name)].some((v) => v.toLowerCase().includes(term)))
    );
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  // Subcategory codes are unique across all categories
  isSubCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.subcategories.some((s) => s.code.toLowerCase() === wanted && s.code !== exceptCode));
  }

  allSubCodes(): Set<string> {
    return new Set(this.records.flatMap((r) => r.subcategories.map((s) => s.code)));
  }

  save(record: CategoryRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      const list = [...this.records];
      list[index] = record;
      // Child categories follow a code change
      this.records = originalCode !== record.code ? list.map((r) => (r.parentCode === originalCode ? { ...r, parentCode: record.code } : r)) : list;
    } else {
      this.records = [record, ...this.records];
    }
  }

  /** Removes categories; their child categories move up to the removed category's parent. */
  remove(codes: string[]): void {
    const parentOf = new Map(this.records.map((r) => [r.code, r.parentCode]));
    const lift = (code: string | null): string | null => (code && codes.includes(code) ? lift(parentOf.get(code) ?? null) : code);
    this.records = this.records.filter((r) => !codes.includes(r.code)).map((r) => ({ ...r, parentCode: lift(r.parentCode) }));
  }

  saveSubcategory(categoryCode: string, sub: SubcategoryRecord, originalCode: string | null): void {
    this.records = this.records.map((r) => {
      if (r.code !== categoryCode) return r;
      const index = originalCode ? r.subcategories.findIndex((s) => s.code === originalCode) : -1;
      const subcategories = index >= 0 ? r.subcategories.map((s, i) => (i === index ? sub : s)) : [...r.subcategories, sub];
      return { ...r, subcategories };
    });
  }

  removeSubcategories(categoryCode: string, codes: string[]): void {
    this.records = this.records.map((r) => (r.code === categoryCode ? { ...r, subcategories: r.subcategories.filter((s) => !codes.includes(s.code)) } : r));
  }
}
