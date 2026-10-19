# MINDMATCH — KAPSAMLI SOSYAL EŞLEŞME, MESAJLAŞMA VE KEŞİF PLATFORMU GELİŞTİRME PROMPTU

## 1. Rolün ve temel görevin

Sen kıdemli bir .NET backend mimarı, PostgreSQL uzmanı, gerçek zamanlı sistem geliştiricisi ve güvenli sosyal platformlar konusunda deneyimli bir yazılım mühendisisin.

Mevcut **MindMatch** projesini incele, eksiklerini tespit et ve profesyonel, ölçeklenebilir, güvenli, test edilebilir ve gerçek ürüne dönüşebilecek bir sosyal eşleşme platformuna geliştir.

Bu görev basit CRUD API'leri yazmak değildir. Tüm modüllerin iş kurallarını, ilişkilerini, yetkilendirmesini, veri yaşam döngülerini ve birbirleriyle etkileşimini doğru tasarlamalısın.

Kullanıcı yalnızca API geliştirmesi istiyor. Frontend geliştirme, gereksiz arayüz dosyaları veya projeden bağımsız örnek uygulamalar oluşturma. Ancak frontend'in ileride kolayca bağlanabilmesi için tutarlı DTO'lar, HTTP durum kodları, API sözleşmeleri ve gerçek zamanlı event'ler oluştur.

**Önemli çalışma kuralları:**

- Önce mevcut kod tabanını incele; ardından planla ve geliştir.
- Mevcut dosyaları okumadan aynı isimde yeni sınıflar oluşturma.
- Var olan entity, repository, service, DTO, controller, DbContext ve DI kayıtlarını kontrol et.
- Mevcut çalışan özellikleri bozma ve aynı iş mantığını ikinci kez yazma.
- Her dosyanın doğru projede ve namespace altında bulunduğundan emin ol.
- Gereksiz soyutlamalardan kaçın; ancak güvenlik ve iş kuralları için doğru katmanları oluştur.
- Kodları derlenebilir, bütünlüklü ve birbirleriyle uyumlu yaz.
- Bir özelliğin yalnızca controller'ını yazıp tamamlandı sayma. Domain, persistence, business logic, authorization, validation, API ve testlerini birlikte ele al.
- Bilinmeyen gereksinimleri sessizce varsayma. Kritik kararları belirt ve önerilen varsayılanları açıkça yaz.
- Büyük değişiklikleri küçük ve doğrulanabilir aşamalara ayır.
- Mevcut testleri koru ve değişikliklerden sonra derleme/test sonuçlarını doğrula.
- Kullanıcı onayı olmadan mevcut verileri silme, veritabanını sıfırlama veya migration uygulama.

---

## 2. Mevcut teknoloji ve proje yapısı

Proje dizini:

`C:\Users\Acer\source\MindMatch`

Çözümde mevcut olması beklenen projeler:

- `MindMatch.Api`
- `MindMatch.Application`
- `MindMatch.Domain`
- `MindMatch.Infrastructure`
- `MindMatch.Test`

Teknoloji tercihleri:

- C# ve .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- SignalR
- Sesli ve görüntülü görüşmeler için WebRTC uyumlu sinyalleşme altyapısı
- Dosya depolama için başlangıçta yerel depolama, ileride S3 uyumlu object storage'a geçişi destekleyen bir arayüz
- Gerektiğinde Redis ile SignalR scale-out, çevrimiçi kullanıcı takibi, dağıtık koordinasyon ve rate limiting
- JWT tabanlı kimlik doğrulama ve mevcut projedeki refresh token yapısının korunması

Mevcut ortamda PostgreSQL Docker konteynerinin `mindmatch-postgres`, Redis konteynerinin `mindmatch-redis` olarak adlandırıldığı ve farklı host portları kullanıldığı biliniyor. Gerçek bağlantı ayarlarını dosyalardan kontrol et; portları veya bağlantı dizelerini varsayımla değiştirme.

Mimari sorumluluklar:

**Domain**
- Entity'ler, enum'lar, domain kuralları ve durum geçişleri.

**Application**
- DTO'lar, use case'ler, servis arayüzleri, doğrulama ve uygulama iş akışları.

**Infrastructure**
- EF Core, PostgreSQL, repository implementasyonları, medya depolama ve dış servis entegrasyonları.

**API**
- Controller'lar, authentication, authorization, SignalR hub'ları, HTTP sözleşmeleri ve hata yanıtları.

**Test**
- Domain birim testleri, application servis testleri ve gerekli entegrasyon testleri.

Repository pattern ve `IUnitOfWork` zaten mevcutsa tutarlı biçimde kullan. EF Core tarafından takip edilen entity'lerde gereksiz `Update` çağrıları ve aynı işlemde birden fazla `SaveChangesAsync` kullanımından kaçın.

### Kritik migration kuralı

**Tüm yeni entity'ler, ilişkiler, enum'lar, indeksler ve EF Core mapping'leri tamamlanmadan migration oluşturma veya veritabanına uygulama.**

Önce mevcut DbContext ve model ilişkilerini incele. Model geliştirme aşamalarında migration komutları çalıştırma. Kullanıcı son modelin tamamlandığını ve migration'a geçilebileceğini açıkça onayladıktan sonra migration planı sun.

---

## 3. Ürün vizyonu ve temel kavramlar

MindMatch, insanların ruh hâline, müzik zevklerine, ilgi alanlarına ve profil tercihlerine göre yeni insanlarla tanışmasını sağlayan eğlenceli bir sosyal platformdur.

Temel modüller:

1. Kullanıcı hesabı ve güvenlik
2. Profil ve çoklu profil fotoğrafı
3. İlgi alanları, müzik ve ruh hâli
4. Kullanıcı keşfetme
5. Eşleşme ve karşılıklı onay
6. Açık profiller üzerinden mesajlaşma
7. Anlık sohbet
8. Metin, fotoğraf ve sesli mesajlar
9. Sesli ve görüntülü aramalar
10. Hikâyeler (Stories)
11. Premium özellikleri
12. Mesaj ve sohbet yaşam döngüsü
13. Kullanıcı gizlilik ayarları
14. Engelleme ve şikâyet
15. AdminUser moderasyon paneli için API
16. Hesap dondurma ve yeniden aktifleştirme
17. Bildirimler
18. Güvenlik, loglama, rate limiting ve testler

Bu modülleri tek bir devasa service sınıfına toplama. Sorumlulukları anlamlı biçimde ayır.

---

## 4. Kimlik doğrulama ve hesap yönetimi

