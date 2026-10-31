import { Component, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ChevronRight,
  CircleAlert,
  Clock,
  Download,
  LucideAngularModule,
  RotateCcw,
  Save,
  Search,
  Settings,
  Share2,
  Shield,
  Upload,
  X
} from 'lucide-angular';
import { AdminPage, formatDate } from '../shared/admin-common';
import { ConfigDetailDrawer } from './config-detail-drawer';
import { changeTone, describeValue, validateSetting } from './config-format';
import { ConfigGroupCard } from './config-group-card';
import { configIcon } from './config-icons';
import { ACTIVE_INTEGRATIONS, ConfigChange, ConfigGroup, ConfigSection, ConfigSetting, ConfigValue } from './system-configuration.data';
import { ConfigValues, SystemConfigurationService } from './system-configuration.service';

interface VisibleGroup {
  group: ConfigGroup;
  settings: ConfigSetting[];
  caption: string;
}

/** Administration > System Configuration */
@Component({
  standalone: true,
  selector: 'app-admin-system-configuration',
  imports: [FormsModule, RouterLink, LucideAngularModule, ConfigGroupCard, ConfigDetailDrawer],
  templateUrl: './system-configuration.html',
  styleUrls: ['../shared/admin-workspace.css', './system-configuration.css']
})
export class SystemConfigurationPage extends AdminPage {
  private readonly service = inject(SystemConfigurationService);

  @ViewChild(ConfigDetailDrawer) private drawer?: ConfigDetailDrawer;

  readonly icons = { Settings, Share2, Shield, Clock, Save, RotateCcw, Upload, Download, Search, CircleAlert, ChevronRight, X };
  readonly activeIntegrations = ACTIVE_INTEGRATIONS;

  sectionId = 'general';
  tab = 'general';
  sectionQuery = '';
  settingsQuery = '';
  selectedKey: string | null = 'timeZone';

  /** Working values: what the form shows, saved or not. */
  draft: ConfigValues = this.service.values();
  errors: Record<string, string> = {};

  showPending = false;
  showHistory = false;
  confirm: { title: string; message: string; action: 'reset' | 'discard' } | null = null;
  saving = false;

  constructor() {
    super();
    const before = this.service.values();
    this.service.load().subscribe({
      // Pick up the saved values for every setting the user has not touched meanwhile
      next: () => {
        const saved = this.service.values();
        const next = { ...this.draft };
        for (const key of Object.keys(saved)) if (next[key] === before[key]) next[key] = saved[key];
        this.draft = next;
        this.validate();
      },
      error: (err: Error) => this.notify(`Could not load the saved configuration. ${err.message}`)
    });
  }

  // ----- Derived data -----

  get sections(): ConfigSection[] {
    return this.service.sections();
  }

  get currentSection(): ConfigSection {
    return this.sections.find((s) => s.id === this.sectionId) ?? this.sections[0];
  }

  get filteredSections(): ConfigSection[] {
    const q = this.sectionQuery.trim().toLowerCase();
    const sq = this.settingsQuery.trim().toLowerCase();
    return this.sections.filter(
      (s) =>
        (!q || s.title.toLowerCase().includes(q) || s.subtitle.toLowerCase().includes(q)) &&
        (!sq || s.groups.some((g) => g.settings.some((x) => this.matches(x, sq))))
    );
  }

  /** Cards for the centre pane: the current section / tab, or settings search results across sections. */
  get visibleGroups(): VisibleGroup[] {
    const sq = this.settingsQuery.trim().toLowerCase();
    if (sq) {
      return this.sections.flatMap((sec) =>
        sec.groups
          .map((g) => ({ group: g, settings: g.settings.filter((x) => this.matches(x, sq)), caption: `${sec.title} · ${g.title}` }))
          .filter((v) => v.settings.length > 0)
      );
    }
    const sec = this.currentSection;
    return sec.groups
      .filter((g) => !sec.tabs || !g.tabs || g.tabs.includes(this.tab))
      .map((g) => ({ group: g, settings: g.settings, caption: '' }));
  }

  get selectedSetting(): ConfigSetting | null {
    return this.selectedKey ? (this.service.setting(this.selectedKey) ?? null) : null;
  }

  get selectedSection(): ConfigSection | null {
    return this.selectedKey ? (this.service.sectionOf(this.selectedKey) ?? null) : null;
  }

  get selectedDraft(): ConfigValue | null {
    return this.selectedKey ? (this.draft[this.selectedKey] ?? null) : null;
  }

