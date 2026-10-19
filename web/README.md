# MindMatch web preview

Hızlı ürün denemesi için hazırlanmış, mevcut API'ye bağlanan Vite istemcisi.

## Çalıştırma

1. API'yi `MindMatch.Api` projesinden `http://localhost:5193` adresinde çalıştır.
2. `web` klasöründe `npm install` ve ardından `npm run dev` çalıştır.
3. Tarayıcıda `http://localhost:5173` adresini aç.

Vite `/api` ve `/hubs` isteklerini API'ye proxy'ler; ayrıca CORS ayarı gerektirmez. Oturum tokenları sekme oturumunda saklanır. Bu klasör hızlı değerlendirme istemcisidir; production dağıtım ayarı içermez.
