using System;
using System.IO;
using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PinewardensOath
{
    [BepInPlugin(Guid, "Pinewarden's Oath", "1.0.2")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("Therzie.Warfare", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jellydotexe.pinewardensoath";
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Localization.Register();
            PrefabManager.OnVanillaPrefabsAvailable += AddItems;
        }

        private void AddItems()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddItems;
            string root = Path.Combine(Path.GetDirectoryName(Info.Location), "assets");
            foreach (var w in Weapons.All)
            {
                try { Add(w, root); }
                catch (Exception e) { Log.LogError("Failed to register " + w.Prefab + ": " + e); }
            }
        }

        // Warfare gives its war pikes the Juggernaut (Piercer) on-hit effect by prefab name; add ours to that list.
        private static void RegisterJuggernaut(string prefab)
        {
            try
            {
                var t = HarmonyLib.AccessTools.TypeByName("Warfare_StatusEffects.StatusEffects.Juggernaut");
                var m = t == null ? null : HarmonyLib.AccessTools.Method(t, "AddToItem", new[] { typeof(string), typeof(int) });
                if (m == null) { Log.LogWarning("Warfare Juggernaut not found; " + prefab + " gets no Piercer effect"); return; }
                m.Invoke(null, new object[] { prefab, 100 });
                Log.LogInfo(prefab + " registered with Warfare's Juggernaut effect");
            }
            catch (Exception e) { Log.LogWarning("Could not register " + prefab + " with Warfare Juggernaut: " + e.GetBaseException().Message); }
        }

        // The thrown spear is a separate projectile prefab with its own copy of the vanilla model: clone it and show ours.
        private static void ReskinProjectile(CustomItem custom, WeaponDef w)
        {
            var shared = custom.ItemDrop.m_itemData.m_shared;
            var vanilla = shared.m_secondaryAttack.m_attackProjectile;
            if (vanilla == null) { Log.LogWarning(w.Prefab + ": no thrown projectile to reskin"); return; }
            var clone = PrefabManager.Instance.CreateClonedPrefab(w.Prefab + "_projectile", vanilla);
            if (!Visuals.ApplyProjectile(clone, custom.ItemPrefab, w)) return;
            PrefabManager.Instance.AddPrefab(clone);
            shared.m_secondaryAttack.m_attackProjectile = clone;
            Log.LogInfo(w.Prefab + ": thrown projectile " + clone.name + " shows the custom model");
        }

        private static void Add(WeaponDef w, string root)
        {
            var config = new ItemConfig
            {
                Name = Localization.NameToken(w),
                Description = Localization.DescToken(w),
                CraftingStation = CraftingStations.Forge,
                MinStationLevel = w.StationLevel,
            };
            foreach (var (item, amount, perLevel) in w.Recipe) config.AddRequirement(item, amount, perLevel);

            string baseName = w.CloneFrom;
            if (PrefabManager.Instance.GetPrefab(baseName) == null)
            {
                if (w.Fallback == null) throw new Exception("base prefab " + baseName + " not found");
                Log.LogWarning(w.Prefab + ": " + baseName + " not found (mod not installed?), using " + w.Fallback);
                baseName = w.Fallback;
            }
            var custom = new CustomItem(w.Prefab, baseName, config);
            var shared = custom.ItemDrop.m_itemData.m_shared;
            w.Stats(shared);
            if (w.StaminaFactor != 1f)
            {   // two-handers cost a little less stamina
                shared.m_attack.m_attackStamina *= w.StaminaFactor;
                shared.m_secondaryAttack.m_attackStamina *= w.StaminaFactor;
            }

            bool model = Visuals.Apply(custom.ItemPrefab, Path.Combine(root, w.Asset), w);
            if (model && w.ThrownModel) ReskinProjectile(custom, w);
            ItemManager.Instance.AddItem(custom);
            if (model)
                RenderManager.Instance.EnqueueRender(new RenderManager.RenderRequest(custom.ItemPrefab)
                {
                    Rotation = Visuals.IconRotation(custom.ItemPrefab),
                    Width = 128, Height = 128,
                }, sprite => { if (sprite != null) shared.m_icons = new[] { sprite }; });
            if (w.WarfareJuggernaut != null && baseName == w.CloneFrom) RegisterJuggernaut(w.Prefab);
            Log.LogInfo("Registered " + w.Prefab + (model ? " with custom model" : " with " + w.CloneFrom + " visuals (fallback)"));
        }
    }
}
