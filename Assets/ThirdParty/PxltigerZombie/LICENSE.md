# Pxltiger - Zombie

- Yayıncı: Pxltiger
- Paket: Zombie
- Sürüm: Draft 1.2
- Kaynak: kullanıcının Asset Store hesabıyla 2026-11-07 tarihinde Package Manager ile, kullanıcının Asset Store hesabından indirilmiş yerel önbellek.
- Lisans: Standard Unity Asset Store EULA. Oyunda kullanım serbest; ham varlıklar ayrı olarak veya açık depoda yeniden dağıtılamaz.
- Alınan dosyalar:
  - FBXs/Zombie1.FBX, Zombie2.FBX, Zombie3.FBX
  - Animations/Zombie@Z_{Idle,Walk,Walk1,Run,Attack,FallingBack,FallingForward}.FBX ve Walk, Walk1, Run için _InPlace sürümleri
  - Textures/Zombie.tga, Zombie_nm.png, Zombie_metallic.tga, Zombie_ao.png, zombie_emission.tga
  Paketin .mat/.controller/prefab/sahne dosyaları alınmadı.
- Yapılan değişiklikler:
  - URP malzemesi Zombie_Pxl.mat (model malzeme slotları remap ile bağlandı); örnek başına +-%10 ton.
  - Kendi Animator controller'ımız (Assets/Generated/Animators/Zombie.controller).
  - Zombie_metallic.tga ve Zombie_ao.png: sRGB kapatıldı; Z_*_InPlace/Z_Idle klipleri döngüsüzse loopTime açıldı (kodla, yalnızca gerekirse); Zombie2/3 avatar bağlantısı geçersizse Zombie1 avatarına yeniden bağlanır.
