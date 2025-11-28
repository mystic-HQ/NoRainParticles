using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace NoRainParticles
{
    [BepInPlugin("MysticDEV.NoRainParticles", "NoRainParticles", "1.0.0")]
    public class NoRainParticlesPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource? logger;
        private readonly Harmony harmony = new Harmony("MysticDEV.NoRainParticles");
        private static bool hasLoggedParticles = false;

        private void Awake()
        {
            logger = Logger;
            harmony.PatchAll();
            logger.LogInfo("NoRainParticles v1.2.3 is loaded!");
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

        public static void LogTimeOfDayEffects()
        {
            try
            {
                TimeOfDay timeOfDay = UnityEngine.Object.FindObjectOfType<TimeOfDay>();
                if (timeOfDay != null)
                {
                    logger?.LogWarning("===== LOGGING TIMEOFDAY EFFECTS =====");

                    if (timeOfDay.effects != null && timeOfDay.effects.Length > 0)
                    {
                        logger?.LogWarning($"Found {timeOfDay.effects.Length} weather effects");

                        for (int i = 0; i < timeOfDay.effects.Length; i++)
                        {
                            var effect = timeOfDay.effects[i];
                            if (effect != null && effect.effectObject != null)
                            {
                                logger?.LogWarning($"Effect[{i}]: '{effect.effectObject.name}' | Enabled: {effect.effectEnabled} | Active: {effect.effectObject.activeSelf}");

                                // Log all children of this effect
                                Transform[] children = effect.effectObject.GetComponentsInChildren<Transform>(true);
                                foreach (Transform child in children)
                                {
                                    if (child.gameObject != effect.effectObject)
                                    {
                                        logger?.LogWarning($"  Child: '{child.gameObject.name}' | Active: {child.gameObject.activeSelf}");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        logger?.LogWarning("TimeOfDay.effects is null or empty!");
                    }

                    logger?.LogWarning("===== END TIMEOFDAY EFFECTS LOG =====");
                }
                else
                {
                    logger?.LogWarning("Could not find TimeOfDay object!");
                }
            }
            catch (System.Exception ex)
            {
                logger?.LogError($"Error logging TimeOfDay effects: {ex.Message}");
            }
        }

        public static void DisableRainParticles()
        {
            try
            {
                int disabledCount = 0;
                ParticleSystem[] allParticleSystems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();

                logger?.LogInfo($"Scanning {allParticleSystems.Length} particle systems...");

                foreach (ParticleSystem ps in allParticleSystems)
                {
                    if (ps == null || ps.gameObject == null) continue;

                    string name = ps.gameObject.name.ToLower();
                    string parentName = ps.transform.parent != null ? ps.transform.parent.name.ToLower() : "";

                    // Try to match rain particles - very broad search
                    bool isRainParticle = name.Contains("rain") ||
                                         parentName.Contains("rain") ||
                                         (parentName.Contains("storm") && name.Contains("particle"));

                    // Exclude gameplay-critical storm effects but NOT rain visuals
                    bool isExcluded = name.Contains("lightning") || name.Contains("thunder") ||
                                     name.Contains("bolt") || name.Contains("strike") ||
                                     name.Contains("puddle") || name.Contains("splash") ||
                                     name.Contains("mud") || name.Contains("ground") ||
                                     name.Contains("magnet") || name.Contains("spark") ||
                                     name.Contains("electric") || name.Contains("charge") ||
                                     name.Contains("static") || name.Contains("blast") ||
                                     name.Contains("warning") || name.Contains("flash") ||
                                     parentName.Contains("magnet") ||
                                     (parentName.Contains("stormy") && (name.Contains("static") ||
                                      name.Contains("blast") || name.Contains("warning")));

                    if (isRainParticle && !isExcluded)
                    {
                        // Try EVERYTHING to disable it
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

                        // Also try disabling the GameObject
                        ps.gameObject.SetActive(false);

                        disabledCount++;
                        logger?.LogInfo($"DISABLED: '{ps.gameObject.name}' (Parent: '{ps.transform.parent?.name}')");
                    }
                }

                logger?.LogInfo($"Disabled {disabledCount} rain particle systems");
            }
            catch (System.Exception ex)
            {
                logger?.LogError($"Error disabling rain particles: {ex.Message}");
            }
        }
    }

    // Patch RoundManager
    [HarmonyPatch(typeof(RoundManager))]
    internal class RoundManagerPatch
    {
        [HarmonyPatch("SetToCurrentLevelWeather")]
        [HarmonyPostfix]
        static void OnSetWeather()
        {
            NoRainParticlesPlugin.logger?.LogInfo("RoundManager.SetToCurrentLevelWeather called");
            NoRainParticlesPlugin.LogTimeOfDayEffects();
            NoRainParticlesPlugin.LogAllParticleSystems();
            NoRainParticlesPlugin.DisableRainParticles();
        }

        [HarmonyPatch("GenerateNewLevelClientRpc")]
        [HarmonyPostfix]
        static void OnGenerateNewLevel()
        {
            NoRainParticlesPlugin.logger?.LogInfo("RoundManager.GenerateNewLevelClientRpc called");
        }
    }

    // Patch TimeOfDay
    [HarmonyPatch(typeof(TimeOfDay))]
    internal class TimeOfDayPatch
    {
        private static float lastCheckTime = 0f;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void OnTimeOfDayStart()
        {
            NoRainParticlesPlugin.logger?.LogInfo("TimeOfDay.Start called");
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void OnTimeOfDayUpdate()
        {
            // Check every 2 seconds instead of every frame for performance
            if (Time.time - lastCheckTime > 2f)
            {
                lastCheckTime = Time.time;
                NoRainParticlesPlugin.DisableRainParticles();
            }
        }
    }

    // Patch StartOfRound
    [HarmonyPatch(typeof(StartOfRound))]
    internal class StartOfRoundPatch
    {
        [HarmonyPatch("StartGame")]
        [HarmonyPostfix]
        static void OnStartGame()
        {
            NoRainParticlesPlugin.logger?.LogInfo("StartOfRound.StartGame called");
            NoRainParticlesPlugin.DisableRainParticles();
        }
    }
}