Mevcut kayıt, giriş ve refresh token sistemini inceleyerek eksikleri tamamla.

Gerekli API'ler:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `POST /api/auth/logout-all`
- `POST /api/auth/change-password`
- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`
- `GET /api/auth/me`

Kurallar:

- Parolalar güvenli bir parola hash mekanizmasıyla saklanmalı.
- Parola hiçbir API yanıtında dönmemeli.
- Parola değiştirme işleminde mevcut parola doğrulanmalı.
- Parola sıfırlama işlemi güvenli, süreli ve tek kullanımlık token kullanmalı.
- Refresh token yenileme ve iptal etme akışları güvenli olmalı.
- Kullanıcı kendi hesabının durumunu görebilmeli.
- Hesabı dondurulan kullanıcı giriş yapamamalı veya mevcut tokenları politika doğrultusunda geçersizleştirilmeli.
- Kimlik doğrulaması ile kullanıcının aktif olma durumu birbirinden ayrılmalı.
- Kullanıcı adı, e-posta ve profil bilgileri için uygun unique indeksler ve doğrulamalar bulunmalı.
- Hassas bilgileri loglara yazma.

Mevcut sistemde `User`, `UserProfile`, `RefreshToken` veya benzeri entity'ler varsa yeni alternatiflerini oluşturmadan önce bunları incele.

---

## 5. Profil sistemi

Bir kullanıcı profilinde en az şu bilgiler desteklenmeli:

- Ad
- Kullanıcı adı (nickname)
- Biyografi
- Yaş veya doğum tarihi için uygun gizlilik modeli
- Şehir ve isteğe bağlı konum bilgisi
- Birden fazla profil fotoğrafı
- Ana profil fotoğrafı seçimi
- İlgi alanları
- Müzik tercihleri
- Güncel ruh hâli
- Profil görünürlüğü
- Mesaj kabul tercihleri
- Hesap ve moderasyon durumu
- Oluşturulma ve güncellenme tarihleri

Önerilen API'ler:

- `GET /api/profiles/me`
- `PUT /api/profiles/me`
- `GET /api/profiles/{userId}`
- `POST /api/profiles/me/photos`
- `PUT /api/profiles/me/photos/{photoId}/primary`
- `DELETE /api/profiles/me/photos/{photoId}`
- `PUT /api/profiles/me/visibility`
- `PUT /api/profiles/me/preferences`

Fotoğraf kuralları:

- Kullanıcı birden fazla fotoğraf ekleyebilmeli.
- Fotoğrafların sırası ve ana fotoğraf seçimi saklanmalı.
- Yalnızca dosya uzantısına güvenilmemeli; gerçek dosya içeriği doğrulanmalı.
- Dosya boyutu, çözünürlük, format ve yükleme sıklığı sınırlandırılmalı.
- Dosyalar PostgreSQL'e binary olarak yazılmamalı.
- Medya erişimi yetki kontrolünden geçmeli.
- Kullanıcının profil fotoğrafını silmesi, mesaj olarak gönderdiği aynı dosyanın veya hikâye medyasının yanlışlıkla silinmesine neden olmamalı. Paylaşılan medya dosyalarının sahipliği ve referansları doğru yönetilmeli.

Profil fotoğrafları ile mesajlarda gönderilen fotoğrafları aynı veri yaşam döngüsüne sahipmiş gibi tasarlama.

---

## 6. Profil görünürlüğü ve mesajlaşma izinleri

Kullanıcının profili herkese açık veya sınırlı görünürlükte olabilmeli.

Görünürlük tek başına mesaj gönderme yetkisi anlamına gelmemeli. Profil görünürlüğü, mesaj istekleri ve engelleme kuralları birbirinden bağımsız kontrol edilmeli.

Önerilen alanlar:

- `ProfileVisibility`
- `AllowMessageRequests`
- `AllowCallsFromNonMatches`
- Gerekirse mesaj isteklerini kimlerin gönderebileceğini belirleyen bir tercih

İş kuralları:

1. Profili açık olan kullanıcılar keşfedilebilir olmalı.
2. Profil açık olsa bile kullanıcı mesaj isteklerini kapatabilmeli.
3. Açık profilli kullanıcıya eşleşme olmadan mesaj gönderilebilmeli; ancak mesaj isteği kuralları, rate limit ve engelleme kontrolleri uygulanmalı.
4. İlk mesajı alan kullanıcının onayı gerekip gerekmediği açık bir iş kuralı olmalı. Güvenli varsayılan olarak eşleşme dışındaki ilk mesajı bir mesaj isteği şeklinde modelle.
5. Kullanıcı mesaj isteğini kabul edebilir, reddedebilir veya göndereni engelleyebilir.
6. Eşleşme dışındaki mesajlar ile karşılıklı eşleşme sonucunda açılan sohbetler aynı `Conversation` modeli üzerinden çalışabilir; ancak kaynakları ve izinleri ayırt edilmelidir.
7. Profilin kapalı olması, mevcut bir eşleşme sohbetini kendiliğinden silmemeli.
8. Engellenen kullanıcı, profil açık olsa bile engelleyen kişiye mesaj gönderememeli veya arama başlatamamalı.

Gerekirse `Conversation` üzerinde bir sohbet kaynağı veya türü tutulabilir. Mevcut modeli incelemeden alanları körlemesine ekleme.

---

## 7. Ruh hâli, müzik ve keşif

MindMatch'in ayırt edici özelliği yalnızca fotoğraf üzerinden eşleşme olmamalı.

Keşif algoritması şu kriterleri destekleyebilmeli:

- Ortak ilgi alanları
- Ortak müzik tercihleri
- Ruh hâli uyumu
- Kullanıcının seçtiği yaş veya diğer filtreler
- Profil görünürlüğü
- Engelleme ve rapor nedeniyle gizlenmesi gereken hesaplar
- Kullanıcının daha önce reddettiği veya görmüş olduğu profiller
- Premium kullanıcıların izin verilen keşif avantajları

Mevcut `MoodCheckIn`, `Interest`, `UserInterest` ve benzeri entity'leri incele.

Ruh hâli geçmişi tutulacaksa veri saklama ve gizlilik politikasını dikkate al. Keşif algoritmasını açıklanabilir ve test edilebilir bir servis olarak oluştur.

Rastgele öneriler üretirken aynı kişiyi art arda göstermemeye çalış. Ancak eşleşme puanlarını veya uyumluluk oranlarını gerçekte hesaplanmıyorsa uydurma.

---

## 8. Eşleşme ve karşılıklı onay akışı

Mevcut `Match` entity'sini, `MatchStatus` enum'unu ve eşleşme servislerini incele.

Temel senaryo:

1. Kullanıcı A, B profilini keşfeder.
2. A, B ile tanışmak için istek veya eşleşme başlatır.
3. B'ye uygun bir bildirim gönderilir.
4. B, A'yı kabul veya reddeder.
5. B reddederse eşleşme reddedilmiş olarak kalır; sohbet açılmaz.
6. B kabul ederse eşleşme onaylanır.
7. Ürün tasarımında iki tarafın da açık onayı gerekiyorsa karşılıklı onay tamamlanmadan `Conversation` oluşturulmaz.
8. Gerekli onaylar tamamlanınca tek bir aktif sohbet oluşturulur veya mevcut sohbet kontrollü biçimde yeniden kullanılır.
9. Aynı iki kişi için eşzamanlı istekler birden fazla aktif eşleşme veya sohbet oluşturmamalı.

Önemli bir ayrıntı: Kullanıcıların profil fotoğrafları, isimleri veya onayları ekrana düşerken **karşı tarafın gerçek adı ve fotoğrafları yalnızca o kişinin izin verdiği ve ürünün görünürlük kurallarının sağlandığı durumda gösterilmelidir.**

Kullanıcı hesabını dondurduğunda keşif sonuçlarında yer almamalı ve yeni eşleşme alamamalı.

Önerilen API'ler:

- `POST /api/matches`
- `GET /api/matches`
- `GET /api/matches/requests`
- `POST /api/matches/{matchId}/accept`
- `POST /api/matches/{matchId}/reject`
- `POST /api/matches/{matchId}/end`

Mevcut endpoint'ler farklıysa yeni endpoint açmak yerine tutarlı bir sözleşmeye geçiş planı hazırla.

---

## 9. Conversation ve sohbet yönetimi

Sohbet sistemi yalnızca mesaj tablosundan oluşmamalı.

Sohbet özellikleri:

- Eşleşme sonucunda oluşan sohbet
- Açık profil üzerinden başlayan mesaj isteği ve sohbet
- Sohbet geçmişi
- Son mesaj bilgisi
- Son mesaj zamanı
- Sohbeti kendi listesinden gizleme
- Sohbeti yeniden gösterme
- Sohbeti arşivleme gibi bir özellik gerekiyorsa silmeden ayrı tutma
- Kullanıcı bazında sohbet ayarları
- Engelleme veya eşleşme sonlandırma sonrasında erişim kuralları
- Mesaj gönderme ve okuma yetkisi
- Sohbetin kaynağı ve durumu

Önerilen API'ler:

- `GET /api/conversations`
- `GET /api/conversations/{conversationId}`
- `GET /api/conversations/{conversationId}/messages`
- `DELETE /api/conversations/{conversationId}` — yalnızca ilgili kullanıcının sohbet listesinden gizleme anlamına gelmeli.
- Gerekirse `POST /api/conversations/{conversationId}/restore`

Bir sohbeti kullanıcının listesinden gizlemek, diğer kullanıcının sohbetini veya mesaj kayıtlarını silmemeli.

Kullanıcı başka bir kullanıcıyı engellediğinde veya eşleşmeyi bitirdiğinde geçmişin nasıl gösterileceğini ve yeni mesajlaşmanın nasıl engelleneceğini açıkça tasarla.

---

## 10. Mesaj sistemi

Tüm gönderilen içerikler tek bir mesaj sisteminde temsil edilmeli.

Desteklenen mesaj türleri:

- Text
- Image
- Voice
- Video
- File — yalnızca bu özellik ürün kapsamına alınırsa

Mevcut `Message` entity'sinde `ConversationId`, `SenderId`, `Content`, `Type`, `Status`, `MediaUrl`, `ReadAtUtc` gibi alanlar varsa bunları koruyarak geliştir.

Mesaj DTO'ları mesaj tipine uygun doğrulama yapmalı:

- Metin mesajında içerik zorunlu olmalı.
- Fotoğraf mesajında geçerli medya referansı bulunmalı.
- Ses mesajında geçerli ses medya referansı bulunmalı.
- Metin ve medya kombinasyonları açıkça desteklenmeli.
- Kullanıcı başka bir sohbetin medya referansını izinsiz kullanamamalı.
- Mesajın göndericisi istemcinin verdiği `SenderId` alanından alınmamalı; authenticated user claim üzerinden belirlenmeli.

Önerilen API'ler:

- `POST /api/messages`
- `GET /api/conversations/{conversationId}/messages`
- `DELETE /api/messages/{messageId}/me`
- `DELETE /api/messages/{messageId}/everyone`

Mesaj geçmişi sayfalama desteklemeli. Tüm mesajları tek sorguda döndürme. Cursor veya uygun pagination kullan.

Mesajlar oluşturulma zamanına göre tutarlı sıralanmalı. Zamanlar UTC tutulmalı.

### Mesaj durumları

Gönderildi, teslim edildi ve görüldü durumları desteklenmeli.

Ancak görüldü bilgisi ve son görülme ayarlarını ilk mesajlaşma sürümünde zorunlu hale getirme. Bunları sonraki bir modül olarak eklenebilir şekilde tasarla.

Kullanıcı okundu bilgilerini kapattığında bunun tam olarak ne anlama geldiğini tanımla. Örneğin karşı tarafın okundu bilgisi görmesi engellenirken mesajın sunucu tarafından işlenmesi veya teslim edilmesi ayrı kavramlar olarak kalmalı.

---

## 11. Mesaj silme ve süreli mesajlar

Bu modül kritik öneme sahip.

### A. Benden sil

Kullanıcı bir mesajı yalnızca kendi ekranından kaldırabilmeli.

Örnek:

- A, B'ye bir mesaj gönderir.
- A mesajı kendisinden siler.
- A mesajı kendi sohbetinde göremez.
- B mesajı görmeye devam eder.
- Mesajın B tarafındaki içeriği veya medya dosyası silinmez.

Bunun için `MessageDeletion` gibi kullanıcıya özel silme kayıtları oluşturulabilir. Aynı mesaj ve kullanıcı için tek kayıt bulunmalı.

### B. Herkesten sil

Gönderen, mesajı herkesten silme yetkisine sahip olmalı. Zaman sınırı isteniyorsa bu ayarlanabilir olmalı; sınır belirlenmeden keyfî bir süre kodlama.

Herkesten silme durumunda:

- Mesaj içeriği normal kullanıcı sorgularından kaldırılmalı.
- Mesajın silindiği bilgisi gerektiğinde yer tutucu olarak gösterilebilmeli.
- Silme işlemi diğer katılımcıya SignalR üzerinden iletilmeli.
- Mesajın fotoğraf veya ses dosyası olması durumunda dosyanın yaşam döngüsü ayrıca yönetilmeli.
- Aynı dosya başka bir mesajda veya profil fotoğrafında da kullanılıyorsa dosyayı gelişigüzel silme.
- Yetkisiz kullanıcı başka birinin mesajını herkesten silememeli.

### C. Premium olmayan kullanıcılar için 24 saat

Premium olmayan kullanıcıların sohbet mesaj içerikleri, tanımlanmış süre dolduğunda silinmeli.

Varsayılan ürün kuralı:

- Standart kullanıcılar: mesaj içeriği için 24 saat.
- Premium kullanıcılar: mesaj içeriği için 7 gün.

Ancak şu ayrıntıları doğru modelle:

1. Süre mesajın oluşturulma zamanından mı, teslim zamanından mı, yoksa okunma zamanından mı başlar? Önerilen varsayılan: `CreatedAtUtc` üzerinden başlat.
2. Silme süresi göndericinin mi, alıcının mı, yoksa iki tarafın üyelik durumuna mı bağlı? Bu kararı kodlamadan önce açıkça belirt. Önerilen ürün politikası: sohbetin süre politikasını belirle ve her iki tarafta tutarlı uygula.
3. Premium üyelik bittiğinde mevcut mesajların süresi değişecek mi? Önerilen yaklaşım: süre politikası mesaj oluşturulurken belirlenir ve saklanır; böylece üyelik değişikliği geçmişin ömrünü geriye dönük değiştirmez.
4. Süre dolduğunda yalnızca içerik mi silinecek, medya da mı temizlenecek? Önerilen yaklaşım: içerik ve bağlı medya güvenli biçimde temizlenir; gerektiği kadar sınırlı audit kaydı korunur.
5. Kullanıcının kendi ekranından sildiği mesaj, otomatik süre dolumundan farklı bir durum olmalı.
6. Moderasyon için gerekli kayıtların ne kadar tutulacağı ayrıca belirlenmeli; otomatik içerik silme ile yasal veya güvenlik amaçlı saklama politikaları çelişmemeli.

Uygun bir `ExpiresAtUtc` alanı ve arka plan temizleme servisi tasarla. Her API isteğinde bütün mesajları tarayarak silme yapma. Batch işlemler, indeksler, tekrar çalıştırılabilirlik ve hata sonrası devam etme düşünülmeli.

**Önemli:** İçeriği silmek ile dosyanın fiziksel olarak silinmesi farklı işlemlerdir. Güvenli ve tekrar çalıştırılabilir bir cleanup süreci tasarla.

---

## 12. Fotoğraf ve ses dosyası yükleme

Mevcut `IMediaStorageService` ve `LocalMediaStorageService` dosyalarını incele.

Ses ve fotoğraf için ortak bir medya altyapısı kullan. İki farklı servis yazmak gerekiyorsa bile depolama sözleşmelerini tutarlı tut.

Önerilen endpoint'ler:

- `POST /api/media/images`
- `POST /api/media/audio`
- `DELETE /api/media/{mediaId}` — yalnızca yetkili ve uygun sahiplik/bağlantı koşullarında.

Kurallar:

- Görseller ve ses kayıtları boyut sınırına sahip olmalı.
- Uzantı ve `Content-Type` tek başına güvenilir doğrulama sayılmamalı.
- Gerçek dosya imzası ve izin verilen formatlar doğrulanmalı.
- Rastgele sunucu dosya adı oluşturulmalı.
- Kullanıcı dosya sistemindeki gerçek yolu belirleyememeli.
- Dosyalar çalıştırılabilir içerik olarak sunulmamalı.
- Hassas medya herkese açık, tahmin edilebilir URL'lerle yayınlanmamalı.
- Medya erişimi kullanıcı, sohbet ve mesaj yetkileriyle denetlenmeli.
- Dosya yüklemelerinde rate limit uygulanmalı.
- Yarım kalmış yüklemeler temizlenmeli.
- Medya silme işlemleri idempotent olmalı.
- Daha sonra yerel depolamadan S3 uyumlu depolamaya geçilebilmeli.

Dosyalar yalnızca yüklenince mesaj oluşturulmuş sayılmamalı. Yükleme ile mesaj gönderimi arasındaki ilişkiyi ve sahipsiz dosyaların temizlenmesini tasarla.

---

## 13. Hikâyeler (Stories)

Kullanıcılar fotoğraf veya video hikâyesi paylaşabilmeli.

Özellikler:

- Fotoğraf hikâyesi
- Video hikâyesi
- İsteğe bağlı açıklama
- 24 saat sonra sona erme
- Hikâyeyi silme
- Hikâyeyi görüntüleyenleri kaydetme
- Aynı kişinin aynı hikâyeyi tekrar görüntülemesini tekil görüntülenme veya görüntülenme sayacı olarak tasarlama
- Kullanıcının kendi hikâyelerinin görüntüleyenlerini görmesi
- Engellenen kişilerin hikâye erişimlerinin kontrolü
- Profil görünürlüğüne göre hikâye keşfi
- Süresi dolmuş hikâyelerin normal akıştan çıkarılması
- Medya dosyalarının süre dolduğunda güvenli temizlenmesi

Önerilen API'ler:

- `POST /api/stories`
- `GET /api/stories/feed`
- `GET /api/stories/me`
- `GET /api/stories/{storyId}`
- `POST /api/stories/{storyId}/views`
- `GET /api/stories/{storyId}/viewers`
- `DELETE /api/stories/{storyId}`

Story feed:

- Kullanıcının erişebildiği hesapları göstermeli.
- Engellenen veya dondurulan hesapları göstermemeli.
- Süresi dolmuş ve silinmiş hikâyeleri göstermemeli.
- Gerektiğinde okunmamış hikâyeleri önceliklendirebilmeli.
- Hikâye içeriğini mesaj tablosuna yazmamalı; bağımsız bir `Story` entity'si kullanmalı.

Mevcut `Story` ve `StoryView` entity'lerini incele ve mümkünse koruyarak geliştir.

---

## 14. Premium sistemi ve keşif önceliği

Premium, kullanıcıya belirli ek avantajlar sunmalı. Ancak engelleme, rapor veya güvenlik kurallarını aşmamalı.

Premium özellik önerileri:

- Keşif ekranında daha fazla görünürlük
- Premium kullanıcıların, tercihleri açıksa belirli keşif alanlarında öne çıkarılması
- Gelişmiş filtreler
- Mesajların daha uzun süre saklanması
- Profil analitiği gibi uygun özellikler
- Premium etiketi veya görünürlük tercihi

Kullanıcıların premium profillerinin uygulamadaki sohbet/keşif ekranının üst kısmında görünmesi isteniyor. Bu, sınırsız veya sürekli aynı kullanıcıların gösterilmesi şeklinde tasarlanmamalı.

Önerilen kurallar:

- Premium kullanıcının öne çıkma tercihi açık olmalı.
- Profil görünürlüğü ve hesap durumu uygun olmalı.
- Engellenen kişiler veya moderasyon nedeniyle kısıtlanan hesaplar öne çıkarılmamalı.
- Görünürlük sıralaması adil, sınırlandırılmış ve test edilebilir olmalı.
- Premium satın alma veya üyelik kontrolü backend tarafında yapılmalı.
- İstemcinin gönderdiği `IsPremium = true` gibi alanlara güvenilmemeli.
- Premium avantajları merkezi bir entitlement/feature kontrolünden yönetilmeli.

Premium modeli mevcut değilse önce yalnızca bu ihtiyaca yetecek bir üyelik modeli oluştur. Ödeme sağlayıcısı entegrasyonunu ayrıca planla; ödeme doğrulamasını taklit ederek premium verme.

---

## 15. Sesli ve görüntülü aramalar

Gerçek zamanlı görüşme için SignalR ve WebRTC görevlerini ayır.

**SignalR:**
- Arama daveti
- Gelen arama bildirimi
- Aramayı kabul etme
- Aramayı reddetme
- Aramayı bitirme
- WebRTC offer/answer ve ICE candidate sinyalleşmesi
- Bağlantı durumlarının bildirimi

**WebRTC:**
- Gerçek ses ve görüntü aktarımı

SignalR mesajlarının kendisi gerçek ses veya video aktarımı yerine kullanılmamalı.

Önerilen API'ler:

- `POST /api/calls`
- `POST /api/calls/{callId}/accept`
- `POST /api/calls/{callId}/reject`
- `POST /api/calls/{callId}/end`
- `GET /api/conversations/{conversationId}/calls`

Gerekli durumlar:

- Ringing
- Answered
- Rejected
- Missed
- Ended

Kurallar:

- Yalnızca ilgili sohbetin katılımcıları arama başlatabilmeli veya yanıtlayabilmeli.
- Engelleme ve mesajlaşma izinleri arama kurallarında da kontrol edilmeli.
- Bir görüşmenin aynı anda birden fazla kez kabul edilmesi engellenmeli.
- Cevapsız arama zaman aşımı kontrollü çalışmalı.
- Süresi dolmuş veya bitmiş arama tekrar kabul edilememeli.
- WebRTC için gerektiğinde STUN/TURN sunucuları kullanılmalı.
- TURN erişim bilgileri kısa ömürlü ve güvenli olmalı.
- SignalR gruplarına kullanıcı istemcisinin keyfî şekilde katılmasına izin verilmemeli.

Mevcut `Call` entity'sini incele. Gerçek görüşme aktarımının ayrıca istemci ve altyapı gerektirdiğini dokümante et; yalnızca API oluşturmayı çalışan görüntülü görüşme olarak tanımlama.

---

## 16. Görüldü, son görülme ve çevrimiçi durumu

Bu modül geliştirilebilir olmalı ancak temel mesajlaşma özelliklerinden ayrı aşamada ele alınmalı.

Ayarlar:

- Okundu bilgisi açık/kapalı
- Son görülme açık/kapalı
- Çevrimiçi görünürlüğü açık/kapalı

API önerileri:

- `GET /api/privacy`
- `PUT /api/privacy`

Kurallar:

- Üç ayar birbirinden bağımsız olmalı.
- Kullanıcı yalnızca kendi gizlilik ayarlarını değiştirebilmeli.
- Son görülme zamanı UTC saklanmalı.
- Çevrimiçi durumu mümkün olduğunca bağlantı/presence sistemiyle belirlenmeli; kalıcı `IsOnline` bayrağı tek başına güvenilir kabul edilmemeli.
- Okundu bilgisi kapalıysa karşı tarafa görüldü bilgisi gönderilmemeli.
- Gizlilik tercihi, mesaj teslim edilmesini engellememeli.
- Bağlantı kesilmesi, uygulamanın kapanması ve yeniden bağlanma senaryoları ele alınmalı.

Bu modülün temel mesaj gönderme akışını geciktirmesine izin verme.

---

## 17. Engelleme ve kullanıcı şikâyet sistemi

Bu özellik ürünün güvenliği için zorunludur.

Kullanıcı bir kişiden rahatsız olduğunda onu raporlayabilmeli ve gerektiğinde engelleyebilmeli.

Önerilen API'ler:

- `POST /api/reports`
- `GET /api/reports/me`
- `POST /api/users/{userId}/block`
- `DELETE /api/users/{userId}/block`
- `GET /api/users/blocked`

Rapor nedenleri örnekleri:

- Taciz
- Hakaret
- İstenmeyen cinsel içerik
- Spam
- Sahte profil
- Tehdit
- Uygunsuz görsel veya ses
- Diğer

Bir raporda şu bilgiler bulunabilir:

- Raporlayan kullanıcı
- Raporlanan kullanıcı
- Rapor nedeni
- Açıklama
- İlişkili sohbet
- İlgili mesaj kimlikleri
- Oluşturulma zamanı
- İnceleme durumu
- Atanmış moderatör
- Moderasyon sonucu
- İşlem zamanı

Rapor gönderilirken kullanıcıdan ilgili mesajları seçebilmesi istenebilir. Bütün sohbet geçmişini varsayılan olarak rapora kopyalama.

Raporlanan mesajın sonradan silinmesi, rapor incelemesini anlamsız hale getirmemeli. Bu yüzden raporla ilişkili içerik için sınırlı ve erişimi kontrollü bir moderasyon snapshot'ı veya referans stratejisi tasarla. Bu strateji gizlilik ve veri saklama politikasına uygun olmalı.

Engelleme kuralları:

- Engellenen kullanıcı yeni mesaj gönderememeli.
- Arama başlatamamalı.
- Keşif ve hikâye erişimi engelleme politikasına göre kısıtlanmalı.
- Engelleme, her iki tarafın eski mesajlarını otomatik silmemeli.
- Engellenen kullanıcıya engelleyen kişinin özel bilgileri sızdırılmamalı.
- Engelleme ve rapor işlemleri tekrarlanabilir ve idempotent olmalı.

---

## 18. AdminUser ve moderasyon API'leri

Normal kullanıcı ile moderatör/yönetici yetkileri kesin biçimde ayrılmalı.

Mevcut `User` rol sistemini incele. Gerekirse mevcut `Admin` rolünü moderasyon yetkileriyle genişlet veya `AdminUser` için ayrı bir yetkilendirme politikası tanımla. Kullanıcı tarafından istenen “AdminUser” kavramını otomatik olarak bütün sistem yetkilerine sahip yeni bir kullanıcı tablosu şeklinde yorumlama.

Önerilen API'ler:

- `GET /api/admin/reports`
- `GET /api/admin/reports/{reportId}`
- `GET /api/admin/reports/{reportId}/messages`
- `POST /api/admin/reports/{reportId}/assign`
- `POST /api/admin/reports/{reportId}/resolve`
- `POST /api/admin/users/{userId}/freeze`
- `POST /api/admin/users/{userId}/activate`
- `GET /api/admin/users/{userId}/moderation-history`
- Gerekirse `GET /api/admin/users/{userId}/conversations`

Bütün endpoint'ler açıkça admin/moderatör politikasıyla korunmalı.

### Moderasyon iş akışı

1. Kullanıcı A, B'yi raporlar.
2. Rapor veritabanına kaydedilir.
3. Yetkili moderatör açık raporlar listesinde bu kaydı görür.
4. Moderatör rapora tıklar.
5. Raporun nedeni, ilişkili sohbet ve rapora eklenen ilgili mesajlar görüntülenir.
6. Moderatör gerektiğinde ilgili mesaj bağlamını inceler.
7. İhlal doğrulanırsa uyarı, geçici hesap dondurma veya başka uygun yaptırım uygulanabilir.
8. İşlem nedeni, işlemi yapan admin, tarih ve sonuç audit kaydına yazılır.

AdminUser arayüzünü geliştirmek bu görev kapsamında değil; ancak frontend'in bağlanabileceği API sözleşmeleri sağlanmalı.

### Moderasyon güvenliği

- Normal kullanıcılar admin endpoint'lerine erişememeli.
- Rol kontrolü yalnızca frontend'de yapılmamalı.
- Admin işlemleri audit log'a kaydedilmeli.
- Bir adminin hangi raporu ve hangi mesajları görüntülediği gerektiğinde denetlenebilir olmalı.
- Sohbet içeriği yalnızca ilgili raporun değerlendirilmesi için gereken kapsamda açılmalı.
- Moderatör yetkileri mümkünse en az ayrıcalık ilkesine göre sınırlandırılmalı.
- Raporlanan kullanıcıya şikâyetçinin kimliği gereksiz yere açıklanmamalı.
- Hesap dondurma işlemi aktif tokenları, giriş yapmayı, yeni mesaj göndermeyi, yeni eşleşmeleri ve aramaları kapsamalı.
- Dondurma ve aktifleştirme işlemleri neden, süre ve uygulayan yönetici bilgisiyle kaydedilmeli.
- Yönetici kendi hesabını veya yetki sınırları dışındaki kullanıcıları keyfî olarak yönetememeli.

---

## 19. Hesap dondurma ve aktifleştirme

Kullanıcı kendi hesabını geçici olarak dondurabilmeli ve daha sonra yeniden aktifleştirebilmeli.

Önerilen API'ler:

- `POST /api/account/freeze`
- `POST /api/account/activate`
- `GET /api/account/status`

Kendi hesabını donduran kullanıcı için:

- Keşif ekranında görünmemeli.
- Yeni eşleşme alamamalı.
- Yeni mesaj ve arama başlatamamalı.
- Hikâyeleri normal akışta görünmemeli.
- Var olan verileri yalnızca dondurma sebebiyle silinmemeli.
- Yeniden aktifleştirdiğinde kurallara uygun şekilde tekrar görünür olabilmeli.

Dondurma ile kalıcı hesap silme birbirinden farklı işlemler olmalı.

Admin tarafından dondurulmuş hesap ile kullanıcının kendi isteğiyle dondurduğu hesap farklı durumlar olarak modellenmeli. Kullanıcı kendi hesabını aktifleştirerek admin yaptırımını aşamamalı.

---

## 20. Bildirim sistemi

Aşağıdaki olaylar için uygulama içi bildirim altyapısı tasarla:

- Yeni eşleşme isteği
- Eşleşme kabul edildi
- Yeni mesaj isteği
- Mesaj isteği kabul edildi
- Yeni mesaj
- Gelen arama
- Yeni hikâye veya ilgili hikâye etkinliği gerekiyorsa
- Rapor sonucuyla ilgili kullanıcıya gösterilmesi uygun olan durumlar

Bildirimleri `Notification` gibi ayrı bir entity ile yönet.

Önerilen API'ler:

- `GET /api/notifications`
- `POST /api/notifications/{notificationId}/read`
- `POST /api/notifications/read-all`

Bildirimlerde kullanıcıya ait olmayan kayıtlar gösterilmemeli. SignalR bildirim iletiminde de kullanıcı kimliği sunucu tarafından doğrulanmalı.

---

## 21. SignalR gerçek zamanlı olaylar

Anlık mesajlaşma ve aramalar için uygun hub yapısını tasarla.

Örnek event'ler:

- `MessageCreated`
- `MessageDeleted`
- `MessageStatusChanged`
- `ConversationUpdated`
- `MatchCreated`
- `MatchAccepted`
- `MessageRequestReceived`
- `CallIncoming`
- `CallAccepted`
- `CallRejected`
- `CallEnded`
- `NotificationReceived`

SignalR hub'ları:

- Kullanıcı kimliğini token claim'lerinden almalı.
- İstemcinin başka bir kullanıcı adına mesaj göndermesine izin vermemeli.
- İstemcinin keyfî sohbet grubuna katılmasını engellemeli.
- Mesaj gönderme yetkisini application servisleriyle tutarlı kontrol etmeli.
- Yeniden bağlanma ve bağlantı kaybı senaryolarını desteklemeli.
- Bir mesajın veritabanına kaydedilmesi ile gerçek zamanlı yayınlanması arasındaki tutarsızlığı dikkate almalı.

Önemli olaylarda transaction sonrası event yayınlama, outbox gibi bir desen veya uygun bir güvenilir yayınlama mekanizması değerlendirilmelidir. Her özellik için gereksiz karmaşıklık ekleme; gereksinime uygun çözüm seç.

---

## 22. API hata yönetimi ve güvenlik

Tüm API'lerde tutarlı hata yanıtları kullan.

Uygun HTTP durumları:

- `200 OK`
- `201 Created`
- `204 No Content`
- `400 Bad Request`
- `401 Unauthorized`
- `403 Forbidden`
- `404 Not Found`
- `409 Conflict`
- `422 Unprocessable Entity` — projede bu yaklaşım seçilirse
- `429 Too Many Requests`
- `500 Internal Server Error`

İş kurallarını controller'lara yığma. Controller yalnızca HTTP girdisini almalı, authenticated kullanıcıyı belirlemeli, application servislerini çağırmalı ve sonucu uygun HTTP yanıtına çevirmeli.

Güvenlik gereksinimleri:

- IDOR kontrolleri
- Authentication ve authorization
- DTO validation
- Rate limiting
- Mesaj spam koruması
- Dosya yükleme güvenliği
- Güvenli hata mesajları
- Hassas verilerin loglardan çıkarılması
- CORS ve HTTPS ayarlarının kontrolü
- JWT ve refresh token güvenliği
- Moderasyon audit kayıtları
- PostgreSQL unique constraint ve foreign key kuralları
- Eşzamanlı isteklerde race condition koruması

Bir kullanıcının başka birinin ID'sini bilmesi, o kullanıcının sohbetine, fotoğrafına, raporuna veya mesajına erişebilmesi anlamına gelmemeli.

---

## 23. Veritabanı tasarımı

Mevcut entity'leri koruyarak aşağıdaki kavramların eksik olup olmadığını incele:

- User
- UserProfile
- ProfilePhoto
- Interest
- UserInterest
- MoodCheckIn
- Match
- Conversation
- Message
- MessageDeletion
- Call
- Story
- StoryView
- UserPrivacySettings
- UserBlock
- MessageRequest
- Report
- ReportedMessage veya eşdeğer rapor-mesaj ilişkisi
- ModerationAction
- Notification
- MediaAsset veya eşdeğer medya metadata modeli
- Premium entitlement/subscription modeli
- İhtiyaç varsa UserPresence

Bunların hepsini körlemesine oluşturma. Mevcut entity'ler ve gereksinimler üzerinden gerekli modeli seç.

Özellikle şu ilişkileri doğrula:

- Bir kullanıcının birden fazla profil fotoğrafı olabilir.
- Bir konuşmanın iki katılımcısı vardır.
- Bir konuşmada çok sayıda mesaj bulunur.
- Bir mesajın tek göndericisi vardır.
- Bir mesaj, kendi ekranından silme kayıtlarına sahip olabilir.
- Bir mesaj herkesten silinmiş olabilir.
- Bir mesajın süresi dolabilir.
- Bir hikâyenin çok sayıda görüntüleme kaydı olabilir.
- Bir kullanıcı çok sayıda kişiyi engelleyebilir.
- Bir rapor, raporlayan ve raporlanan kullanıcılarla ilişkilidir.
- Bir rapor bir veya birden fazla ilgili mesajla bağlantılı olabilir.
- Moderasyon işlemlerinin audit kayıtları olmalıdır.

Gerekli indeksleri sorgu desenlerine göre seç. Özellikle kullanıcı ID'si, sohbet ID'si, mesaj zamanı, rapor durumu, hikâye sona erme zamanı ve süreli mesaj temizliği için indeksleri değerlendir.

Delete behavior kararlarını bilinçli ver. Sohbet, mesaj, rapor ve audit kayıtları arasındaki foreign key davranışlarını rastgele `Cascade` yapma.

---

## 24. Arka plan görevleri

Aşağıdaki işlemler için uygun arka plan görevleri tasarla:

- Süresi dolmuş hikâyeleri normal akıştan kaldırma
- Süresi dolmuş mesaj içeriklerini temizleme
- Artık kullanılmayan medya dosyalarını temizleme
- Cevapsız aramaları `Missed` durumuna geçirme
- Gerekli geçici kayıtları temizleme
- Gerekirse premium üyelik sürelerini değerlendirme

Bu görevler:

- Uygulama yeniden başladığında devam edebilmeli.
- Aynı kayıt üzerinde tekrar çalıştırılabilir olmalı.
- Batch olarak çalışmalı.
- Veritabanını her seferinde tamamen taramamalı.
- Hataları loglamalı.
- Bir kayıttaki hata yüzünden tüm işi kalıcı olarak durdurmamalı.
- Birden fazla API instance'ı çalışırsa aynı işlemin çakışmasını önlemeli.

Gereksiz yere ayrı bir microservice oluşturma. Önce mevcut çözümün ölçeğine uygun bir `BackgroundService` veya benzeri yapı değerlendir.

---

## 25. Test stratejisi

Mevcut `MindMatch.Test` projesini incele ve var olan testleri koru.

En azından şu iş kurallarını test et:

### Authentication
- Geçersiz parola
- Refresh token yenileme
- İptal edilmiş token
- Dondurulmuş hesabın erişimi

### Profil
- Birden fazla fotoğraf
- Ana fotoğraf seçimi
- Yetkisiz profil güncelleme
- Gizli profilin keşifte görünmemesi

### Match
- İstek oluşturma
- Kabul
- Ret
- Ret durumunda sohbet oluşturulmaması
- Eşzamanlı isteklerde mükerrer sohbet oluşmaması

### Conversation
- Katılımcıların sohbeti görebilmesi
- Başka kullanıcının sohbetine erişememe
- Bir kullanıcı için gizleme işleminin diğer kullanıcıyı etkilememesi

### Message
- Metin mesajı gönderme
- Fotoğraf ve ses mesajı
- Mesajın yalnızca sohbet katılımcıları tarafından okunabilmesi
- Benden silme
- Herkesten silme
- Yetkisiz silme girişimleri
- Süre dolumu
- Premium ve standart saklama süreleri
- Premium üyeliğin sona ermesi senaryosu

### Stories
- Hikâye oluşturma
- 24 saatlik süre
- Yetkisiz hikâye erişimi
- Tekil görüntülenme
- Engellenen kullanıcının erişememesi

### Moderasyon
- Rapor oluşturma
- Raporun yalnızca yetkili admin tarafından incelenebilmesi
- Moderasyon aksiyonlarının audit kaydı
- Kullanıcının kendi hesabını dondurması
- Admin tarafından dondurulan hesabın kullanıcı tarafından yetkisiz aktifleştirilememesi

### Real-time
- Mesajın doğru sohbet grubuna gönderilmesi
- Yetkisiz hub işlemleri
- Silme olayının doğru katılımcılara iletilmesi

Gerçek veritabanı gerektiren testlerde uygun integration test altyapısı kullan. Yalnızca mock'larla gerçek EF Core ilişkilerinin doğru olduğunu iddia etme.

---

## 26. Geliştirme aşamaları

Bu kadar kapsamlı ürünü tek seferde kontrolsüz biçimde kodlama.

### Aşama 1 — Mevcut kodu analiz et
- Çözüm dosyalarını ve proje referanslarını incele.
- Entity, DTO, interface, repository, service, controller ve DbContext envanteri çıkar.
- Mevcut endpoint'leri ve authentication mekanizmasını kontrol et.
- Çakışan veya eksik modelleri belirle.
- Derleme durumunu doğrula.
- Migration oluşturma.

### Aşama 2 — Domain ve veri modelini tamamla
- Profil fotoğrafları
- Açık profil ve mesaj istekleri
- Eşleşme onay akışı
- Mesaj silme
- Süreli mesajlar
- Medya metadata
- Hikâyeler
- Engelleme ve rapor
- Moderasyon
- Bildirimler
- Premium entitlement

İhtiyaç duyulmayan entity'leri ekleme. İlişkileri, unique indeksleri ve durum geçişlerini doğrula.

### Aşama 3 — Repository ve application servisleri
Her modülün iş kurallarını servislerde uygula. Yetki kontrollerini yalnızca controller'a bırakma.

### Aşama 4 — REST API
DTO validation, doğru HTTP durumları, authorization, pagination ve tutarlı hata yanıtlarını uygula.

### Aşama 5 — Medya
Fotoğraf ve ses yükleme, erişim kontrolü, mesajlara bağlama, silme ve temizlik akışlarını tamamla.

### Aşama 6 — SignalR
Mesajlaşma, bildirimler, eşleşme olayları ve arama sinyalleşmesini ekle.

### Aşama 7 — Süreli mesajlar ve hikâyeler
Süre politikalarını, background cleanup ve medya temizliğini uygula.

### Aşama 8 — Moderasyon ve premium
Admin API'leri, rapor inceleme, hesap dondurma, audit log ve premium avantajlarını uygula.

### Aşama 9 — Gizlilik
Görüldü, son görülme ve çevrimiçi durum ayarlarını tamamla.

### Aşama 10 — Test ve migration hazırlığı
- `dotnet build`
- Uygun testler
- EF Core model doğrulaması
- İlişki ve indeks incelemesi
- Geriye dönük uyumluluk kontrolü
- Migration planı

Migration oluşturma veya uygulama öncesinde kullanıcıdan açık onay al.

---

## 27. Kod üretirken uyulacak çıktı formatı

Her geliştirme adımında şu sırayı izle:

1. **Mevcut durum:** İlgili dosyalarda ne olduğunu kısaca belirt.
2. **Karar:** Neden bu tasarımı seçtiğini ve iş kuralını açıkla.
3. **Dosya listesi:** Oluşturulacak ve güncellenecek dosyaların tam yollarını yaz.
4. **Kod:** Her dosya için eksiksiz, derlenebilir kod ver.
5. **Bağlantılar:** DI, DbContext, namespace ve proje referanslarında gereken değişiklikleri göster.
6. **API sözleşmesi:** HTTP metodu, endpoint, request ve response örneği ver.
7. **Güvenlik:** Yetkilendirme ve kritik kontrolleri belirt.
8. **Doğrulama:** Build ve ilgili test komutlarını ver.
9. **Sonraki adım:** Bir sonraki modülü belirt.

Kod yazmadan önce dosyanın mevcut içeriğini incele. Bir dosya zaten varsa komple üzerine yazmanın gerekli olup olmadığını değerlendir; küçük bir değişiklik yeterliyse yalnızca eklenecek veya değiştirilecek bölümleri açıkça belirt.

Derleme hatası oluşturabilecek eksik interface veya DTO bırakma. Namespace ve dependency injection kayıtlarını unutma.

Kullanıcının henüz onaylamadığı migration, veri silme, veritabanı sıfırlama veya üretim ortamını değiştirme işlemlerini gerçekleştirme.

---

## 28. İlk yapacağın işlem

Şimdi hemen bütün projeyi baştan yazmaya çalışma.

Önce `C:\Users\Acer\source\MindMatch` çözümünü ve mevcut kod tabanını analiz et.

Özellikle şu dosyaları kontrol et:

- `MindMatch.Domain/Entities/User.cs` veya projedeki gerçek kullanıcı entity'si
- `UserProfile.cs`
- `Match.cs`
- `Conversation.cs`
- `Message.cs`
- `Call.cs`
- `Story.cs`
- `StoryView.cs`
- `MindMatchDbContext.cs`
- `IUnitOfWork.cs`
- Match ve Conversation repository/service dosyaları
- `MessagesController.cs`
- `Program.cs`
- Mevcut authentication ve refresh token dosyaları
- Test projesindeki mevcut testler

Sonra bana şu çıktıları ver:

1. Mevcut mimarinin özeti.
2. Tamamlanmış, eksik ve hatalı özelliklerin listesi.
3. Mevcut entity'ler ile ihtiyaç duyulan modeller arasındaki farklar.
4. Çakışan veya eksik interface, DTO, repository, service ve controller'lar.
5. Önerdiğin nihai domain modeli ve ilişkiler.
6. Riskli kararlar ve netleştirilmesi gereken iş kuralları.
7. Geliştirme sırası ve ilk aşamada değiştirilecek dosyalar.

Analizden sonra ilk aşamanın kodlamasına başla. Mevcut dosyaları görmeden onların varlığını veya içeriğini varsayma. Projeyi mümkün olduğunca küçük, doğrulanabilir adımlarla geliştir; her adımda tutarlı ve derlenebilir bir sonuç üret.

**Nihai hedef:** MindMatch'i yalnızca birkaç mesaj endpoint'i olan bir demo olmaktan çıkarıp; açık profiller, karşılıklı onay, gerçek zamanlı sohbet, ses/fotoğraf mesajları, geçici içerik, hikâyeler, premium keşif, gizlilik, engelleme, raporlama ve denetlenebilir moderasyon özellikleri bulunan, güvenli ve geliştirilebilir bir sosyal platform backend'ine dönüştürmek.