# LinuxDualBoot — USB'siz Linux Dual Boot Sihirbazı

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6)
![Firmware](https://img.shields.io/badge/firmware-UEFI%20%2B%20GPT-informational)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

USB bellek kullanmadan, Windows'u silmeden Linux kurmaya hazırlanmanızı sağlayan bir Windows Forms (.NET) sihirbazı.

*A Windows wizard that prepares a USB-free Linux dual-boot on UEFI/GPT systems: it shrinks `C:`, creates a small FAT32 partition, extracts the ISO onto it, adds a Windows Boot Manager entry so the live system can boot directly, and generates a personalised install guide.*

> ⚠️ **Uyarı / Warning**
> Bu araç disk bölümlerini değiştirir. Çalıştırmadan önce **önemli verilerinizi yedekleyin**. Yazılım "olduğu gibi" sunulur, hiçbir garanti verilmez (bkz. [LICENSE](LICENSE)). İlk kullanımda mutlaka **deneme modunu** kullanın.

## İçindekiler

- [Nasıl çalışır?](#nasıl-çalışır)
- [Desteklenen Linux dağıtımları](#desteklenen-linux-dağıtımları)
- [Güvenlik tasarımı](#güvenlik-tasarımı)
- [Gereksinimler](#gereksinimler)
- [Derleme ve çalıştırma](#derleme-ve-çalıştırma)
- [Bilinen sınırlamalar](#bilinen-sınırlamalar)
- [Geri alma](#geri-alma)
- [Katkı](#katkı)

## Nasıl çalışır?

1. **Sistem kontrolü:** UEFI/GPT, Secure Boot, BitLocker, C: boyutu ve boş alan kontrol edilir. UEFI + GPT değilse araç devam etmez.
2. **ISO doğrulama:** SHA256 hesaplanır (resmî değerle karşılaştırabilirsiniz). Aynı anda ISO salt okunur bağlanıp tek geçişte taranır: `EFI\BOOT\BOOTX64.EFI` varlığı, 4 GB üstü dosya olmaması, önyükleme etiketi.
3. **Yapılandırma:** Linux alanı, dosya sistemi, takas türü ve zorunlu onaylar (yedek, Secure Boot, BitLocker anahtarı).
4. **Uygulama:** `C:` küçültülür → geçici `LINUXISO` FAT32 bölümü açılır → ISO içeriği çok iş parçacıklı kopyalanır → önyükleme etiketi gerekirse güncellenir → doğrulanır → Windows Boot Manager'a "Linux Live" girişi eklenir.
5. **Rehber:** Seçimlerinize göre kişiselleştirilmiş kurulum adımları üretilir.

Yeniden başlatınca açılan menüden **Linux Live** girişini seçerek canlı sistemi başlatırsınız; kurulumda **boş alanı** kullanıp **mevcut EFI bölümünü biçimlendirmeden** bağlarsınız.

## Desteklenen Linux dağıtımları

Yöntem dağıtıma değil, **ISO'nun yapısına** bağlıdır. ISO şu üç koşulu sağlıyorsa çalışması beklenir:

1. `EFI\BOOT\BOOTX64.EFI` içeriyor (UEFI'den doğrudan açılabiliyor),
2. 4 GB'dan büyük tek dosya içermiyor (FAT32 sınırı),
3. Canlı sistemi bir dosya sistemi **etiketiyle** ya da **tarayarak** buluyor.

Sihirbaz 1. ve 2. koşulu ISO'yu seçtiğinizde otomatik denetler; uygun olmayan ISO'da durur. Etiket kullanan ISO'larda (`archisolabel=`, `CDLABEL=`, GRUB `--label`) etiketi kopyada `LINUXISO` olarak günceller.

| Aile | Örnek dağıtımlar | Canlı sistemi bulma | Durum |
|---|---|---|---|
| **Arch tabanlı (archiso)** | CachyOS, Arch Linux, EndeavourOS, Manjaro, Garuda, ArcoLinux, BlackArch | `archisolabel=` → otomatik güncellenir | ✅ Hedeflenen aile (CachyOS ile geliştirildi) |
| **Ubuntu tabanlı (casper)** | Ubuntu, Kubuntu, Xubuntu, Lubuntu, Linux Mint, Pop!_OS, elementary OS, Zorin OS | Aygıtları tarar, etiket gerekmez | 🟡 Beklenen uyumlu — ISO ≥ 4 GB dosya içeriyorsa denetim durdurur |
| **Debian tabanlı (live-boot)** | Debian Live, MX Linux, Kali, Parrot, Tails dışı live ISO'lar, antiX | `live/filesystem.squashfs` taranır | 🟡 Beklenen uyumlu |
| **Fedora/RHEL tabanlı (dracut)** | Fedora Workstation/KDE/Spins, Nobara, AlmaLinux, Rocky Linux | `root=live:CDLABEL=` → otomatik güncellenir | 🟡 Beklenen uyumlu |
| **openSUSE** | Tumbleweed/Leap Live, GeckoLinux | `root=live:CDLABEL=` → otomatik güncellenir | 🟡 Beklenen uyumlu |
| **Diğer** | NixOS, Void, Gentoo live, Solus, Alpine… | Dağıtıma göre değişir | ❔ Yapılandırmayı elle kontrol edin |
| **Yalnızca BIOS/Legacy ISO'lar** | Eski sürümler, bazı yeniden paketlenmiş ISO'lar | — | ❌ `BOOTX64.EFI` yoksa sihirbaz reddeder |

**Durum açıklaması**

- ✅ Yazarlar tarafından denenmiş yapı.
- 🟡 ISO yapısı ve boot mantığı gereği çalışması beklenir; **donanımda doğrulanmadı**. Denediyseniz sonucu Issue olarak bildirin, tabloyu güncelleyelim.
- ❔ Otomatik denetimleri geçse bile canlı sistemin bölümü bulup bulmayacağı garanti değildir.

> Dağıtımın kendi ISO'sunu kullanın; üçüncü taraf yeniden paketlenmiş ISO'larda yapı değişmiş olabilir. Desteklenen Windows tarafı: Windows 10 ve 11, UEFI + GPT, x64.

## Güvenlik tasarımı

- Deneme modu **varsayılan olarak açık**: yalnızca çalıştırılacak PowerShell betiklerini gösterir.
- Yalnızca şunları yapar: `Resize-Partition` (C:), `New-Partition` + `Format-Volume` (kendi oluşturduğu bölüm), isteğe bağlı `powercfg /h off`.
- EFI sistem bölümüne, MSR'a ve Recovery'ye **dokunmaz**. Mevcut BCD girişlerini değiştirmez; yalnızca `bcdedit /copy {bootmgr}` ile **yeni bir "Linux Live" girişi** ekler ve `/timeout 15` ayarlar. Geri alma: `bcdedit /delete {GUID}`.
- Gerçek uygulama için `ONAYLIYORUM` yazmanız gerekir.
- Yönetici hakkı gerektirir (`app.manifest`).

## Gereksinimler

- Windows 10/11 x64, **UEFI + GPT** disk (Legacy/MBR desteklenmez)
- Yönetici hakları
- Disk Yönetimi'nin `C:`'yi küçültebilmesi (taşınamaz dosyalar alt sınırı belirler)
- Derlemek için .NET SDK (proje `net10.0-windows` hedefler; gerekirse `TargetFramework`'ü değiştirin)

## Derleme ve çalıştırma

```powershell
git clone https://github.com/umutakksy/LinuxDualBoot.git
cd LinuxDualBoot

# Geliştirme
dotnet build -c Release
# bin\Release\net10.0-windows\CachyDualBoot.exe  (yönetici olarak açılır)

# Tek dosya, bağımsız (önceden derlenmiş / ReadyToRun) sürüm
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## Performans notları

- SHA256 hesabı ve ISO yapı taraması **paralel** çalışır; ISO tek geçişte taranır.
- Dosya kopyalama `robocopy /MT:16` ile çok iş parçacıklıdır.
- Yayınlanan sürüm ReadyToRun derlenir (daha hızlı açılış); gereksiz dil kaynakları dışarıda bırakılır.

## Bilinen sınırlamalar

- **Dağıtım uyumu:** Yukarıdaki tabloda ✅ olmayan dağıtımlar donanımda doğrulanmamıştır.
- FAT32 sınırı nedeniyle 4 GB'dan büyük dosya içeren ISO'lar desteklenmez.
- Geçici bölüm firmware boot menüsünde görünmeyebilir; bu yüzden sihirbaz Windows Boot Manager'a ayrı bir giriş ekler ve açılışta o menüden seçilir.
- Secure Boot kapatılmalıdır (çoğu Linux ISO'su Microsoft imzalı değildir).
- Gerçek donanımda kapsamlı test edilmedi; geri bildirim ve PR'lar memnuniyetle karşılanır.
- Gerçek modda sihirbazı ikinci kez çalıştırmayın: `C:` bir kez daha küçülür.

## Geri alma

Kurulumdan vazgeçerseniz önce yönetici CMD'de `bcdedit /delete {GUID}` ile "Linux Live" girişini kaldırın (`bcdedit /enum all`), sonra Disk Yönetimi'nde `LINUXISO` bölümünü silin, ardından `C:` → *Birimi Genişlet*.

## Katkı

Issue ve PR'lar açıktır. Özellikle başka dağıtımların ISO yapıları ve farklı firmware'lerdeki test sonuçları değerlidir.

## Lisans

[MIT](LICENSE)