  get dirtySettings(): ConfigSetting[] {
    return this.service.allSettings().filter((s) => this.draft[s.key] !== s.value);
  }

  get pending(): ConfigChange[] {
    return this.service.pending();
  }

  get history(): ConfigChange[] {
    return this.service.history();
  }

  get recentChanges(): ConfigChange[] {
    return this.history.slice(0, 5);
  }

  get securityPolicies(): number {
    return this.service.activePolicies();
  }

  // ----- Template helpers -----

  icon(name: string) {
    return configIcon(name);
  }

  sectionDirtyCount(sec: ConfigSection): number {
    return sec.groups.reduce((n, g) => n + g.settings.filter((s) => this.draft[s.key] !== s.value).length, 0);
  }

  date(iso: string): string {
    return formatDate(iso);
  }

  tone(c: ConfigChange): string {
    return changeTone(c.status);
  }

  display(c: ConfigChange, v: ConfigValue): string {
    const s = this.service.setting(c.key);
    return s ? describeValue(s, v) : String(v);
  }

  private matches(s: ConfigSetting, q: string): boolean {
    return s.label.toLowerCase().includes(q) || s.description.toLowerCase().includes(q) || s.key.toLowerCase().includes(q);
  }

  // ----- Navigation -----

  selectSection(sec: ConfigSection): void {
    this.sectionId = sec.id;
    this.tab = sec.tabs?.[0]?.id ?? '';
    this.settingsQuery = '';
    this.selectedKey = sec.groups[0]?.settings[0]?.key ?? null;
  }

  /** Selecting a setting from search results / recent changes also jumps to its section. */
  selectSetting(key: string): void {
    this.selectedKey = key;
    const sec = this.service.sectionOf(key);
    if (!sec || this.settingsQuery.trim()) return;
    this.sectionId = sec.id;
    if (sec.tabs) {
      const group = sec.groups.find((g) => g.settings.some((s) => s.key === key));
      if (group?.tabs && !group.tabs.includes(this.tab)) this.tab = group.tabs[0];
    }
  }

  openChange(key: string): void {
    this.showHistory = false;
    this.settingsQuery = '';
    this.selectSetting(key);
  }

  // ----- Editing -----

  onValueChange(e: { key: string; value: ConfigValue }): void {
    this.draft = { ...this.draft, [e.key]: e.value };
    this.selectedKey = e.key;
    this.validate();
  }

  private validate(): void {
    const errors: Record<string, string> = {};
    for (const s of this.service.allSettings()) {
      const msg = validateSetting(s, this.draft[s.key]);
      if (msg) errors[s.key] = msg;
    }
    this.errors = errors;
  }

  /** Records every changed setting: applied now, or sent for review for approval-only sections. */
  saveAll(): void {
    const dirty = this.dirtySettings;
    if (!dirty.length) {
      this.notify('There are no changes to save.');
      return;
    }
    const invalid = dirty.filter((s) => this.errors[s.key]);
    if (invalid.length) {
      this.selectSetting(invalid[0].key);
      this.notify(`Fix ${invalid.length} invalid setting${invalid.length > 1 ? 's' : ''} before saving.`);
      return;
    }
    if (this.saving) return;
    this.saving = true;
    this.service.submit(dirty.map((s) => ({ key: s.key, value: this.draft[s.key] })), 'Saved from System Configuration.').subscribe({
      next: (changes) => {
        this.saving = false;
        this.draft = this.service.values();
        this.validate();
        this.notify(this.summary(changes.filter((c) => c.status === 'Applied').length, changes.filter((c) => c.status === 'Pending').length));
      },
      error: (err: Error) => {
        this.saving = false;
        this.notify(`Changes not saved. ${err.message}`);
      }
    });
  }

  /** "Apply Change" in the drawer: records the selected setting with its reason. */
  applySelected(reason: string): void {
    const s = this.selectedSetting;
    if (!s) return;
    if (this.draft[s.key] === s.value) {
      this.notify(`Change the value of ${s.label} first, then apply it.`);
      return;
    }
    if (this.errors[s.key]) {
      this.notify(this.errors[s.key]);
      return;
    }
    if (this.saving) return;
    this.saving = true;
    this.service.submit([{ key: s.key, value: this.draft[s.key] }], reason).subscribe({
      next: ([change]) => {
        this.saving = false;
        this.draft = { ...this.draft, [s.key]: this.service.setting(s.key)?.value ?? this.draft[s.key] };
        this.validate();
        this.drawer?.reset();
        this.notify(change?.status === 'Pending' ? `${s.label} change sent for review.` : `${s.label} updated.`);
      },
      error: (err: Error) => {
        this.saving = false;
        this.notify(`${s.label} not saved. ${err.message}`);
      }
    });
  }

