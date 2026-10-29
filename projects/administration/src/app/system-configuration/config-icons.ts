import {
  Archive,
  Bell,
  Building2,
  Calendar,
  ClipboardCheck,
  Database,
  FolderOpen,
  Globe,
  Hash,
  KeyRound,
  Link,
  Lock,
  Mail,
  Settings,
  Shield,
  SlidersHorizontal,
  User,
  Workflow
} from 'lucide-angular';

/** Icons referenced by name from system-configuration.data.ts. */
export const CONFIG_ICONS: Record<string, typeof Settings> = {
  Archive,
  Bell,
  Building2,
  Calendar,
  ClipboardCheck,
  Database,
  FolderOpen,
  Globe,
  Hash,
  KeyRound,
  Link,
  Lock,
  Mail,
  Settings,
  Shield,
  SlidersHorizontal,
  User,
  Workflow
};

export function configIcon(name: string): typeof Settings {
  return CONFIG_ICONS[name] ?? Settings;
}
