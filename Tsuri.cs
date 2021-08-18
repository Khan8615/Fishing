using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using WebSocketSharp;
using Random = UnityEngine.Random;
using Oxide.Core.Plugins;
using Oxide.Plugins.TsuriEx;

/*
 * Credits to Colon Blow for the original work/idea
 */

/*
 * This is a re-write 2.0.0 / Work in Progress!
 * tsuri.allowed
 * tsuri.makepole
 * tsuri.repairpole
 * tsuri.unlimited
 * Added SkinId Support
 * Added Custom Name Support
 * Added Icon timer config option
 * Added Icon Position settings in config
 * Added Durability options on the fishing pole
 * Added a repairpole chat command that costs wood and amount
 * New chat command is /repairpole
 * Added sound affects/animation
 * Added PopupNotification Plugin Support
 * Added Currency Integration features
 * Added New permission fishing.unlimited ( lets you bypass the condition loss while fishing if enabled )
 * Had to repair a lot of the old code logic took days to debug it all..
 *
 * This Beta Update 2.0.01
 * Fixed Sound/Effect issues
 * Converted to a CovalencePlugin
 * Re-structured the code order to not be spaghetti
 * Removed Debug code
 * Fixed Compound Bow fuck up during re-write.
 *
 * This Beta Update 2.0.11
 * Fixed Pollution of oxide naming space
 * Added Image Library Support
 * Updated Image URL links
 * Removed more missed debug code.
 * Performance Improvements
 *
 * This Beta Update 2.0.12
 * This update fixes being able to fish anywhere issues.
 *
 * This update 2.0.13
 * Fixes Global broadcast issue when having popups enabled.
 *
 * This update 2.0.14
 * Fixed not being able to craft multiple poles with /makepole command + solved losing a stack of spears.
 * Added basic value updater feature to config.
 *
 * This update 2.0.15
 * Added Working Zlevels Support Patch/feature update pending on the Zelvels plugin.
 * Added Experimental PlayerChallenges Support
 * Added API system
 *
 * TODO
 * GUIAnnouncement Support, Add AutoUpdater functions.., Add a new chance option for getting like a bag of 5 coins + random item drop option.
 * Bug
 * Fish stacking issues. Stacking with any other fish skin in the same container breaks it. Appears to be a game bug relation with small.trout and different skins.
 *
 * New features
* You can now create as many fish as you want inside the config!
* The same goes for the Custom Time bonus chances!
* Same for the LootBoxCrates!
* Much more to be done/in the works
 */

namespace Oxide.Plugins
{
    [Info("Tsuri", "Khan", "2.0.15")]
    [Description("Adds fishing to the rust game Beta/Re-write work in progress! CS file is named Tsuri not Fishing!")]
    public class Tsuri : CovalencePlugin
    {
        #region Refrences
        
        [PluginReference] 
        private Plugin PopupNotifications, Economics, ServerRewards, ImageLibrary, ZLevelsRemastered, PlayerChallenges;

        #endregion

        #region Fields

        private readonly Dictionary<ulong, string> _gui = new Dictionary<ulong, string>();
        private static Tsuri _instance;
        private PluginConfig _config;
        private bool _isFishReady;

        #endregion

        #region Config
        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new Exception();
                }
                
                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("Loaded default config.");

