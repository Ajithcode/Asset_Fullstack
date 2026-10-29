export type TemplateStatus = 'Active' | 'Draft' | 'Inactive';
export type Channel = 'email' | 'sms' | 'inApp' | 'push';
export type RecipientType = 'Assignee' | 'Supervisor' | 'Team';
export type TemplateIcon = 'mail' | 'file' | 'truck' | 'calendar' | 'shield' | 'box' | 'user' | 'wrench' | 'bell' | 'alert';
export type Tone = 'purple' | 'orange' | 'blue' | 'red' | 'green' | 'pink';

export interface EmailContent {
  subject: string;
  fromName: string;
  replyTo: string;
  recipientType: RecipientType;
  body: string;
}

export interface NotificationTemplate {
  id: string;
  name: string;
  code: string;
  category: string;
  owner: string;
  description: string;
  status: TemplateStatus;
  version: string;
  /** ISO date-time */
  updated: string;
  triggerEvent: string;
  channels: Record<Channel, boolean>;
  language: string;
  priority: string;
  deliveryTiming: string;
  retryPolicy: string;
  email: EmailContent;
  sms: { text: string };
  inApp: { title: string; body: string; link: string };
  push: { title: string; body: string };
  icon: TemplateIcon;
  tone: Tone;
}

export interface VariableGroup {
  name: string;
  icon: 'wo' | 'asset' | 'user' | 'org';
  variables: string[];
}

export const CHANNEL_LABELS: Record<Channel, string> = { email: 'Email', sms: 'SMS', inApp: 'In-App', push: 'Push' };
export const CHANNELS: Channel[] = ['email', 'sms', 'inApp', 'push'];
