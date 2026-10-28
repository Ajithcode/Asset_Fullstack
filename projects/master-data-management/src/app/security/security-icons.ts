import {
  Bell,
  Box,
  Calendar,
  ChartColumn,
  ClipboardCheck,
  Database,
  Factory,
  FileText,
  LayoutDashboard,
  LifeBuoy,
  Lock,
  Package,
  Radio,
  Settings,
  ShieldCheck,
  ShoppingCart,
  Smartphone,
  Trash2,
  Truck,
  Users,
  Wallet,
  Wrench
} from 'lucide-angular';

/** Icons offered for modules on the Module Access Master. */
export const MODULE_ICONS = {
  dashboard: LayoutDashboard,
  box: Box,
  wrench: Wrench,
  clipboard: ClipboardCheck,
  package: Package,
  factory: Factory,
  shield: ShieldCheck,
  chart: ChartColumn,
  radio: Radio,
  truck: Truck,
  file: FileText,
  wallet: Wallet,
  cart: ShoppingCart,
  calendar: Calendar,
  trash: Trash2,
  bell: Bell,
  help: LifeBuoy,
  phone: Smartphone,
  database: Database,
  settings: Settings,
  lock: Lock,
  users: Users
};

export type ModuleIcon = keyof typeof MODULE_ICONS;
export const MODULE_ICON_KEYS = Object.keys(MODULE_ICONS) as ModuleIcon[];

export function moduleIcon(key: string) {
  return MODULE_ICONS[key as ModuleIcon] ?? Box;
}
