using System;
using System.Collections.Generic;

namespace PinewardensOath
{
    // Pinewarden's Oath, the Black Forest set. Each item clones a vanilla (or Therzie Warfare) weapon for its moveset,
    // sounds and skill, then sets its own damage. Target: clearly better than bronze, below iron and the Swamp set.
    // Every piece adds a small fire bonus: the Elder and the greydwarfs burn.
    internal sealed class WeaponDef
    {
        public string Prefab, CloneFrom, Fallback, Asset, Name, Description;
        public float HeadFraction = 0.3f;           // top part of the model used for the swing trail
        public bool FlipBlade;                      // rotate 180 degrees around the haft if the blade faces the wrong way (shield: turn it right side up)
        public float ShieldRoll;                    // degrees to turn a shield's face in its own plane (round shields have no long axis to line up)
        public bool IsShield;                       // round shield: fitted to the vanilla shield by size and facing, no haft
        public string WarfareJuggernaut;            // register with Therzie Warfare's Juggernaut (Piercer) effect, like its own war pikes
        public bool FlipHead;                       // the head goes on the end nearer the hand (vanilla spears are held near the middle)
        public bool ThrownModel;                    // the thrown projectile (spear) shows our model too
        public float StaminaFactor = 1f;            // attack stamina multiplier (two-handers: 0.9)
        public Action<ItemDrop.ItemData.SharedData> Stats;
        public (string item, int amount, int perLevel)[] Recipe;
        public int StationLevel = 2;
    }

    internal static class Weapons
    {
        internal const int Fire = 5, FirePerLevel = 1;

        // Vanilla numbers for reference (dumped from the game 2026-11-14):
        //   Bronze Mace blunt 35 | Bronze Sword slash 35 | Bronze Axe slash 40 chop 40 | Bronze Atgeir pierce 45
        //   Bronze Spear pierce 35 | Copper Knife 12/12 | Bronze Buckler block 16 | Finewood Bow pierce 32
        //   Iron: Mace 55 | Sword 55 | Axe 60 | Atgeir 65 | Sledge 55 | Elderbark Spear 55 | Banded Shield block 42 | Huntsman Bow 42
        //   Warfare Elder War Pike pierce 52 + spirit 10 (we drop the spirit: this set's element is fire)
        //   Elder and all greydwarfs are VeryWeak to fire, skeletons Weak, trolls Normal, draugr Resistant.
        private static (string, int, int)[] Std(int bronze, int core, int hide) => new[]
        {
            ("Bronze", bronze, bronze / 2), ("RoundLog", core, core / 2), ("TrollHide", hide, 1), ("AncientSeed", 1, 0)
        };

        private static void Burn(ItemDrop.ItemData.SharedData s)
        {
            s.m_damages.m_fire = Fire;
            s.m_damagesPerLevel.m_fire = FirePerLevel;
        }

