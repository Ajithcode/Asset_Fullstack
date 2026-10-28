import { Injectable } from '@angular/core';
import { initialServiceProviders } from './service-provider.data';
import { ServiceProviderRecord } from './service-provider.model';

/** DEMO MODE: in-memory service providers; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class ServiceProviderService {
  private records: ServiceProviderRecord[] = initialServiceProviders();

  all(): ServiceProviderRecord[] {
    return this.records;
  }

  find(code: string | null): ServiceProviderRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  countries(): string[] {
    return [...new Set(this.records.map((r) => r.country))].sort((a, b) => a.localeCompare(b));
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  save(record: ServiceProviderRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [record, ...this.records];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
