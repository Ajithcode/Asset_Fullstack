import {
  ChartColumn,
  Cpu,
  DoorOpen,
  LayoutGrid,
  LucideIconData,
  MapPin,
  Package,
  Server,
  SquareParking,
  TreePine,
  Truck
} from 'lucide-angular';
import { LocationType } from './location.model';

const TYPE_ICONS: Record<LocationType, LucideIconData> = {
  Area: ChartColumn,
  Room: DoorOpen,
  Rack: Server,
  Bin: Package,
  Workstation: LayoutGrid,
  Equipment: Cpu,
  'Parking Bay': SquareParking,
  Yard: TreePine,
  Dock: Truck
};

export function iconForType(type: string): LucideIconData {
  return TYPE_ICONS[type as LocationType] ?? MapPin;
}
