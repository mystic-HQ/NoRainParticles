using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NoRainParticles
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class NoRainParticlesPlugin : BaseUnityPlugin
    {
        private static ManualLogSource? logger;
        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);

        private void Awake()
        {
            logger = Logger;
            harmony.PatchAll();
            logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");
        }
    }

    [HarmonyPatch(typeof(TimeOfDay))]
    internal class TimeOfDayPatch
    {
        // Patch the weather effects to disable rain particle rendering
        [HarmonyPatch("SetWeatherBasedOnVariables")]
        [HarmonyPostfix]
        static void DisableRainParticles(TimeOfDay __instance)
        {
            // Find and disable rain particle systems
            ParticleSystem[] allParticleSystems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();

            foreach (ParticleSystem ps in allParticleSystems)
            {
                // Check if this is a rain particle system
                if (IsRainParticleSystem(ps))
                {
                    // Disable rendering but keep the system active for collision/sound triggers
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
            }
        }

        private static bool IsRainParticleSystem(ParticleSystem ps)
        {
            if (ps == null || ps.gameObject == null) return false;

            string name = ps.gameObject.name.ToLower();

            // Common rain particle system names in Lethal Company
            return name.Contains("rain") &&
                   !name.Contains("terrain") &&
                   !name.Contains("lightning") &&
                   !name.Contains("thunder");
        }
    }

    [HarmonyPatch(typeof(StormyWeather))]
    internal class StormyWeatherPatch
    {
        // Patch storm weather specifically
        [HarmonyPatch("OnEnable")]
        [HarmonyPostfix]
        static void DisableStormRainParticles(StormyWeather __instance)
        {
            // Disable particle renderers in the storm weather object
            ParticleSystemRenderer[] renderers = __instance.GetComponentsInChildren<ParticleSystemRenderer>();

            foreach (var renderer in renderers)
            {
                if (renderer.gameObject.name.ToLower().Contains("rain"))
                {
                    renderer.enabled = false;
                }
            }
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.yourname.norainparticles";
        public const string PLUGIN_NAME = "NoRainParticles";
        public const string PLUGIN_VERSION = "1.0.0";
    }
}