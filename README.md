<div align="center">

<img src="docs/images/banner.png" width="860" alt="Cook & Dash">

# 🍔 Cook & Dash Online

**Arkadaşlarınla aynı mutfakta hamburger yetiştirdiğin, 1–4 oyunculu çevrimiçi bir restoran oyunu. Unity 6 ve Netcode for GameObjects ile geliştirdiğim, Google Play'e hazırlanan erken erişim oyunum.**

![Unity](https://img.shields.io/badge/Unity-6-000000?logo=unity&logoColor=white)
![Netcode](https://img.shields.io/badge/Netcode%20for%20GameObjects-Relay%20%C2%B7%20Lobby-blue)
![Platform](https://img.shields.io/badge/Windows%20%C2%B7%20Android-lightgrey)
![Sürüm](https://img.shields.io/badge/s%C3%BCr%C3%BCm-0.9.0%20erken%20eri%C5%9Fim-orange)

<img src="docs/images/gameplay.gif" width="600" alt="Oynanış">

</div>

## Oyun
Müşteriler gelir, siparişler yağar, mutfak karışır. Oyunun akışı şöyle:
- Malzemeyi kasasından alırsın, tezgâhta hazırlarsın, köfteyi ocakta pişirirsin.
- Yemeği tabağa koyup siparişi veren masaya süre dolmadan götürürsün.
- Kirli tabakları yıkamazsan temiz tabak kalmaz.
- Bölüm aralarında kazandığın parayla yeni tezgâh, ocak, masa ve kasa alıp mutfağını kendi düzenine göre kurarsın.
- Bir bölümde çok müşteri kaçırırsan oyun biter.

| | |
|---|---|
| <img src="docs/images/02_mutfak.jpg" alt="Mutfak"> | <img src="docs/images/03_cok-oyunculu.jpg" alt="Çok oyunculu"> |
| <img src="docs/images/05_magaza.jpg" alt="Mağaza"> | <img src="docs/images/07_bolum-sonu.jpg" alt="Bölüm sonu"> |

## Özellikler
- **1–4 oyuncu**
  - Tek kişilik mod çevrimdışı oynanır.
  - Çok oyunculu modda Unity **Lobby** ile oda kurulur, kodla katılınır.
  - **Relay** ile NAT arkasından internet üzerinden oynanır.
- **Sunucu yetkili ağ mimarisi:** Oyun durumu host'ta tutulur, istemciler RPC ve NetworkVariable ile senkronize edilir. Sahiplik tabanlı hareket ve animasyon senkronu kullanılır.
- **Müşteri davranışı:** Müşteriler NavMesh ile restorana gelir, boş masaya oturur, sipariş verir, sabırları tükenince ayrılır. NavMesh, oyuncunun kurduğu düzene göre çalışma anında yeniden hesaplanır.
- **İstasyon sistemi:** `IBox`, `ICraft`, `Iwork` ve `Icargo` arayüzleriyle tanımlanan istasyonlar var: malzeme kasası, tezgâh, ocak, lavabo, tabaklık ve masa. Yeni istasyon eklemek bir arayüzü uygulamak kadar kolay.
- **Tarif sistemi:** Enum tabanlı malzeme, tarif ve tabak durum makinesi kullanılıyor (alt ekmek → köfte → marul → kapak).
- **Oyuncu sayısına göre zorluk:** Müşteri sayısı, gelme sıklığı ve kaçırma hakkı ekibin büyüklüğüne göre ayarlanır.
- **Otomatik kayıt:** Bölüm aralarında mutfağın düzeni, açılmamış kargolar ve para kaydedilir; "Devam et" ile kalınan yerden sürdürülür.
- **Sonradan katılma:** Bölüm aralarında odaya yeni oyuncu girebilir, düşen oyuncu geri dönebilir.
- **Mobil:** Dokunmatik kontroller (kayan joystick ve iki büyük düğme), Türkçe ve İngilizce dil desteği. Android hedef API 36 (ARM64 + ARMv7).

## Mimari
```mermaid
flowchart LR
    subgraph Ağ
        LOBBY[Unity Lobby<br>oda listesi · kod] --> RELAY[Unity Relay<br>NAT geçişi]
        RELAY --> NGO[Netcode for GameObjects<br>host yetkili]
    end
    NGO --> GM[gameManager<br>bölüm · zorluk · kayıt]
    GM --> TM[tableManager<br>masalar · müşteri akışı]
    TM --> ENT[entity<br>NavMesh müşteri]
    GM --> RM[recipeManager<br>siparişler]
    P[Oyuncu<br>raycast etkileşim] --> ST{İstasyon<br>arayüzleri}
    ST --> BOX[IBox · kasa]
    ST --> CRAFT[ICraft · tezgâh/masa]
    ST --> WORK[Iwork · ocak/lavabo]
    ST --> CARGO[Icargo · satın alınan eşya]
```

## Nasıl denenir
[Releases](../../releases) sayfasında iki dosya var:
- **Windows:** `CookDash-0.9.0-Windows.zip`. Zip'i açıp `CookDash.exe` ile başlat. Tek kişilik mod internetsiz çalışır. Aynı bilgisayarda iki kopyayla çok oyunculu deneme için `CookDash.exe -profile oyuncu1` ve `-profile oyuncu2` ile aç.
- **Android:** `CookDash-0.9.0.apk`. Telefona kopyalayıp kur ("bilinmeyen kaynaklar" izni gerekir).

## Kod örnekleri
Oyunun kaynak kodu kapalıdır. Mimariyi gösteren birkaç parça [`showcase/`](showcase) klasöründe:
- [`Inventory/Interface`](showcase/Inventory/Interface): istasyon sözleşmeleri.
- [`Box/stoveBox.cs`](showcase/Box/stoveBox.cs) ve [`Box/washingBox.cs`](showcase/Box/washingBox.cs): ağ senkronlu, zamanlı istasyonlar.
- [`Entity/entity.cs`](showcase/Entity/entity.cs): sunucuda çalışan NavMesh müşteri davranışı ve istemci tarafı yumuşatma.
- [`Manager/recipeManager.cs`](showcase/Manager/recipeManager.cs): sipariş üretimi.

---
<sub>© Ahmet Emin Arslanbaş — Tüm hakları saklıdır. Kaynak kod paylaşılmamıştır; satın alınan asset paketleri kendi lisanslarına tabidir.</sub>