        internal static readonly List<WeaponDef> All = new List<WeaponDef>
        {
            new WeaponDef
            {
                Prefab = "MacePinewarden", CloneFrom = "MaceBronze", Asset = "bf_mace", HeadFraction = 0.22f,
                Name = "Conekeeper",
                Description = "The warden carried the forest’s unopened winter in his fist.",
                Stats = s => { s.m_damages.m_blunt = 42; Burn(s); },
                Recipe = Std(10, 6, 2),
            },
            new WeaponDef
            {
                Prefab = "SwordPinewarden", CloneFrom = "SwordBronze", Asset = "bf_sword", HeadFraction = 0.75f,
                Name = "Needle of the Watch",
                Description = "A narrow path through roots is still a path.",
                Stats = s => { s.m_damages.m_slash = 42; Burn(s); },
                Recipe = Std(10, 4, 2),
            },
            new WeaponDef
            {
                // chop and tool tier stay the Bronze Axe's: it fells trees like a Black Forest axe
                Prefab = "AxePinewarden", CloneFrom = "AxeBronze", Asset = "bf_axe", HeadFraction = 0.3f,   // no FlipBlade: this Tripo axe already rests edge-down like the Bronze Axe
                Name = "Rootcleaver",
                Description = "Its first duty was to fell the trees that would not sleep.",
                Stats = s => { s.m_damages.m_slash = 46; Burn(s); },
                Recipe = Std(10, 6, 2),
            },
            new WeaponDef
            {
                // Warfare's Elder War Pike gives the war pike moveset; without Warfare it falls back to the Bronze Atgeir
                Prefab = "PikePinewarden", CloneFrom = "WarpikeElder_TW", Fallback = "AtgeirBronze", WarfareJuggernaut = "yes",
                Asset = "bf_pike", HeadFraction = 0.22f, StaminaFactor = 0.9f,
                Name = "Towerroot",
                Description = "Raised at the ruined gate, it taught the brutes to keep their distance.",
                Stats = s =>
                {
                    s.m_damages.m_pierce = 46; s.m_damages.m_spirit = 0; s.m_damagesPerLevel.m_spirit = 0; Burn(s);
                },
                Recipe = Std(14, 12, 3),
            },
            new WeaponDef
            {
                Prefab = "SledgePinewarden", CloneFrom = "SledgeIron", Asset = "bf_sledge", HeadFraction = 0.23f, StaminaFactor = 0.9f, FlipBlade = true,   // user, in-game: the knot head hung upside down
                Name = "Elder’s Knuckle",
                Description = "When the stones refused the roots, the warden answered.",
                Stats = s => { s.m_damages.m_blunt = 46; Burn(s); },
                Recipe = Std(16, 14, 3),
            },
            new WeaponDef
            {
                Prefab = "AtgeirPinewarden", CloneFrom = "AtgeirBronze", Asset = "bf_atgeir", HeadFraction = 0.27f, StaminaFactor = 0.9f,
                Name = "Boughreaper",
                Description = "The forest’s reaching branch learned an edge.",
                Stats = s => { s.m_damages.m_pierce = 52; Burn(s); },
                Recipe = Std(14, 12, 3),
            },
            new WeaponDef
            {
                Prefab = "ShieldPinewarden", CloneFrom = "ShieldBanded", Asset = "bf_shield", IsShield = true, ShieldRoll = 90f,
                Name = "Seedfast",
                Description = "Seven winters of falling branches could not open it.",
                Stats = s => { s.m_blockPower = 28; s.m_blockPowerPerLevel = 6; },   // Bronze Buckler 16 (+6), Banded Shield 42
                Recipe = Std(10, 10, 3),
            },
            new WeaponDef
            {
                Prefab = "KnifePinewarden", CloneFrom = "KnifeCopper", Asset = "bf_dagger", HeadFraction = 0.55f,
                Name = "Pathfinder’s Thorn",
                Description = "Yrsa kept the last clear path within arm’s reach.",
                Stats = s => { s.m_damages.m_slash = 14; s.m_damages.m_pierce = 14; Burn(s); },
                Recipe = Std(6, 2, 1),
            },
            new WeaponDef
            {
                Prefab = "SpearPinewarden", CloneFrom = "SpearBronze", Asset = "bf_spear", HeadFraction = 0.15f, ThrownModel = true, FlipHead = true,
                Name = "Farwatch",
                Description = "The watchtower fell; its reach did not.",
                Stats = s => { s.m_damages.m_pierce = 42; Burn(s); },
                Recipe = Std(8, 8, 2),
            },
            new WeaponDef
            {
                // the Finewood Bow is a rigid mesh like the Crude Bow, so ours swaps in like any other weapon
                Prefab = "BowPinewarden", CloneFrom = "BowFineWood", Asset = "bf_bow", HeadFraction = 0.5f, FlipBlade = true,
                Name = "Stillbough",
                Description = "She bent the quiet wood and listened for an answer.",
                Stats = s => { s.m_damages.m_pierce = 36; Burn(s); },
                Recipe = Std(6, 12, 3),
            },
        };
    }
}
