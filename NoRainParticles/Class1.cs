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

            UnityEngine.Object.DontDestroyOnLoad(this.gameObject);

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
                // Prevent double-subscribing if Start() somehow runs more than once
                __instance.StartedLandingShip -= OnShipStartedLanding;
                __instance.StartedLandingShip += OnShipStartedLanding;
                logger?.LogInfo("Subscribed to StartedLandingShip event");
            }
        }

        private static void OnShipStartedLanding()
        {
            try
            {
                logger?.LogInfo("StartedLandingShip event fired - starting delayed rain particle scan...");

                StartOfRound? shipInstance = StartOfRound.Instance;

                if (shipInstance != null)
                {
                    shipInstance.StartCoroutine(DelayedDisableRainParticles());
                }
                else
                {
                    logger?.LogWarning("StartOfRound.Instance is null/destroyed - skipping rain particle scan this landing.");
                }
            }
            catch (System.Exception ex)
            {
                logger?.LogError($"Exception in OnShipStartedLanding: {ex}");
            }
        }

        private static IEnumerator DelayedDisableRainParticles()
        {
            // Wait half a second for the particles to spawn
            yield return new WaitForSeconds(0.5f);

            logger?.LogInfo("Delayed scan executing...");
            DisableRainParticles();

            // Scan again to make sure
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
                    string parentName = ps.transform.parent != null
                        ? ps.transform.parent.name.ToLower()
                        : "";

                    bool isRainParticle =
                        name.Contains("rain") ||
                        parentName.Contains("rain") ||
                        (parentName.Contains("storm") && name.Contains("particle")); // Doesn't remove sparks from lightnings but removes rain from Stormy weathers

                    // Exclude all that are essential to play properly
                    bool isExcluded =
                        name.Contains("lightning") || name.Contains("thunder") ||
                        name.Contains("bolt") || name.Contains("strike") ||
                        name.Contains("puddle") || name.Contains("splash") ||
                        name.Contains("mud") || name.Contains("ground") ||
                        name.Contains("magnet") || name.Contains("spark") ||
                        name.Contains("electric") || name.Contains("charge") ||
                        name.Contains("static") || name.Contains("blast") ||
                        name.Contains("warning") || name.Contains("flash") ||
                        parentName.Contains("magnet") ||
                        (parentName.Contains("stormy") && (
                            name.Contains("static") ||
                            name.Contains("blast") ||
                            name.Contains("warning")));

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
}
