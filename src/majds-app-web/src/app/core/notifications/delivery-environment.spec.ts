import { channelsOutsideProd } from './delivery-environment';

const settings = (values: Record<string, string>) => (name: string, fallback = '') => values[name] ?? fallback;

describe('channelsOutsideProd', () => {
  it('shows nothing in Prod, and nothing when nothing is configured', () => {
    expect(channelsOutsideProd(settings({}))).toEqual([]);
    expect(channelsOutsideProd(settings({ 'Notifications.DeliveryMode': 'Prod' }))).toEqual([]);
  });

  it('reports the general mode for every channel', () => {
    expect(channelsOutsideProd(settings({ 'Notifications.DeliveryMode': 'Dev' }))).toEqual([{ channel: 'Email', mode: 'Dev' }]);
    expect(channelsOutsideProd(settings({ 'Notifications.DeliveryMode': 'test' }), ['Email', 'Sms'])).toEqual([
      { channel: 'Email', mode: 'Test' },
      { channel: 'Sms', mode: 'Test' }
    ]);
  });

  it('lets a channel have its own mode, either way', () => {
    expect(channelsOutsideProd(settings({ 'Notifications.DeliveryMode': 'Dev', 'Notifications.Email.DeliveryMode': 'Prod' }))).toEqual([]);
    expect(channelsOutsideProd(settings({ 'Notifications.DeliveryMode': 'Prod', 'Notifications.Email.DeliveryMode': 'Test' }))).toEqual([{ channel: 'Email', mode: 'Test' }]);
  });

  it('treats a mode nobody recognises as Dev, because the server sends nothing for it', () => {
    expect(channelsOutsideProd(settings({ 'Notifications.DeliveryMode': 'Staging' }))).toEqual([{ channel: 'Email', mode: 'Dev' }]);
  });
});
