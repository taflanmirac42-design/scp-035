using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using UnityEngine;

namespace SCP035Plugin
{
    public class SCP035Plugin : Plugin<Config>
    {
        public static SCP035Plugin Instance;
        public static readonly string Version = "1.0.0";

        // Mask wearer'ı takip et
        private Player maskWearer = null;
        private float maskWearerHealth = 300f;
        private const float MASK_MAX_HP = 300f;
        private const float HEALTH_DRAIN_RATE = 0.5f;
        private bool isHealthDraining = false;
        private Pickup maskPickup = null;

        // Light Containment Zone odaları
        private static readonly RoomType[] LightContainmentRooms = new[]
        {
            RoomType.LczClassDSpawn,
            RoomType.LczToilets,
            RoomType.LczGlassBox,
            RoomType.LczCurve,
            RoomType.LczStraight,
            RoomType.LczShowers,
            RoomType.LczCafe,
            RoomType.LczOffice,
            RoomType.LczIntercom,
            RoomType.LczPlants,
        };

        public override void OnEnabled()
        {
            Instance = this;
            base.OnEnabled();
            Log.Info("SCP-035 Plugin etkinleştirildi!");
            Log.Info("Siyah Maske Light Containment Zone'da oluşacak!");
            Log.Info("Maskayı bulunca direkt takılacak ve çıkarılamayacak!");
            Log.Info("Maske takanı: Tüm eşyaları kullanabilir, tüm kapıları açabilir!");
            
            // Event'leri register et
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
            Exiled.Events.Handlers.Player.Hurting += OnPlayerHurting;
            Exiled.Events.Handlers.Player.UsingItem += OnUsingItem;
            Exiled.Events.Handlers.Player.InteractingDoor += OnInteractingDoor;
            Exiled.Events.Handlers.Player.InteractingLocker += OnInteractingLocker;
            Exiled.Events.Handlers.Player.UsingRadioItem += OnUsingRadioItem;
        }

        public override void OnDisabled()
        {
            base.OnDisabled();
            Log.Info("SCP-035 Plugin devre dışı bırakıldı!");
            
            // Event'leri unregister et
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;
            Exiled.Events.Handlers.Player.Died -= OnPlayerDied;
            Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;
            Exiled.Events.Handlers.Player.Hurting -= OnPlayerHurting;
            Exiled.Events.Handlers.Player.UsingItem -= OnUsingItem;
            Exiled.Events.Handlers.Player.InteractingDoor -= OnInteractingDoor;
            Exiled.Events.Handlers.Player.InteractingLocker -= OnInteractingLocker;
            Exiled.Events.Handlers.Player.UsingRadioItem -= OnUsingRadioItem;
        }

        // Oyun başladığında
        private void OnRoundStarted()
        {
            Log.Info("Yeni oyun başladı! SCP-035 maskası Light Containment Zone'da oluşturuluyor...");
            
            // Rastgele bir Light Containment Zone odası seç
            RoomType randomRoom = LightContainmentRooms[UnityEngine.Random.Range(0, LightContainmentRooms.Length)];
            Room room = RoomType.LczClassDSpawn.GetRandomRoom();

            if (room != null)
            {
                // Rastgele bir yer seç
                Vector3 spawnPosition = room.Position + Vector3.up * 1f;
                
                // Maskayı oluştur
                maskPickup = ItemType.KeycardFacilityManager.GetRandomSpawnProperties().Item.Spawn(spawnPosition);
                
                if (maskPickup != null)
                {
                    maskPickup.DisplayName = "SCP-035 Siyah Maske";
                    Log.Info($"SCP-035 maskası {randomRoom} odasında oluşturuldu!");
                }
            }
        }

        // Eşya aldığında - DIREKT GİYİN!
        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev.Item == null || ev.Player == null) return;

