/**
 * The delivery environment of the notification channels (Dev, Test or Prod), read from the client-visible settings. Prod is the normal case and
 * shows nothing; Dev and Test are announced in a banner so nobody wonders why a message did not arrive, or thinks a test message went to a real person.
 */
export type DeliveryEnvironment = 'Dev' | 'Test' | 'Prod';

export interface ChannelEnvironment {
  channel: string;
  mode: 'Dev' | 'Test';
}

/** The channels that are not in Prod. A channel's own mode beats the general one; a value nobody recognises is treated as Dev, as the server does. */
export function channelsOutsideProd(get: (name: string, fallback?: string) => string, channels: readonly string[] = ['Email']): ChannelEnvironment[] {
  const general = normalize(get('Notifications.DeliveryMode', 'Prod'));
  const result: ChannelEnvironment[] = [];
  for (const channel of channels) {
    const own = get(`Notifications.${channel}.DeliveryMode`, '').trim();
    const mode = own === '' ? general : normalize(own);
    if (mode !== 'Prod') result.push({ channel, mode });
  }
  return result;
}

function normalize(value: string): DeliveryEnvironment {
  const found = (['Dev', 'Test', 'Prod'] as const).find((m) => m.toLowerCase() === value.trim().toLowerCase());
  return found ?? 'Dev';
}
