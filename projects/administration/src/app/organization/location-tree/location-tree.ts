import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Building2, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, Grid3x3, Layers, LucideAngularModule, LucideIconData, MapPin, Search, X } from 'lucide-angular';
import { LocationType, OrgLocation } from '../organization.model';
import { OrganizationService } from '../organization.service';

interface TreeRow {
  node: OrgLocation;
  depth: number;
  hasChildren: boolean;
}

const TYPE_ICONS: Record<LocationType, LucideIconData> = { Location: Building2, Building: Building2, Floor: Layers, Zone: Grid3x3 };

/** Left "Location Hierarchy" tree: organization > location > building > floor > zone. */
@Component({
  standalone: true,
  selector: 'app-location-tree',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './location-tree.html',
  styleUrls: ['../../shared/admin-workspace.css', '../organization.css']
})
export class LocationTree {
  /** Organization shown as the root node. */
  @Input({ required: true }) rootLabel!: string;
  /** Selected node code; null selects the organization root. */
  @Input() selected: string | null = null;
  @Output() selectedChange = new EventEmitter<string | null>();

  private readonly service = inject(OrganizationService);
  readonly icons = { Building2, ChevronDown, ChevronRight, ChevronsDownUp, ChevronsUpDown, MapPin, Search, X };

  rootOpen = true;
  // Opened down to Zone 2A as in the design
  expanded = new Set<string>(['LOC-001', 'LOC-002', 'LOC-003']);
  searchOpen = false;
  term = '';

  /** Visible rows, depth first. While searching, matches are shown with their ancestors, all opened. */
  get rows(): TreeRow[] {
    const term = this.term.trim().toLowerCase();
    const keep = term ? this.matchingBranch(term) : null;
    const out: TreeRow[] = [];
    const walk = (parent: string | null, depth: number) => {
      for (const node of this.service.childrenOf(parent)) {
        if (keep && !keep.has(node.code)) continue;
        const hasChildren = this.service.childrenOf(node.code).length > 0;
        out.push({ node, depth, hasChildren });
        if (hasChildren && (keep || this.expanded.has(node.code))) walk(node.code, depth + 1);
      }
    };
    if (this.rootOpen || keep) walk(null, 1);
    return out;
  }

  private matchingBranch(term: string): Set<string> {
    const keep = new Set<string>();
    for (const l of this.service.all()) {
      if (l.name.toLowerCase().includes(term) || l.code.toLowerCase().includes(term)) {
        keep.add(l.code);
        for (const a of this.service.ancestorsOf(l.code)) keep.add(a.code);
      }
    }
    return keep;
  }

  iconOf(type: LocationType): LucideIconData {
    return TYPE_ICONS[type];
  }

  isOpen(row: TreeRow): boolean {
    return !!this.term.trim() || this.expanded.has(row.node.code);
  }

  toggle(row: TreeRow, event: Event): void {
    event.stopPropagation();
    const code = row.node.code;
    if (this.expanded.has(code)) this.expanded.delete(code);
    else this.expanded.add(code);
  }

  toggleRoot(event: Event): void {
    event.stopPropagation();
    this.rootOpen = !this.rootOpen;
  }

  select(code: string | null): void {
    this.selectedChange.emit(code);
  }

  expandAll(): void {
    this.rootOpen = true;
    this.expanded = new Set(this.service.all().filter((l) => l.type !== 'Zone').map((l) => l.code));
  }

  collapseAll(): void {
    this.expanded = new Set();
  }

  toggleSearch(): void {
    this.searchOpen = !this.searchOpen;
    if (!this.searchOpen) this.term = '';
  }

  /** Opens the branch down to a node, e.g. after it was picked in the table. */
  reveal(code: string): void {
    this.rootOpen = true;
    for (const a of this.service.ancestorsOf(code)) this.expanded.add(a.code);
  }

  indent(depth: number): string {
    return `${depth * 16}px`;
  }
}
