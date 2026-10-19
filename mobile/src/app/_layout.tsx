import { useEffect, useState } from 'react';
import { Stack } from 'expo-router';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StatusBar } from 'expo-status-bar';
import { useSession } from '../state/session';
import { colors } from '../theme/tokens';
import { Loading, Screen } from '../components/ui';

const queryClient = new QueryClient({ defaultOptions: { queries: { staleTime: 20_000, retry: 1, refetchOnWindowFocus: false } } });
export default function RootLayout() {
  const initialize = useSession(s => s.initialize);
  const [booted, setBooted] = useState(false);
  useEffect(() => { initialize().finally(() => setBooted(true)); }, [initialize]);
  return <GestureHandlerRootView style={{ flex: 1, backgroundColor: colors.canvas }}><QueryClientProvider client={queryClient}><StatusBar style="dark" />{!booted ? <Screen><Loading label="MindMatch hazırlanıyor" /></Screen> : <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.canvas }, animation: 'fade_from_bottom' }}><Stack.Screen name="index" /><Stack.Screen name="(tabs)" /><Stack.Screen name="conversation/[id]" options={{ animation: 'slide_from_right' }} /><Stack.Screen name="story/[id]" options={{ presentation: 'fullScreenModal' }} /><Stack.Screen name="premium" options={{ presentation: 'modal' }} /><Stack.Screen name="requests" options={{ presentation: 'modal' }} /><Stack.Screen name="settings" options={{ presentation: 'modal' }} /><Stack.Screen name="admin" options={{ presentation: 'modal' }} /><Stack.Screen name="report" options={{ presentation: 'modal' }} /><Stack.Screen name="recover" options={{ presentation: 'modal' }} /><Stack.Screen name="call/[id]" options={{ presentation: 'fullScreenModal' }} /></Stack>}</QueryClientProvider></GestureHandlerRootView>;
}

