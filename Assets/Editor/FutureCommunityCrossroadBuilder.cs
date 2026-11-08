using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FutureCommunity.EditorTools
{
    /// <summary>
    /// 使用 Unity 基础几何体（Plane / Cube / Sphere）程序化搭建“未来社区十字路口”场景。
    /// 菜单入口：Tools/Future Community/Build Crossroad Scene
    /// 批处理入口：-executeMethod FutureCommunity.EditorTools.FutureCommunityCrossroadBuilder.BuildFromCommandLine
    /// </summary>
    public static class FutureCommunityCrossroadBuilder
    {
        private const string ScenePath = "Assets/Scenes/FutureCommunityCrossroad.unity";
        private const string MaterialFolder = "Assets/Materials/FutureCommunity";

        private const float GroundSize = 100f;
        private const float RoadWidth = 8f;
        private const float RoadBaseHeight = 0.02f;
        private const float RoadHorizontalThickness = 0.12f;
        private const float RoadVerticalThickness = 0.14f;
        private const float TrafficLightPoleHeight = 6f;

        private static readonly BuildingSpec[] BuildingSpecs =
        {
            // 东北角（3 栋）
            new BuildingSpec("Building_1", 15f, 16f, 9f, 9f, 18f, 0),
            new BuildingSpec("Building_2", 31f, 15f, 10f, 12f, 30f, 1),
            new BuildingSpec("Building_3", 16f, 33f, 8f, 10f, 24f, 2),

            // 西北角（3 栋）
            new BuildingSpec("Building_4", -15f, 15f, 10f, 10f, 26f, 3),
            new BuildingSpec("Building_5", -31f, 16f, 9f, 9f, 14f, 1),
            new BuildingSpec("Building_6", -16f, 33f, 9f, 11f, 36f, 0),

            // 西南角（3 栋）
            new BuildingSpec("Building_7", -15f, -16f, 9f, 10f, 20f, 2),
            new BuildingSpec("Building_8", -32f, -15f, 11f, 9f, 28f, 3),
            new BuildingSpec("Building_9", -16f, -34f, 8f, 9f, 12f, 1),

            // 东南角（3 栋）
            new BuildingSpec("Building_10", 15f, -15f, 10f, 9f, 32f, 0),
            new BuildingSpec("Building_11", 32f, -16f, 10f, 11f, 16f, 3),
            new BuildingSpec("Building_12", 16f, -34f, 9f, 10f, 22f, 2),
        };

        // 四个路口方向（对角布置在路沿外侧），Y 轴旋转让灯头朝向路口中心。
        private static readonly Vector2[] TrafficLightPositions =
        {
            new Vector2(5.5f, 5.5f),
            new Vector2(-5.5f, 5.5f),
            new Vector2(-5.5f, -5.5f),
            new Vector2(5.5f, -5.5f),
        };

        private static readonly float[] TrafficLightYawAngles = { 225f, 135f, 45f, 315f };

        [MenuItem("Tools/Future Community/Build Crossroad Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildScene();
            EditorUtility.DisplayDialog(
                "Future Community",
                "十字路口场景已生成：\n" + ScenePath,
                "好");
        }

        /// <summary>
        /// 供 Unity 命令行批处理调用的入口，失败时返回非 0 退出码。
        /// </summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                BuildScene();
                Debug.Log("[CrossroadBuilder] 场景生成完成：" + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[CrossroadBuilder] 场景生成失败：\n" + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildScene()
        {
            EnsureFolder(MaterialFolder);
            MaterialSet materials = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironment();
            CreateCameraAndSun();

            GameObject root = new GameObject("FutureCommunityCrossroad");
            Transform rootTransform = root.transform;
            rootTransform.position = Vector3.zero;

            CreateGround(rootTransform, materials.Ground);
            CreateRoads(rootTransform, materials.Asphalt);

            List<Footprint> footprints = new List<Footprint>();
            CreateBuildings(rootTransform, materials, footprints);
            CreateTrafficLights(rootTransform, materials, footprints);
            ValidateLayout(footprints);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.InvalidOperationException("无法保存场景：" + ScenePath);

            RegisterSceneInBuildSettings();
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.55f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.36f, 0.40f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.17f, 0.18f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
        }

        private static void CreateCameraAndSun()
        {
            GameObject sunObject = new GameObject("Directional Light");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.position = new Vector3(0f, 30f, 0f);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            cameraObject.transform.position = new Vector3(85f, 95f, -85f);
            cameraObject.transform.LookAt(new Vector3(0f, 4f, 0f));
        }

        private static void CreateGround(Transform parent, Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = Vector3.zero;
            // Plane 原始尺寸是 10x10，放大 10 倍得到 100x100 的地面。
            ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateRoads(Transform parent, Material material)
        {
            Transform roadsRoot = new GameObject("Roads").transform;
            roadsRoot.SetParent(parent, false);

            // 两条路都以地表上方 2cm 为底面；纵向路略高 2cm，
            // 保证交叉口顶面不共面，避免 z-fighting。
            CreatePrimitive(
                PrimitiveType.Cube,
                "Road_H",
                roadsRoot,
                new Vector3(0f, RoadBaseHeight + RoadHorizontalThickness * 0.5f, 0f),
                new Vector3(GroundSize, RoadHorizontalThickness, RoadWidth),
                material);

            CreatePrimitive(
                PrimitiveType.Cube,
                "Road_V",
                roadsRoot,
                new Vector3(0f, RoadBaseHeight + RoadVerticalThickness * 0.5f, 0f),
                new Vector3(RoadWidth, RoadVerticalThickness, GroundSize),
                material);
        }

        private static void CreateBuildings(Transform parent, MaterialSet materials, List<Footprint> footprints)
        {
            Transform buildingsRoot = new GameObject("Buildings").transform;
            buildingsRoot.SetParent(parent, false);

            foreach (BuildingSpec spec in BuildingSpecs)
            {
                Material buildingMaterial = materials.Buildings[spec.MaterialIndex % materials.Buildings.Length];
                CreatePrimitive(
                    PrimitiveType.Cube,
                    spec.Name,
                    buildingsRoot,
                    new Vector3(spec.CenterX, spec.Height * 0.5f, spec.CenterZ),
                    new Vector3(spec.Width, spec.Height, spec.Depth),
                    buildingMaterial);

                footprints.Add(new Footprint(
                    spec.Name,
                    spec.CenterX - spec.Width * 0.5f,
                    spec.CenterZ - spec.Depth * 0.5f,
                    spec.Width,
                    spec.Depth));
            }
        }

        private static void CreateTrafficLights(Transform parent, MaterialSet materials, List<Footprint> footprints)
        {
            Transform lightsRoot = new GameObject("TrafficLights").transform;
            lightsRoot.SetParent(parent, false);

            for (int i = 0; i < TrafficLightPositions.Length; i++)
            {
                int index = i + 1;
                Vector2 position = TrafficLightPositions[i];

                GameObject lampRoot = new GameObject("TrafficLight_" + index);
                lampRoot.transform.SetParent(lightsRoot, false);
                lampRoot.transform.localPosition = new Vector3(position.x, 0f, position.y);
                lampRoot.transform.localRotation = Quaternion.Euler(0f, TrafficLightYawAngles[i], 0f);

                // 柱子
                CreatePrimitive(
                    PrimitiveType.Cube,
                    "TrafficLight_Pole_" + index,
                    lampRoot.transform,
                    new Vector3(0f, TrafficLightPoleHeight * 0.5f, 0f),
                    new Vector3(0.4f, TrafficLightPoleHeight, 0.4f),
                    materials.TrafficPole);

                // 顶部横臂，用于承托红绿两个灯球
                CreatePrimitive(
                    PrimitiveType.Cube,
                    "TrafficLight_Arm_" + index,
                    lampRoot.transform,
                    new Vector3(0f, TrafficLightPoleHeight + 0.075f, 0f),
                    new Vector3(1.5f, 0.15f, 0.3f),
                    materials.TrafficPole);

                // 红、绿灯球（球体直径 0.5，底部刚好落在横臂顶面）
                CreatePrimitive(
                    PrimitiveType.Sphere,
                    "TrafficLight_Red_" + index,
                    lampRoot.transform,
                    new Vector3(-0.42f, TrafficLightPoleHeight + 0.4f, 0f),
                    Vector3.one * 0.5f,
                    materials.RedLight);

                CreatePrimitive(
                    PrimitiveType.Sphere,
                    "TrafficLight_Green_" + index,
                    lampRoot.transform,
                    new Vector3(0.42f, TrafficLightPoleHeight + 0.4f, 0f),
                    Vector3.one * 0.5f,
                    materials.GreenLight);

                // 旋转后的横臂会扫过 0.75 半径，用 1.8x1.8 的保守占地做校验。
                footprints.Add(new Footprint(
                    "TrafficLight_" + index,
                    position.x - 0.9f,
                    position.y - 0.9f,
                    1.8f,
                    1.8f));
            }
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = localScale;

            if (material != null)
                gameObject.GetComponent<Renderer>().sharedMaterial = material;

            return gameObject;
        }

        private static MaterialSet CreateMaterials()
        {
            return new MaterialSet
            {
                Ground = GetOrCreateMaterial(
                    "M_Ground",
                    new Color(0.20f, 0.28f, 0.22f),
                    0f,
                    0.12f),
                Asphalt = GetOrCreateMaterial(
                    "M_Road_Asphalt",
                    new Color(0.11f, 0.115f, 0.125f),
                    0.05f,
                    0.28f),
                Buildings = new[]
                {
                    GetOrCreateMaterial("M_Building_Glass", new Color(0.30f, 0.44f, 0.55f), 0.55f, 0.85f),
                    GetOrCreateMaterial("M_Building_Concrete", new Color(0.74f, 0.75f, 0.78f), 0f, 0.30f),
                    GetOrCreateMaterial("M_Building_Sand", new Color(0.63f, 0.54f, 0.42f), 0.1f, 0.35f),
                    GetOrCreateMaterial("M_Building_Teal", new Color(0.20f, 0.46f, 0.50f), 0.35f, 0.65f),
                },
                TrafficPole = GetOrCreateMaterial(
                    "M_TrafficPole",
                    new Color(0.16f, 0.18f, 0.21f),
                    0.80f,
                    0.70f),
                RedLight = GetOrCreateMaterial(
                    "M_Light_Red",
                    new Color(0.75f, 0.06f, 0.05f),
                    0f,
                    0.60f,
                    new Color(1f, 0.10f, 0.06f),
                    6f),
                GreenLight = GetOrCreateMaterial(
                    "M_Light_Green",
                    new Color(0.05f, 0.75f, 0.25f),
                    0f,
                    0.60f,
                    new Color(0.10f, 1f, 0.35f),
                    6f),
            };
        }

        private static Material GetOrCreateMaterial(
            string materialName,
            Color baseColor,
            float metallic,
            float smoothness,
            Color? emission = null,
            float emissionIntensity = 1f)
        {
            string assetPath = MaterialFolder + "/" + materialName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                    throw new System.InvalidOperationException("找不到 URP Lit / Standard 着色器，无法创建材质。");

                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, assetPath);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", emission.Value * emissionIntensity);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ValidateLayout(List<Footprint> footprints)
        {
            Rect horizontalRoad = Rect.MinMaxRect(
                -GroundSize * 0.5f,
                -RoadWidth * 0.5f,
                GroundSize * 0.5f,
                RoadWidth * 0.5f);
            Rect verticalRoad = Rect.MinMaxRect(
                -RoadWidth * 0.5f,
                -GroundSize * 0.5f,
                RoadWidth * 0.5f,
                GroundSize * 0.5f);

            for (int i = 0; i < footprints.Count; i++)
            {
                Footprint current = footprints[i];
                EnsureInsideGround(current);

                if (Overlaps(current.Area, horizontalRoad) || Overlaps(current.Area, verticalRoad))
                {
                    throw new System.InvalidOperationException(
                        current.Name + " 与马路重叠，请调整位置或尺寸。");
                }

                for (int j = i + 1; j < footprints.Count; j++)
                {
                    Footprint other = footprints[j];
                    if (Overlaps(current.Area, other.Area))
                    {
                        throw new System.InvalidOperationException(
                            current.Name + " 与 " + other.Name + " 重叠，请调整位置或尺寸。");
                    }
                }
            }

            Debug.Log("[CrossroadBuilder] 布局校验通过：共 " + footprints.Count + " 个建筑/红绿灯对象，无重叠且未压占马路。");
        }

        private static void EnsureInsideGround(Footprint footprint)
        {
            const float halfGround = GroundSize * 0.5f;
            Rect area = footprint.Area;
            if (area.xMin < -halfGround || area.xMax > halfGround ||
                area.yMin < -halfGround || area.yMax > halfGround)
            {
                throw new System.InvalidOperationException(
                    footprint.Name + " 超出 100x100 地面范围。");
            }
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            const float tolerance = 0.001f;
            return a.xMin < b.xMax - tolerance &&
                   a.xMax > b.xMin + tolerance &&
                   a.yMin < b.yMax - tolerance &&
                   a.yMax > b.yMin + tolerance;
        }

        private static void RegisterSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(scene => scene.path == ScenePath))
                return;

            // 仅登记，不默认勾选，避免影响工程现有构建内容。
            scenes.Add(new EditorBuildSettingsScene(ScenePath, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private readonly struct BuildingSpec
        {
            public readonly string Name;
            public readonly float CenterX;
            public readonly float CenterZ;
            public readonly float Width;
            public readonly float Depth;
            public readonly float Height;
            public readonly int MaterialIndex;

            public BuildingSpec(
                string name,
                float centerX,
                float centerZ,
                float width,
                float depth,
                float height,
                int materialIndex)
            {
                Name = name;
                CenterX = centerX;
                CenterZ = centerZ;
                Width = width;
                Depth = depth;
                Height = height;
                MaterialIndex = materialIndex;
            }
        }

        private readonly struct Footprint
        {
            public readonly string Name;
            public readonly Rect Area;

            public Footprint(string name, float xMin, float zMin, float width, float depth)
            {
                Name = name;
                Area = new Rect(xMin, zMin, width, depth);
            }
        }

        private sealed class MaterialSet
        {
            public Material Ground;
            public Material Asphalt;
            public Material[] Buildings;
            public Material TrafficPole;
            public Material RedLight;
            public Material GreenLight;
        }
    }
}