                LoadDefaultConfig();
            }
        }
        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            #region Fields

            // Sounds Effects
            public string CatchSoundEffect;
            public string MissedSoundEffect;
            public string RepairSoundEffect;

            // UI Display crap
            public string ChatPrefix;
            public bool UsePopup;
            public bool UseFishIcons;
            public string DefaultFishIcon;
            public float IconDisplayTime;
            public string ShowFishCatchIconAnchorMin;
            public string ShowFishCatchIconAnchorMax;

            // Currency crap 
            public bool UseServerRewards;
            public bool UseEconomics;
            public bool UseCustom;
            public string CurrencyShortname;
            public ulong CurrencySkin;
            public string CurrencyName;

            // Loot Box Settings
            public bool UseRandomLootBox;
            public float LootBoxDespawnTime;

            // Fishing Pole Modifications
            public bool EnableSpearTimer;
            public float ReAttackTimer;
            public bool EnableCastTimer;
            public float TimeTillReCastPole;
            public bool EnableConditionLoss;
            public int RepairItemCost;
            public float MaxItemCondition;
            public float ConditionLossMax;
            public float ConditionLossMin;

            // Increase Chance Modifiers
            public int DefaultCatchChance;
            public bool WeaponModifier;
            public int WeaponModifierBonus;
            public bool AttireModifier;
            public int AttireModifierBonus;
            public int AttireBonousID;
            public bool ItemModifier;
            public int ItemModifierBonus;
            public int ItemBonusID;
            public bool TimeModifier;

            public List<FishConfig> FishConfigs;
            public List<LootBoxConfig> LootBoxConfigs;
            public List<TimeConfig> TimeConfigs;

            #endregion

            public FishConfig GetRandomFish()
            {
                List<FishConfig> fishs = new List<FishConfig>();

                foreach (FishConfig fishConfig in FishConfigs)
                {
                    if (Random.Range(1, 100) <= fishConfig.Rarity)
                        fishs.Add(fishConfig);
                }

                return fishs.Count > 0 ? fishs[0] : FishConfigs.GetRandom();
            }

            public LootBoxConfig GetRandomBox()
            {
                List<LootBoxConfig> boxes = new List<LootBoxConfig>();

                foreach (LootBoxConfig lootBoxConfig in LootBoxConfigs)
                {
                    if (Random.Range(1, 100) <= lootBoxConfig.Rarity)
                        boxes.Add(lootBoxConfig);
                }

                return boxes.Count > 0 ? boxes[0] : LootBoxConfigs.GetRandom();
            }
            
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    // Special Effects
                    CatchSoundEffect = "assets/unimplemented/fishing_rod/vm_fishing_rod/catch_fish.prefab",
                    MissedSoundEffect = "assets/unimplemented/fishing_rod/vm_fishing_rod/catch_empty.prefab",
                    RepairSoundEffect = "assets/bundled/prefabs/fx/repairbench/itemrepair.prefab",

                    // UI Display crap
                    ChatPrefix = "<color=#32CD32>Fishing</color>: ",
                    UsePopup = false,
                    UseFishIcons = true,
                    DefaultFishIcon = "https://i.imgur.com/rBEmhpg.png",
                    IconDisplayTime = 6f,
                    ShowFishCatchIconAnchorMin = "0.220 0.03",
                    ShowFishCatchIconAnchorMax = "0.260 0.10",

                    // Currency crap 
                    UseServerRewards = false,
                    UseEconomics = false,
                    UseCustom = false,
                    CurrencyShortname = "scrap",
                    CurrencySkin = 0UL,
                    CurrencyName = "",

                    // Loot Box Settings
                    UseRandomLootBox = true,
                    LootBoxDespawnTime = 200f,

                    // Fishing Pole Modifications
                    EnableSpearTimer = true,
                    ReAttackTimer = 6f,
                    EnableCastTimer = true,
                    TimeTillReCastPole = 6f,
                    EnableConditionLoss = true,
                    RepairItemCost = 10,
                    MaxItemCondition = 150f,
                    ConditionLossMax = 5f,
                    ConditionLossMin = 1f,

                    // Increase Chance Modifiers
                    DefaultCatchChance = 70,
                    WeaponModifier = false,
                    WeaponModifierBonus = 10,
                    AttireModifier = false,
                    AttireModifierBonus = 10,
                    AttireBonousID = -23994173,
                    ItemModifier = false,
                    ItemModifierBonus = 10,
                    ItemBonusID = -1651220691,
                    TimeModifier = false,

                    FishConfigs = new List<FishConfig>
                    {
                        new FishConfig
                        {
                            DisplayName = "Red Snapper",
                            Icon = "https://i.imgur.com/vvovLM2.png",
                            SkinID = 2489018062,
                            Amount = 10,
                            Currency = 10,
                            Rarity = 40
                        },
                        new FishConfig
                        {
                            DisplayName = "Bass",
                            Icon = "https://i.imgur.com/RYnkZbr.png",
                            SkinID = 2489017909,
                            Amount = 10,
                            Currency = 10,
                            Rarity = 30,
                        },
                        new FishConfig
                        {
                            DisplayName = "Alantic Cod",
                            Icon = "https://i.imgur.com/Y0H9VNF.png",
                            SkinID = 2489017761,
                            Amount = 10,
                            Currency = 10,
                            Rarity = 20
                        },
                        new FishConfig
                        {
                            DisplayName = "Medium Trout",
                            Icon = "https://i.imgur.com/JgNvQlm.png",
                            SkinID = 2489017627,
                            Amount = 10,
                            Currency = 10,
                            Rarity = 8
                        },
                        new FishConfig
                        {
                            DisplayName = "Khan Fish",
                            Icon = "https://i.imgur.com/MBiW2G7.png",
                            SkinID = 2489010628,
                            Amount = 10,
                            Currency = 10,
                            Rarity = 2
                        }
                    },
                    LootBoxConfigs = new List<LootBoxConfig>
                    {
                        new LootBoxConfig
                        {
                            DisplayName = "Common Crate",
                            Rarity = 40,
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                        },
                        new LootBoxConfig
                        {
                            DisplayName = "Second Common Crate",
                            Rarity = 30,
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        },
                        new LootBoxConfig
                        {
                            DisplayName = "Basic Crate",
                            Rarity = 20,
                            Prefab = "assets/bundled/prefabs/radtown/crate_basic.prefab",
                        },
                        new LootBoxConfig
                        {
                            DisplayName = "Mine Crate",
                            Rarity = 8,
                            Prefab = "assets/bundled/prefabs/radtown/crate_mine.prefab",
                        },
                        new LootBoxConfig
                        {
                            DisplayName = "Elite Crate",
                            Rarity = 2,
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                        }
                    },
                    TimeConfigs = new List<TimeConfig>
                    {
                        new TimeConfig
                        {
                            After = 0f,
                            Before = 4f,
                            CatchChanceBonous = 3,
                        },
                        new TimeConfig
                        {
                            After = 8f,
                            Before = 12f,
                            CatchChanceBonous = 5,
                        },
                        new TimeConfig
                        {
                            After = 16f,
                            Before = 20f,
                            CatchChanceBonous = 7,
                        },
                        new TimeConfig
                        {
                            After = 20f,
                            Before = 24f,
                            CatchChanceBonous = 10,
                        },
                    }
                };
            }
        }

        private class FishConfig
        {
            public string DisplayName;
            public string Icon;
            public ulong SkinID;
            public int Amount;
            public double Currency;
            public float Rarity;

            public bool GiveItem(BasePlayer player, int amount)
            {
                Item item = ItemManager.CreateByItemID(-1878764039, amount, SkinID);
                item.name = DisplayName;
                item.skin = SkinID;
                item.MarkDirty();

                return player.inventory.GiveItem(item);
            }
        }

        private class LootBoxConfig
        {
            public string DisplayName;
            public int Rarity;
            public string Prefab;
        }

        private class TimeConfig
        {
            public float After;
            public float Before;
            public int CatchChanceBonous;
        }

        #endregion

        #region Lang

        private string Msg(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private Dictionary<string, string> _messages = new Dictionary<string, string>()
        {
            ["missed"] = "Dang the fish got away...",
            ["notlookingatwater"] = "You must be aiming at water!",
            ["notstandinginwater"] = "You must be standing in water!",
            ["alreadyfishing"] = "You are already fishing dingus!",
            ["toosoon"] = "Please wait to try that again!",
            ["cantmove"] = "You must stay still while fishing!",
            ["wrongweapon"] = "You are not holding a fishing pole!",
            ["correctitem"] = "You must be holding a spear to make a fishing pole!",
            ["fishcaught"] = "You've reeled in {0}, {1}, \n rarity for this fish is {2}%",
            ["fishcaughtmoney"] = "You've reeled in {0}, {1}, \n Earned {2} coins \n Rarity {3}%",
            ["randomitem"] = "You found something in the water!?",
            ["chancetext1"] = "Your chance to catch a fish is : {0}",
            ["chancetext2"] = "at Current time of : ",
            ["NoItem"] = "You are not holding a fishing pole!",
            ["Repaired"] = "Your fishing pole has been repaired!",
            ["NotEnough"] = "You did not have enough wood {0} \n To repair your rod",
            ["AlreadyMax"] = "Is already at max repair ability!",
            ["Warning"] = "Warning total combined Catch chances cannot exceed 100%",
            ["ErrorName"] = "Aye fishy needs a name! {0}\n Please update config, you got nothing!",
            ["LootBox"] = "Hey! Your fishing line got caught on something!? \n {0}"
        };

        #endregion

        #region ImageLibrary

        private void LoadImages()
        {
            Dictionary<string, string> imageListFishes = new Dictionary<string, string>();

            List<KeyValuePair<string, ulong>> fishIcons = new List<KeyValuePair<string, ulong>>();

            foreach (FishConfig fishConfig in _config.FishConfigs)
            {
                if (!fishConfig.Icon.IsNullOrEmpty() && !imageListFishes.ContainsKey(fishConfig.Icon))
                {
                    imageListFishes.Add(fishConfig.Icon, fishConfig.Icon);
                }

                fishIcons.Add(new KeyValuePair<string, ulong>(fishConfig.DisplayName, fishConfig.SkinID));
            }

            imageListFishes.Add(_config.DefaultFishIcon, _config.DefaultFishIcon);

            if (fishIcons.Count > 0)
            {
                ImageLibrary?.Call("LoadImageList", Title, fishIcons, null);
            }

            ImageLibrary?.Call("ImportImageList", Title, imageListFishes, 0UL, true, new Action(FishReady));

            if (!ImageLibrary)
            {
                _isFishReady = true;
            }
        }

        private void FishReady()
        {
            _isFishReady = true;
        }

        #endregion

        #region Oxide Hooks

        private void Loaded()
        {
            _instance = this;
            lang.RegisterMessages(_messages, this);
            permission.RegisterPermission("tsuri.allowed", this);
            permission.RegisterPermission("tsuri.makepole", this);
            permission.RegisterPermission("tsuri.repairpole", this);
            permission.RegisterPermission("tsuri.unlimited", this);
        }

        private void OnServerInitialized()
        {
            //ItemManager.FindItemDefinition("fish.troutsmall").stackable = 500;
            LoadImages();
        }

        private void Unload()
        {
            DestroyAll<FishingControl>();
            DestroyAll<SpearFishingControl>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                string guiInfo;
                if (_gui.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
            }

            _instance = null;
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
            var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing) isfishing.OnDestroy();
            var hascooldown = player.GetComponent<SpearFishingControl>();
            if (hascooldown) hascooldown.OnDestroy();
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            var isfishing = player.GetComponent<FishingControl>();
            if (isfishing)
            {
                if (input.WasJustPressed(BUTTON.FORWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.BACKWARD)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.RIGHT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.LEFT)) isfishing.playermoved = true;
                if (input.WasJustPressed(BUTTON.JUMP)) isfishing.playermoved = true;
            }

            if (!UsingCastRod(player) || !input.WasJustPressed(BUTTON.FIRE_PRIMARY)) return;
            TakeRodDurability(player);
            if (ValidateCastFish(player)) ProcessCastFish(player);
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo == null) return;
            if (!IsAllowed(attacker, "tsuri.allowed")) return;
            if (HasFishingCooldown(attacker))
            {
                PopupMessageArgs(attacker, "toosoon");
                return;
            }

            if (!UsingSpearRod(hitInfo)) return;
            if (LookingAtWater(attacker) || attacker.IsHeadUnderwater())
            {
                ProcessSpearFish(attacker, hitInfo);
            }
        }

        #endregion

        #region Craft Fishing Pole

        private void MakeFishingPole(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            int amount = activeItem.amount;
            if (activeItem != null && activeItem.info.shortname.Contains("spear"))
            {
                activeItem.Remove();
                ulong skinid = 1393234529;
                if (activeItem.info.shortname == "spear.stone") skinid = 1393231089;
                Item pole = ItemManager.CreateByItemID(1569882109, amount, skinid);
                pole.info.condition.enabled = true;
                pole.maxCondition = _config.MaxItemCondition;
                pole.condition = _config.MaxItemCondition;
                pole.MarkDirty();
                if (!player.inventory.GiveItem(pole))
                {
                    pole.Drop(player.eyes.position, Vector3.forward);
                    ShowGameTip(player, "No Room in Inventory, Dropped New Fishing Pole !!", 5f);
                    return;
                }

                ShowGameTip(player, "New Fishing Pole Placed in your Inventory !!", 5f);
                return;
            }

            ShowGameTip(player, "correctitem", 5f);
        }

        #endregion

        #region Cast Fishing Process

        private bool ValidateCastFish(BasePlayer player)
        {
            if (IsFishing(player))
            {
                PopupMessageArgs(player, "alreadyfishing");
                return false;
            }

            if (HasFishingCooldown(player))
            {
                PopupMessageArgs(player, "toosoon");
                return false;
            }

            if (LookingAtWater(player)) return true;
            PopupMessageArgs(player, "notlookingatwater");
            return false;
        }

        private void ProcessCastFish(BasePlayer player)
        {
            Vector3 castPointSpawn = new Vector3();
            RaycastHit castPointHit;
            if (Physics.Raycast(player.eyes.HeadRay(), out castPointHit, 50f, LayerMask.GetMask("Water")))
                castPointSpawn = castPointHit.point;
            var addfishing = player.gameObject.AddComponent<FishingControl>();
            addfishing.SpawnBobber(castPointSpawn);
        }

        #endregion

        #region Cast Fishing Control

        private class FishingControl : MonoBehaviour
        {
            private BasePlayer _player;
            public string anchormaxstr;
            public float counter;
            private BaseEntity _bobber;
            public bool playermoved;
            private Vector3 _bobberpos;

            private void Awake()
            {
                _player = GetComponentInParent<BasePlayer>();
                counter = _instance._config.TimeTillReCastPole;
                if (!_instance._config.EnableCastTimer || counter < 0.1f) counter = 0.1f;
                // playermoved = false;
            }

            /*class Bobbering : MonoBehaviour 
            {
                
            }*/
            public void SpawnBobber(Vector3 pos)
            {
                float waterheight = TerrainMeta.WaterMap.GetHeight(pos);

                pos = new Vector3(pos.x, waterheight, pos.z);
                var createdPrefab =
                    GameManager.server.CreateEntity("assets/prefabs/tools/fishing rod/bobber/bobber.prefab", pos,
                        Quaternion.identity);
                _bobber = createdPrefab?.GetComponent<BaseEntity>();
                _bobber.enableSaving = false;
                _bobber.transform.eulerAngles = new Vector3(270, 0, 0);
                _bobber?.Spawn();
                _bobberpos = _bobber.transform.position;
            }

            private void FixedUpdate()
            {
                _bobberpos = _bobber.transform.position;
                if (counter <= 0f)
                {
                    RollForFish(_bobberpos);
                    return;
                }

                if (playermoved)
                {
                    PlayerMoved();
                    return;
                }

                counter = counter - 0.1f;
                Fishingindicator(_player, counter);
            }

            private void PlayerMoved()
            {
                if (_bobber != null && !_bobber.IsDestroyed)
                {
                    _bobber.Kill();
                }

                _instance.PopupMessageArgs(_player, "cantmove");
                OnDestroy();
            }

            private void RollForFish(Vector3 pos)
            {
                if (_player != null) _instance.FishChanceRoll(_player, pos);
                if (_bobber != null && !_bobber.IsDestroyed)
                {
                    _bobber.Kill();
                }

                OnDestroy();
            }

            private string GetGUIString(float counter)
            {
                string guistring = "0.60 0.145";
                var getrefreshtime = _instance._config.TimeTillReCastPole;
                if (counter < getrefreshtime * 0.1)
                {
                    guistring = "0.42 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.2)
                {
                    guistring = "0.44 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.3)
                {
                    guistring = "0.46 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.4)
                {
                    guistring = "0.48 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.5)
                {
                    guistring = "0.50 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.6)
                {
                    guistring = "0.52 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.7)
                {
                    guistring = "0.54 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.8)
                {
                    guistring = "0.56 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.9)
                {
                    guistring = "0.58 0.145";
                    return guistring;
                }

                return guistring;
            }

            public void Fishingindicator(BasePlayer player, float counter)
            {
                DestroyCui(player);
                string anchormaxstr = GetGUIString(counter);
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = {Command = $"", Color = "0.0 0.0 1.0 0.6"},
                    RectTransform = {AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr},
                    Text = {Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight}
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            private void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(_player);
                Destroy(this);
            }
        }

        #endregion

        #region Spear Fishing Process

        private void ProcessSpearFish(BasePlayer attacker, HitInfo hitInfo)
        {
            if (_config.EnableConditionLoss)
            {
                float maxloss = 1f - (_config.ConditionLossMax / 100f);
                float minloss = 1f - (_config.ConditionLossMin / 100f);
                Item activeItem = attacker.GetActiveItem();
                if (activeItem != null) activeItem.condition *= Random.Range(minloss, maxloss);
            }

            if (attacker != null && hitInfo.HitPositionWorld != null)
                FishChanceRoll(attacker, hitInfo.HitPositionWorld);
            if (_config.EnableSpearTimer) attacker.gameObject.AddComponent<SpearFishingControl>();
        }

        #endregion

        #region Spear Fishing Control

        private class SpearFishingControl : MonoBehaviour
        {
            private BasePlayer _player;

            //public string anchormaxstr;
            public float counter;

            private void Awake()
            {
                _player = GetComponentInParent<BasePlayer>();
                counter = _instance._config.ReAttackTimer;
                if (!_instance._config.EnableSpearTimer || counter < 0.1f) counter = 0.1f;
            }

            private void FixedUpdate()
            {
                if (counter <= 0f) OnDestroy();
                counter = counter - 0.1f;
                Fishingindicator(_player, counter);
            }

            private string GetGUIString(float counter)
            {
                string guistring = "0.60 0.145";
                var getrefreshtime = _instance._config.ReAttackTimer;
                if (counter < getrefreshtime * 0.1)
                {
                    guistring = "0.42 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.2)
                {
                    guistring = "0.44 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.3)
                {
                    guistring = "0.46 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.4)
                {
                    guistring = "0.48 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.5)
                {
                    guistring = "0.50 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.6)
                {
                    guistring = "0.52 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.7)
                {
                    guistring = "0.54 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.8)
                {
                    guistring = "0.56 0.145";
                    return guistring;
                }

                if (counter < getrefreshtime * 0.9)
                {
                    guistring = "0.58 0.145";
                    return guistring;
                }

                return guistring;
            }

            public void Fishingindicator(BasePlayer player, float counter)
            {
                DestroyCui(player);
                string anchormaxstr = GetGUIString(counter);
                var fishingindicator = new CuiElementContainer();

                fishingindicator.Add(new CuiButton
                {
                    Button = {Command = $"", Color = "1.0 0.0 0.0 0.6"},
                    RectTransform = {AnchorMin = "0.40 0.125", AnchorMax = anchormaxstr},
                    Text = {Text = (""), FontSize = 14, Color = "1.0 1.0 1.0 0.6", Align = TextAnchor.MiddleRight}
                }, "Overall", "FishingGui");
                CuiHelper.AddUi(player, fishingindicator);
            }

            private void DestroyCui(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "FishingGui");
            }

            public void OnDestroy()
            {
                DestroyCui(_player);
                Destroy(this);
            }
        }

        #endregion

        #region Fishing Checks

        private bool IsFishing(BasePlayer baseplayer)
        {
            var isfishing = baseplayer.GetComponent<FishingControl>();
            return isfishing;
        }

        private bool HasFishingCooldown(BasePlayer baseplayer)
        {
            var incooldown = baseplayer.GetComponent<SpearFishingControl>();
            return incooldown;
        }
        
        private bool UsingCastRod(BasePlayer player)
        {
            Item activeItem = player.GetActiveItem();
            return activeItem != null && activeItem.info.shortname.Contains("fishingrod.handmade");
        }

        private void TakeRodDurability(BasePlayer player)
        {
            if (!_config.EnableConditionLoss) return;
            if (IsAllowed(player, "tsuri.unlimited")) return;
            Item item = player.GetActiveItem();
            item?.LoseCondition(Random.Range(_config.ConditionLossMin, _config.ConditionLossMax));
        }

        private bool UsingSpearRod(HitInfo hitInfo)
        {
            return hitInfo.WeaponPrefab.ToString().Contains("spear") || hitInfo.WeaponPrefab.ToString().Contains("bow");
        }

        private bool LookingAtWater(BasePlayer player) // TODO fix being able to fish through player floor/building
        {
            float waterHitDistance = 0;
            float groundHitDistance = 100f;
            
            Ray ray = new Ray(player.eyes.position, player.eyes.HeadForward());

            var rayHitWaterLayer = Physics.RaycastAll(ray, 25f, LayerMask.GetMask("Water"));
            var rayHitGroundLayer = Physics.RaycastAll(ray, 25f, LayerMask.GetMask("Terrain", "World", "Construction", "Building"));

            foreach (var hit in rayHitWaterLayer)
            {
                waterHitDistance = hit.distance;
            }
            foreach (var hit in rayHitGroundLayer)
            {
                groundHitDistance = hit.distance;
            }
            //if (waterHitDistance > 0 && groundHitDistance == null) return true;
            if (waterHitDistance < groundHitDistance && waterHitDistance > 0) return true;
            return false;
        }

        #endregion

        #region Fish Catch Process

        public void FishChanceRoll(BasePlayer player, Vector3 hitloc)
        {
            int totalchance = CatchFishModifier(player);
            if (totalchance > 100)
            {
                PopupMessageArgs(player, "Warning");
                return;
            }

            int roll = Random.Range(1, 100);

            if (roll <= totalchance)
            {
                if (_config.UseRandomLootBox && RollBoxOrFish())
                {
                    SpawnLootBox(player, hitloc);
                    return;
                }

                FishTypeRoll(player);
                return;
            }

            PopupMessageArgs(player, "missed");
            player.RunEffect(_config.MissedSoundEffect);
        }

        private void FishTypeRoll(BasePlayer player)
        {
            FishConfig fishConfig = _config.GetRandomFish();

            if (CurrencyEnabled())
            {
                fishConfig.GiveItem(player, fishConfig.Amount); // Bug fix fish stacking issue in player inventory.
                AddFishingPoints(player, fishConfig.Amount);
                AddCurrency(player, fishConfig.Currency);
                CatchFishCui(player, fishConfig.Icon, fishConfig.DisplayName, fishConfig.SkinID);
                PopupMessageArgs(player, "fishcaughtmoney", fishConfig.Amount, fishConfig.DisplayName, fishConfig.Currency, fishConfig.Rarity);
                player.RunEffect(_config.CatchSoundEffect);
                player.Command("note.inv", -1878764039, fishConfig.Amount, fishConfig.DisplayName);
                return;
            }

            fishConfig.GiveItem(player, fishConfig.Amount);
            AddFishingPoints(player, fishConfig.Amount);
            CatchFishCui(player, fishConfig.Icon, fishConfig.DisplayName, fishConfig.SkinID);
            PopupMessageArgs(player, "fishcaught", fishConfig.Amount, fishConfig.DisplayName, fishConfig.Rarity);
            player.RunEffect(_config.CatchSoundEffect);
            player.Command("note.inv", -1878764039, fishConfig.Amount, fishConfig.DisplayName);
        }

        // Fishing Pole highest catch chance
        // Bows Second best (since 1 try per shot )
        // Spears lowest chance cuss you can spam click them..
        private int CatchFishModifier(BasePlayer player) // TODO
        {
            int chances = new int();
            int fishingLevel = 1;

            if (_config.WeaponModifier)
            {
                Item activeItem = player.GetActiveItem();
                if (activeItem != null)
                {
                    if (activeItem.info.shortname == "spear.stone" || activeItem.skin == 1393231089 || activeItem.info.shortname == "crossbow" || activeItem.info.shortname == "bow.compound")
                    {
                        chances += _config.WeaponModifierBonus;
                    }
                }
            }

            if (_config.AttireModifier)
            {
                int hasBoonieOn = player.inventory.containerWear.GetAmount(_config.AttireBonousID, true);
                if (hasBoonieOn > 0) chances += _config.AttireModifierBonus;
            }

            if (_config.ItemModifier)
            {
                int hasPookie = player.inventory.containerMain.GetAmount(_config.ItemBonusID, true);
                if (hasPookie > 0) chances += _config.ItemModifierBonus;
            }

            if (_config.TimeModifier)
            {
                var currenttime = TOD_Sky.Instance.Cycle.Hour;
                TimeConfig timeConfig =
                    _config.TimeConfigs.FirstOrDefault(x => currenttime < x.Before && currenttime > x.After);

                if (timeConfig != null)
                {
                    chances += timeConfig.CatchChanceBonous;
                }
            }
            
            if (ZLevelsRemastered)
            {
                fishingLevel = Convert.ToInt32(ZLevelsRemastered.Call("getLevel", player.userID, "FH") ?? 1);
                chances += fishingLevel * chances + _config.DefaultCatchChance;
                return chances;
            }

            int totalchance = chances + _config.DefaultCatchChance;
            return totalchance;
        }

        #endregion

        #region Fish LootBox Process

        private bool RollBoxOrFish()
        {
            var roll = Random.Range(1, 10);
            return roll == 1;
        }

        private void SpawnLootBox(BasePlayer player, Vector3 hitloc)
        {
            int amount = 1;
            AddFishingPoints(player, amount);
            LootBoxConfig lootBoxConfigs = _config.GetRandomBox();
            var createdPrefab = GameManager.server.CreateEntity(lootBoxConfigs.Prefab, hitloc);
            _instance.PopupMessageArgs(player, "LootBox", lootBoxConfigs.DisplayName);
            BaseEntity treasurebox = createdPrefab?.GetComponent<BaseEntity>();
            treasurebox.enableSaving = false;
            treasurebox?.Spawn();
            timer.Once(_config.LootBoxDespawnTime, () => CheckTreasureDespawn(treasurebox));
        }

        private void CheckTreasureDespawn(BaseEntity treasurebox)
        {
            if (treasurebox != null) treasurebox.Kill();
        }

        #endregion

        #region Currency

        public int PriceRounder(double amount)
        {
            if (amount <= 0.5)
            {
                return (int) Math.Ceiling(amount);
            }

            return (int) Math.Round(amount);
        }

        private void AddCurrency(BasePlayer player, double amount)
        {
            if (amount <= 0)
            {
                PopupMessageArgs(player,
                    "Warning currency amount for this fish is not set in the config! \n amount received is 0");
                return;
            }

            if (_config.UseEconomics)
            {
                Economics?.Call("Deposit", player.UserIDString, amount);
                return;
            }

            if (_config.UseServerRewards)
            {
                ServerRewards?.Call("AddPoints", player.UserIDString, PriceRounder(amount));
                return;
            }

            if (!_config.UseCustom) return;
            Item currency =
                ItemManager.CreateByName(_config.CurrencyShortname, PriceRounder(amount), _config.CurrencySkin);
            if (!_config.CurrencyName.IsNullOrEmpty())
            {
                currency.name = _config.CurrencyName;
                currency.MarkDirty();
            }

            player.GiveItem(currency);
        }

        private bool CurrencyEnabled()
        {
            return _config.UseEconomics || _config.UseServerRewards || _config.UseCustom;
        }

        #endregion

        #region GUI

        private void CatchFishCui(BasePlayer player, string fishicon, string name, ulong id)
        {
            if (_config.UseFishIcons)
                if (!fishicon.IsNullOrEmpty())
                {
                    fishicon = (string) ImageLibrary?.Call("GetImage", fishicon, 0UL, false);
                }
                else if ((bool) (ImageLibrary?.Call("HasImage", name, id) ?? false))
                {
                    fishicon = (string) ImageLibrary?.Call("GetImage", name, id, false);
                }
                else
                {
                    fishicon = (string) ImageLibrary?.Call("GetImage", _config.DefaultFishIcon);
                }
            FishingGui(player, fishicon);
        }

        private void FishingGui(BasePlayer player, string fishicon)
        {
            DestroyCui(player);

            var elements = new CuiElementContainer();
            _gui[player.userID] = CuiHelper.GetGuid();

            if (_config.UseFishIcons)
            {
                elements.Add(new CuiElement
                {
                    Name = _gui[player.userID],
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            //Color = "0 0 0 0.8",
                            Png = fishicon,
                            /*Color = "1 1 1 1", Url = fishicon,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga"*/
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = _config.ShowFishCatchIconAnchorMin,
                            AnchorMax = _config.ShowFishCatchIconAnchorMax
                        }
                    }
                });
            }

            CuiHelper.AddUi(player, elements);
            timer.Once(_config.IconDisplayTime, () => DestroyCui(player));
        }

        private void DestroyCui(BasePlayer player)
        {
            string guiInfo;
            if (_gui.TryGetValue(player.userID, out guiInfo)) CuiHelper.DestroyUi(player, guiInfo);
        }

        #endregion

        #region Command

        [Command("castpole")]
        private void CmdChatCastFishingPole(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.ToBasePlayer();
            if (!_isFishReady)
            {
                PopupMessageArgs(player, "ImageLibrary is not ready yet please wait");
                return;
            }
            if (!IsAllowed(player, "tsuri.allowed")) return;
            if (ValidateCastFish(player)) ProcessCastFish(player);
        }

        [Command("repairpole")]
        private void CmdChatReapirPole(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.ToBasePlayer();
            if (!IsAllowed(player, "tsuri.repairpole")) return;
            Item item = player.GetActiveItem();
            if (item == null || item.info.itemid != 1569882109)
            {
                PopupMessageArgs(player, "NoItem");
                return;
            }

            if (item.condition >= _config.MaxItemCondition)
            {
                PopupMessageArgs(player, "AlreadyMax");
                return;
            }

            if (player.inventory.GetAmount(-151838493) < _config.RepairItemCost)
            {
                PopupMessageArgs(player, "NotEnough", _config.RepairItemCost);
                return;
            }

            item.DoRepair(0.1f);
            player.RunEffect(_config.RepairSoundEffect);
            player.inventory.Take(null, -151838493, _config.RepairItemCost);
            PopupMessageArgs(player, "Repaired");
        }

        [Command("fishchance")]
        private void CmdChatFishChance(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.ToBasePlayer();
            int catchchance = CatchFishModifier(player);
            PopupMessageArgs(player, "chancetext1", catchchance + "%\n");
            ProcessCastFish(player);
        }

        [Command("makepole")]
        private void CmdChatMakeFishingPole(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player = iplayer.ToBasePlayer();
            if (!_isFishReady)
            {
                PopupMessageArgs(player, "ImageLibrary is not ready yet please wait");
                return;
            }
            if (!IsAllowed(player, "tsuri.makepole")) return;
            MakeFishingPole(player);
        }

        [ConsoleCommand("castpole")]
        private void CmdConsoleCastFishingPole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player == null) return;
            if (!_isFishReady)
            {
                PopupMessageArgs(player, "ImageLibrary is not ready yet please wait");
                return;
            }
            if (!IsAllowed(player, "tsuri.allowed")) return;
            if (ValidateCastFish(player)) ProcessCastFish(player);
        }

        [ConsoleCommand("makepole")]
        private void CmdConsoleMakeFishingPole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player() ?? null;
            if (player == null) return;
            if (!_isFishReady)
            {
                PopupMessageArgs(player, "ImageLibrary is not ready yet please wait");
                return;
            }
            if (!IsAllowed(player, "tsuri.makepole")) return;
            MakeFishingPole(player);
        }

        #endregion

        #region API

        private void AddFishingPoints(BasePlayer player, int fish)
        {
            if (PlayerChallenges)
            {
                PlayerChallenges.Call("OnFishCaught", player, fish);
            }
            if (ZLevelsRemastered)
            {
                ZLevelsRemastered.Call("fishingHandler", player, fish);
            }
        }

        #endregion

        #region Misc Helpers

        private void ShowGameTip(BasePlayer player, string message, float time)
        {
            player.SendConsoleCommand("gametip.showgametip", message);
            player.Invoke(() =>
            {
                if (player == null) return;

                player.SendConsoleCommand("gametip.hidegametip");
            }, time);
        }

        public void PopupMessageArgs(BasePlayer player, string key, params object[] args)
        {
            if (_config.UsePopup)
            {
                PopupNotifications?.Call("CreatePopupNotification", _config.ChatPrefix + Msg(key, player.UserIDString, args), player);
            }
            else
            {
                player.ChatMessage(_config.ChatPrefix + Msg(key, player.UserIDString, args));
            }
        }

        private bool IsAllowed(BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.userID.ToString(), perm);
        }

        private void DestroyAll<T>()
        {
            var objects = GameObject.FindObjectsOfType(typeof(T));
            if (objects == null) return;
            foreach (var gameObj in objects)
                GameObject.Destroy(gameObj);
        }

        #endregion
    }

    #region Extension Methods

    namespace TsuriEx
    {
        public static class PlayerEx
        {
            public static BasePlayer ToBasePlayer(this IPlayer player)
            {
                return player?.Object as BasePlayer;
            }

            public static void RunEffect(this BasePlayer player, string prefab)
            {
                Effect effect = new Effect();
                effect.Init(Effect.Type.Generic, player.ServerPosition, Vector3.zero);
                effect.pooledString = prefab;
                EffectNetwork.Send(effect, player.Connection);
            }
        }
    }    

    #endregion
}