  /** Drawer "Cancel": drops the unsaved value of the selected setting. */
  revertSelected(): void {
    const s = this.selectedSetting;
    if (!s || this.draft[s.key] === s.value) return;
    this.draft = { ...this.draft, [s.key]: s.value };
    this.validate();
  }

  askDiscard(): void {
    if (!this.dirtySettings.length) {
      this.notify('There are no unsaved changes.');
      return;
    }
    this.confirm = { title: 'Discard changes?', message: `${this.dirtySettings.length} unsaved change(s) will be lost.`, action: 'discard' };
  }

  askReset(): void {
    this.confirm = {
      title: 'Reset to defaults?',
      message: `All ${this.currentSection.title} settings will be set back to their default values. Review them and Save Changes to apply.`,
      action: 'reset'
    };
  }

  confirmAction(): void {
    if (!this.confirm) return;
    if (this.confirm.action === 'discard') {
      this.draft = this.service.values();
      this.notify('Unsaved changes discarded.');
    } else {
      const next = { ...this.draft };
      let n = 0;
      for (const g of this.currentSection.groups) {
        for (const s of g.settings) {
          if (next[s.key] !== s.defaultValue) n++;
          next[s.key] = s.defaultValue;
        }
      }
      this.draft = next;
      this.notify(n ? `${n} setting(s) reset to defaults. Save Changes to apply.` : 'All settings already use their defaults.');
    }
    this.validate();
    this.confirm = null;
  }

  // ----- Pending review -----

  approve(c: ConfigChange): void {
    const before = this.service.setting(c.key)?.value;
    this.service.approve(c.id);
    // Keep the form in step unless the user is editing that setting
    if (this.draft[c.key] === before) this.draft = { ...this.draft, [c.key]: c.newValue };
    this.validate();
    this.notify(`${c.setting} change approved and applied.`);
    if (!this.pending.length) this.showPending = false;
  }

  reject(c: ConfigChange): void {
    this.service.reject(c.id);
    this.notify(`${c.setting} change rejected.`);
    if (!this.pending.length) this.showPending = false;
  }

  // ----- Import / export -----

  exportConfig(): void {
    const settings: ConfigValues = {};
    for (const s of this.service.allSettings()) {
      if (s.type !== 'logo') settings[s.key] = s.value;
    }
    const json = JSON.stringify({ organization: this.organization, exportedAt: new Date().toISOString(), settings }, null, 2);
    const url = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `asset360-configuration-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    URL.revokeObjectURL(url);
    this.notify('Configuration exported.');
  }

  /** Loads a previously exported file into the form as unsaved changes. */
  importConfig(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      let parsed: unknown;
      try {
        parsed = JSON.parse(String(reader.result));
      } catch {
        this.notify('That file is not valid JSON.');
        return;
      }
      if (!parsed || typeof parsed !== 'object') {
        this.notify('No settings found in that file.');
        return;
      }
      const root = parsed as { settings?: Record<string, unknown> };
      const incoming: Record<string, unknown> =
        root.settings && typeof root.settings === 'object' ? root.settings : (parsed as Record<string, unknown>);
      const next = { ...this.draft };
      let applied = 0;
      let skipped = 0;
      for (const [key, value] of Object.entries(incoming)) {
        const s = this.service.setting(key);
        const ok = !!s && s.type !== 'logo' && typeof value === typeof s.value && !validateSetting(s, value as ConfigValue);
        if (ok) {
          next[key] = value as ConfigValue;
          applied++;
        } else {
          skipped++;
        }
      }
      this.draft = next;
      this.validate();
      this.notify(`Imported ${applied} setting(s)${skipped ? `, skipped ${skipped}` : ''}. Review and Save Changes to apply.`);
    };
    reader.readAsText(file);
  }

  private summary(applied: number, queued: number): string {
    const parts: string[] = [];
    if (applied) parts.push(`${applied} change${applied > 1 ? 's' : ''} saved`);
    if (queued) parts.push(`${queued} sent for review`);
    return parts.join(', ') + '.';
  }
}
