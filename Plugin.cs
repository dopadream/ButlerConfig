using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace ButlerSettings
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(LETHAL_CONFIG, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "dopadream.lethalcompany.ButlerSettings", PLUGIN_NAME = "Butler Settings", PLUGIN_VERSION = "1.0.0", LETHAL_CONFIG = "ainavt.lc.lethalconfig";
        internal static new ManualLogSource Logger;
        internal static ConfigEntry<int> configMaxCount, configPowerLevel, configDamage, configHealthMultiplayer, configHealthSingleplayer;
        internal static ConfigEntry<float> configAttackCooldown;

        internal void initLethalConfig()
        {
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configMaxCount, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configPowerLevel, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configDamage, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configHealthMultiplayer, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configHealthSingleplayer, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configAttackCooldown, true));


            LethalConfig.LethalConfigManager.SkipAutoGen();
        }

        void Awake()
        {
            Logger = base.Logger;

            configMaxCount = Config.Bind("General", "Max Spawn Count", 7,
                new ConfigDescription("Defines the max spawn count of Butlers."));

            configPowerLevel = Config.Bind("General", "Power Level", 2,
                new ConfigDescription("Defines the power level of Butlers."));

            configDamage = Config.Bind("General", "Attack Damage", 10,
                new ConfigDescription("Defines how much damage the Butlers deal per hit."));

            configHealthMultiplayer = Config.Bind("General", "Multiplayer Health", 8,
                new ConfigDescription("Defines the health of Butlers when playing multiplayer."));

            configHealthSingleplayer = Config.Bind("General", "Singleplayer Health", 2,
                new ConfigDescription("Defines the health of Butlers when playing singleplayer."));

            configAttackCooldown = Config.Bind("General", "Attack Cooldown", 0.0f,
                new ConfigDescription("Adds an attack cooldown after stabbing the player. Not vanilla behavior.",
                                    new AcceptableValueRange<float>(0.0f, 2.0f)));

            if (Chainloader.PluginInfos.ContainsKey(LETHAL_CONFIG))
            {
                initLethalConfig();
            }


            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }


        [HarmonyPatch]

        class ButlerSettingsPatches
        {
            [HarmonyPatch(typeof(ButlerEnemyAI), nameof(ButlerEnemyAI.Start))]
            [HarmonyPostfix]
            static void ButlerStartPostFix(ButlerEnemyAI __instance)
            {
                __instance.enemyType.MaxCount = configMaxCount.Value;
                __instance.enemyHP = configHealthMultiplayer.Value;

                if (StartOfRound.Instance.connectedPlayersAmount == 0)
                {
                    __instance.enemyHP = configHealthSingleplayer.Value;
                }
            }

            [HarmonyPatch(typeof(ButlerEnemyAI), nameof(ButlerEnemyAI.OnCollideWithPlayer))]
            [HarmonyPrefix]
            static void ButlerAttackPrefix(ButlerEnemyAI __instance, Collider other)
            {
                if (__instance.isEnemyDead)
                {
                    return;
                }

                if (__instance.currentBehaviourStateIndex != 2)
                {
                    if (Time.realtimeSinceStartup - __instance.timeSinceStealthStab < 10f)
                    {
                        return;
                    }

                    __instance.timeSinceStealthStab = Time.realtimeSinceStartup;
                    if (Random.Range(0, 100) < 95)
                    {
                        return;
                    }
                }

                if (__instance.timeSinceHittingPlayer < 0.25f)
                {
                    return;
                }

                PlayerControllerB playerControllerB = __instance.MeetsStandardPlayerCollisionConditions(other);
                if (!(playerControllerB != null))
                {
                    return;
                }

                __instance.timeSinceHittingPlayer = 0f;
                if (playerControllerB == GameNetworkManager.Instance.localPlayerController)
                {
                    if (__instance.currentBehaviourStateIndex != 2)
                    {
                        __instance.berserkModeTimer = 3f;
                    }

                    playerControllerB.DamagePlayer(configDamage.Value, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
                    __instance.StabPlayerServerRpc((int)playerControllerB.playerClientId, __instance.currentBehaviourStateIndex != 2);
                    __instance.SetEnemyStunned(true, configAttackCooldown.Value, playerControllerB);
                }
                return;
            }
        }


    }
}