# Google Calendar API Kurulum Rehberi (CRM)

Bu dokuman, CRM projesinde Google entegrasyonunun calismasi icin gerekli Google Cloud ve API ayarlarini adim adim anlatir.

## 1) Google Cloud Projesi Hazirlama

1. `https://console.cloud.google.com/` adresine gir.
2. Yeni proje olustur veya mevcut projeyi sec.
3. Proje secili oldugunu sag ustten kontrol et.

## 2) OAuth Consent Screen Ayari

1. `APIs & Services > OAuth consent screen` sayfasina git.
2. User Type sec:
   - Dahili kurum hesabi yoksa `External` sec.
3. Zorunlu alanlari doldur:
   - App name
   - User support email
   - Developer contact email
4. Save and Continue ile tamamla.

Not: Test kullanicisi gerekiyorsa ayni ekranda test user ekle.

## 3) Google Calendar API Etkinlestirme

1. `APIs & Services > Library` sayfasina git.
2. `Google Calendar API` ara.
3. `Enable` ile aktif et.

## 4) OAuth Client Olusturma (Web Application)

1. `APIs & Services > Credentials` ekranina git.
2. `Create Credentials > OAuth client ID` sec.
3. Application type: `Web application`.
4. `Authorized redirect URIs` alanina asagidakileri ekle:
   - Local: `http://localhost:5001/api/integrations/google/callback`
   - Production: `https://crm.v3rii.com/api/integrations/google/callback`
5. Kaydet.
6. Olusan ekrandan su iki degeri al:
   - `Client ID`
   - `Client Secret`

## 5) CRM API Konfigurasyonu

`/Users/cannasif/Documents/V3rii/verii_crm_api/appsettings.json` dosyasindaki `Google` bolumune degerleri gir:

```json
"Google": {
  "ClientId": "YOUR_CLIENT_ID.apps.googleusercontent.com",
  "ClientSecret": "YOUR_CLIENT_SECRET",
  "RedirectUri": "http://localhost:5001/api/integrations/google/callback",
  "Scopes": "https://www.googleapis.com/auth/calendar.events"
}
```

Production icin ayni alanlari `appsettings.Production.json` veya environment variable ile yonet.

## 6) Uygulamayi Calistirma

1. API'yi yeniden baslat.
2. Web uygulamadan `Google Entegrasyon > Baglanti` sayfasina git.
3. `Google ile Baglan` butonuna tikla.
4. Google izin ekranindan onay ver.

Basarili callback sonrasi:
- Durum `Baglandi` gorunmeli.
- `GoogleEmail` alani dolu gelmeli.

## 7) Test Event Dogrulama

Baglanti kurulduktan sonra API tarafinda test endpoint:

- `POST /api/integrations/google/test-event`

Basariliysa bir `eventId` doner ve Google Calendar `primary` takvimde test etkinligi olusur.

## 8) Sik Karsilasilan Hatalar

### Hata: `Google OAuth configuration is missing`
- `Google:ClientId` veya `Google:ClientSecret` bos.
- Konfig dosyasini duzeltip API restart et.

### Hata: `redirect_uri_mismatch`
- Google Console'daki redirect URI ile API config'teki `Google:RedirectUri` birebir ayni degil.
- Protokol (`http/https`), domain, port, path birebir ayni olmali.

### Hata: Yetki var ama callback fail oluyor
- API URL/port degismis olabilir.
- JWT oturum/cerez tarafinda farkli domain sorunu olabilir.

## 9) Guvenlik Notu

- `ClientSecret` kaynak koda plain yazilmamali; ideal olarak environment variable veya gizli kasa (vault) kullanilmalidir.
- En azindan production ortaminda secret degerlerini appsettings dosyasinda tutma.

