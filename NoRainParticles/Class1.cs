using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NoRainParticles
{
    [BepInPlugin("MysticDEV.NoRainParticles", "NoRainParticles", "1.0.1")]
    public class NoRainParticlesPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource? logger;
        private readonly Harmony harmony = new Harmony("MysticDEV.NoRainParticles");
        private static bool hasLoggedParticles = false;

        private void Awake()
        {
            logger = Logger;
            harmony.PatchAll();
            logger.LogInfo("NoRainParticles v1.0.0 is loaded!");
        }

        public static void LogAllParticleSystems()
        {
            if (hasLoggedParticles) return;

            try
            {
                logger?.LogWarning("===== LOGGING ALL PARTICLE SYSTEMS =====");

                ParticleSystem[] allParticleSystems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
                logger?.LogWarning($"Found {allParticleSystems.Length} total particle systems in scene");

                foreach (ParticleSystem ps in allParticleSystems)
                {
                    if (ps == null || ps.gameObject == null) continue;

                    string name = ps.gameObject.name;
                    string parentName = ps.transform.parent != null ? ps.transform.parent.name : "NO_PARENT";
                    bool isActive = ps.gameObject.activeInHierarchy;
                    bool isEmitting = ps.isEmitting;
                    int particleCount = ps.particleCount;

                    logger?.LogWarning($"Particle: '{name}' | Parent: '{parentName}' | Active: {isActive} | Emitting: {isEmitting} | Count: {particleCount}");
                }

                logger?.LogWarning("===== END PARTICLE SYSTEMS LOG =====");
                hasLoggedParticles = true;
            }
            catch (System.Exception ex)
            {
                logger?.LogError($"Error logging particles: {ex.Message}");
            }
        }

        public static void DisableRainParticles()
        {
            try
            {
                int disabledCount = 0;
                ParticleSystem[] allParticleSystems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();

                foreach (ParticleSystem ps in allParticleSystems)
                {
                    if (ps == null || ps.gameObject == null) continue;

                    string name = ps.gameObject.name.ToLower();
                    string parentName = ps.transform.parent != null ? ps.transform.parent.name.ToLower() : "";

                    // Match rain particles
                    bool isRainParticle = name.Contains("rain") ||
                                         parentName.Contains("rain") ||
                                         (parentName.Contains("storm") && name.Contains("particle"));

                    // Exclude gameplay-critical effects
                    bool isExcluded = name.Contains("lightning") || name.Contains("thunder") ||
                                     name.Contains("bolt") || name.Contains("strike");

                    if (isRainParticle && !isExcluded)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Clear();

                        var emission = ps.emission;
                        emission.enabled = false;

                        var renderer = ps.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null)
                        {
                            renderer.enabled = false;
                            renderer.forceRenderingOff = true;
                        }

                        ps.gameObject.SetActive(false);

                        disabledCount++;
                        logger?.LogInfo($"Disabled: '{ps.gameObject.name}'");
                    }
                }

                if (disabledCount > 0)
                {
                    logger?.LogInfo($"Disabled {disabledCount} rain particle systems");
                }
            }
            catch (System.Exception ex)
            {
                logger?.LogError($"Error disabling rain particles: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(RoundManager))]
    internal class RoundManagerPatch
    {
        [HarmonyPatch("GenerateNewLevelClientRpc")]
        [HarmonyPostfix]
        static void OnGenerateNewLevel()
        {
            NoRainParticlesPlugin.logger?.LogInfo("Moon loaded - starting rain particle checks");
            NoRainParticlesPlugin.LogAllParticleSystems();
            NoRainParticlesPlugin.DisableRainParticles();
        }

        [HarmonyPatch("FinishGeneratingLevel")]
        [HarmonyPostfix]
        static void OnFinishGeneratingLevel()
        {
            NoRainParticlesPlugin.logger?.LogInfo("Level generation finished");
            NoRainParticlesPlugin.DisableRainParticles();
        }
    }

    [HarmonyPatch(typeof(TimeOfDay))]
    internal class TimeOfDayPatch
    {
        private static float lastCheckTime = 0f;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void OnUpdate(TimeOfDay __instance)
        {
            // Only check if a level is loaded
            if (RoundManager.Instance == null || !RoundManager.Instance.currentLevel.sceneName.Contains("Level")) return;

            // Check every 2 seconds
            if (Time.time - lastCheckTime > 2f)
            {
                lastCheckTime = Time.time;
                NoRainParticlesPlugin.DisableRainParticles();
            }
        }
    }
}