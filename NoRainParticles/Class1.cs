using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Collections;

namespace NoRainParticles
{
    [BepInPlugin("MysticDEV.NoRainParticles", "NoRainParticles", "1.0.4")]
    public class NoRainParticlesPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource? logger;
        private readonly Harmony harmony = new Harmony("MysticDEV.NoRainParticles");
        private static NoRainParticlesPlugin? instance;

        private void Awake()
        {
            instance = this;
            logger = Logger;
            harmony.PatchAll();
            logger.LogInfo("NoRainParticles v1.0.4 is loaded!");
        }

        // Subscribe to the StartedLandingShip event
        [HarmonyPatch(typeof(StartOfRound), "Start")]
        internal class StartPatch
        {
            [HarmonyPostfix]
            static void SubscribeToLandingEvent(StartOfRound __instance)
            {
                __instance.StartedLandingShip += OnShipStartedLanding;
                logger?.LogInfo("Subscribed to StartedLandingShip event");
            }
        }

        private static void OnShipStartedLanding()
        {
            logger?.LogInfo("StartedLandingShip event fired - starting delayed rain particle scan...");
            instance?.StartCoroutine(DelayedDisableRainParticles());
        }

        private static IEnumerator DelayedDisableRainParticles()
        {
            // Wait a few frames for the rain particles to spawn
            yield return new WaitForSeconds(0.5f);

            logger?.LogInfo("Delayed scan executing...");
            DisableRainParticles();

            // Do another scan after a bit more time just to be sure
            yield return new WaitForSeconds(0.5f);
            DisableRainParticles();
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
                        logger?.LogInfo($"DISABLED: '{ps.gameObject.name}' (Parent: '{ps.transform.parent?.name ?? "None"}')");
                    }
                }

                logger?.LogInfo($"Scan complete - Disabled {disabledCount} rain particle systems");
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