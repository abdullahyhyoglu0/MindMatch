import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5193', changeOrigin: true },
      '/hubs': { target: 'http://localhost:5193', changeOrigin: true, ws: true }
    }
  }
});
