import * as signalR from '@microsoft/signalr';

/**
 * Connection options shared by every SignalR hub.
 *
 * - The token factory is called on every connect and automatic reconnect, so it always gets a current
 *   (refreshed if necessary) access token instead of the one captured when the component mounted.
 * - WebSockets without the negotiate round-trip: with several API servers behind a round-robin load
 *   balancer, a negotiate handled by one server followed by a connect landing on another fails, so this
 *   removes the need for sticky sessions.
 */
export function hubConnectionOptions(getAccessToken: () => Promise<string | null>): signalR.IHttpConnectionOptions {
  return {
    accessTokenFactory: async () => (await getAccessToken()) ?? '',
    transport: signalR.HttpTransportType.WebSockets,
    skipNegotiation: true,
  };
}
