import {
  Briefcase,
  Building,
  ChartColumn,
  CircleDollarSign,
  ClipboardList,
  FlaskConical,
  LucideIconData,
  Server,
  Settings,
  ShieldCheck,
  ShoppingCart,
  Signal,
  Truck,
  Users,
  Wallet,
  Wrench
} from 'lucide-angular';

/** Built-in logos for masters without an uploaded image (business units, departments). */
export const MASTER_ICONS = {
  chart: ChartColumn,
  gear: Settings,
  truck: Truck,
  building: Building,
  cart: ShoppingCart,
  bars: Signal,
  shield: ShieldCheck,
  flask: FlaskConical,
  users: Users,
  wallet: Wallet,
  briefcase: Briefcase,
  server: Server,
  wrench: Wrench,
  clipboard: ClipboardList,
  dollar: CircleDollarSign
} satisfies Record<string, LucideIconData>;

export type MasterIcon = keyof typeof MASTER_ICONS;
export const MASTER_ICON_KEYS = Object.keys(MASTER_ICONS) as MasterIcon[];
