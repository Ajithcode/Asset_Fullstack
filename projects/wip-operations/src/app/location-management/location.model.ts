/** Operational locations (bays, cells, stores) that job orders move through. */

export interface MovementControls {
  /** Block a move when the destination is full. */
  requireCapacity: boolean;
  /** The current stage must be complete before the job leaves. */
  requireStage: boolean;
  /** The destination's code must be scanned (or typed) on arrival. */
  scanQr: boolean;
  /** Moves wait for a supervisor before they take effect. */
  supervisorApproval: boolean;
}

export interface OpLocation {
  code: string;
  name: string;
  type: string;
  site: string;
  building: string;
  floor: string;
  zone: string;
  /** Concurrent jobs. */
  capacity: number;
  supervisor: string;
  shift: string;
  status: 'Active' | 'Inactive';
  /** Resource code of the workstation at this location ('' for none). */
  workstation: string;
  assets: string[];
  /** Codes of the locations a job may move to next. */
  next: string[];
  controls: MovementControls;
}

export interface Occupancy {
  job: string;
  location: string;
  stage: string;
  /** Local date-time yyyy-MM-ddTHH:mm. */
  arrival: string;
}

export interface Movement {
  id: number;
  job: string;
  from: string;
  to: string;
  at: string;
  by: string;
  status: 'Completed' | 'Pending Approval' | 'Rejected';
}
