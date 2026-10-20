import './style.css';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const app = document.querySelector('#app');
const tokenStore = {
  get access() { return sessionStorage.getItem('mm_access'); },
  get refresh() { return sessionStorage.getItem('mm_refresh'); },
  set(auth) {
    sessionStorage.setItem('mm_access', auth.accessToken);
    sessionStorage.setItem('mm_refresh', auth.refreshToken);
  },
  clear() { sessionStorage.removeItem('mm_access'); sessionStorage.removeItem('mm_refresh'); }
};

const state = {
  user: null, profile: null, section: 'discover', candidates: [], matches: [],
  conversations: [], currentConversation: null, messages: [], requests: [],
  notifications: [], stories: [], myStories: [], mood: null, hub: null, busy: false,
  people: new Map(), presence: new Map(), storyViewers: [],
  storyIndex: 0, interests: [], photos: [], music: { genres: [] }, blocked: []
};

const icons = {
  discover: '✳', chats: '◌', requests: '↗', matches: '♡', stories: '◉', profile: '☻',
  bell: '♧', sparkle: '✦', arrow: '↗', send: '↑', logout: '↪'
};

function escapeHtml(value = '') {
  return String(value).replace(/[&<>"']/g, (char) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  })[char]);
}

function errorText(body, fallback) {
  return body?.message || body?.title || fallback;
}

async function request(path, options = {}, retry = true) {
  const headers = new Headers(options.headers || {});
  if (tokenStore.access) headers.set('Authorization', `Bearer ${tokenStore.access}`);
  if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');
  let response = await fetch(path, { ...options, headers });
  if (response.status === 401 && retry && tokenStore.refresh) {
    try {
      const refreshResponse = await fetch('/api/auth/refresh', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: tokenStore.refresh })
      });
      if (refreshResponse.ok) {
        tokenStore.set(await refreshResponse.json());
        return request(path, options, false);
      }
    } catch { /* the original API error is reported below */ }
    tokenStore.clear();
    if (!response.ok) showAuth();
  }
  const contentType = response.headers.get('content-type') || '';
  const body = response.status === 204 ? null
    : contentType.includes('application/json') ? await response.json().catch(() => null)
      : await response.text().catch(() => '');
  if (!response.ok) throw new Error(errorText(body, `İstek başarısız (${response.status})`));
  return body;
}

function showToast(message, type = 'info') {
  const host = document.querySelector('#toast-host');
  if (!host) return;
  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  toast.textContent = message;
  host.append(toast);
  setTimeout(() => toast.remove(), 3600);
}

function initials(value = 'M') {
  return value.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('').toUpperCase();
}

function avatar(name, tone = 'peach', size = '') {
  return `<span class="avatar ${tone} ${size}">${escapeHtml(initials(name))}</span>`;
}

function showAuth(mode = 'login', message = '') {
  state.user = null;
  state.hub?.stop().catch(() => {});
  state.hub = null;
  app.innerHTML = `
    <main class="auth-page">
      <div class="auth-story">
        <div class="brand"><span class="brand-mark">m</span><span>mindmatch</span></div>
        <div class="auth-copy">
          <span class="eyebrow">DAHA ANLAMLI TANIŞMALAR</span>
          <h1>Bugün nasıl<br/><em>hissediyorsun?</em></h1>
          <p>Ruh hâline, ortak ilgi alanlarına ve müzik zevkine göre yeni insanlarla tanış.</p>
          <div class="orbit orbit-one"></div><div class="orbit orbit-two"></div>
          <span class="float-note note-one">✦ aynı ritim</span><span class="float-note note-two">♡ yeni bir bağ</span>
        </div>
        <div class="auth-foot">Kendin gibi ol. Gerisi kendiliğinden gelir.</div>
      </div>
      <section class="auth-panel">
        <div class="auth-card">
          <span class="eyebrow">MINDMATCH'E HOŞ GELDİN</span>
          <h2>${mode === 'login' ? 'Tekrar merhaba' : 'Hikâyen burada başlıyor'}</h2>
          <p class="muted">${mode === 'login' ? 'Sana iyi gelen insanlarla kaldığın yerden devam et.' : 'Bir hesap oluştur, sana yakın hisleri keşfet.'}</p>
          <div class="auth-tabs"><button class="${mode === 'login' ? 'active' : ''}" data-auth-mode="login">Giriş yap</button><button class="${mode === 'register' ? 'active' : ''}" data-auth-mode="register">Kayıt ol</button></div>
          <form id="auth-form" class="form-stack">
            ${mode === 'register' ? `<label>Görünen ad<input name="displayName" maxlength="40" placeholder="Sana nasıl hitap edelim?" required /></label><label>Kullanıcı adı <span class="optional">(isteğe bağlı)</span><input name="nickname" maxlength="30" placeholder="ornek.kullanici" /></label>` : ''}
            <label>E-posta<input name="email" type="email" autocomplete="email" placeholder="sen@ornek.com" required /></label>
            <label>Parola<input name="password" type="password" minlength="8" autocomplete="${mode === 'login' ? 'current-password' : 'new-password'}" placeholder="En az 8 karakter" required /></label>
            ${message ? `<div class="inline-message error">${escapeHtml(message)}</div>` : ''}
            <button class="button primary full" type="submit">${mode === 'login' ? 'Giriş yap' : 'Hesabımı oluştur'} <span>↗</span></button>
          </form>
          <p class="auth-legal">Devam ederek Kullanım Koşulları ve Gizlilik Politikası'nı kabul etmiş olursun.</p>
        </div>
      </section>
    </main><div id="toast-host" class="toast-host"></div>`;
  app.querySelectorAll('[data-auth-mode]').forEach(button => button.addEventListener('click', () => showAuth(button.dataset.authMode)));
  app.querySelector('#auth-form').addEventListener('submit', submitAuth);
}

