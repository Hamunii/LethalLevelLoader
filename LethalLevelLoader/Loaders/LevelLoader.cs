﻿using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Navigation;
using UnityEngine.AI;
using LethalLevelLoader.Tools;
using UnityEngine.Rendering.HighDefinition;

namespace LethalLevelLoader
{
    public static class LevelLoader
    {
        internal static List<MeshCollider> customLevelMeshCollidersList = new List<MeshCollider>();

        internal static AnimatorOverrideController shipAnimatorOverrideController = null!;
        internal static AnimationClip defaultShipFlyToMoonClip = null!;
        internal static AnimationClip defaultShipFlyFromMoonClip = null!;

        internal static Vector3 defaultDustCloudFogVolumeSize;
        internal static Vector3 defaultFoggyFogVolumeSize;

        internal static LocalVolumetricFog? dustCloudFog;
        internal static LocalVolumetricFog? foggyFog;


        internal static GameObject defaultQuicksandPrefab = null!;

        internal static FootstepSurface[] defaultFootstepSurfaces = null!;

        internal static Dictionary<Collider, List<Material>> cachedLevelColliderMaterialDictionary = new Dictionary<Collider, List<Material>>();
        internal static Dictionary<string, List<Collider>> cachedLevelMaterialColliderDictionary = new Dictionary<string, List<Collider>>();
        internal static Dictionary<string, FootstepSurface> activeExtendedFootstepSurfaceDictionary = new Dictionary<string, FootstepSurface>();
        internal static LayerMask triggerMask;


        internal static async void EnableMeshColliders()
        {
            List<MeshCollider> instansiatedCustomLevelMeshColliders = new List<MeshCollider>();

            int counter = 0;
            foreach (MeshCollider meshCollider in UnityEngine.Object.FindObjectsOfType<MeshCollider>())
                if (meshCollider.gameObject.name.Contains(" (LLL Tracked)"))
                    instansiatedCustomLevelMeshColliders.Add(meshCollider);

            Task[] meshColliderEnableTasks = new Task[instansiatedCustomLevelMeshColliders.Count];

            foreach (MeshCollider meshCollider in instansiatedCustomLevelMeshColliders)
            {
                meshColliderEnableTasks[counter] = EnableMeshCollider(meshCollider);
                counter++;
            }

            await Task.WhenAll(meshColliderEnableTasks);

            //customLevelMeshCollidersList.Clear();
        }

        internal static async Task EnableMeshCollider(MeshCollider meshCollider)
        {
            meshCollider.enabled = true;
            meshCollider.gameObject.name.Replace(" (LLL Tracked)", "");
            await Task.Yield();
        }

        internal static void RefreshShipAnimatorClips(ExtendedLevel extendedLevel)
        {
            DebugHelper.Log("Refreshing Ship Animator Clips!", DebugType.Developer);
            shipAnimatorOverrideController["HangarShipLandB"] = extendedLevel.ShipFlyToMoonClip;
            shipAnimatorOverrideController["ShipLeave"] = extendedLevel.ShipFlyFromMoonClip;
        }

        internal static void RefreshFogSize(ExtendedLevel extendedLevel)
        {
            dustCloudFog.parameters.size = extendedLevel.OverrideDustStormVolumeSize;
            foggyFog.parameters.size = extendedLevel.OverrideFoggyVolumeSize;
        }

        internal static void RefreshFootstepSurfaces()
        {
            List<FootstepSurface> activeFootstepSurfaces = new List<FootstepSurface>(defaultFootstepSurfaces);
            foreach (ExtendedFootstepSurface extendedSurface in LevelManager.CurrentExtendedLevel.ExtendedMod.ExtendedFootstepSurfaces)
            {
                extendedSurface.footstepSurface.surfaceTag = "Untagged";
                activeFootstepSurfaces.Add(extendedSurface.footstepSurface);
            }

            Patches.StartOfRound.footstepSurfaces = activeFootstepSurfaces.ToArray();
        }

        internal static void BakeSceneColliderMaterialData(Scene scene)
        {
            cachedLevelColliderMaterialDictionary.Clear();
            cachedLevelMaterialColliderDictionary.Clear();
            activeExtendedFootstepSurfaceDictionary = GetActiveExtendedFoostepSurfaceDictionary();

            triggerMask = LayerMask.NameToLayer("Triggers");

            List<Collider> allSceneColliders = new List<Collider>();

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Collider collider in rootObject.GetComponents<Collider>())
                {
                    if (ValidateCollider(collider) && !allSceneColliders.Contains(collider))
                        allSceneColliders.Add(collider);
                }
                foreach (Collider collider in rootObject.GetComponentsInChildren<Collider>())
                {
                    if (ValidateCollider(collider) && !allSceneColliders.Contains(collider))
                        allSceneColliders.Add(collider);
                }
            }
            
            foreach (Collider sceneCollider in allSceneColliders)
            {
                if (sceneCollider.TryGetComponent(out MeshRenderer meshRenderer))
                {
                    if (!cachedLevelColliderMaterialDictionary.ContainsKey(sceneCollider))
                        cachedLevelColliderMaterialDictionary.Add(sceneCollider, new List<Material>(meshRenderer.sharedMaterials));
                    foreach (Material material in meshRenderer.sharedMaterials)
                    {
                        if (!cachedLevelMaterialColliderDictionary.ContainsKey(material.name))
                            cachedLevelMaterialColliderDictionary.Add(material.name, new List<Collider> { sceneCollider });
                        else if (!cachedLevelMaterialColliderDictionary[material.name].Contains(sceneCollider))
                            cachedLevelMaterialColliderDictionary[material.name].Add(sceneCollider);
                    }
                }
            }

            //DebugHelper.DebugCachedLevelColliderData();
        }

        internal static bool ValidateCollider(Collider collider)
        {
            if (collider == null) return (false);
            if (collider.gameObject.activeSelf == false) return (false);
            if (collider.isTrigger == true) return (false);
            if (collider.gameObject.layer == triggerMask) return (false);
            if (collider.gameObject.CompareTag("Untagged") == false) return (false);

            return (true);
        }

        internal static Dictionary<string, FootstepSurface> GetActiveExtendedFoostepSurfaceDictionary()
        {
            Dictionary<string, FootstepSurface> returnDict = new Dictionary<string, FootstepSurface>();

            foreach (ExtendedFootstepSurface extendedFootstepSurface in LevelManager.CurrentExtendedLevel.ExtendedMod.ExtendedFootstepSurfaces)
                foreach (Material material in extendedFootstepSurface.associatedMaterials)
                    if (!returnDict.ContainsKey(material.name))
                        returnDict.Add(material.name, extendedFootstepSurface.footstepSurface);


            return (returnDict);
        }

        public static bool TryGetFootstepSurface(Collider collider, out FootstepSurface? footstepSurface)
        {
            footstepSurface = null;

            if (cachedLevelColliderMaterialDictionary.TryGetValue(collider, out List<Material> materials))
                foreach (Material material in materials)
                    activeExtendedFootstepSurfaceDictionary.TryGetValue(material.name, out footstepSurface);

            return (footstepSurface != null);
        }
    }
}