            // Maske kontrol et
            if (IsBlackMask(ev.Item))
            {
                Log.Info($"{ev.Player.Nickname} SCP-035 maskayı buldu! DIREKT TAKILIYİN!");
                
                // Maskayı engelle (envantere gelmeyecek)
                ev.IsAllowed = false;

                // Maskeyi takılı yap
                ApplyMask(ev.Player, ev.Item);
            }
        }

        // Maskayı oyuncuya takmak
        private void ApplyMask(Player player, Pickup item)
        {
            MaskWearer = player;
            maskWearerHealth = MASK_MAX_HP;
            
            // Görsel efekt - Siyah maske
            player.EnableEffect(EffectType.Ensnared, 2);
            player.EnableEffect(EffectType.Flashed, 1);
            
            // Broadcast mesajı
            player.Broadcast(5, "<color=black>███ SCP-035: MASKE SANA SAHİP ███\n███ KURTULUŞ YOK! ███</color>");
            player.ShowHint("🖤 Maske seni kontrol ediyor!\n🛑 Çıkaramıyorsun!\n⚔️ Diğer insanları vurabilirssin!\n🛡️ SCPler seni vuramayacak!\n✅ Tüm eşyaları, kapıları kullanabilirsin!", 5);
            
            // İsim değiştir
            player.DisplayNickname = $"[SCP-035] {player.Nickname}";
            
            // Siyah ekran efekti
            player.EnableEffect(EffectType.Blinded, 1);
            
            // HP azalmasını başlat
            if (!isHealthDraining)
            {
                Timing.RunCoroutine(DrainHealth());
            }
            
            Log.Info($"{player.Nickname} SCP-035 maskasını taktı ve artık kontrolü altında!");
        }

        // Eşya bıraktığında - ÇIKARSA ENGELLE!
        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (ev.Player == MaskWearer)
            {
                Log.Info($"{ev.Player.Nickname} maskayı çıkarmaya çalıştı! ENGELLENDI!");
                
                // Çıkarmayı engelle
                ev.IsAllowed = false;
                
                ev.Player.ShowHint("❌ Maskayı çıkaramıyorsun!\n🖤 Maske sana tutunmuş!", 3);
                ev.Player.Broadcast(3, "<color=red>Maskayı çıkaramıyorsun! Maske sana tutunmuş!</color>");
            }
        }

        // Oyuncu öldüğünde
        private void OnPlayerDied(DiedEventArgs ev)
        {
            if (ev.Player == MaskWearer)
            {
                Log.Info($"{ev.Player.Nickname} SCP-035 maskayı takıyken öldü! Maske düştü!");
                
                // İsim geri al
                ev.Player.DisplayNickname = ev.Player.Nickname;
                ev.Player.RemoveEffect(EffectType.Ensnared);
                
                MaskWearer = null;
                isHealthDraining = false;
                
                // Maskayı düşür (cesede takmak için)
                if (maskPickup != null)
                {
                    maskPickup.Position = ev.Player.Position;
                }
            }
        }

        // SCPler maskayı takanı vuramaz!
        private void OnPlayerHurting(HurtingEventArgs ev)
        {
            if (ev.Player == null || ev.Attacker == null) return;

            // Eğer hedef maske takanıysa ve saldırgan SCP ise
            if (ev.Player == MaskWearer && ev.Attacker.IsScp)
            {
                Log.Info($"SCP ({ev.Attacker.Nickname}) maskayı takanı ({ev.Player.Nickname}) vurdu ama başarısız!");
                
                // Hasarı engelle
                ev.IsAllowed = false;
                
                // Uyarı mesajı
                ev.Player.ShowHint("🛡️ SCP saldırısı engellendi!", 2);
                ev.Attacker.ShowHint("❌ Bu oyuncuyu vuramazzın! (SCP-035 koruması)", 2);
                
                return;
            }

            // Maske takanı normal insanları vurabiliyor
            if (ev.Attacker == MaskWearer && !ev.Player.IsScp)
            {
                ev.IsAllowed = true;
            }
        }

        // Eşya kullanma - HEPSI SERBEST!
        private void OnUsingItem(UsingItemEventArgs ev)
        {
            if (ev.Player == null) return;

            if (ev.Player == MaskWearer)
            {
                Log.Info($"Maske takanı ({ev.Player.Nickname}) eşya kullandı: {ev.Item.Type}");
                // TÜM eşyaları kullanabilir
                ev.IsAllowed = true;
            }
        }

        // Kapı açma - HEPSI SERBEST!
        private void OnInteractingDoor(InteractingDoorEventArgs ev)
        {
            if (ev.Player == null) return;

            if (ev.Player == MaskWearer)
            {
                Log.Info($"Maske takanı ({ev.Player.Nickname}) kapıyı açmaya çalıştı!");
                // TÜM kapıları açabilir
                ev.IsAllowed = true;
            }
        }

        // Dolapla etkileşim - SERBEST!
        private void OnInteractingLocker(InteractingLockerEventArgs ev)
        {
            if (ev.Player == MaskWearer)
            {
                Log.Info($"Maske takanı ({ev.Player.Nickname}) dolapla etkileşim kurdu!");
                ev.IsAllowed = true;
            }
        }

        // Radyo kullanma - SERBEST!
        private void OnUsingRadioItem(UsingRadioItemEventArgs ev)
        {
            if (ev.Player == MaskWearer)
            {
                Log.Info($"Maske takanı ({ev.Player.Nickname}) radyoyu kullandı!");
                ev.IsAllowed = true;
            }
        }

        // HP'yi zamanla azalt
        private IEnumerator<float> DrainHealth()
        {
            isHealthDraining = true;

            while (maskWearer != null && maskWearer.IsAlive)
            {
                maskWearerHealth -= HEALTH_DRAIN_RATE;
                maskWearer.Health = maskWearerHealth;

                // Ekrana sağlık göster
                if (maskWearerHealth % 10 < HEALTH_DRAIN_RATE)
                {
                    maskWearer.ShowHint($"🖤 SCP-035 Sağlık: {maskWearerHealth:F0}/{MASK_MAX_HP}\n⚔️ Diğer insanları öldür!", 1);
                }

                if (maskWearerHealth <= 0)
                {
                    maskWearer.Health = 0;
                    maskWearer.Kill("SCP-035 Mask");
                    break;
                }

                yield return Timing.WaitForSeconds(1f);
            }

            isHealthDraining = false;
        }

        // Maskayı başka bir oyuncuya takmak (cesede basılırsa)
        public void TransferMaskToPlayer(Player newWearer)
        {
            if (maskWearer != null)
            {
                maskWearer.RemoveEffect(EffectType.Ensnared);
                maskWearer.DisplayNickname = maskWearer.Nickname;
            }

            ApplyMask(newWearer, maskPickup);
            
            Log.Info($"Maske {newWearer.Nickname} tarafından takıldı!");
            newWearer.Broadcast(5, "<color=black>███ SCP-035: YENİ MASKE SAHİBİ ███</color>");
        }

        // Siyah Maske kontrolü
        private bool IsBlackMask(Pickup item)
        {
            if (item.DisplayName.Contains("Black Mask") || 
                item.DisplayName.Contains("SCP-035") ||
                item.DisplayName.Contains("Maske"))
            {
                return true;
            }
            
            if (item.Type == ItemType.KeycardFacilityManager)
            {
                return true;
            }

            return false;
        }

        public Player MaskWearer
        {
            get { return maskWearer; }
            set { maskWearer = value; }
        }
    }

    public class Config : IConfig
    {
        public string Name => "SCP-035 Plugin";
        public string Author => "taflanmirac42-design";
        public Version Version => new Version(1, 0, 0);
        public Version RequiredExiledVersion => new Version(8, 0, 0);
        public bool Debug { get; set; }
        public bool Enable { get; set; } = true;
        public LogLevel LogLevel { get; set; } = LogLevel.Info;
    }
}
