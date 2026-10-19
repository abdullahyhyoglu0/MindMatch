import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { API_URL, sessionTokens } from '../api/client';

let connection: ReturnType<HubConnectionBuilder['build']> | null = null;
let starting: Promise<void> | null = null;
let callConnection: ReturnType<HubConnectionBuilder['build']> | null = null;
export async function connectMessages() {
  if (connection?.state === HubConnectionState.Connected) return connection;
  if (!connection) connection = new HubConnectionBuilder().withUrl(`${API_URL}/hubs/messages`, { accessTokenFactory: async () => (await sessionTokens.access()) || '' }).withAutomaticReconnect().configureLogging(LogLevel.Error).build();
  if (!starting) starting = connection.start().finally(() => { starting = null; });
  try { await starting; } catch { return null; }
  return connection;
}
export async function stopMessages() { if (connection) await connection.stop().catch(() => {}); connection = null; }
export function getMessageHub() { return connection; }
export async function connectCalls() {
  if (callConnection?.state === HubConnectionState.Connected) return callConnection;
  if (!callConnection) callConnection = new HubConnectionBuilder().withUrl(`${API_URL}/hubs/calls`, { accessTokenFactory: async () => (await sessionTokens.access()) || '' }).withAutomaticReconnect().configureLogging(LogLevel.Error).build();
  try { if (callConnection.state === HubConnectionState.Disconnected) await callConnection.start(); return callConnection; } catch { return null; }
}
export async function stopCalls() { if (callConnection) await callConnection.stop().catch(() => {}); callConnection = null; }

