using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Pigeon.Movement;
using System;
using System.IO;
using UnityEngine;
namespace OpticalPersistence;

[MycoMod(null, ModFlags.IsClientSide)]
[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class OpticalPersistence : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "DBR.OpticalPersistence";
    public const string PLUGIN_NAME = "OpticalPersistence";
    public const string PLUGIN_VERSION = "1.4.0";

    internal static Sprite PersistenceIcon;

    internal static new ManualLogSource Logger;
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded");

        Harmony harmony = new(PLUGIN_GUID);
        try
        {
            harmony.PatchAll();
        }
        catch (Exception ex)
        {
            Logger.LogError((object)("Error applying patches: " + ex.Message));
        }

        PersistenceIcon = LoadExternalIcon("persistence.png", this);
    }

    private static readonly AccessTools.FieldRef<ShieldProjector, float> LastAggroTime = AccessTools.FieldRefAccess<ShieldProjector, float>("lastAggroTime"); 

    [HarmonyPatch(typeof(ShieldProjector), "OnBeforeTakeDamage")]
    public static class OpticalPersistencePatch
    {

        [HarmonyPrefix]
        public static bool Prefix(ShieldProjector __instance, ref DamageData data)
        {

            // Only override behavior when duration exceeds cooldown and skill not active.
            if ((__instance.Data.aggroDuration < __instance.Data.aggroCooldown) || IsAggroing(__instance))
            {
                return true;
            }

            else 
            {
                float resistance = 1f - (__instance.Data.aggroDamageResistance * 0.5f);
                data.damage *= resistance;

                return false;
            }

        }
    }

    //Try to prevent stackdisplay updates running every frame
    private static bool hasAggroed = false;

    [HarmonyPatch(typeof(ShieldProjector), "Update")]
    public static class UpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ShieldProjector __instance)
        {
            if (__instance.Data.aggroDuration > 0 && __instance.Data.aggroDuration > __instance.Data.aggroCooldown)
            {
                bool isAggroing = IsAggroing(__instance);

                if (isAggroing && !hasAggroed)

                {
                    hasAggroed = true;
                    __instance.Player.RemoveStackDisplay(typeof(OpticalPersistence));
                }

                else if (!isAggroing && hasAggroed)
                {
                    hasAggroed = false;
                    __instance.Player.UpdateStackDisplay(typeof(OpticalPersistence), "Optical Persistence", PersistenceIcon, 1);

                }
            }
        }
    }

    [HarmonyPatch(typeof(ShieldProjector), nameof(ShieldProjector.OnUpgradesEnabled))]
    public static class OpticalAttractionDisplayEnablePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ShieldProjector __instance)
        {
            if (__instance.Data.aggroDuration > __instance.Data.aggroCooldown && !IsAggroing(__instance) )
            {
                __instance.Player.UpdateStackDisplay(typeof(OpticalPersistence), "Optical Persistence", PersistenceIcon, 1);
            }
        }
    }

    [HarmonyPatch(typeof(ShieldProjector), nameof(ShieldProjector.OnUpgradesDisabled))]
    public static class OpticalAttractionDisplayDisablePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Player.LocalPlayer.RemoveStackDisplay(typeof(OpticalPersistence));
        }
    }

    public static bool IsAggroing(ShieldProjector instance)
    {
        return Time.time - LastAggroTime(instance) < instance.Data.aggroDuration;
    }

    internal static Sprite LoadExternalIcon(string iconName, BaseUnityPlugin pluginInstance)
    {
        string path = Path.Combine(Path.GetDirectoryName(pluginInstance.Info.Location), iconName);
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            byte[] array = File.ReadAllBytes(path);
            Texture2D texture = new(2, 2);

            ImageConversion.LoadImage(texture, array);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        catch (Exception ex)
        {
            Logger.LogError("Icon Load Failed: " + ex.Message);
            return null;
        }
    }
}
