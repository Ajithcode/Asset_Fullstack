/** Material issued to, used on, scrapped or returned from a job order. */

export type TxType = 'Issue' | 'Consumption' | 'Return' | 'Scrap';

export interface Batch {
  lot: string;
  supplier: string;
  /** yyyy-MM-dd, or '' when the material does not expire. */
  expiry: string;
}

export interface MaterialLine {
  code: string;
  name: string;
  category: string;
  uom: string;
  planned: number;
  issued: number;
  used: number;
  scrap: number;
  returned: number;
  /** AED per unit of measure. */
  unitCost: number;
  stage: string;
  batches: Batch[];
}

export interface MaterialTx {
  id: number;
  /** Local date-time yyyy-MM-ddTHH:mm. */
  at: string;
  type: TxType;
  /** Material code. */
  code: string;
  qty: number;
  ref: string;
  by: string;
  stage: string;
  lot: string;
  notes: string;
}

/** Which material batch went into which output unit. */
export interface TraceLink {
  serial: string;
  code: string;
  lot: string;
  supplier: string;
  expiry: string;
  at: string;
}

export interface MaterialException {
  id: number;
  code: string;
  text: string;
  /** Transaction that raised it. */
  ref: string;
  status: 'Review Required' | 'Closed';
  at: string;
}

export interface JobMaterials {
  job: string;
  name: string;
  status: string;
  stage: string;
  site: string;
  store: string;
  materials: MaterialLine[];
  /** Newest first. */
  transactions: MaterialTx[];
  traceability: TraceLink[];
  exceptions: MaterialException[];
}
