/** People, teams, equipment, workstations and tools that job orders book. */

export type ResourceType = 'Person' | 'Team' | 'Equipment' | 'Workstation' | 'Tool';
export type BookingKind = 'Assigned' | 'Reserved' | 'Unavailable';
export type Availability = 'Available' | 'Assigned' | 'Reserved' | 'Unavailable';

export interface Booking {
  id: number;
  /** Job order code; '' for time the resource is unavailable (leave, maintenance). */
  job: string;
  /** Stage of the job, or the reason the resource is unavailable. */
  stage: string;
  /** Local date-time yyyy-MM-ddTHH:mm. */
  start: string;
  end: string;
  kind: BookingKind;
}

export interface Resource {
  code: string;
  name: string;
  type: ResourceType;
  skills: string[];
  /** e.g. "8 h/day", "4 members", "1 job at a time". */
  capacity: string;
  site: string;
  location: string;
  calendar: string;
  owner: string;
  linkedAsset: { tag: string; name: string } | null;
  readiness: { label: string; ok: boolean }[];
  certifications: { name: string; validUntil: string }[];
  /** Share of available hours booked this week, percent. */
  utilization: number;
  bookings: Booking[];
  notes: string;
}

/** A job order asking for a resource that has not been booked yet. */
export interface BookingRequest {
  id: number;
  job: string;
  stage: string;
  resource: string;
  start: string;
  end: string;
  requestedBy: string;
  status: 'Pending' | 'Approved' | 'Rejected';
}
