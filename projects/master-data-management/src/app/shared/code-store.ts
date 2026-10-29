/**
 * DEMO MODE: in-memory list of records keyed by `code`, shared by the simpler master services.
 * Replace the subclasses' storage with API calls when the backend is ready.
 */
export abstract class CodeStore<T extends { code: string; name?: string }> {
  protected records: T[];

  protected constructor(initial: T[]) {
    this.records = initial;
  }

  all(): T[] {
    return this.records;
  }

  find(code: string | null | undefined): T | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  findByName(name: string): T | undefined {
    return this.records.find((r) => r.name === name);
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => (r.name ?? '').toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** Replaces the record that had `originalCode`, or adds a new one at the end. */
  save(record: T, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [...this.records, record];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