async function submitAuth(event) {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const isRegister = event.currentTarget.elements.displayName;
  const payload = { email: form.get('email'), password: form.get('password') };
  const mode = isRegister ? 'register' : 'login';
  if (isRegister) {
    payload.displayName = form.get('displayName');
    payload.nickname = form.get('nickname') || null;
  }
  const submit = event.currentTarget.querySelector('button[type=submit]');
  submit.disabled = true;
  submit.textContent = 'Bağlanıyor…';
  try {
    tokenStore.set(await request(`/api/auth/${mode}`, { method: 'POST', body: JSON.stringify(payload) }, false));
    await startApp();
  } catch (error) {
    showAuth(mode, error.message);
  }
}

async function startApp() {
  try {
    [state.user, state.profile] = await Promise.all([
      request('/api/auth/me'), request('/api/profiles/me')
    ]);
    await Promise.all([loadInterests(), loadSection('discover')]);
    connectRealtime();
    loadNotifications().then(renderApp).catch(() => {});
    renderApp();
  } catch (error) {
    tokenStore.clear();
    showAuth('login', error.message);
  }
}

function connectRealtime() {
  state.hub?.stop().catch(() => {});
  state.hub = new HubConnectionBuilder()
    .withUrl('/hubs/messages', { accessTokenFactory: () => tokenStore.access || '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Error)
    .build();
  const refreshCurrent = async () => {
    if (state.section === 'chats') await loadConversations(state.currentConversation?.id);
    if (state.section === 'requests') await loadRequests();
    if (state.section === 'matches') await loadMatches();
    renderApp();
  };
  ['MessageCreated', 'MessageDeleted', 'MessageHidden', 'MessageStatusChanged', 'ConversationUpdated']
    .forEach(name => state.hub.on(name, refreshCurrent));
  ['NotificationReceived', 'MatchCreated', 'MatchAccepted', 'MessageRequestReceived']
    .forEach(name => state.hub.on(name, async () => { await loadNotifications(); await refreshCurrent(); }));
  state.hub.start().catch(() => {});
  state.hub.onreconnected(async () => {
    if (state.currentConversation) {
      try { await state.hub.invoke('JoinConversation', state.currentConversation.id); } catch { /* reload still works */ }
    }
  });
}

async function loadSection(section) {
  state.section = section;
  try {
    if (section === 'discover') await loadDiscover();
    if (section === 'chats') await loadConversations(state.currentConversation?.id);
    if (section === 'requests') await loadRequests();
    if (section === 'matches') await loadMatches();
    if (section === 'stories') await loadStories();
    if (section === 'profile') { state.profile = await request('/api/profiles/me'); await loadProfileExtras(); }
    if (section === 'notifications') await loadNotifications();
  } catch (error) { showToast(error.message, 'error'); }
  renderApp();
}

async function loadDiscover() {
  try { state.mood = await request('/api/moodcheckins/me/latest'); }
  catch (error) {
    if (!String(error.message).includes('(404)')) throw error;
    state.mood = null;
  }
  if (!state.mood || state.mood.isExpired) { state.candidates = []; return; }
  const result = await request('/api/discovery?page=1&pageSize=20');
  state.candidates = result.items || [];
}
async function loadMatches() { state.matches = await request('/api/matches'); }
async function loadRequests() {
  const [matches, messageRequests] = await Promise.all([
    request('/api/matches/requests'), request('/api/message-requests/incoming')
  ]);
  state.requests = [
    ...(matches || []).map(item => ({ kind: 'match', ...item })),
    ...(messageRequests || []).map(item => ({ kind: 'message', ...item }))
  ];
}
async function loadConversations(selectedId) {
  state.conversations = await request('/api/conversations');
  if (!state.conversations.length) {
    state.currentConversation = null; state.messages = []; return;
  }
  state.currentConversation = state.conversations.find(item => item.id === selectedId) || state.conversations[0];
  const ids = [...new Set(state.conversations.map(x => x.otherUserId))];
  await Promise.all(ids.map(async id => {
    try {
      const [person, presence] = await Promise.all([request(`/api/profiles/${id}`), request(`/api/presence/${id}`)]);
      state.people.set(id, person); state.presence.set(id, presence);
    } catch { state.people.set(id, { userId: id, displayName: 'MindMatch üyesi' }); }
  }));
  const page = await request(`/api/conversations/${state.currentConversation.id}/messages?pageSize=50`);
  state.messages = (page.items || []).reverse();
  await Promise.allSettled(state.messages.filter(m=>m.senderId!==state.user.userId&&m.status!==3).map(m=>request(`/api/messages/${m.id}/read`,{method:'POST'})));
  if (state.hub?.state === 'Connected') {
    try { await state.hub.invoke('JoinConversation', state.currentConversation.id); } catch { /* REST remains available */ }
  }
}
async function loadStories() { [state.stories,state.myStories]=await Promise.all([request('/api/stories/feed?take=30'),request('/api/stories/me')]); await Promise.allSettled([...new Set(state.stories.map(x=>x.userId))].map(async id=>state.people.set(id,await request(`/api/profiles/${id}`)))); }
async function loadNotifications() { state.notifications = await request('/api/notifications?pageSize=40'); }
async function loadInterests(){try{state.interests=await request('/api/interests');}catch{state.interests=[];}}
async function loadProfileExtras(){const results=await Promise.allSettled([request('/api/profiles/me/photos'),request('/api/preferences/music'),request('/api/interests'),request('/api/blocks')]);if(results[0].status==='fulfilled')state.photos=results[0].value;if(results[1].status==='fulfilled')state.music=results[1].value;if(results[2].status==='fulfilled')state.interests=results[2].value;if(results[3].status==='fulfilled')state.blocked=results[3].value;}

function renderApp() {
  if (!state.user) return;
  const active = state.section;
  app.innerHTML = `
    <div class="app-layout">
      <aside class="sidebar">
        <div class="brand"><span class="brand-mark">m</span><span>mindmatch</span></div>
        <div class="sidebar-label">MENÜ</div>
        <nav class="main-nav">
          ${navItem('discover', 'Keşfet', active)}${navItem('chats', 'Sohbetler', active, state.conversations.length)}
          ${navItem('requests', 'İstekler', active, state.requests.length)}${navItem('matches', 'Eşleşmeler', active)}
          ${navItem('stories', 'Hikâyeler', active)}
        </nav>
        <div class="sidebar-bottom"><div class="sidebar-label">HESAP</div>${navItem('profile', 'Profilim', active)}${navItem('notifications', 'Bildirimler', active, state.notifications.filter(x => !x.isRead).length)}</div>
        <button class="user-mini" data-action="profile">${avatar(state.profile?.displayName || state.user.email, 'lilac')}<span class="user-mini-info"><strong>${escapeHtml(state.profile?.displayName || 'Profilini tamamla')}</strong><small>${escapeHtml(state.user.email)}</small></span><span class="mini-more">•••</span></button>
        <button class="logout-link" data-action="logout">↪ <span>Oturumu kapat</span></button>
      </aside>
      <main class="main-area">
        <header class="topbar"><div class="crumb"><span>MindMatch</span><b>/</b><strong>${sectionTitle(active)}</strong></div><div class="top-actions"><span class="online-indicator"><i></i> Güvendesin</span><button class="icon-button" data-action="notifications" aria-label="Bildirimler">${icons.bell}${state.notifications.some(x => !x.isRead) ? '<i class="unread-dot"></i>' : ''}</button><button class="avatar-button" data-action="profile">${avatar(state.profile?.displayName || state.user.email, 'lilac')}</button></div></header>
        <div class="content" id="content">${renderSection()}</div>
      </main>
    </div><div id="toast-host" class="toast-host"></div>`;
  bindAppEvents();
}

function navItem(key, label, active, count = 0) {
  return `<button class="nav-item ${key === active ? 'selected' : ''}" data-nav="${key}"><span class="nav-icon">${icons[key]}</span><span>${label}</span>${count ? `<small>${count}</small>` : ''}</button>`;
}
function sectionTitle(section) {
  return ({ discover: 'Keşfet', chats: 'Sohbetler', requests: 'İstekler', matches: 'Eşleşmeler', stories: 'Hikâyeler', profile: 'Profilim', notifications: 'Bildirimler' })[section] || 'Keşfet';
}

function renderSection() {
  if (state.section === 'discover') return state.mood && !state.mood.isExpired ? renderDiscover() : renderMoodCheckIn();
  if (state.section === 'chats') return renderChats();
  if (state.section === 'requests') return renderRequests();
  if (state.section === 'matches') return renderMatches();
  if (state.section === 'stories') return renderStories();
  if (state.section === 'profile') return renderProfile();
  return renderNotifications();
}

function renderMoodCheckIn() {
  const groups = [
    { name: 'mood', title: 'Şu an nasıl hissediyorsun?', options: [[1,'Mutlu','☀'],[2,'Üzgün','☁'],[3,'Stresli','〰'],[4,'Yalnız','♡'],[5,'Sakin','☾'],[6,'Endişeli','◌'],[7,'Heyecanlı','✦'],[8,'Yorgun','◡'],[9,'Nötr','—']] },
    { name: 'energy', title: 'Enerjin nasıl?', options: [[1,'Düşük','↓'],[2,'Orta','→'],[3,'Yüksek','↑']] },
    { name: 'desiredConversation', title: 'Nasıl bir sohbet istersin?', options: [[1,'İçimi dökmek','♡'],[2,'Tavsiye almak','✦'],[3,'Eğlenmek','☺'],[4,'Derin sohbet','◉'],[5,'Günlük sohbet','☕'],[6,'Yeni arkadaşlık','＋'],[7,'Sadece dinlenmek','耳']] }
  ];
  return `<section class="page-heading"><div><span class="eyebrow">BUGÜNÜN CHECK-IN'İ</span><h1>Şu anki <em>ruh hâlin nasıl?</em></h1><p>Seçimlerin iki saat boyunca keşif eşleşmelerini ve sohbet önerilerini şekillendirir.</p></div></section><form class="mood-checkin" id="mood-form">${groups.map(group => `<fieldset class="mood-group"><legend>${group.title}</legend><div class="mood-options">${group.options.map(([value,label,icon]) => `<label class="mood-option"><input type="radio" name="${group.name}" value="${value}" required><span class="mood-choice"><b>${icon}</b>${label}</span></label>`).join('')}</div></fieldset>`).join('')}<p class="mood-privacy">Bu, kendi seçtiğin kısa bir check-in'dir; uygulama duygularını otomatik olarak analiz etmez.</p><button class="button primary" type="submit">Keşfi ruh hâlime göre başlat <span>↗</span></button></form>`;
}

function renderDiscover() {
  const candidate = state.candidates[0];
  return `<section class="page-heading"><div><span class="eyebrow">SENİN İÇİN</span><h1>Yeni bir bağa <em>yer aç.</em></h1><p>Bugünkü ruh hâline yakın insanları keşfet.</p></div><button class="button subtle" data-action="reload-discovery">↻ Yenile</button></section>
    <div class="discover-layout"><div class="discover-main">${candidate ? renderCandidate(candidate) : `<div class="empty-state"><span class="empty-icon">✦</span><h3>Şimdilik hepsi bu kadar</h3><p>Daha sonra tekrar uğra, yeni yüzler burada olacak.</p><button class="button secondary" data-action="reload-discovery">Yeniden dene</button></div>`}</div>
    <aside class="side-card mood-card"><span class="mood-symbol">☼</span><span class="eyebrow">KÜÇÜK BİR HATIRLATMA</span><h3>Doğru insan, doğru hisle başlar.</h3><p>Ortak ilgi alanları ve ruh hâli eşleşmeleri tanışmayı kolaylaştırır.</p><div class="mood-chips"><span>müzik</span><span>ilgi alanları</span><span>ruh hâli</span></div></aside></div>`;
}

function renderCandidate(person) {
  const name = person.displayName || person.nickname || 'Yeni biri';
  const tone = ['peach', 'mint', 'blue', 'lilac'][name.length % 4];
  const reasons = (person.discoveryReasons || []).slice(0, 3).map(reason => `<span class="reason-chip">✦ ${escapeHtml(reason)}</span>`).join('');
  return `<article class="person-card"><div class="person-cover ${tone}"><span class="cover-orb orb-a"></span><span class="cover-orb orb-b"></span><div class="person-card-top"><span class="status-pill">${person.showPremiumBadge ? '✦ Premium' : '✳ Yeni keşif'}</span><button class="icon-button glass" data-action="skip-person" data-id="${person.userId}" title="Geç">×</button></div><div class="person-monogram">${escapeHtml(initials(name))}</div><div class="cover-note">${person.city ? escapeHtml(person.city) : 'aynı frekansta'}</div></div><div class="person-info"><div class="person-title"><div><h2>${escapeHtml(name)}${person.age ? `, ${person.age}` : ''}</h2><span class="handle">@${escapeHtml(person.nickname || 'mindmatch')}</span></div><span class="verified">✦</span></div><p class="person-bio">${escapeHtml(person.bio || 'Henüz kendinden bahsetmemiş.')}</p><div class="reason-list">${reasons || '<span class="reason-chip">✦ Yeni insanlarla tanışmaya açık</span>'}</div><div class="person-actions"><button class="button secondary" data-action="skip-person" data-id="${person.userId}">Pas geç</button><button class="button primary" data-action="like-person" data-id="${person.userId}">Tanışmak istiyorum <span>♡</span></button></div></div></article>`;
}

function renderChats() {
  const list = state.conversations.map(item => { const p=state.people.get(item.otherUserId)||{}; const name=p.displayName||'MindMatch üyesi'; return `<button class="conversation-row ${state.currentConversation?.id === item.id ? 'active' : ''}" data-action="select-conversation" data-id="${item.id}">${avatar(name, 'blue')}<span class="conversation-meta"><strong>${escapeHtml(name)}</strong><small>${item.lastMessageAtUtc ? new Date(item.lastMessageAtUtc).toLocaleString('tr-TR') : 'Sohbet başladı'}</small></span><span class="row-arrow">›</span></button>`; }).join('');
  if (!state.conversations.length) return `<section class="page-heading"><div><span class="eyebrow">BAĞLANTILARIN</span><h1>Sohbetler</h1><p>Tanışmaların burada sohbete dönüşür.</p></div></section>${emptyBlock('◌', 'Henüz sohbetin yok', 'Bir eşleşme ya da kabul edilmiş mesaj isteği sonrası sohbet burada görünür.', 'Keşfet', 'discover')}`;
  const conversation = state.currentConversation;
  const person=state.people.get(conversation.otherUserId)||{}; const name=person.displayName||'MindMatch üyesi'; const presence=state.presence.get(conversation.otherUserId)||{};
  const presenceText=presence.isOnline?'Şu an çevrimiçi':presence.lastSeenAtUtc?`Son görülme ${relativeTime(presence.lastSeenAtUtc)}`:'MindMatch sohbeti';
  return `<section class="page-heading compact"><div><span class="eyebrow">BAĞLANTILARIN</span><h1>Sohbetler</h1></div><span class="muted">${state.conversations.length} aktif sohbet</span></section><div class="chat-shell"><aside class="conversation-list">${list}</aside><section class="chat-panel"><header class="chat-header">${avatar(name, 'blue')}<div><strong>${escapeHtml(name)}</strong><small class="presence ${presence.isOnline?'online':''}">${escapeHtml(presenceText)}</small></div><button class="icon-button chat-more" data-action="person-menu" data-id="${conversation.otherUserId}" aria-label="Sohbet seçenekleri">•••</button></header><div class="message-list" id="message-list">${state.messages.length ? state.messages.map(renderMessage).join('') : '<div class="chat-empty">İlk mesajı sen gönder, sohbet başlasın ✨</div>'}</div><form class="composer" id="message-form"><input name="content" maxlength="5000" placeholder="Güzel bir sohbet başlat…" autocomplete="off" required /><button class="send-button" aria-label="Mesaj gönder">${icons.send}</button></form></section></div>`;
}
function relativeTime(value){const m=Math.max(0,Math.floor((Date.now()-new Date(value).getTime())/60000)); if(m<1)return 'az önce'; if(m<60)return `${m} dk. önce`; const h=Math.floor(m/60); if(h<24)return `${h} sa. önce`; return `${Math.floor(h/24)} gün önce`;}

function renderMessage(message) {
  const mine = message.senderId === state.user.userId;
  return `<div class="message-line ${mine ? 'mine' : ''}"><div class="message-bubble">${escapeHtml(message.isDeletedForEveryone ? 'Bu mesaj silindi.' : message.content || '')}<small>${new Date(message.createdAtUtc).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}${mine ? ` · ${message.status === 3 ? 'Görüldü' : message.status === 2 ? 'İletildi' : 'Gönderildi'}` : ''}</small></div></div>`;
}

function renderRequests() {
  const cards = state.requests.map(item => {
    const isMatch = item.kind === 'match';
    const otherId = isMatch ? item.requesterId : item.senderId;
    const title = isMatch ? 'Eşleşme isteği' : 'Mesaj isteği';
    return `<article class="request-card">${avatar(otherId, isMatch ? 'peach' : 'mint')}<div class="request-content"><span class="eyebrow">${title.toUpperCase()}</span><h3>Kullanıcı ${escapeHtml(String(otherId).slice(0, 6))}</h3><p>${escapeHtml(isMatch ? 'Seninle tanışmak istiyor.' : item.initialMessage || 'Sana bir mesaj isteği gönderdi.')}</p><small>${new Date(item.createdAtUtc).toLocaleString('tr-TR')}</small></div><div class="request-actions"><button class="button secondary" data-action="reject-request" data-kind="${item.kind}" data-id="${isMatch ? item.id : item.conversationId}">Şimdi değil</button><button class="button primary" data-action="accept-request" data-kind="${item.kind}" data-id="${isMatch ? item.id : item.conversationId}">Kabul et</button></div></article>`;
  }).join('');
  return `<section class="page-heading"><div><span class="eyebrow">YENİ BAĞLANTILAR</span><h1>İstekler</h1><p>Tanışma davetlerine göz at.</p></div></section>${cards || emptyBlock('↗', 'Bekleyen isteğin yok', 'Yeni bir istek geldiğinde burada göreceksin.')}`;
}

function renderMatches() {
  const cards = state.matches.map(match => {
    const other = match.requesterId === state.user.userId ? match.receiverId : match.requesterId;
    const status = ({ 1: 'Yanıt bekliyor', 2: 'Eşleştiniz', 3: 'Reddedildi', 4: 'Sonlandırıldı', 5: 'Süresi doldu' })[match.status] || 'Durum';
    return `<article class="match-row">${avatar(other, match.status === 2 ? 'mint' : 'lilac')}<div><strong>Kullanıcı ${escapeHtml(String(other).slice(0, 6))}</strong><small>${new Date(match.createdAtUtc).toLocaleDateString('tr-TR')}</small></div><span class="match-status ${match.status === 2 ? 'positive' : ''}">${status}</span>${match.status === 2 ? `<button class="text-button" data-action="go-chats">Sohbete git ↗</button>` : ''}</article>`;
  }).join('');
  return `<section class="page-heading"><div><span class="eyebrow">KARŞILIKLI HİS</span><h1>Eşleşmeler</h1><p>Birbirinize evet dediğiniz insanlar.</p></div></section><div class="list-card">${cards || emptyBlock('♡', 'Henüz eşleşme yok', 'Keşfe çıkıp birine merhaba diyebilirsin.', 'Keşfet', 'discover')}</div>`;
}

function renderStories() {
  const cards=state.stories.map((s,i)=>`<button class="story-bubble ${s.hasViewed?'viewed':''}" data-action="open-story" data-index="${i}"><span class="story-ring">${avatar(state.people.get(s.userId)?.displayName||'M','peach','large')}</span><strong>${escapeHtml(state.people.get(s.userId)?.displayName||'Hikâye')}</strong></button>`).join('');
  return `<section class="page-heading"><div><span class="eyebrow">24 SAAT SENİNLE</span><h1>Hikâyeler</h1><p>Günün küçük anlarını paylaş ve bağlantılarını yakala.</p></div><button class="button primary" data-action="new-story">＋ Hikâye ekle</button></section><div class="story-strip"><button class="story-bubble own-story" data-action="new-story"><span class="own-add">＋</span><strong>Hikâyen</strong></button>${state.myStories.map(s=>`<button class="story-bubble" data-action="my-story-viewers" data-id="${s.id}"><span class="story-ring">${avatar(state.profile?.displayName||'Ben','lilac','large')}</span><strong>${escapeHtml(s.caption||'Benim anım')}</strong></button>`).join('')}${cards}</div><div class="story-grid">${state.stories.length?state.stories.map(s=>`<article class="story-card ${s.hasViewed?'viewed':''}"><div class="story-ring">${avatar(state.people.get(s.userId)?.displayName||'M','blue','large')}</div><strong>${escapeHtml(state.people.get(s.userId)?.displayName||'Yeni hikâye')}</strong><small>${s.hasViewed?'Görüldü':'Yeni hikâye'} · ${relativeTime(s.createdAtUtc)}</small><p>${escapeHtml(s.caption||'Paylaşılan bir an')}</p><button class="button secondary full" data-action="open-story" data-id="${s.id}" data-index="${state.stories.indexOf(s)}">Görüntüle</button></article>`).join(''):emptyBlock('◉','Henüz hikâye yok','İlk anını paylaş; bağlantıların 24 saat boyunca görebilsin.')}</div>`;
}

function showStoryComposer(){const host=document.createElement('div');host.className='modal-backdrop';host.innerHTML=`<form class="modal-card" id="story-upload-form"><button type="button" class="modal-close" data-dismiss>×</button><span class="eyebrow">YENİ BİR AN</span><h2>Hikâyeni paylaş</h2><p class="muted">Fotoğraf veya kısa video yükle. Hikâyen 24 saat sonra kaybolur.</p><label>Medya<input name="file" type="file" accept="image/*,video/*" required></label><label>Bir not ekle<input name="caption" maxlength="500" placeholder="Bu anı özel yapan ne?" /></label><button class="button primary full" type="submit">Hikâyeyi paylaş ↗</button></form>`;document.body.append(host);host.querySelector('[data-dismiss]').onclick=()=>host.remove();host.querySelector('form').addEventListener('submit',submitStory);}
async function submitStory(event){event.preventDefault();const form=event.currentTarget;const file=form.elements.file.files[0];if(!file)return;const video=file.type.startsWith('video/');try{const data=new FormData();data.append('file',file);const media=await request(`/api/media?category=${video?5:4}`,{method:'POST',body:data});await request('/api/stories',{method:'POST',body:JSON.stringify({mediaAssetId:media.id,mediaType:video?2:1,caption:form.elements.caption.value||null})});form.closest('.modal-backdrop').remove();await loadStories();renderApp();showToast('Hikâyen 24 saat boyunca paylaşımda ✨','success');}catch(error){showToast(error.message,'error');}}
async function openStory(index,id){if(id)index=state.stories.findIndex(x=>x.id===id);if(index<0||index>=state.stories.length)return;const story=state.stories[index];try{await request(`/api/stories/${story.id}/view`,{method:'POST'});const src=story.mediaAssetId?`/api/media/${story.mediaAssetId}`:'';const content=story.mediaType===2?`<video src="${src}" autoplay controls playsinline></video>`:`<img src="${src}" alt="Hikâye medyası">`;const layer=document.createElement('div');layer.className='story-viewer';layer.innerHTML=`<div class="story-progress"><i></i></div><button class="story-viewer-close">×</button>${content}<div class="story-viewer-caption">${escapeHtml(story.caption||'')}</div><button class="story-next">›</button>`;document.body.append(layer);const close=()=>{layer.remove();loadStories().then(renderApp);};layer.querySelector('.story-viewer-close').onclick=close;layer.querySelector('.story-next').onclick=()=>{layer.remove();openStory(index+1);};if(index===state.stories.length-1)layer.querySelector('.story-next').hidden=true;setTimeout(()=>{if(layer.isConnected)close();},15000);}catch(error){showToast(error.message,'error');}}
async function showStoryViewers(id){try{const viewers=await request(`/api/stories/${id}/viewers`);const names=await Promise.all(viewers.slice(0,4).map(async v=>{try{return (await request(`/api/profiles/${v.viewerId}`)).displayName;}catch{return 'MindMatch üyesi';}}));showToast(names.length?`${viewers.length} görüntüleyen · ${names.join(', ')}`:'Henüz görüntüleyen yok');}catch(error){showToast(error.message,'error');}}
async function uploadProfilePhoto(){const file=document.querySelector('#profile-photo-file')?.files?.[0];if(!file){showToast('Önce bir fotoğraf seç.','error');return;}try{const data=new FormData();data.append('file',file);const media=await request('/api/media?category=1',{method:'POST',body:data});await request('/api/profiles/me/photos',{method:'POST',body:JSON.stringify({mediaAssetId:media.id})});await loadProfileExtras();renderApp();showToast('Profil fotoğrafı eklendi.','success');}catch(error){showToast(error.message,'error');}}
function showPersonMenu(userId){const layer=document.createElement('div');layer.className='modal-backdrop';layer.innerHTML=`<section class="modal-card"><button class="modal-close" data-close>×</button><span class="eyebrow">SOHBET GÜVENLİĞİ</span><h2>Bu kişiyle ilgili</h2><p class="muted">Deneyimini kontrol et. Raporlar gizli olarak incelenir.</p><button class="button secondary" data-report>Rapor et</button><button class="button secondary" data-block>Bu kişiyi engelle</button></section>`;document.body.append(layer);layer.querySelector('[data-close]').onclick=()=>layer.remove();layer.addEventListener('click',e=>{if(e.target===layer)layer.remove();});layer.querySelector('[data-block]').onclick=async()=>{try{await request(`/api/blocks/${userId}`,{method:'POST'});layer.remove();showToast('Kullanıcı engellendi.','success');await loadConversations();renderApp();}catch(error){showToast(error.message,'error');}};layer.querySelector('[data-report]').onclick=()=>{layer.querySelector('.modal-card').innerHTML=`<button class="modal-close" data-close>×</button><span class="eyebrow">GÜVENLİK RAPORU</span><h2>Rapor et</h2><form id="report-form"><label>Neden<select name="reason"><option value="1">Taciz</option><option value="2">Hakaret</option><option value="3">Cinsel içerik</option><option value="4">Spam</option><option value="5">Sahte profil</option><option value="6">Tehdit</option><option value="7">Uygunsuz medya</option><option value="8">Diğer</option></select></label><label>Açıklama<textarea name="description" maxlength="1000" rows="3" placeholder="İstersen ayrıntı ekle"></textarea></label><button class="button primary full">Gizli raporu gönder</button></form>`;layer.querySelector('[data-close]').onclick=()=>layer.remove();layer.querySelector('#report-form').onsubmit=async e=>{e.preventDefault();const fd=new FormData(e.currentTarget);try{await request('/api/reports',{method:'POST',body:JSON.stringify({reportedUserId:userId,reason:Number(fd.get('reason')),description:fd.get('description')||null,conversationId:state.currentConversation?.id||null,messageIds:[]})});layer.remove();showToast('Raporun güvenli ekibe iletildi.','success');}catch(error){showToast(error.message,'error');}};};}

function renderProfile() {
  const profile = state.profile || {};
  return `<section class="page-heading"><div><span class="eyebrow">KENDİNİ ANLAT</span><h1>Profilin, <em>senin alanın.</em></h1><p>Tanışmaların seni doğru yansıtması için bilgilerini güncel tut.</p></div></section><div class="profile-editor"><div class="profile-preview">${avatar(profile.displayName || state.user.email, 'lilac', 'xl')}<h2>${escapeHtml(profile.displayName || 'İsmini ekle')}</h2><span>@${escapeHtml(profile.nickname || 'kullanici.adi')}</span><p>${escapeHtml(profile.bio || 'Kısa bir biyografi ekle ve insanlara kendinden biraz bahset.')}</p><div class="preview-state ${profile.isProfileVisible ? 'visible' : ''}"><i></i>${profile.isProfileVisible ? 'Profilin keşifte görünür' : 'Profilin şu an gizli'}</div></div><form class="profile-form" id="profile-form"><label>Görünen ad<input name="displayName" maxlength="40" value="${escapeHtml(profile.displayName || '')}" required /></label><label>Kullanıcı adı<input name="nickname" maxlength="30" value="${escapeHtml(profile.nickname || '')}" placeholder="ornek.kullanici" /></label><label>Şehir<input name="city" maxlength="100" value="${escapeHtml(profile.city || '')}" placeholder="İstanbul" /></label><label>Biyografi<textarea name="bio" maxlength="300" rows="4" placeholder="Neleri sevdiğinden bahset…">${escapeHtml(profile.bio || '')}</textarea></label><section class="profile-section"><h3>İlgi alanların</h3><div class="interest-picker">${state.interests.map(i=>`<label><input type="checkbox" name="interestIds" value="${i.id}" ${(profile.interestIds||[]).includes(i.id)?'checked':''}><span>${escapeHtml(i.name)}</span></label>`).join('')||'<small>İlgi alanı bulunamadı.</small>'}</div></section><section class="profile-section"><h3>Müzik zevkin</h3><input name="genres" value="${escapeHtml((state.music.genres||[]).join(', '))}" placeholder="Indie, jazz, elektronik"><small>Türleri virgülle ayır.</small></section><section class="profile-section"><h3>Profil fotoğrafları</h3><div class="photo-row">${state.photos.map(p=>`<div class="profile-photo"><img src="/api/media/${p.mediaAssetId}" alt="Profil fotoğrafı"><button type="button" data-action="primary-photo" data-id="${p.id}">${p.isPrimary?'★':'☆'}</button><button type="button" data-action="delete-photo" data-id="${p.id}">×</button></div>`).join('')}</div><input type="file" id="profile-photo-file" accept="image/*"><button type="button" class="button secondary" data-action="upload-profile-photo">Fotoğraf yükle</button></section><label class="toggle-row"><span><strong>Profilimi keşifte göster</strong><small>Profil görünürlüğü mesaj izni anlamına gelmez.</small></span><input type="checkbox" name="visible" ${profile.isProfileVisible ? 'checked' : ''} /></label><label class="toggle-row"><span><strong>Mesaj isteği kabul et</strong><small>Eşleşmediğin kişiler sana istek gönderebilir.</small></span><input type="checkbox" name="allowRequests" ${profile.allowMessageRequests ? 'checked' : ''} /></label>${[['allowCallsFromNonMatches','Eşleşmediğim kişilerden arama kabul et'],['showOnlineStatus','Çevrimiçi durumumu göster'],['showLastSeen','Son görülmemi göster'],['showReadReceipts','Okundu bilgisini göster'],['showPremiumBadge','Premium rozetimi göster']].map(([key,label])=>`<label class="toggle-row"><span><strong>${label}</strong></span><input type="checkbox" name="${key}" ${profile[key]?'checked':''}></label>`).join('')}<button class="button primary" type="submit">Değişiklikleri kaydet <span>↗</span></button></form></div>`;
}

function renderNotifications() {
  const items = state.notifications.map(item => `<article class="notification-row ${item.isRead ? '' : 'unread'}">${avatar(item.title || 'M', item.isRead ? 'blue' : 'peach')}<div><strong>${escapeHtml(item.title)}</strong><p>${escapeHtml(item.body)}</p><small>${new Date(item.createdAtUtc).toLocaleString('tr-TR')}</small></div>${!item.isRead ? `<button class="text-button" data-action="read-notification" data-id="${item.id}">Okundu işaretle</button>` : '<span class="read-mark">✓</span>'}</article>`).join('');
  return `<section class="page-heading"><div><span class="eyebrow">HABERDAR OL</span><h1>Bildirimler</h1><p>Bağlantılarından gelen yenilikler.</p></div><button class="button subtle" data-action="read-all">Tümünü okundu işaretle</button></section><div class="list-card">${items || emptyBlock('♧', 'Yeni bildirim yok', 'Bir haber olduğunda burada bulacaksın.')}</div>`;
}

function emptyBlock(icon, title, body, button, action) {
  return `<div class="empty-state"><span class="empty-icon">${icon}</span><h3>${title}</h3><p>${body}</p>${button ? `<button class="button secondary" data-action="${action}">${button}</button>` : ''}</div>`;
}

function bindAppEvents() {
  app.querySelectorAll('[data-nav]').forEach(button => button.addEventListener('click', () => loadSection(button.dataset.nav)));
  app.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', () => handleAction(button)));
  app.querySelector('#message-form')?.addEventListener('submit', sendMessage);
  app.querySelector('#profile-form')?.addEventListener('submit', saveProfile);
  app.querySelector('#mood-form')?.addEventListener('submit', saveMoodCheckIn);
  const messageList = app.querySelector('#message-list');
  if (messageList) messageList.scrollTop = messageList.scrollHeight;
}

async function saveMoodCheckIn(event) {
  event.preventDefault();
  const values = new FormData(event.currentTarget);
  const submit = event.currentTarget.querySelector('button[type=submit]');
  submit.disabled = true;
  try {
    state.mood = await request('/api/moodcheckins', { method: 'POST', body: JSON.stringify({
      mood: Number(values.get('mood')), energy: Number(values.get('energy')),
      desiredConversation: Number(values.get('desiredConversation'))
    }) });
    await loadDiscover();
    renderApp();
  } catch (error) { showToast(error.message, 'error'); submit.disabled = false; }
}

async function handleAction(button) {
  const id = button.dataset.id;
  try {
    switch (button.dataset.action) {
      case 'reload-discovery': await loadDiscover(); renderApp(); break;
      case 'skip-person':
        await request(`/api/discovery/${id}/reject`, { method: 'POST' });
        state.candidates = state.candidates.filter(person => person.userId !== id);
        renderApp(); break;
      case 'like-person':
        await request('/api/matches', { method: 'POST', body: JSON.stringify({ receiverId: id }) });
        try { await request(`/api/discovery/${id}/seen`, { method: 'POST' }); } catch { /* the match request already succeeded */ }
        state.candidates = state.candidates.filter(person => person.userId !== id);
        showToast('Tanışma isteğin gönderildi ✨', 'success'); renderApp(); break;
      case 'accept-request':
        await request(button.dataset.kind === 'match' ? `/api/matches/${id}/accept` : `/api/message-requests/${id}/accept`, { method: 'POST' });
        await loadRequests(); await loadConversations(); showToast('İstek kabul edildi.', 'success'); renderApp(); break;
      case 'reject-request':
        await request(button.dataset.kind === 'match' ? `/api/matches/${id}/reject` : `/api/message-requests/${id}/reject`, { method: 'POST' });
        await loadRequests(); renderApp(); break;
      case 'select-conversation': await loadConversations(id); renderApp(); break;
      case 'person-menu': showPersonMenu(id); break;
      case 'go-chats': await loadSection('chats'); break;
      case 'view-story': case 'open-story': await openStory(Number(button.dataset.index)||0,id); break;
      case 'new-story': showStoryComposer(); break;
      case 'my-story-viewers': await showStoryViewers(id); break;
      case 'reload-stories': await loadStories(); renderApp(); break;
      case 'upload-profile-photo': await uploadProfilePhoto(); break;
      case 'primary-photo': await request(`/api/profiles/me/photos/${id}/primary`,{method:'PUT'}); await loadProfileExtras(); renderApp(); break;
      case 'delete-photo': await request(`/api/profiles/me/photos/${id}`,{method:'DELETE'}); await loadProfileExtras(); renderApp(); break;
      case 'read-notification':
        await request(`/api/notifications/${id}/read`, { method: 'POST' });
        await loadNotifications(); renderApp(); break;
      case 'read-all':
        await request('/api/notifications/read-all', { method: 'POST' });
        await loadNotifications(); renderApp(); break;
      case 'profile': await loadSection('profile'); break;
      case 'notifications': await loadSection('notifications'); break;
      case 'logout': await logout(); break;
    }
  } catch (error) { showToast(error.message, 'error'); }
}

async function sendMessage(event) {
  event.preventDefault();
  const field = event.currentTarget.elements.content;
  const content = field.value.trim();
  if (!content || !state.currentConversation) return;
  field.disabled = true;
  try {
    const message = await request('/api/messages', {
      method: 'POST', body: JSON.stringify({ conversationId: state.currentConversation.id, type: 1, content })
    });
    state.messages.push(message); field.value = ''; renderApp();
  } catch (error) { showToast(error.message, 'error'); }
  finally { field.disabled = false; field.focus(); }
}

async function saveProfile(event) {
  event.preventDefault();
  const fields = new FormData(event.currentTarget);
  try {
    state.profile = await request('/api/profiles/me', { method: 'PUT', body: JSON.stringify({
      displayName: fields.get('displayName'), nickname: fields.get('nickname') || null,
      bio: fields.get('bio') || null, city: fields.get('city') || null,
      birthDate: state.profile.birthDate || null, gender: state.profile.gender || 0,
      genderPreference: state.profile.genderPreference || 0,
      minimumMatchAge: state.profile.minimumMatchAge || null, maximumMatchAge: state.profile.maximumMatchAge || null
    }) });
    await request(`/api/profiles/me/visibility?isVisible=${fields.has('visible')}`, { method: 'PATCH' });
    await request('/api/interests/me',{method:'PUT',body:JSON.stringify({interestIds:fields.getAll('interestIds')})});
    await request('/api/preferences/music',{method:'PUT',body:JSON.stringify({genres:(fields.get('genres')||'').split(',').map(x=>x.trim()).filter(Boolean)})});
    await request('/api/profiles/me/preferences', { method: 'PUT', body: JSON.stringify({
      allowMessageRequests: fields.has('allowRequests'), allowCallsFromNonMatches: fields.has('allowCallsFromNonMatches'),
      showOnlineStatus: fields.has('showOnlineStatus'), showLastSeen: fields.has('showLastSeen'),
      showReadReceipts: fields.has('showReadReceipts'), showPremiumBadge: fields.has('showPremiumBadge'),
      premiumPriorityOptIn: state.profile.premiumPriorityOptIn
    }) });
    state.profile = await request('/api/profiles/me'); await loadProfileExtras();
    showToast('Profilin güncellendi.', 'success'); renderApp();
  } catch (error) { showToast(error.message, 'error'); }
}

async function logout() {
  try { await request('/api/auth/logout', { method: 'POST', body: JSON.stringify({ refreshToken: tokenStore.refresh }) }); }
  catch { /* local sign-out still completes */ }
  tokenStore.clear(); showAuth();
}

if (tokenStore.access) startApp(); else showAuth();
