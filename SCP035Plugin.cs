using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
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
        private const float HEALTH_DRAIN_RATE = 0.5f; // Her saniyede azalacak HP
        private bool isHealthDraining = false;

        // Siyah Maske Item
        private const ushort BLACK_MASK_ID = 35; // SCP-035 ID
        private ItemType BLACK_MASK_TYPE = ItemType.KeycardFacilityManager; // Temeli

        public override void OnEnabled()
        {
            Instance = this;
            base.OnEnabled();
            Log.Info("SCP-035 Plugin etkinleştirildi!");
            Log.Info("Siyah Maske aktif!");
            
            // Event'leri register et
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem;
        }

        public override void OnDisabled()
        {
            base.OnDisabled();
            Log.Info("SCP-035 Plugin devre dışı bırakıldı!");
            
            // Event'leri unregister et
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;
            Exiled.Events.Handlers.Player.Died -= OnPlayerDied;
            Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;
        }

        // Eşya aldığında
        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev.Item == null || ev.Player == null) return;

            // Siyah Maske kontrol et
            if (IsBlackMask(ev.Item))
            {
                MaskWearer = ev.Player;
                maskWearerHealth = MASK_MAX_HP;
                
                Log.Info($"{ev.Player.Nickname} SCP-035 (Siyah Maske) taktı!");
                
                // Görsel efekt - Siyah görünüş
                ev.Player.EnableEffect(EffectType.Ensnared, 2);
                ev.Player.EnableEffect(EffectType.Flashed, 1);
                
                // Broadcast mesajı
                ev.Player.Broadcast(5, "<color=black>███ SCP-035: MASKE SANA SAHİP ███</color>");
                
                // İsim değiştir
                ev.Player.DisplayNickname = $"[SCP-035] {ev.Player.Nickname}";
                
                // HP azalmasını başlat
                if (!isHealthDraining)
                {
                    Timing.RunCoroutine(DrainHealth());
                }
                
                ev.IsAllowed = false; // Item'i al
            }
        }

        // Eşya bıraktığında
        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (ev.Player == MaskWearer)
            {
                Log.Info($"{ev.Player.Nickname} SCP-035 maskayı çıkardı!");
                
                // İsim geri al
                ev.Player.DisplayNickname = ev.Player.Nickname;
                
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
                
                MaskWearer = null;
                isHealthDraining = false;
                
                ev.Player.Broadcast(5, "<color=red>Maskanın kontrolü altında öldün!</color>");
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

        // Maskayı başka bir oyuncuya takmak (Cesede basılırsa)
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

            Timing.RunCoroutine(DrainHealth());
        }

        // Siyah Maske kontrolü
        private bool IsBlackMask(Pickup item)
        {
            // Item adı "Black Mask" ya da "SCP-035" içeriyorsa
            if (item.DisplayName.Contains("Black Mask") || 
                item.DisplayName.Contains("SCP-035") ||
                item.DisplayName.Contains("Maske"))
            {
                return true;
            }
            
            // Ya da belirli bir ItemType ise
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
