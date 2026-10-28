import { Injectable } from '@angular/core';
import { CodeStore } from '../shared/code-store';
import { initialCertificationTypes, initialPermitTypes, initialRegulations, initialStandards } from './compliance.data';
import { CertificationTypeRecord, PermitTypeRecord, RegulationRecord, StandardRecord } from './compliance.model';

/*
 * DEMO MODE: in-memory compliance masters; replace with API calls when the backend is ready.
 * Certification types and regulations refer to standards by code.
 */

@Injectable({ providedIn: 'root' })
export class StandardService extends CodeStore<StandardRecord> {
  constructor() {
    super(initialStandards());
  }
}

@Injectable({ providedIn: 'root' })
export class CertificationTypeService extends CodeStore<CertificationTypeRecord> {
  constructor() {
    super(initialCertificationTypes());
  }

  usingStandard(code: string): CertificationTypeRecord[] {
    return this.records.filter((r) => r.standardCode === code);
  }

  recodeStandard(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.standardCode === from ? { ...r, standardCode: to } : r));
  }
}

@Injectable({ providedIn: 'root' })
export class PermitTypeService extends CodeStore<PermitTypeRecord> {
  constructor() {
    super(initialPermitTypes());
  }
}

@Injectable({ providedIn: 'root' })
export class RegulationService extends CodeStore<RegulationRecord> {
  constructor() {
    super(initialRegulations());
  }

  usingStandard(code: string): RegulationRecord[] {
    return this.records.filter((r) => r.standardCodes.includes(code));
  }

  recodeStandard(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.standardCodes.includes(from) ? { ...r, standardCodes: r.standardCodes.map((c) => (c === from ? to : c)) } : r));
  }
}
