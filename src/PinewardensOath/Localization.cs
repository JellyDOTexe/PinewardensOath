using System.Collections.Generic;
using Jotunn.Managers;

namespace PinewardensOath
{
    internal static class Localization
    {
        internal static string NameToken(WeaponDef w) => "$item_" + w.Prefab.ToLowerInvariant();
        internal static string DescToken(WeaponDef w) => "$item_" + w.Prefab.ToLowerInvariant() + "_description";

        internal static void Register()
        {
            var en = new Dictionary<string, string>();
            foreach (var w in Weapons.All)
            {
                en[NameToken(w).Substring(1)] = w.Name;
                en[DescToken(w).Substring(1)] = w.Description;
            }
            LocalizationManager.Instance.AddLocalization("English", en);
        }
    }
}
