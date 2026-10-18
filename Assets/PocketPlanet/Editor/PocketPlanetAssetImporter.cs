using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPlanet.Editor
{
    public static class PocketPlanetAssetImporter
    {
        private const string HeroRoot = "Assets/PocketPlanet/Art/Characters/Pip";
        private const string HeroNative = HeroRoot + "/Native";
        private const string PropsRoot = "Assets/PocketPlanet/Art/Props";
        private const string ResourcesRoot = "Assets/PocketPlanet/Resources";
        private const string CharactersRes = ResourcesRoot + "/Characters";
        private const string PropsRes = ResourcesRoot + "/Props";
        private const string MaterialsRoot = "Assets/PocketPlanet/Art/Materials";

        private static readonly (string Name, string Fbx, int Mode, bool Loop)[] HeroClips =
        {
            ("Idle", "PipIdle.fbx", 0, true),
            ("Walk", "PipWalk.fbx", 1, true),
            ("Run", "PipRun.fbx", 2, true),
            ("Turn", "PipTurn.fbx", 3, false),
            ("Jump", "PipJump.fbx", 4, false),
            ("Fall", "PipFall.fbx", 5, true),
            ("Landing", "PipLanding.fbx", 6, false),
            ("Pickup", "PipPickup.fbx", 7, false),
            ("Carry", "PipCarry.fbx", 8, true),
            ("Throw", "PipThrow.fbx", 9, false),
            ("Interact", "PipInteract.fbx", 10, false),
            ("Celebrate", "PipCelebrate.fbx", 11, true),
        };

        private static readonly string[] PropFbxNames =
        {
            "Starberry",
            "StarSeed",
            "BouncyShroom",
            "PushBall",
            "DominoBlock",
            "CrystalLever",
            "UnfoldingBridge",
            "AlienPodHome",
            "PastelRockPlatform",
            "GlowingFloraTuft",
            "BloomPlant",
            "BloopCreature",
            "StarhopperShip",
            "CustomuseFloraStatue",
            "TripoLushTree",
            "TripoAlienPalmTree",
            "TripoFlowerBush",
            "TripoFlyingSaucer",
            "TripoNpcAstra",
            "TripoSolarisCore",
            "TripoSpaceAsteroid",
            "TripoDeadwoodTree",
            "TripoObsidianRuin",
            "TripoNpcNyx",
            "TripoShrineLantern",
            "TripoCrashedSaucer",
            "TripoObsidianCrystal",
            "TripoSpookyMushroom",
        };

        [MenuItem("PocketPlanet/Import All Assets & Build Prefabs")]
        public static void ImportAllMenu()
        {
            ImportAll();
        }

        public static void ImportAllBatch()
        {
            try
            {
                ImportAll();
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PocketPlanetAssetImporter] Failed: " + ex);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }

        public static void ImportAll()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureAudioClips();
            Material pipMat = BuildPipMaterial();
            Material propsAtlasMat = BuildPropsAtlasMaterial();
            GameObject pipPrefab = ImportHeroAndBuildPrefab(pipMat);
            ImportAllPropsAndBuildPrefabs(propsAtlasMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[PocketPlanetAssetImporter] Successfully imported PipHero (" +
                      (pipPrefab != null ? pipPrefab.name : "null") + ") and " + PropFbxNames.Length + " prop prefabs.");
        }

        private static void EnsureFolders()
        {
            string[] dirs =
            {
                "Assets/PocketPlanet",
                "Assets/PocketPlanet/Art",
                MaterialsRoot,
                HeroRoot,
                HeroNative,
                HeroRoot + "/Generated",
                PropsRoot,
                ResourcesRoot,
                CharactersRes,
                PropsRes,
                ResourcesRoot + "/Audio",
                "Assets/PocketPlanet/Scenes",
            };
            foreach (string d in dirs)
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
        }

        private static void ConfigureAudioClips()
        {
            string audioDir = ResourcesRoot + "/Audio";
            if (!Directory.Exists(audioDir))
                return;

            foreach (string file in Directory.GetFiles(audioDir, "*.wav"))
            {
                string assetPath = file.Replace("\\", "/");
                if (AssetImporter.GetAtPath(assetPath) is AudioImporter ai)
                {
                    AudioImporterSampleSettings settings = ai.defaultSampleSettings;
                    settings.loadType = assetPath.Contains("bgm_")
                        ? AudioClipLoadType.Streaming
                        : AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    ai.defaultSampleSettings = settings;
                    ai.forceToMono = false;
                    ai.SaveAndReimport();
                }
            }
        }

        private static Texture2D ConfigureTexture(string path, bool sRgb, FilterMode filterMode = FilterMode.Bilinear)
        {
            if (!File.Exists(path))
                return null;
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = sRgb;
                ti.mipmapEnabled = true;
                ti.alphaIsTransparency = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = filterMode;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 2048;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Shader FindLitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        private static void AssignMaterialTextures(Material mat, Texture2D albedo, Texture2D emission, float smoothness, float emissionIntensity)
        {
            mat.color = Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.04f);

            if (emission != null && emissionIntensity > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emission);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.white * emissionIntensity);
            }
        }

        private static Material CreateOrUpdateMaterial(string assetPath, Texture2D albedo, Texture2D emission, float smoothness, float emissionIntensity)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = FindLitShader();
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                mat.shader = shader;
            }
            AssignMaterialTextures(mat, albedo, emission, smoothness, emissionIntensity);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material BuildPipMaterial()
        {
            Texture2D albedo = ConfigureTexture(HeroRoot + "/Pip_Albedo.png", true, FilterMode.Bilinear);
            return CreateOrUpdateMaterial(MaterialsRoot + "/PipHeroMat.mat", albedo, null, 0.42f, 0f);
        }

        private static Material BuildPropsAtlasMaterial()
        {
            Texture2D albedo = ConfigureTexture(PropsRoot + "/Props_Albedo.png", true, FilterMode.Point);
            Texture2D emission = ConfigureTexture(PropsRoot + "/Props_Emission.png", true, FilterMode.Point);
            return CreateOrUpdateMaterial(MaterialsRoot + "/PropsAtlasMat.mat", albedo, emission, 0.42f, 0.20f);
        }

        private static void ConfigureFbxImporter(string path, bool importAnimation)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter mi)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                mi.importAnimation = importAnimation;
                mi.importConstraints = false;
                mi.preserveHierarchy = true;
                mi.optimizeGameObjects = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.isReadable = true;
                mi.importNormals = ModelImporterNormals.Import;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.SaveAndReimport();
            }
        }

        private static GameObject ImportHeroAndBuildPrefab(Material pipMat)
        {
            string bodyFbxPath = HeroNative + "/PipBody.fbx";
            ConfigureFbxImporter(bodyFbxPath, false);
            foreach (var c in HeroClips)
                ConfigureFbxImporter(HeroNative + "/" + c.Fbx, true);

            GameObject bodyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(bodyFbxPath);
            if (bodyAsset == null)
                throw new InvalidOperationException("Missing hero FBX at " + bodyFbxPath);

            string genDir = HeroRoot + "/Generated";
            var builtClips = new Dictionary<string, AnimationClip>();

            foreach (var spec in HeroClips)
            {
                AnimationClip srcClip = LoadFirstClip(HeroNative + "/" + spec.Fbx);
                if (srcClip == null)
                    throw new InvalidOperationException("No AnimationClip found in " + spec.Fbx);

                string dstPath = $"{genDir}/Pip_{spec.Name}.anim";
                AnimationClip dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(dstPath);
                if (dst == null)
                {
                    dst = new AnimationClip();
                    AssetDatabase.CreateAsset(dst, dstPath);
                }
                EditorUtility.CopySerialized(srcClip, dst);
                dst.name = "Pip_" + spec.Name;
                RemapPathsToRigRoot(dst, bodyAsset.transform);
                SetClipLoop(dst, spec.Loop);
                EditorUtility.SetDirty(dst);
                builtClips[spec.Name] = dst;
            }

            // Build AnimatorController with Mode integer parameter + smooth transitions
            string controllerPath = genDir + "/PipAnimator.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Mode", AnimatorControllerParameterType.Int);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            var states = new List<(AnimatorState State, int Mode, bool Loop)>();
            for (int i = 0; i < HeroClips.Length; i++)
            {
                var spec = HeroClips[i];
                AnimatorState st = sm.AddState(spec.Name, new Vector3(280f + (i % 3) * 230f, (i / 3) * 85f, 0f));
                st.motion = builtClips[spec.Name];
                if (spec.Name == "Idle")
                    sm.defaultState = st;
                states.Add((st, spec.Mode, spec.Loop));
            }

            // Use direct state-to-state transitions so Mode changes blend cleanly without AnyState re-triggering
            foreach (var from in states)
            {
                foreach (var to in states)
                {
                    if (from.Mode == to.Mode)
                        continue;
                    AnimatorStateTransition tr = from.State.AddTransition(to.State);
                    tr.hasExitTime = false;
                    tr.duration = (to.Mode == 4 || to.Mode == 6 || to.Mode == 7 || to.Mode == 9) ? 0.08f : 0.14f;
                    tr.canTransitionToSelf = false;
                    tr.AddCondition(AnimatorConditionMode.Equals, to.Mode, "Mode");
                }
            }
            EditorUtility.SetDirty(controller);

            // Build Generic Avatar & Prefab
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(bodyAsset);
            instance.name = "PipHero";
            try
            {
                Avatar avatar = AvatarBuilder.BuildGenericAvatar(instance, "");
                avatar.name = "PipGenericAvatar";
                string avatarPath = genDir + "/PipGenericAvatar.asset";
                if (AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath) != null)
                    AssetDatabase.DeleteAsset(avatarPath);
                AssetDatabase.CreateAsset(avatar, avatarPath);

                Animator anim = instance.GetComponent<Animator>();
                if (anim == null) anim = instance.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.avatar = avatar;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = pipMat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    if (r is SkinnedMeshRenderer smr)
                        smr.updateWhenOffscreen = true;
                }

                string prefabPath = CharactersRes + "/PipHero.prefab";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ImportAllPropsAndBuildPrefabs(Material propsAtlasMat)
        {
            foreach (string propName in PropFbxNames)
            {
                string fbxPath = $"{PropsRoot}/{propName}.fbx";
                if (!File.Exists(fbxPath))
                {
                    Debug.LogWarning("[PocketPlanetAssetImporter] Missing prop FBX: " + fbxPath);
                    continue;
                }
                ConfigureFbxImporter(fbxPath, false);

                // Check if this prop has a dedicated Customuse Albedo texture (BloopCreature, StarhopperShip, CustomuseFloraStatue)
                string customTexPath = $"{PropsRoot}/{propName}_Albedo.png";
                Material matToUse = propsAtlasMat;
                if (File.Exists(customTexPath))
                {
                    Texture2D customAlbedo = ConfigureTexture(customTexPath, true, FilterMode.Bilinear);
                    matToUse = CreateOrUpdateMaterial($"{MaterialsRoot}/{propName}Mat.mat", customAlbedo, null, 0.42f, 0f);
                }

                GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxAsset == null)
                    continue;

                // Wrap in an upright root GameObject so single-mesh FBX axis conversion (-90 X) is preserved on the child!
                GameObject rootWrapper = new GameObject(propName);
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                inst.name = propName + "_Model";
                inst.transform.SetParent(rootWrapper.transform, false);
                try
                {
                    foreach (Renderer r in rootWrapper.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = matToUse;
                        r.sharedMaterials = mats;
                        r.shadowCastingMode = ShadowCastingMode.On;
                        r.receiveShadows = true;
                    }
                    PrefabUtility.SaveAsPrefabAsset(rootWrapper, $"{PropsRes}/{propName}.prefab");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(rootWrapper);
                }
            }
        }

        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            return null;
        }

        private static void RemapPathsToRigRoot(AnimationClip clip, Transform root)
        {
            Transform rig = root.Find("PipRig");
            if (rig == null)
            {
                foreach (Transform child in root)
                {
                    if (child.Find("Root") != null || child.Find("Hips") != null)
                    {
                        rig = child;
                        break;
                    }
                }
            }
            if (rig == null)
                return;

            string rigPrefix = AnimationUtility.CalculateTransformPath(rig, root);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            var toRemap = new List<(EditorCurveBinding OldBinding, EditorCurveBinding NewBinding, AnimationCurve Curve)>();

            // Read all curves FIRST before clearing any curve (since clearing localRotation.x clears the whole quaternion group)
            foreach (EditorCurveBinding binding in bindings)
            {
                if (root.Find(binding.path) != null)
                    continue;
                string candidate = string.IsNullOrEmpty(binding.path) ? rigPrefix : rigPrefix + "/" + binding.path;
                if (root.Find(candidate) != null)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        var remapped = binding;
                        remapped.path = candidate;
                        toRemap.Add((binding, remapped, new AnimationCurve(curve.keys)));
                    }
                }
            }

            if (toRemap.Count > 0)
            {
                foreach (var item in toRemap)
                    AnimationUtility.SetEditorCurve(clip, item.OldBinding, null);
                foreach (var item in toRemap)
                    AnimationUtility.SetEditorCurve(clip, item.NewBinding, item.Curve);
            }
        }

        private static void SetClipLoop(AnimationClip clip, bool loop)
        {
            var so = new SerializedObject(clip);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                settings.FindPropertyRelative("m_LoopTime").boolValue = loop;
                settings.FindPropertyRelative("m_LoopBlend").boolValue = loop;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
