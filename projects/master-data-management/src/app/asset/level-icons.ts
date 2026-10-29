import {
  Archive,
  Ban,
  CircleCheck,
  CircleDot,
  Flag,
  Flame,
  Gauge,
  HeartPulse,
  Hourglass,
  Info,
  Recycle,
  Settings,
  ShieldAlert,
  ShieldCheck,
  ShieldHalf,
  Siren,
  Sparkles,
  Star,
  ThumbsUp,
  TriangleAlert,
  Wrench
} from 'lucide-angular';

/** Built-in icons for the small lookup masters (Criticality, Condition) when no image is uploaded. */
export const LEVEL_ICONS = {
  shield: ShieldHalf,
  shieldCheck: ShieldCheck,
  shieldAlert: ShieldAlert,
  alert: TriangleAlert,
  siren: Siren,
  flame: Flame,
  flag: Flag,
  gauge: Gauge,
  star: Star,
  info: Info,
  dot: CircleDot,
  gear: Settings,
  sparkles: Sparkles,
  thumbsUp: ThumbsUp,
  check: CircleCheck,
  heart: HeartPulse,
  wrench: Wrench,
  hourglass: Hourglass,
  ban: Ban,
  recycle: Recycle,
  archive: Archive
};

export type LevelIcon = keyof typeof LEVEL_ICONS;
export const LEVEL_ICON_KEYS = Object.keys(LEVEL_ICONS) as LevelIcon[];

/** `#ef4444` style colours only; the forms validate against this. */
export const HEX_COLOR = /^#[0-9a-f]{6}$/i;
