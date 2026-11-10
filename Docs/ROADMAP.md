# Zombosland — Yol Haritası (referanslar ve sıradaki işler)

Yazıldı: 2026-11-10. Referans kararı geliştiricinin; iş sırası öneridir, değişebilir.
Kodda "ne nerede" için: `Docs/CODEMAP.md`.

## Referanslar: her oyundan tek katman

| Referans | Aldığımız katman | Projede durum (2026-11-10) |
|---|---|---|
| Left 4 Dead 2 | Zombiler: davranış, sürü baskını, özel türler, director | Tek tür var, yavaş (kovalama 1.6–2.2 m/sn); `ZombieDirector` sadece sahne yöneticisi |
| 7 Days to Die | Üs: barikat kur, zombiler yıksın, onar | Büyük kısmı kodda (`BuildSlot`, `BreakBarricade` durumu, onarım kiti) |
| Project Zomboid | Oyuncunun kuralları: ses çeker, yaralanma, gizlilik | Gürültü sistemi ve bandaj var |

Kurallar:
- Zombi davranışı L4D2'den gelir. Project Zomboid yalnız oyuncu tarafına karışır (kanama, ses, gizlenme), zombinin kendisine değil.
- L4D2'nin temposu alınır, ceset sayısı alınmaz: mermi kıt kalır, baskın 8–12 zombi (30+ değil).
- Fikir alınır; isim, model, ses kopyalanmaz.

Hedef döngü: ses yap → director baskın yollasın → barikat zaman kazandırsın → onar.

## İş listesi

Efor: S = küçük, M = orta, L = büyük.

### 0. Bekleyenler
- [ ] Vuruş hissi, gölge/ceset ve menü değişikliklerini oyunda dene (10 Kasım'da yazıldı; derleniyor, ama oynanarak denenmedi)
- [ ] Çizilmeyen huş ağaçlarını düzelt (`OpenWorldBuilder.Terrain.cs` → `MakeBirchPrefab`; mesh kökte değil) — S
- [ ] v0.8 betikli testlerini bu makinede koş, raporu ve sürüm numarasını 0.8 yap

### 1. Zombiler — Left 4 Dead 2
- [ ] 1. Zombi değerlerini (hız, can, hasar, görüş) koddan veri dosyasına taşı (`ZombieDef`) — M. Aşağıdakilerin ön şartı.
- [ ] 2. Fark edince koşarak saldırı: kovalama ~5 m/sn, can ~50–60 — S
- [ ] 3. Stamina: sınırsız koşuyu kaldır — S
- [ ] 4. Zombiler birbirini uyarsın — S
- [ ] 5. Bekleme halleri (yaslanma, oturma, sallanma) — M. Önce uygun animasyon var mı bakılmalı.

### 2. Barikat — 7 Days to Die
- [ ] 6. Mevcut döngüyü oynanır yap: kur → saldırsınlar → kırılsın → onar; barikat canı görünsün, kırılma sesi — S–M. Önce şu an nasıl oynandığına bakılmalı.
- [ ] 7. Barikat çakmak ses çıkarsın ve zombi çeksin — S
- [ ] 8. Üste daha fazla kapı/pencere yuvası — M
- [ ] 9. Barikat seviyeleri: tahta → metal takviye — M

### 3. Director — Left 4 Dead 2
- [ ] 10. Gürültü birikim ölçeri (silah, üretim, çekiç) — S
- [ ] 11. Baskın: ölçer dolunca 8–12 zombi görüş dışında doğup koşsun — M
- [ ] 12. Tempo: baskından sonra sakin dönem — S
- [ ] 13. Baskın uyarısı: uzaktan uğultu/çığlık — S
- [ ] 14. Zombi havuzu (yeniden yaratmak yerine geri dönüşüm) — M

### 4. Oyuncu kuralları — Project Zomboid
- [ ] 15. Kanama: ısırılınca can yavaşça azalır, bandaj durdurur — S–M
- [ ] 16. Gizlilik: eğilerek yürümek daha az ses çıkarsın — S

### 5. Özel zombiler — teker teker
- [ ] 17. Çığırtkan: görünce bağırır, baskın çağırır — M
- [ ] 18. Ağır: çok canlı, barikatı hızlı kırar — M
- [ ] 19. Şişman: ölünce patlar, sesi zombi çeker — M

### 6. Büyük sürü için performans
- [ ] 20. Zombi başına 14 SkinnedMeshRenderer'ı 1–2'ye indir — M–L
- [ ] 21. Uzak zombiler için LOD/uyku — M
- [ ] 22. 10 / 30 / 50 zombide ölçüm — S

### Menüler
- [x] Aşama 1: duraklatma menüsü, sekmeli ayarlar (Oyun / Ses / Grafik / Kontroller) ve ana menü — yazıldı, derleniyor, oyunda denenmedi
- [ ] Aşama 2: envanter / üretim ekranı ve HUD (görünüm, kullanım) — aşama 1 görüldükten sonra
- [ ] Bilinen eksik: duraklatma menüsü açıkken E tuşu hâlâ etkileşime basabiliyor (`Interactor.cs`)
- [ ] Editörde her Play'de ana menü çıkıyor; geliştirme için atlama seçeneği düşünülebilir

## İlk oynanabilir döngü
1, 2, 3, 6, 10, 11. Bunlar bitince döngü oynanıp hissine bakılır; gerisi üstüne eklenir.

## Şimdilik yok
Açlık-susuzluk, yetenek ağacı, serbest inşa, araç, co-op, 30+ zombilik sürü.

## 10 Kasım 2026'da yapılanlar (commit edilmedi; derleniyor, oynanarak denenmedi)
- Menüler aşama 1: `PauseMenu.cs` ve `GameSettings.cs` yeniden yazıldı, `MainMenu.cs` ve `SettingsPanel.cs` eklendi, `UiTheme.cs`'e menü bileşenleri eklendi.
- Vuruş hissi: hit-stop, yakın dövüş sarsıntısı, kafa vuruşu sesi ve kanı, yerde kan lekesi, ses varyantları. Ayarlar `Assets/Scripts/Sandbox.HitFeel.cs` içinde.
- Gölge: mesafe 110 → 60, gölge haritası 4096 → 2048, yumuşak gölge Orta (`PC_RPAsset.asset`, `LookDevSetup.cs`).
- Ceset: limit 24 → 10; oturan ragdoll kinematik oluyor (`ZombieDirector.cs`, `ZombieRagdoll.cs`).
- `Docs/CODEMAP.md` eklendi.
