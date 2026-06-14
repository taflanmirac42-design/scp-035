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
            RoomType.LczStraight,
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
            
            // Event'leri register et
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
            Exiled.Events.Handlers.Player.Hurting += OnPlayerHurting;
            Exiled.Events.Handlers.Player.UsingItem += OnUsingItem;
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

        // Eşya aldığında
        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev.Item == null || ev.Player == null) return;

            // Maske kontrol et
            if (IsBlackMask(ev.Item))
            {
                MaskWearer = ev.Player;
                maskWearerHealth = MASK_MAX_HP;
                
                Log.Info($"{ev.Player.Nickname} SCP-035 maskayı taktı!");
                
                // Görsel efekt - Siyah maske
                ev.Player.EnableEffect(EffectType.Ensnared, 2);
                ev.Player.EnableEffect(EffectType.Flashed, 1);
                
                // Broadcast mesajı
                ev.Player.Broadcast(5, "<color=black>███ SCP-035: MASKE SANA SAHİP ███</color>");
                ev.Player.ShowHint("🖤 Artık normal bir insan gibi çalışabilirsin!\n🛡️ SCPler seni vuramayacak!\n⚔️ Diğer insanları vurabilirsın!", 5);
                
                // İsim değiştir
                ev.Player.DisplayNickname = $"[SCP-035] {ev.Player.Nickname}";
                
                // HP azalmasını başlat
                if (!isHealthDraining)
                {
                    Timing.RunCoroutine(DrainHealth());
                }
                
                ev.IsAllowed = false;
            }
        }

        // Eşya bıraktığında
        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (ev.Player == MaskWearer)
            {
                Log.Info($"{ev.Player.Nickname} maskayı çıkardı!");
                
                // İsim geri al
                ev.Player.DisplayNickname = ev.Player.Nickname;
                ev.Player.RemoveEffect(EffectType.Ensnared);
                
                MaskWearer = null;
                isHealthDraining = false;
                
                ev.Player.Broadcast(3, "<color=green>Maskadan kurtuldun!</color>");
            }
        }

        // Oyuncu öldüğünde
        private void OnPlayerDied(DiedEventArgs ev)
        {
            if (ev.Player == MaskWearer)
            {
                Log.Info($"{ev.Player.Nickname} SCP-035 maskayı takıyken öldü!");
                
                // İsim geri al
                ev.Player.DisplayNickname = ev.Player.Nickname;
                ev.Player.RemoveEffect(EffectType.Ensnared);
                
                MaskWearer = null;
                isHealthDraining = false;
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

            // Maske takanı herkes (SCP dahil) tarafından vurulamaz
            // HAYIR - Maske takanı normal insanları vurabiliyor ama SCPler vuramıyor
            if (ev.Attacker == MaskWearer)
            {
                // Maske takanı herkesin (SCP dahil) haricinde vurabiliyor
                if (ev.Player.IsScp)
                {
                    // SCPler de maske takanı vuramaz çünkü yukarıda engellendi
                    ev.IsAllowed = true; // SCPler vurabilir ama normal insanlar vuramıyor
                }
                else
                {
                    // Normal insanları vurabiliyor
                    ev.IsAllowed = true;
                }
            }
        }

        // Eşya kullanma (medikit, anahtar kartı, vb)
        private void OnUsingItem(UsingItemEventArgs ev)
        {
            if (ev.Player == null) return;

            if (ev.Player == MaskWearer)
            {
                Log.Info($"Maske takanı ({ev.Player.Nickname}) eşya kullandı!");
                // Normal insan gibi eşya kullanabilir
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
                    maskWearer.ShowHint($"🖤 SCP-035 Sağlık: {maskWearerHealth:F0}/{MASK_MAX_HP}", 1);
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

        // Maskayı başka bir oyuncuya takmak
        public void TransferMaskToPlayer(Player newWearer)
        {
            if (maskWearer != null)
            {
                maskWearer.RemoveEffect(EffectType.Ensnared);
                maskWearer.DisplayNickname = maskWearer.Nickname;
            }

            MaskWearer = newWearer;
            maskWearerHealth = MASK_MAX_HP;
            newWearer.Health = MASK_MAX_HP;

            Log.Info($"Maske {newWearer.Nickname} tarafından takıldı!");
            
            // Görsel efekt
            newWearer.EnableEffect(EffectType.Ensnared, 2);
            newWearer.EnableEffect(EffectType.Flashed, 1);
            
            // İsim değiştir
            newWearer.DisplayNickname = $"[SCP-035] {newWearer.Nickname}";
            
            newWearer.Broadcast(5, "<color=black>███ SCP-035: YENİ MASKE SAHİBİ ███</color>");
            newWearer.ShowHint("🖤 Artık normal bir insan gibi çalışabilirsin!", 5);

            Timing.RunCoroutine(DrainHealth());
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
