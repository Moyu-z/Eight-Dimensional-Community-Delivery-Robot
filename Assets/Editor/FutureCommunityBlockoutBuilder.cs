using System;
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
    /// 使用 Plane / Cube / Cylinder 等基础几何体程序化搭建未来社区白模。
    /// District_01：100x100 住宅区，含十字道路、深灰人行道、12 栋住宅、
    /// 低矮商业配套、蓝色配送站、路灯与行道树。
    /// District_02：东侧 100x100 扩展地块，含无缝对接道路、5 栋加宽的蓝色
    /// 半透明玻璃板楼、3 条 15m 高空中连廊、2 个银色商业综合体和未来路灯。
    /// District_03：北侧 100x100 医疗地块，主干道在此 T 字断头，后方为
    /// 大型白色医院综合体，屋顶设红十字，并以绿化带和绿篱与商业区隔离。
    /// 菜单入口：Tools/Future Community/Build Blockout Scene
    /// </summary>
    public static class FutureCommunityBlockoutBuilder
    {
        private const string ScenePath = "Assets/Scenes/FutureCommunity_Blockout.unity";
        private const string MaterialFolder = "Assets/Materials/FutureCommunityBlockout";

        private const float GroundSize = 100f;
        private const float RoadWidth = 9f;
        private const float RoadHalfWidth = RoadWidth * 0.5f;
        private const float SidewalkWidth = 2.5f;
        private const float SidewalkOuter = RoadHalfWidth + SidewalkWidth;
        private const float ClearCorridorHalfWidth = 8f;
        private const float PodiumHeight = 3f;
        private const float PodiumExtra = 2f;

        // 建筑围绕十字路口四象限布置，全部避开道路与人行道。
        private static readonly BuildingSpec[] BuildingSpecs =
        {
            // 东北象限
            new BuildingSpec("Residential_NE_01", 18f, 18f, 12f, 12f, 26f, 0),
            new BuildingSpec("Residential_NE_02", 36f, 17f, 12f, 14f, 38f, 2),
            new BuildingSpec("Residential_NE_03", 17f, 37f, 13f, 12f, 20f, 1),

            // 西北象限
            new BuildingSpec("Residential_NW_01", -18f, 18f, 12f, 12f, 32f, 1),
            new BuildingSpec("Residential_NW_02", -36f, 18f, 12f, 12f, 22f, 0),
            new BuildingSpec("Residential_NW_03", -17f, 37f, 12f, 13f, 40f, 2),

            // 西南象限
            new BuildingSpec("Residential_SW_01", -18f, -18f, 12f, 12f, 36f, 2),
            new BuildingSpec("Residential_SW_02", -37f, -18f, 12f, 12f, 24f, 0),
            new BuildingSpec("Residential_SW_03", -18f, -37f, 12f, 12f, 30f, 1),

            // 东南象限（配送站位于该象限靠近路口处）
            new BuildingSpec("Residential_SE_01", 37f, -18f, 12f, 12f, 34f, 1),
            new BuildingSpec("Residential_SE_02", 18f, -37f, 13f, 12f, 28f, 0),
            new BuildingSpec("Residential_SE_03", 37f, -37f, 12f, 12f, 42f, 2),
        };

        // 低矮宽体商业配套：南侧超市、南侧社区医院、北侧社区商业。
        private static readonly CommercialSpec[] CommercialSpecs =
        {
            new CommercialSpec("Commercial_Supermarket", -36f, -44.5f, 13f, 7f, 5.5f, 0),
            new CommercialSpec("Commercial_Hospital", 36f, -46.5f, 13f, 4f, 7.5f, 1),
            new CommercialSpec("Commercial_Store", 36f, 45f, 13f, 6.5f, 6f, 2),
        };

        // District_02 为现代商业与科技园混合区，采用完全不同的蓝玻璃 + 银色金属风格。
        // 坐标是 District_02_Expansion 根节点下的局部坐标。
        // 宽大板楼布局：X 向 26m，Z 向 14-16m，避免继续呈现细高柱状。
        private static readonly TowerSpec[] District02Towers =
        {
            new TowerSpec("Tower_BlueGlass_NE_01", 34f, 35f, 26f, 14f, 56f),
            new TowerSpec("Tower_BlueGlass_NE_02", 34f, 17f, 26f, 14f, 60f),
            new TowerSpec("Tower_BlueGlass_NW_01", -30f, 18f, 26f, 16f, 48f),
            new TowerSpec("Tower_BlueGlass_SW_01", -30f, -25f, 26f, 16f, 52f),
            new TowerSpec("Tower_BlueGlass_SE_01", 34f, -24f, 26f, 16f, 44f),
        };

        private static readonly MallSpec[] District02Malls =
        {
            new MallSpec("Mall_Silver_NW_01", -35f, 42f, 24f, 10f, 10f),
            new MallSpec("Mall_Silver_SE_01", 35f, -42f, 24f, 10f, 12f),
        };

        // 三条天桥：东北双塔之间、东西两处跨越横向道路的高空连廊。
        private static readonly SkybridgeSpec[] District02Skybridges =
        {
            new SkybridgeSpec("Skybridge_NE_Towers", 34f, 26f, 8f, 4f, 2.4f),
            new SkybridgeSpec("Skybridge_East_OverRoad", 34f, -3f, 30f, 4f, 2.4f),
            new SkybridgeSpec("Skybridge_West_OverRoad", -30f, -3.5f, 31f, 4f, 2.4f),
        };

        [MenuItem("Tools/Future Community/Build Blockout Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Build();

            EditorUtility.DisplayDialog(
                "Future Community",
                "未来社区基础白模已生成并保存：\n" + ScenePath,
                "好");
        }

        /// <summary>
        /// 供命令行批处理调用的入口，失败时返回非 0 退出码。
        /// </summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[BlockoutBuilder] 场景生成完成：" + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[BlockoutBuilder] 场景生成失败：\n" + exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("请先退出 Play Mode 再生成白模场景。");

            EnsureFolder(MaterialFolder);
            MigrateObsoleteMaterials();
            MaterialSet materials = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureEnvironment();
            CreateCameraAndSun();

            GameObject root = new GameObject("FutureCommunity_Blockout");
            Transform rootTransform = root.transform;

            CreateGround(rootTransform, materials);
            CreateRoadNetwork(rootTransform, materials);
            CreateSidewalks(rootTransform, materials);
            CreateRoadMarkings(rootTransform, materials);

            List<Footprint> footprints = new List<Footprint>();
            CreateBuildings(rootTransform, materials, footprints);
            CreateCommercialBuildings(rootTransform, materials, footprints);
            GameObject station = CreateDeliveryStation(rootTransform, materials, footprints);
            CreateStreetFurniture(rootTransform, materials);

            // 第二阶段扩展地块：先完成地面、十字道路、人行道和标线，
            // 道路中心与 District_01 完全保持一致，便于后续对接。
            GameObject expansionDistrict = CreateExpansionDistrict(materials);

            // 第三阶段医疗地块：主干道在 Z=100 处断头形成 T 字路口，医院位于其后。
            GameObject medicalDistrict = CreateMedicalDistrict(materials);

            ValidateLayout(footprints);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("无法保存场景：" + ScenePath);

            RegisterSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            FrameSceneView();

            Selection.activeGameObject = medicalDistrict;
            Debug.Log(
                "[BlockoutBuilder] 白模生成完成。District_01 住宅 " + BuildingSpecs.Length +
                " 栋、商业配套 " + CommercialSpecs.Length +
                " 栋，配送站位置 " + station.transform.position +
                "；District_02 蓝玻璃板楼 " + District02Towers.Length +
                " 栋、银色商业体 " + District02Malls.Length +
                " 个、天桥 " + District02Skybridges.Length +
                " 条、未来路灯 16 盏；District_03 T 字路口与医院已完成，场景已保存至 " + ScenePath);
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.64f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.43f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.21f, 0.20f);
            RenderSettings.ambientIntensity = 1.18f;
            RenderSettings.fog = false;
        }

        private static void CreateCameraAndSun()
        {
            GameObject sunObject = new GameObject("Directional Light");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.89f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunObject.transform.position = new Vector3(0f, 40f, 0f);
            sunObject.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
            RenderSettings.sun = sun;

            // 摄像机方向的补光，避免背光面的体块过暗、失去白模层次。
            GameObject fillObject = new GameObject("Fill Light");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.68f, 0.76f, 0.90f);
            fill.intensity = 0.38f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            // 白模展示使用中性冷灰背景，避免默认天空盒的暖褐色地平线干扰体块阅读。
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.68f, 0.72f, 0.77f);
            // 45 度俯视下用 60 度视场角，保证 L 形三个地块都完整入画。
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 600f;
            camera.allowHDR = true;
            // 抬高并移到 L 形三个地块的包围盒中心：中心视线在 (50,0,50) 落地。
            cameraObject.transform.position = new Vector3(50f, 150f, -100f);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        }

        private static GameObject CreateGround(
            Transform parent,
            MaterialSet materials,
            string groundName = "Ground_100x100")
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = groundName;
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = Vector3.zero;
            // Plane 的原始尺寸为 10x10，缩放 10 倍后得到 100x100 的地面。
            ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = materials.Ground;
            return ground;
        }

        private static GameObject CreateExpansionDistrict(MaterialSet materials)
        {
            // 新地块整体作为一个独立父节点，内部坐标与 District_01 完全一致，
            // 只是根节点整体平移 +100m，因此道路断面、宽度和高度可以无缝复制。
            GameObject district = new GameObject("District_02_Expansion");
            district.transform.position = new Vector3(GroundSize, 0f, 0f);

            CreateGround(district.transform, materials, "Ground_100x100_District02");
            CreateRoadNetwork(district.transform, materials);
            CreateSidewalks(district.transform, materials);
            CreateRoadMarkings(district.transform, materials);
            CreateDistrict02Buildings(district.transform, materials);
            CreateDistrict02Skybridges(district.transform, materials);
            CreateDistrict02StreetLights(district.transform, materials);

            Debug.Log(
                "[BlockoutBuilder] District_02_Expansion 完成：地面中心 (100,0,0)，" +
                "东西向道路 x=50..150，与 District_01 在 x=50 处无缝对接；" +
                "蓝玻璃板楼 " + District02Towers.Length + " 栋，银色商业体 " + District02Malls.Length +
                " 个，天桥 " + District02Skybridges.Length + " 条。");
            return district;
        }

        private static GameObject CreateMedicalDistrict(MaterialSet materials)
        {
            // 第三地块位于 District_02 正北：x=50..150，z=50..150。
            // 主干道从南侧进入，在本地 z=0（世界 Z=100）处与横向道路形成 T 字。
            GameObject district = new GameObject("District_03_Medical");
            district.transform.position = new Vector3(100f, 0f, 100f);

            CreateGround(district.transform, materials, "Ground_100x100_District03");
            CreateTeeRoadNetwork(district.transform, materials);
            CreateTeeSidewalks(district.transform, materials);
            CreateTeeRoadMarkings(district.transform, materials);
            CreateMedicalBuffer(district.transform, materials);
            CreateHospitalComplex(district.transform, materials);

            Debug.Log("[BlockoutBuilder] District_03_Medical 完成：T 字路口、绿化隔离带、大型医院综合体。");
            return district;
        }

        private static void CreateTeeRoadNetwork(Transform district, MaterialSet materials)
        {
            Transform roadsRoot = new GameObject("Roads").transform;
            roadsRoot.SetParent(district, false);

            // 横向道路（T 字横杆），顶面 0.10。
            CreatePrimitive(PrimitiveType.Cube, "Road_Tee_Crossbar", roadsRoot,
                new Vector3(0f, 0.05f, 0f),
                new Vector3(GroundSize, 0.10f, RoadWidth),
                materials.Asphalt);

            // 主干道（T 字竖杆）从南边界 z=-50 延伸到横杆北沿 z=4.5，顶面 0.12。
            CreatePrimitive(PrimitiveType.Cube, "Road_Tee_Stem", roadsRoot,
                new Vector3(0f, 0.06f, -22.75f),
                new Vector3(RoadWidth, 0.12f, 54.5f),
                materials.Asphalt);
        }

        private static void CreateTeeSidewalks(Transform district, MaterialSet materials)
        {
            Transform sidewalksRoot = new GameObject("Sidewalks").transform;
            sidewalksRoot.SetParent(district, false);

            const float armLength = GroundSize * 0.5f - RoadHalfWidth; // 45.5
            const float armCenter = RoadHalfWidth + armLength * 0.5f;  // 27.25
            const float sidewalkCenter = RoadHalfWidth + SidewalkWidth * 0.5f; // 5.75

            // 横杆北侧连续人行道；南侧在主干道入口处断开。
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_T_North", sidewalksRoot,
                new Vector3(0f, 0.08f, sidewalkCenter),
                new Vector3(GroundSize, 0.16f, SidewalkWidth), materials.Sidewalk);

            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_T_South_E", sidewalksRoot,
                new Vector3(armCenter, 0.08f, -sidewalkCenter),
                new Vector3(armLength, 0.16f, SidewalkWidth), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_T_South_W", sidewalksRoot,
                new Vector3(-armCenter, 0.08f, -sidewalkCenter),
                new Vector3(armLength, 0.16f, SidewalkWidth), materials.Sidewalk);

            // 主干道两侧人行道，从南边界一直到横杆南侧人行道。
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_T_Stem_E", sidewalksRoot,
                new Vector3(sidewalkCenter, 0.08f, -armCenter),
                new Vector3(SidewalkWidth, 0.16f, armLength), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_T_Stem_W", sidewalksRoot,
                new Vector3(-sidewalkCenter, 0.08f, -armCenter),
                new Vector3(SidewalkWidth, 0.16f, armLength), materials.Sidewalk);
        }

        private static void CreateTeeRoadMarkings(Transform district, MaterialSet materials)
        {
            Transform markingsRoot = new GameObject("RoadMarkings").transform;
            markingsRoot.SetParent(district, false);

            // 横向道路中心虚线。
            for (float x = -46f; x <= 46f; x += 6f)
            {
                if (Mathf.Abs(x) < 8f)
                    continue;

                CreatePrimitive(PrimitiveType.Cube, "LaneDash_T_EW", markingsRoot,
                    new Vector3(x, 0.106f, 0f),
                    new Vector3(3f, 0.012f, 0.18f), materials.Marking);
            }

            // 主干道中心虚线，到 T 字口前结束。
            for (float z = -46f; z <= -10f; z += 6f)
            {
                CreatePrimitive(PrimitiveType.Cube, "LaneDash_T_NS", markingsRoot,
                    new Vector3(0f, 0.126f, z),
                    new Vector3(0.18f, 0.012f, 3f), materials.Marking);
            }

            for (int i = 0; i < 5; i++)
            {
                float lateral = -3.6f + i * 1.8f;

                // 横杆北侧、主干道入口前的斑马线。
                CreatePrimitive(PrimitiveType.Cube, "Crosswalk_T_Stem", markingsRoot,
                    new Vector3(lateral, 0.126f, -7.3f),
                    new Vector3(0.55f, 0.012f, 2.2f), materials.Marking);

                // 横杆东西两侧的斑马线。
                CreatePrimitive(PrimitiveType.Cube, "Crosswalk_T_E", markingsRoot,
                    new Vector3(7.3f, 0.106f, lateral),
                    new Vector3(2.2f, 0.012f, 0.55f), materials.Marking);
                CreatePrimitive(PrimitiveType.Cube, "Crosswalk_T_W", markingsRoot,
                    new Vector3(-7.3f, 0.106f, lateral),
                    new Vector3(2.2f, 0.012f, 0.55f), materials.Marking);
            }
        }

        private static void CreateMedicalBuffer(Transform district, MaterialSet materials)
        {
            Transform bufferRoot = new GameObject("MedicalBuffer").transform;
            bufferRoot.SetParent(district, false);

            // 商业区与医疗区之间的绿化缓冲：12m 深草带 + 绿篱 + 门柱。
            CreatePrimitive(PrimitiveType.Cube, "GreenBelt_West", bufferRoot,
                new Vector3(-29f, 0.11f, -44f),
                new Vector3(42f, 0.22f, 12f),
                materials.Ground);
            CreatePrimitive(PrimitiveType.Cube, "GreenBelt_East", bufferRoot,
                new Vector3(29f, 0.11f, -44f),
                new Vector3(42f, 0.22f, 12f),
                materials.Ground);

            CreatePrimitive(PrimitiveType.Cube, "HedgeBase_West", bufferRoot,
                new Vector3(-28f, 0.3f, -38f),
                new Vector3(40f, 0.6f, 0.9f),
                materials.BuildingTrim);
            CreatePrimitive(PrimitiveType.Cube, "HedgeBase_East", bufferRoot,
                new Vector3(28f, 0.3f, -38f),
                new Vector3(40f, 0.6f, 0.9f),
                materials.BuildingTrim);

            CreatePrimitive(PrimitiveType.Cube, "Hedge_West", bufferRoot,
                new Vector3(-28f, 1.5f, -38f),
                new Vector3(40f, 1.8f, 0.8f),
                materials.TreeLeaf);
            CreatePrimitive(PrimitiveType.Cube, "Hedge_East", bufferRoot,
                new Vector3(28f, 1.5f, -38f),
                new Vector3(40f, 1.8f, 0.8f),
                materials.TreeLeaf);

            CreatePrimitive(PrimitiveType.Cube, "GatePillar_West", bufferRoot,
                new Vector3(-8.5f, 1.5f, -38f),
                new Vector3(0.9f, 3f, 0.9f),
                materials.SilverMetal);
            CreatePrimitive(PrimitiveType.Cube, "GatePillar_East", bufferRoot,
                new Vector3(8.5f, 1.5f, -38f),
                new Vector3(0.9f, 3f, 0.9f),
                materials.SilverMetal);

            float[] treeXs = { -34f, -18f, 18f, 34f };
            for (int i = 0; i < treeXs.Length; i++)
            {
                CreateStreetTree(bufferRoot, new Vector3(treeXs[i], 0.22f, -44f), i, materials);
            }
        }

        private static void CreateHospitalComplex(Transform district, MaterialSet materials)
        {
            Transform hospitalRoot = new GameObject("HospitalComplex").transform;
            hospitalRoot.SetParent(district, false);

            // 医院前广场，与 T 字路口北侧人行道平接。
            CreatePrimitive(PrimitiveType.Cube, "HospitalForecourt", hospitalRoot,
                new Vector3(0f, 0.08f, 9.5f),
                new Vector3(80f, 0.16f, 5f),
                materials.Plaza);

            // 巨大的白色主楼 + 中央塔楼，占满 T 字路口后方。
            CreatePrimitive(PrimitiveType.Cube, "HospitalMainBlock", hospitalRoot,
                new Vector3(0f, 9f, 27f),
                new Vector3(76f, 18f, 30f),
                materials.BuildingMaterials[0]);

            CreatePrimitive(PrimitiveType.Cube, "HospitalCentralTower", hospitalRoot,
                new Vector3(0f, 13f, 27f),
                new Vector3(30f, 26f, 16f),
                materials.BuildingMaterials[0]);

            CreatePrimitive(PrimitiveType.Cube, "HospitalMainRoof", hospitalRoot,
                new Vector3(0f, 18.25f, 27f),
                new Vector3(77f, 0.5f, 31f),
                materials.SilverMetal);
            CreatePrimitive(PrimitiveType.Cube, "HospitalTowerRoof", hospitalRoot,
                new Vector3(0f, 26.25f, 27f),
                new Vector3(31f, 0.5f, 17f),
                materials.SilverMetal);

            // 屋顶大型红十字：两片扁平 Cube 叠成十字。
            CreatePrimitive(PrimitiveType.Cube, "RoofCross_V", hospitalRoot,
                new Vector3(0f, 26.6f, 27f),
                new Vector3(2.6f, 0.3f, 12f),
                materials.HospitalSign);
            CreatePrimitive(PrimitiveType.Cube, "RoofCross_H", hospitalRoot,
                new Vector3(0f, 26.6f, 27f),
                new Vector3(12f, 0.3f, 2.6f),
                materials.HospitalSign);

            // 南立面入口：玻璃门厅、雨棚、落客门廊。
            CreatePrimitive(PrimitiveType.Cube, "HospitalEntranceGlass", hospitalRoot,
                new Vector3(0f, 2.2f, 11.9f),
                new Vector3(22f, 4f, 0.2f),
                materials.Glass);
            CreatePrimitive(PrimitiveType.Cube, "HospitalCanopy", hospitalRoot,
                new Vector3(0f, 5f, 9.5f),
                new Vector3(26f, 0.5f, 5f),
                materials.SilverMetal);
            CreatePrimitive(PrimitiveType.Cube, "HospitalCanopyColumn_W", hospitalRoot,
                new Vector3(-11f, 2.5f, 8f),
                new Vector3(0.45f, 5f, 0.45f),
                materials.SilverMetal);
            CreatePrimitive(PrimitiveType.Cube, "HospitalCanopyColumn_E", hospitalRoot,
                new Vector3(11f, 2.5f, 8f),
                new Vector3(0.45f, 5f, 0.45f),
                materials.SilverMetal);

            // 南立面横向病房窗带。
            for (int floor = 0; floor < 3; floor++)
            {
                CreatePrimitive(PrimitiveType.Cube, "HospitalWindowBand", hospitalRoot,
                    new Vector3(0f, 7f + floor * 3.5f, 11.9f),
                    new Vector3(60f, 1.15f, 0.16f),
                    materials.Glass);
            }

            // 中央塔楼高处的窗带。
            CreatePrimitive(PrimitiveType.Cube, "HospitalTowerWindowBand_Low", hospitalRoot,
                new Vector3(0f, 20f, 18.9f),
                new Vector3(24f, 1.2f, 0.16f),
                materials.Glass);
            CreatePrimitive(PrimitiveType.Cube, "HospitalTowerWindowBand_High", hospitalRoot,
                new Vector3(0f, 23f, 18.9f),
                new Vector3(24f, 1.2f, 0.16f),
                materials.Glass);

            // 入口上方的红色发光标识带。
            CreatePrimitive(PrimitiveType.Cube, "HospitalSignBand", hospitalRoot,
                new Vector3(0f, 6.1f, 11.88f),
                new Vector3(14f, 0.8f, 0.2f),
                materials.HospitalSign);

            List<Footprint> footprints = new List<Footprint>
            {
                new Footprint("HospitalMainBlock", -38f, 12f, 76f, 30f)
            };
            ValidateHospitalLayout(footprints);
        }

        private static void ValidateHospitalLayout(List<Footprint> footprints)
        {
            // T 字地块的主干道只存在于 z<=8 的南半区，北半区允许医院跨越道路中线。
            Rect crossbarCorridor = Rect.MinMaxRect(-GroundSize * 0.5f, -ClearCorridorHalfWidth,
                GroundSize * 0.5f, ClearCorridorHalfWidth);
            Rect stemCorridor = Rect.MinMaxRect(-ClearCorridorHalfWidth, -GroundSize * 0.5f,
                ClearCorridorHalfWidth, ClearCorridorHalfWidth);

            const float edgeMargin = 1f;
            for (int i = 0; i < footprints.Count; i++)
            {
                Footprint current = footprints[i];
                Rect area = current.Area;

                if (area.xMin < -GroundSize * 0.5f + edgeMargin ||
                    area.xMax > GroundSize * 0.5f - edgeMargin ||
                    area.yMin < -GroundSize * 0.5f + edgeMargin ||
                    area.yMax > GroundSize * 0.5f - edgeMargin)
                {
                    throw new InvalidOperationException(
                        "District_03: " + current.Name + " 超出医疗地块范围（至少保留 1 米边距）。");
                }

                if (Overlaps(area, crossbarCorridor) || Overlaps(area, stemCorridor))
                {
                    throw new InvalidOperationException(
                        "District_03: " + current.Name + " 压占了 T 字道路或人行道，请调整位置。");
                }

                for (int j = i + 1; j < footprints.Count; j++)
                {
                    Footprint other = footprints[j];
                    if (Overlaps(area, other.Area))
                    {
                        throw new InvalidOperationException(
                            "District_03: " + current.Name + " 与 " + other.Name + " 重叠，请调整位置或尺寸。");
                    }
                }
            }

            Debug.Log("[BlockoutBuilder] District_03 医院布局校验通过。");
        }

        private static void CreateDistrict02Buildings(Transform district, MaterialSet materials)
        {
            List<Footprint> footprints = new List<Footprint>();

            Transform towersRoot = new GameObject("BlueGlassTowers").transform;
            towersRoot.SetParent(district, false);

            foreach (TowerSpec spec in District02Towers)
            {
                CreateGlassTower(towersRoot, spec, materials);

                // 4 米高银色基座 + 外挑 2 米，作为塔楼的占位/碰撞边界。
                footprints.Add(new Footprint(
                    spec.Name,
                    spec.X - (spec.Width + 2f) * 0.5f,
                    spec.Z - (spec.Depth + 2f) * 0.5f,
                    spec.Width + 2f,
                    spec.Depth + 2f));
            }

            Transform mallsRoot = new GameObject("SilverCommercialComplexes").transform;
            mallsRoot.SetParent(district, false);

            for (int i = 0; i < District02Malls.Length; i++)
            {
                MallSpec spec = District02Malls[i];
                CreateSilverMall(mallsRoot, spec, i, materials);

                // 屋顶挑檐每侧外挑 0.7 米，用 1.4 米的额外宽度做校验。
                footprints.Add(new Footprint(
                    spec.Name,
                    spec.X - (spec.Width + 1.4f) * 0.5f,
                    spec.Z - (spec.Depth + 1.4f) * 0.5f,
                    spec.Width + 1.4f,
                    spec.Depth + 1.4f));
            }

            ValidateLocalDistrictLayout(footprints, "District_02");
        }

        private static void CreateDistrict02Skybridges(Transform district, MaterialSet materials)
        {
            Transform bridgesRoot = new GameObject("Skybridges").transform;
            bridgesRoot.SetParent(district, false);

            foreach (SkybridgeSpec spec in District02Skybridges)
            {
                Transform bridgeRoot = new GameObject(spec.Name).transform;
                bridgeRoot.SetParent(bridgesRoot, false);
                bridgeRoot.localPosition = new Vector3(spec.X, 15f, spec.Z);

                // 半透明玻璃连廊主体，高度约 15m，两端各嵌入塔楼约 2m。
                GameObject glass = CreatePrimitive(PrimitiveType.Cube, "GlassBridge", bridgeRoot,
                    Vector3.zero,
                    new Vector3(spec.Width, spec.Height, spec.Length),
                    materials.BlueGlass);
                glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                // 银色桥底板，让玻璃连廊在鸟瞰视角下有清晰的结构边界。
                CreatePrimitive(PrimitiveType.Cube, "BridgeFloor", bridgeRoot,
                    new Vector3(0f, -spec.Height * 0.5f - 0.15f, 0f),
                    new Vector3(spec.Width + 0.4f, 0.3f, spec.Length + 0.4f),
                    materials.SilverMetal);

                // 两侧细银色护栏。
                CreatePrimitive(PrimitiveType.Cube, "BridgeRail_L", bridgeRoot,
                    new Vector3(-spec.Width * 0.5f - 0.08f, 0.15f, 0f),
                    new Vector3(0.16f, 0.35f, spec.Length),
                    materials.SilverMetal);
                CreatePrimitive(PrimitiveType.Cube, "BridgeRail_R", bridgeRoot,
                    new Vector3(spec.Width * 0.5f + 0.08f, 0.15f, 0f),
                    new Vector3(0.16f, 0.35f, spec.Length),
                    materials.SilverMetal);
            }

            Debug.Log("[BlockoutBuilder] District_02 天桥完成：共 " + District02Skybridges.Length + " 条，中心高度 Y=15。");
        }

        private static void CreateGlassTower(
            Transform parent,
            TowerSpec spec,
            MaterialSet materials)
        {
            Transform towerRoot = new GameObject(spec.Name).transform;
            towerRoot.SetParent(parent, false);
            towerRoot.localPosition = new Vector3(spec.X, 0f, spec.Z);

            const float podiumHeight = 4f;
            CreatePrimitive(PrimitiveType.Cube, "Podium", towerRoot,
                new Vector3(0f, podiumHeight * 0.5f, 0f),
                new Vector3(spec.Width + 2f, podiumHeight, spec.Depth + 2f),
                materials.SilverDark);

            GameObject glassBody = CreatePrimitive(PrimitiveType.Cube, "GlassCurtainWall", towerRoot,
                new Vector3(0f, podiumHeight + spec.Height * 0.5f, 0f),
                new Vector3(spec.Width, spec.Height, spec.Depth),
                materials.BlueGlass);
            // 半透明幕墙不投射完全不透明的硬阴影，使体块更接近玻璃。
            glassBody.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            float halfWidth = spec.Width * 0.5f;
            float halfDepth = spec.Depth * 0.5f;
            float centerY = podiumHeight + spec.Height * 0.5f;

            // 四角银色竖向框架。
            foreach (float xSign in new[] { -1f, 1f })
            {
                foreach (float zSign in new[] { -1f, 1f })
                {
                    CreatePrimitive(PrimitiveType.Cube, "CornerMullion", towerRoot,
                        new Vector3(xSign * halfWidth, centerY, zSign * halfDepth),
                        new Vector3(0.42f, spec.Height, 0.42f),
                        materials.SilverMetal);
                }
            }

            // 水平楼层线，透过玻璃可见，强化幕墙分层。
            for (int floor = 1; floor <= 4; floor++)
            {
                float y = podiumHeight + spec.Height * floor / 5f;
                CreatePrimitive(PrimitiveType.Cube, "FloorBand", towerRoot,
                    new Vector3(0f, y, 0f),
                    new Vector3(spec.Width + 0.36f, 0.22f, spec.Depth + 0.36f),
                    materials.SilverMetal);
            }

            // 南、西、东三个立面增加竖向细框，避免玻璃盒子过于单调。
            for (int bay = 1; bay <= 2; bay++)
            {
                float x = -halfWidth + spec.Width * bay / 3f;
                CreatePrimitive(PrimitiveType.Cube, "Mullion_S", towerRoot,
                    new Vector3(x, centerY, -halfDepth - 0.06f),
                    new Vector3(0.16f, spec.Height, 0.16f),
                    materials.SilverMetal);

                float z = -halfDepth + spec.Depth * bay / 3f;
                CreatePrimitive(PrimitiveType.Cube, "Mullion_W", towerRoot,
                    new Vector3(-halfWidth - 0.06f, centerY, z),
                    new Vector3(0.16f, spec.Height, 0.16f),
                    materials.SilverMetal);
                CreatePrimitive(PrimitiveType.Cube, "Mullion_E", towerRoot,
                    new Vector3(halfWidth + 0.06f, centerY, z),
                    new Vector3(0.16f, spec.Height, 0.16f),
                    materials.SilverMetal);
            }

            CreatePrimitive(PrimitiveType.Cube, "RoofCap", towerRoot,
                new Vector3(0f, podiumHeight + spec.Height + 0.35f, 0f),
                new Vector3(spec.Width + 0.8f, 0.7f, spec.Depth + 0.8f),
                materials.SilverMetal);

            CreatePrimitive(PrimitiveType.Cube, "RoofCrown", towerRoot,
                new Vector3(0f, podiumHeight + spec.Height + 0.7f + 1.5f, 0f),
                new Vector3(spec.Width * 0.34f, 3f, spec.Depth * 0.34f),
                materials.SilverDark);
        }

        private static void CreateSilverMall(
            Transform parent,
            MallSpec spec,
            int index,
            MaterialSet materials)
        {
            Transform mallRoot = new GameObject(spec.Name).transform;
            mallRoot.SetParent(parent, false);
            mallRoot.localPosition = new Vector3(spec.X, 0f, spec.Z);

            const float plinthHeight = 0.4f;
            CreatePrimitive(PrimitiveType.Cube, "Plinth", mallRoot,
                new Vector3(0f, plinthHeight * 0.5f, 0f),
                new Vector3(spec.Width + 1f, plinthHeight, spec.Depth + 1f),
                materials.SilverDark);

            CreatePrimitive(PrimitiveType.Cube, "Body", mallRoot,
                new Vector3(0f, plinthHeight + spec.Height * 0.5f, 0f),
                new Vector3(spec.Width, spec.Height, spec.Depth),
                materials.SilverMetal);

            float top = plinthHeight + spec.Height;
            CreatePrimitive(PrimitiveType.Cube, "RoofSlab", mallRoot,
                new Vector3(0f, top + 0.3f, 0f),
                new Vector3(spec.Width + 1.4f, 0.6f, spec.Depth + 1.4f),
                materials.SilverMetal);

            CreatePrimitive(PrimitiveType.Cube, "RoofUnit", mallRoot,
                new Vector3(0f, top + 0.6f + 0.9f, 0f),
                new Vector3(spec.Width * 0.28f, 1.8f, spec.Depth * 0.34f),
                materials.SilverMetal);

            float faceZ = spec.Depth * 0.5f + 0.08f;
            GameObject glassSouth = CreatePrimitive(PrimitiveType.Cube, "GlassBand_S", mallRoot,
                new Vector3(0f, plinthHeight + 2.2f, -faceZ),
                new Vector3(spec.Width * 0.82f, 3.2f, 0.16f),
                materials.BlueGlass);
            glassSouth.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            GameObject glassNorth = CreatePrimitive(PrimitiveType.Cube, "GlassBand_N", mallRoot,
                new Vector3(0f, plinthHeight + 2.2f, faceZ),
                new Vector3(spec.Width * 0.82f, 3.2f, 0.16f),
                materials.BlueGlass);
            glassNorth.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            CreatePrimitive(PrimitiveType.Cube, "Canopy_S", mallRoot,
                new Vector3(0f, plinthHeight + 3.95f, -faceZ - 0.8f),
                new Vector3(spec.Width * 0.55f, 0.2f, 1.6f),
                materials.SilverMetal);
            CreatePrimitive(PrimitiveType.Cube, "Canopy_N", mallRoot,
                new Vector3(0f, plinthHeight + 3.95f, faceZ + 0.8f),
                new Vector3(spec.Width * 0.55f, 0.2f, 1.6f),
                materials.SilverMetal);

            // 屋顶边缘的青色发光带与南立面的发光媒体屏，形成未来商业界面。
            CreatePrimitive(PrimitiveType.Cube, "GlowBand_S", mallRoot,
                new Vector3(0f, top + 0.25f, -spec.Depth * 0.5f - 0.1f),
                new Vector3(spec.Width * 0.7f, 0.18f, 0.2f),
                materials.TechGlow);
            CreatePrimitive(PrimitiveType.Cube, "GlowBand_N", mallRoot,
                new Vector3(0f, top + 0.25f, spec.Depth * 0.5f + 0.1f),
                new Vector3(spec.Width * 0.7f, 0.18f, 0.2f),
                materials.TechGlow);

            CreatePrimitive(PrimitiveType.Cube, "MediaScreen_S", mallRoot,
                new Vector3(0f, plinthHeight + 4.8f, -faceZ - 0.02f),
                new Vector3(spec.Width * 0.32f, 2.4f, 0.16f),
                materials.TechGlow);

            // 第一座购物中心增加一个退台体块，避免两栋商业体完全一样。
            if (index == 0)
            {
                CreatePrimitive(PrimitiveType.Cube, "UpperMass", mallRoot,
                    new Vector3(0f, top + 0.6f + 2f, 0f),
                    new Vector3(spec.Width * 0.42f, 4f, spec.Depth * 0.66f),
                    materials.SilverMetal);
                CreatePrimitive(PrimitiveType.Cube, "UpperMassGlow", mallRoot,
                    new Vector3(0f, top + 0.6f + 4.1f, 0f),
                    new Vector3(spec.Width * 0.3f, 0.2f, spec.Depth * 0.5f),
                    materials.TechGlow);
            }
        }

        private static void CreateDistrict02StreetLights(Transform district, MaterialSet materials)
        {
            Transform lightsRoot = new GameObject("FutureStreetLights").transform;
            lightsRoot.SetParent(district, false);

            float[] positions = { -40f, -20f, 20f, 40f };
            foreach (float position in positions)
            {
                CreateFutureStreetLight(lightsRoot, new Vector3(position, 0.16f, 6.3f), materials);
                CreateFutureStreetLight(lightsRoot, new Vector3(position, 0.16f, -6.3f), materials);
                CreateFutureStreetLight(lightsRoot, new Vector3(6.3f, 0.16f, position), materials);
                CreateFutureStreetLight(lightsRoot, new Vector3(-6.3f, 0.16f, position), materials);
            }
        }

        private static void CreateFutureStreetLight(
            Transform parent,
            Vector3 position,
            MaterialSet materials)
        {
            Transform lightRoot = new GameObject("FutureStreetLight").transform;
            lightRoot.SetParent(parent, false);
            lightRoot.localPosition = position;

            CreatePrimitive(PrimitiveType.Cube, "Base", lightRoot,
                new Vector3(0f, 0.1f, 0f),
                new Vector3(0.5f, 0.2f, 0.5f),
                materials.SilverDark);

            // 6.4 米细长灯杆，顶部是青色发光球与发光环。
            CreatePrimitive(PrimitiveType.Cylinder, "Pole", lightRoot,
                new Vector3(0f, 3.2f, 0f),
                new Vector3(0.16f, 3.2f, 0.16f),
                materials.BuildingTrim);

            CreatePrimitive(PrimitiveType.Cylinder, "GlowCollar", lightRoot,
                new Vector3(0f, 5.52f, 0f),
                new Vector3(0.46f, 0.05f, 0.46f),
                materials.TechGlow);

            CreatePrimitive(PrimitiveType.Sphere, "GlowOrb", lightRoot,
                new Vector3(0f, 6.48f, 0f),
                new Vector3(0.62f, 0.62f, 0.62f),
                materials.TechGlow);
        }

        private static void ValidateLocalDistrictLayout(List<Footprint> footprints, string districtName)
        {
            Rect verticalCorridor = Rect.MinMaxRect(-ClearCorridorHalfWidth, -GroundSize * 0.5f,
                ClearCorridorHalfWidth, GroundSize * 0.5f);
            Rect horizontalCorridor = Rect.MinMaxRect(-GroundSize * 0.5f, -ClearCorridorHalfWidth,
                GroundSize * 0.5f, ClearCorridorHalfWidth);

            const float edgeMargin = 1f;
            for (int i = 0; i < footprints.Count; i++)
            {
                Footprint current = footprints[i];
                Rect area = current.Area;

                if (area.xMin < -GroundSize * 0.5f + edgeMargin ||
                    area.xMax > GroundSize * 0.5f - edgeMargin ||
                    area.yMin < -GroundSize * 0.5f + edgeMargin ||
                    area.yMax > GroundSize * 0.5f - edgeMargin)
                {
                    throw new InvalidOperationException(
                        districtName + ": " + current.Name + " 超出扩展地块范围（至少保留 1 米边距）。");
                }

                if (Overlaps(area, verticalCorridor) || Overlaps(area, horizontalCorridor))
                {
                    throw new InvalidOperationException(
                        districtName + ": " + current.Name + " 压占了道路或人行道，请调整位置。");
                }

                for (int j = i + 1; j < footprints.Count; j++)
                {
                    Footprint other = footprints[j];
                    if (Overlaps(area, other.Area))
                    {
                        throw new InvalidOperationException(
                            districtName + ": " + current.Name + " 与 " + other.Name + " 重叠，请调整位置或尺寸。");
                    }
                }
            }

            Debug.Log(
                "[BlockoutBuilder] " + districtName + " 布局校验通过：共 " + footprints.Count +
                " 个建筑占位，无重叠、未压占道路，且均位于扩展地块范围内。");
        }

        private static void CreateRoadNetwork(Transform parent, MaterialSet materials)
        {
            Transform roadsRoot = new GameObject("Roads").transform;
            roadsRoot.SetParent(parent, false);

            // 东西向道路略低，南北向道路略高，使交叉口顶面不共面，避免 Z-fighting。
            CreatePrimitive(
                PrimitiveType.Cube,
                "Road_EastWest",
                roadsRoot,
                new Vector3(0f, 0.05f, 0f),
                new Vector3(GroundSize, 0.10f, RoadWidth),
                materials.Asphalt);

            CreatePrimitive(
                PrimitiveType.Cube,
                "Road_NorthSouth",
                roadsRoot,
                new Vector3(0f, 0.06f, 0f),
                new Vector3(RoadWidth, 0.12f, GroundSize),
                materials.Asphalt);
        }

        private static void CreateSidewalks(Transform parent, MaterialSet materials)
        {
            Transform sidewalksRoot = new GameObject("Sidewalks").transform;
            sidewalksRoot.SetParent(parent, false);

            const float armLength = GroundSize * 0.5f - RoadHalfWidth; // 45.5
            const float armCenter = RoadHalfWidth + armLength * 0.5f;  // 27.25
            const float verticalArmLength = GroundSize * 0.5f - SidewalkOuter; // 43
            const float verticalArmCenter = SidewalkOuter + verticalArmLength * 0.5f; // 28.5
            const float sidewalkCenter = RoadHalfWidth + SidewalkWidth * 0.5f; // 5.75

            // 东西向道路两侧的人行道，包含路口四个转角铺装。
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_EW_N_NE", sidewalksRoot,
                new Vector3(armCenter, 0.08f, sidewalkCenter),
                new Vector3(armLength, 0.16f, SidewalkWidth), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_EW_N_NW", sidewalksRoot,
                new Vector3(-armCenter, 0.08f, sidewalkCenter),
                new Vector3(armLength, 0.16f, SidewalkWidth), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_EW_S_NE", sidewalksRoot,
                new Vector3(armCenter, 0.08f, -sidewalkCenter),
                new Vector3(armLength, 0.16f, SidewalkWidth), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_EW_S_NW", sidewalksRoot,
                new Vector3(-armCenter, 0.08f, -sidewalkCenter),
                new Vector3(armLength, 0.16f, SidewalkWidth), materials.Sidewalk);

            // 南北向道路两侧的人行道，从转角铺装外侧开始，避免与上表面重叠。
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_NS_E_NE", sidewalksRoot,
                new Vector3(sidewalkCenter, 0.08f, verticalArmCenter),
                new Vector3(SidewalkWidth, 0.16f, verticalArmLength), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_NS_E_SE", sidewalksRoot,
                new Vector3(sidewalkCenter, 0.08f, -verticalArmCenter),
                new Vector3(SidewalkWidth, 0.16f, verticalArmLength), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_NS_W_NW", sidewalksRoot,
                new Vector3(-sidewalkCenter, 0.08f, verticalArmCenter),
                new Vector3(SidewalkWidth, 0.16f, verticalArmLength), materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Sidewalk_NS_W_SW", sidewalksRoot,
                new Vector3(-sidewalkCenter, 0.08f, -verticalArmCenter),
                new Vector3(SidewalkWidth, 0.16f, verticalArmLength), materials.Sidewalk);
        }

        private static void CreateRoadMarkings(Transform parent, MaterialSet materials)
        {
            Transform markingsRoot = new GameObject("RoadMarkings").transform;
            markingsRoot.SetParent(parent, false);

            // 中心虚线：跳过路口区域，避免标线铺满交叉口。
            for (float offset = -46f; offset <= 46f; offset += 6f)
            {
                if (Mathf.Abs(offset) < 8f)
                    continue;

                CreatePrimitive(PrimitiveType.Cube, "LaneDash_EW", markingsRoot,
                    new Vector3(offset, 0.106f, 0f),
                    new Vector3(3f, 0.012f, 0.18f), materials.Marking);

                CreatePrimitive(PrimitiveType.Cube, "LaneDash_NS", markingsRoot,
                    new Vector3(0f, 0.126f, offset),
                    new Vector3(0.18f, 0.012f, 3f), materials.Marking);
            }

            // 四条人行横道（斑马线），分别位于路口四侧。
            for (int i = 0; i < 5; i++)
            {
                float lateral = -3.6f + i * 1.8f;

                CreatePrimitive(PrimitiveType.Cube, "Crosswalk_N", markingsRoot,
                    new Vector3(lateral, 0.126f, 7.3f),
                    new Vector3(0.55f, 0.012f, 2.2f), materials.Marking);
                CreatePrimitive(PrimitiveType.Cube, "Crosswalk_S", markingsRoot,
                    new Vector3(lateral, 0.126f, -7.3f),
                    new Vector3(0.55f, 0.012f, 2.2f), materials.Marking);
                CreatePrimitive(PrimitiveType.Cube, "Crosswalk_E", markingsRoot,
                    new Vector3(7.3f, 0.106f, lateral),
                    new Vector3(2.2f, 0.012f, 0.55f), materials.Marking);
                CreatePrimitive(PrimitiveType.Cube, "Crosswalk_W", markingsRoot,
                    new Vector3(-7.3f, 0.106f, lateral),
                    new Vector3(2.2f, 0.012f, 0.55f), materials.Marking);
            }
        }

        private static void CreateBuildings(
            Transform parent,
            MaterialSet materials,
            List<Footprint> footprints)
        {
            Transform buildingsRoot = new GameObject("ResidentialBuildings").transform;
            buildingsRoot.SetParent(parent, false);

            for (int i = 0; i < BuildingSpecs.Length; i++)
            {
                BuildingSpec spec = BuildingSpecs[i];
                CreateBuilding(buildingsRoot, spec, i, materials);

                // 用带 1 米退台的基座作为碰撞/占位校验范围。
                footprints.Add(new Footprint(
                    spec.Name,
                    spec.X - (spec.Width + PodiumExtra) * 0.5f,
                    spec.Z - (spec.Depth + PodiumExtra) * 0.5f,
                    spec.Width + PodiumExtra,
                    spec.Depth + PodiumExtra));
            }
        }

        private static void CreateBuilding(
            Transform parent,
            BuildingSpec spec,
            int index,
            MaterialSet materials)
        {
            Transform buildingRoot = new GameObject(spec.Name).transform;
            buildingRoot.SetParent(parent, false);
            buildingRoot.localPosition = new Vector3(spec.X, 0f, spec.Z);

            Material towerMaterial =
                materials.BuildingMaterials[spec.MaterialIndex % materials.BuildingMaterials.Length];

            // 深色基座让塔楼落地更稳，也便于后续做入口与裙房。
            CreatePrimitive(PrimitiveType.Cube, "Podium", buildingRoot,
                new Vector3(0f, PodiumHeight * 0.5f, 0f),
                new Vector3(spec.Width + PodiumExtra, PodiumHeight, spec.Depth + PodiumExtra),
                materials.BuildingPodium);

            CreatePrimitive(PrimitiveType.Cube, "Tower", buildingRoot,
                new Vector3(0f, PodiumHeight + spec.Height * 0.5f, 0f),
                new Vector3(spec.Width, spec.Height, spec.Depth),
                towerMaterial);

            float roofTop = PodiumHeight + spec.Height;
            CreatePrimitive(PrimitiveType.Cube, "RoofCap", buildingRoot,
                new Vector3(0f, roofTop + 0.25f, 0f),
                new Vector3(spec.Width + 0.7f, 0.5f, spec.Depth + 0.7f),
                materials.BuildingTrim);

            CreatePrimitive(PrimitiveType.Cube, "RoofUnit", buildingRoot,
                new Vector3(0f, roofTop + 1.1f, 0f),
                new Vector3(spec.Width * 0.26f, 1.2f, spec.Depth * 0.22f),
                materials.BuildingPodium);

            // 每隔三栋加一根屋顶天线，打破天际线的重复感。
            if (index % 3 == 0)
            {
                CreatePrimitive(PrimitiveType.Cube, "RoofAntenna", buildingRoot,
                    new Vector3(0f, roofTop + 3f, 0f),
                    new Vector3(0.14f, 5.5f, 0.14f),
                    materials.BuildingTrim);
            }

            // 面向道路的玻璃竖条，让纯体块也能读出立面方向。
            if (index % 2 == 0)
            {
                CreatePrimitive(PrimitiveType.Cube, "FacadeAccent", buildingRoot,
                    new Vector3(-spec.Width * 0.25f, PodiumHeight + spec.Height * 0.5f,
                        -spec.Depth * 0.5f - 0.08f),
                    new Vector3(1.9f, spec.Height * 0.86f, 0.16f),
                    materials.Glass);
            }
            else
            {
                CreatePrimitive(PrimitiveType.Cube, "FacadeAccent", buildingRoot,
                    new Vector3(spec.Width * 0.5f + 0.08f, PodiumHeight + spec.Height * 0.5f,
                        spec.Depth * 0.22f),
                    new Vector3(0.16f, spec.Height * 0.86f, 1.9f),
                    materials.Glass);
            }

            // 住宅入口朝向南北向主路，形成统一的街道界面。
            float frontSign = spec.Z > 0f ? -1f : 1f;
            float frontZ = frontSign * (spec.Depth * 0.5f + PodiumExtra * 0.5f);

            CreatePrimitive(PrimitiveType.Cube, "Entrance", buildingRoot,
                new Vector3(-spec.Width * 0.18f, 1.25f, frontZ + frontSign * 0.08f),
                new Vector3(2.4f, 2.5f, 0.16f),
                materials.Glass);

            CreatePrimitive(PrimitiveType.Cube, "EntranceCanopy", buildingRoot,
                new Vector3(-spec.Width * 0.18f, 2.72f, frontZ + frontSign * 0.55f),
                new Vector3(3.8f, 0.18f, 1.2f),
                materials.BuildingTrim);
        }

        private static void CreateCommercialBuildings(
            Transform parent,
            MaterialSet materials,
            List<Footprint> footprints)
        {
            Transform commercialRoot = new GameObject("CommercialBuildings").transform;
            commercialRoot.SetParent(parent, false);

            for (int i = 0; i < CommercialSpecs.Length; i++)
            {
                CommercialSpec spec = CommercialSpecs[i];
                CreateCommercialBuilding(commercialRoot, spec, materials);

                const float plinthExtra = 0.8f;
                footprints.Add(new Footprint(
                    spec.Name,
                    spec.X - (spec.Width + plinthExtra) * 0.5f,
                    spec.Z - (spec.Depth + plinthExtra) * 0.5f,
                    spec.Width + plinthExtra,
                    spec.Depth + plinthExtra));
            }
        }

        private static void CreateCommercialBuilding(
            Transform parent,
            CommercialSpec spec,
            MaterialSet materials)
        {
            Transform buildingRoot = new GameObject(spec.Name).transform;
            buildingRoot.SetParent(parent, false);
            buildingRoot.localPosition = new Vector3(spec.X, 0f, spec.Z);

            Material wallMaterial;
            Material signMaterial;
            if (spec.Style == 1)
            {
                wallMaterial = materials.BuildingMaterials[0];
                signMaterial = materials.HospitalSign;
            }
            else if (spec.Style == 2)
            {
                wallMaterial = materials.CommercialWallAlt;
                signMaterial = materials.MarketSign;
            }
            else
            {
                wallMaterial = materials.CommercialWall;
                signMaterial = materials.MarketSign;
            }

            const float plinthHeight = 0.3f;
            CreatePrimitive(PrimitiveType.Cube, "Plinth", buildingRoot,
                new Vector3(0f, plinthHeight * 0.5f, 0f),
                new Vector3(spec.Width + 0.8f, plinthHeight, spec.Depth + 0.8f),
                materials.BuildingPodium);

            CreatePrimitive(PrimitiveType.Cube, "Body", buildingRoot,
                new Vector3(0f, plinthHeight + spec.Height * 0.5f, 0f),
                new Vector3(spec.Width, spec.Height, spec.Depth),
                wallMaterial);

            float top = plinthHeight + spec.Height;
            CreatePrimitive(PrimitiveType.Cube, "RoofSlab", buildingRoot,
                new Vector3(0f, top + 0.18f, 0f),
                new Vector3(spec.Width + 0.9f, 0.36f, spec.Depth + 0.9f),
                materials.BuildingTrim);

            CreatePrimitive(PrimitiveType.Cube, "RoofUnit", buildingRoot,
                new Vector3(0f, top + 0.36f + 0.45f, 0f),
                new Vector3(spec.Width * 0.24f, 0.9f, spec.Depth * 0.34f),
                materials.BuildingPodium);

            float faceZ = spec.Depth * 0.5f + 0.08f;
            float glassY = plinthHeight + 1.25f;
            float canopyY = plinthHeight + 2.95f;

            // 商业体两侧都做长条玻璃橱窗，朝向社区一侧和临街一侧都能读出商业界面。
            CreatePrimitive(PrimitiveType.Cube, "Storefront_S", buildingRoot,
                new Vector3(0f, glassY, -faceZ),
                new Vector3(spec.Width * 0.82f, 2.4f, 0.16f),
                materials.Glass);
            CreatePrimitive(PrimitiveType.Cube, "Storefront_N", buildingRoot,
                new Vector3(0f, glassY, faceZ),
                new Vector3(spec.Width * 0.82f, 2.4f, 0.16f),
                materials.Glass);

            CreatePrimitive(PrimitiveType.Cube, "Canopy_S", buildingRoot,
                new Vector3(0f, canopyY, -faceZ - 0.6f),
                new Vector3(spec.Width * 0.74f, 0.18f, 1.2f),
                materials.BuildingTrim);
            CreatePrimitive(PrimitiveType.Cube, "Canopy_N", buildingRoot,
                new Vector3(0f, canopyY, faceZ + 0.6f),
                new Vector3(spec.Width * 0.74f, 0.18f, 1.2f),
                materials.BuildingTrim);

            if (spec.Style == 1)
            {
                // 社区医院：南侧立面放醒目的红色十字，北侧放发光标识。
                float crossY = top - 2.2f;
                CreatePrimitive(PrimitiveType.Cube, "HospitalCross_V", buildingRoot,
                    new Vector3(0f, crossY, -faceZ - 0.05f),
                    new Vector3(0.7f, 2.8f, 0.2f),
                    signMaterial);
                CreatePrimitive(PrimitiveType.Cube, "HospitalCross_H", buildingRoot,
                    new Vector3(0f, crossY, -faceZ - 0.05f),
                    new Vector3(2.8f, 0.7f, 0.2f),
                    signMaterial);
                CreatePrimitive(PrimitiveType.Cube, "Sign_N", buildingRoot,
                    new Vector3(0f, top - 1.2f, faceZ + 0.05f),
                    new Vector3(spec.Width * 0.58f, 0.9f, 0.18f),
                    signMaterial);
            }
            else
            {
                float signY = top - 1.35f;
                CreatePrimitive(PrimitiveType.Cube, "Sign_S", buildingRoot,
                    new Vector3(0f, signY, -faceZ - 0.02f),
                    new Vector3(spec.Width * 0.58f, 0.95f, 0.18f),
                    signMaterial);
                CreatePrimitive(PrimitiveType.Cube, "Sign_N", buildingRoot,
                    new Vector3(0f, signY, faceZ + 0.02f),
                    new Vector3(spec.Width * 0.58f, 0.95f, 0.18f),
                    signMaterial);
            }
        }

        private static GameObject CreateDeliveryStation(
            Transform parent,
            MaterialSet materials,
            List<Footprint> footprints)
        {
            Transform stationRoot = new GameObject("DeliveryStation").transform;
            stationRoot.SetParent(parent, false);
            stationRoot.localPosition = new Vector3(16f, 0f, -16f);

            // 站前广场：从 x=8 开始，正好避开通往交叉口的人行道。
            CreatePrimitive(PrimitiveType.Cube, "Forecourt", stationRoot,
                new Vector3(0f, 0.07f, 0f),
                new Vector3(16f, 0.14f, 14f),
                materials.Plaza);

            // 两处接驳坡道，把站前广场分别与西侧、北侧人行道连通。
            CreatePrimitive(PrimitiveType.Cube, "Access_West", stationRoot,
                new Vector3(-8.5f, 0.08f, 0f),
                new Vector3(1f, 0.16f, 4f),
                materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "Access_North", stationRoot,
                new Vector3(0f, 0.08f, 8f),
                new Vector3(4f, 0.16f, 2f),
                materials.Sidewalk);

            CreatePrimitive(PrimitiveType.Cube, "StationBody", stationRoot,
                new Vector3(0f, 0.14f + 2.2f, 0f),
                new Vector3(9f, 4.4f, 7f),
                materials.StationBlue);

            CreatePrimitive(PrimitiveType.Cube, "StationSkirt", stationRoot,
                new Vector3(0f, 0.14f + 0.2f, 0f),
                new Vector3(9.5f, 0.4f, 7.5f),
                materials.StationDark);

            CreatePrimitive(PrimitiveType.Cube, "Canopy", stationRoot,
                new Vector3(0f, 5.02f, 0f),
                new Vector3(11.4f, 0.35f, 9.4f),
                materials.StationDark);

            // 顶棚四周的发光包边：鸟瞰时形成清晰的青色轮廓，强化驿站识别度。
            CreatePrimitive(PrimitiveType.Cube, "CanopyGlow_W", stationRoot,
                new Vector3(-5.79f, 5.27f, 0f),
                new Vector3(0.18f, 0.16f, 9.58f),
                materials.StationSign);
            CreatePrimitive(PrimitiveType.Cube, "CanopyGlow_E", stationRoot,
                new Vector3(5.79f, 5.27f, 0f),
                new Vector3(0.18f, 0.16f, 9.58f),
                materials.StationSign);
            CreatePrimitive(PrimitiveType.Cube, "CanopyGlow_N", stationRoot,
                new Vector3(0f, 5.275f, 4.79f),
                new Vector3(11.58f, 0.16f, 0.18f),
                materials.StationSign);
            CreatePrimitive(PrimitiveType.Cube, "CanopyGlow_S", stationRoot,
                new Vector3(0f, 5.275f, -4.79f),
                new Vector3(11.58f, 0.16f, 0.18f),
                materials.StationSign);

            float[] columnXs = { -5.1f, 5.1f };
            float[] columnZs = { -4.1f, 4.1f };
            foreach (float x in columnXs)
            {
                foreach (float z in columnZs)
                {
                    CreatePrimitive(PrimitiveType.Cube, "CanopyColumn", stationRoot,
                        new Vector3(x, 2.5f, z),
                        new Vector3(0.32f, 5f, 0.32f),
                        materials.StationDark);
                }
            }

            // 驿站入口与发光门头位于西侧，正对社区道路和机器人停靠区。
            CreatePrimitive(PrimitiveType.Cube, "EntranceDoor", stationRoot,
                new Vector3(-4.58f, 1.84f, 0f),
                new Vector3(0.2f, 2.8f, 3f),
                materials.Glass);

            // 门口三级台阶：从站前广场（0.14m）逐级抬升到入口门槛（0.44m）。
            CreatePrimitive(PrimitiveType.Cube, "EntranceStep_1", stationRoot,
                new Vector3(-6.5f, 0.19f, 0f),
                new Vector3(0.8f, 0.10f, 3f),
                materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "EntranceStep_2", stationRoot,
                new Vector3(-5.7f, 0.24f, 0f),
                new Vector3(0.8f, 0.20f, 3f),
                materials.Sidewalk);
            CreatePrimitive(PrimitiveType.Cube, "EntranceStep_3", stationRoot,
                new Vector3(-4.9f, 0.29f, 0f),
                new Vector3(0.8f, 0.30f, 3f),
                materials.Sidewalk);

            CreatePrimitive(PrimitiveType.Cube, "StationSign", stationRoot,
                new Vector3(-4.66f, 4.02f, 0f),
                new Vector3(0.26f, 1.05f, 6.2f),
                materials.StationSign);

            // 屋顶信标灯，在鸟瞰视角下作为配送站的视觉锚点。
            CreatePrimitive(PrimitiveType.Cube, "BeaconPole", stationRoot,
                new Vector3(3.6f, 6.2f, 2.6f),
                new Vector3(0.18f, 2.2f, 0.18f),
                materials.StationDark);
            CreatePrimitive(PrimitiveType.Cube, "Beacon", stationRoot,
                new Vector3(3.6f, 7.55f, 2.6f),
                new Vector3(0.9f, 0.9f, 0.9f),
                materials.StationSign);

            // 广场角落的立式导视牌，远处也能识别驿站位置。
            CreatePrimitive(PrimitiveType.Cube, "PylonBase", stationRoot,
                new Vector3(-6.8f, 0.25f, -5.6f),
                new Vector3(1.1f, 0.5f, 1.1f),
                materials.StationDark);
            CreatePrimitive(PrimitiveType.Cube, "PylonPole", stationRoot,
                new Vector3(-6.8f, 4.3f, -5.6f),
                new Vector3(0.3f, 7.6f, 0.3f),
                materials.StationDark);
            CreatePrimitive(PrimitiveType.Cube, "PylonSign", stationRoot,
                new Vector3(-6.8f, 8.9f, -5.6f),
                new Vector3(0.45f, 2.5f, 3.6f),
                materials.StationSign);

            // 台阶北侧布置机器人停靠圆盘与充电桩，避免遮挡入口。
            CreatePrimitive(PrimitiveType.Cylinder, "DeliveryPad", stationRoot,
                new Vector3(-6.2f, 0.17f, 3f),
                new Vector3(2.4f, 0.04f, 2.4f),
                materials.StationSign);

            CreatePrimitive(PrimitiveType.Cube, "ChargePost_L", stationRoot,
                new Vector3(-6.6f, 0.6f, 6f),
                new Vector3(0.45f, 1.2f, 0.45f),
                materials.StationDark);
            CreatePrimitive(PrimitiveType.Cube, "ChargePost_R", stationRoot,
                new Vector3(-4.4f, 0.6f, 6f),
                new Vector3(0.45f, 1.2f, 0.45f),
                materials.StationDark);
            CreatePrimitive(PrimitiveType.Cube, "ChargePostCap_L", stationRoot,
                new Vector3(-6.6f, 1.24f, 6f),
                new Vector3(0.5f, 0.12f, 0.5f),
                materials.StationSign);
            CreatePrimitive(PrimitiveType.Cube, "ChargePostCap_R", stationRoot,
                new Vector3(-4.4f, 1.24f, 6f),
                new Vector3(0.5f, 0.12f, 0.5f),
                materials.StationSign);

            // 简单的包裹堆，帮助从场景中一眼识别配送功能。
            CreatePrimitive(PrimitiveType.Cube, "ParcelStack_A", stationRoot,
                new Vector3(5.6f, 0.54f, 3.9f),
                new Vector3(1.1f, 0.8f, 1.1f),
                materials.Parcel);
            CreatePrimitive(PrimitiveType.Cube, "ParcelStack_B", stationRoot,
                new Vector3(5.6f, 1.34f, 3.9f),
                new Vector3(0.8f, 0.8f, 0.8f),
                materials.Parcel);
            CreatePrimitive(PrimitiveType.Cube, "ParcelStack_C", stationRoot,
                new Vector3(4.4f, 0.44f, 4.1f),
                new Vector3(0.8f, 0.6f, 0.9f),
                materials.Parcel);

            GameObject anchor = new GameObject("DeliveryStationAnchor");
            anchor.transform.SetParent(stationRoot, false);
            anchor.transform.localPosition = new Vector3(-6.2f, 0.2f, 3f);

            footprints.Add(new Footprint(
                "DeliveryStation",
                stationRoot.position.x - 8f,
                stationRoot.position.z - 7f,
                16f,
                14f));

            return stationRoot.gameObject;
        }

        private static void CreateStreetFurniture(Transform parent, MaterialSet materials)
        {
            Transform furnitureRoot = new GameObject("StreetFurniture").transform;
            furnitureRoot.SetParent(parent, false);

            Transform lightsRoot = new GameObject("StreetLights").transform;
            lightsRoot.SetParent(furnitureRoot, false);

            Transform treesRoot = new GameObject("StreetTrees").transform;
            treesRoot.SetParent(furnitureRoot, false);

            // 沿四条街边按 15 米间距布置路灯与行道树，路口 7 米范围内留空。
            float[] along = { -40f, -25f, -10f, 10f, 25f, 40f };
            for (int i = 0; i < along.Length; i++)
            {
                float x = along[i];
                float z = along[i];

                CreateStreetLight(lightsRoot, new Vector3(x, 0.16f, 6.3f), materials);
                TryCreateStreetTree(treesRoot, new Vector3(x + 2.2f, 0.16f, 6.3f), i, materials);
                CreateStreetLight(lightsRoot, new Vector3(x, 0.16f, -6.3f), materials);
                TryCreateStreetTree(treesRoot, new Vector3(x + 2.2f, 0.16f, -6.3f), i + 1, materials);

                CreateStreetLight(lightsRoot, new Vector3(6.3f, 0.16f, z), materials);
                TryCreateStreetTree(treesRoot, new Vector3(6.3f, 0.16f, z + 2.2f), i + 2, materials);
                CreateStreetLight(lightsRoot, new Vector3(-6.3f, 0.16f, z), materials);
                TryCreateStreetTree(treesRoot, new Vector3(-6.3f, 0.16f, z + 2.2f), i + 3, materials);
            }
        }

        private static void CreateStreetLight(
            Transform parent,
            Vector3 position,
            MaterialSet materials)
        {
            Transform lightRoot = new GameObject("StreetLight").transform;
            lightRoot.SetParent(parent, false);
            lightRoot.localPosition = position;

            CreatePrimitive(PrimitiveType.Cube, "Base", lightRoot,
                new Vector3(0f, 0.09f, 0f),
                new Vector3(0.42f, 0.18f, 0.42f),
                materials.BuildingTrim);

            // Cylinder 原始高度为 2，缩放 y=2.75 后得到 5.5 米细长灯杆。
            CreatePrimitive(PrimitiveType.Cylinder, "Pole", lightRoot,
                new Vector3(0f, 2.75f, 0f),
                new Vector3(0.16f, 2.75f, 0.16f),
                materials.BuildingTrim);

            CreatePrimitive(PrimitiveType.Sphere, "LampBulb", lightRoot,
                new Vector3(0f, 5.72f, 0f),
                new Vector3(0.55f, 0.55f, 0.55f),
                materials.LampHead);
        }

        private static void TryCreateStreetTree(
            Transform parent,
            Vector3 position,
            int index,
            MaterialSet materials)
        {
            // 驿站入口和停靠区前方不种树，避免遮挡门头、台阶和机器人泊位。
            if (position.x > 4f && position.x < 26f &&
                position.z > -26f && position.z < -4f)
                return;

            CreateStreetTree(parent, position, index, materials);
        }

        private static void CreateStreetTree(
            Transform parent,
            Vector3 position,
            int index,
            MaterialSet materials)
        {
            Transform treeRoot = new GameObject("StreetTree").transform;
            treeRoot.SetParent(parent, false);
            treeRoot.localPosition = position;

            float variation = 0.9f + (index % 3) * 0.08f;

            // Cylinder 原始高度为 2，缩放 y=1.6 后得到 3.2 米树干。
            CreatePrimitive(PrimitiveType.Cylinder, "Trunk", treeRoot,
                new Vector3(0f, 1.6f * variation, 0f),
                new Vector3(0.5f * variation, 1.6f * variation, 0.5f * variation),
                materials.TreeTrunk);

            CreatePrimitive(PrimitiveType.Sphere, "Canopy", treeRoot,
                new Vector3(0f, 3.75f * variation, 0f),
                new Vector3(3.2f, 2.7f, 3.2f) * variation,
                materials.TreeLeaf);
        }

        private static void ValidateLayout(List<Footprint> footprints)
        {
            Rect verticalCorridor = Rect.MinMaxRect(
                -ClearCorridorHalfWidth, -GroundSize * 0.5f,
                ClearCorridorHalfWidth, GroundSize * 0.5f);
            Rect horizontalCorridor = Rect.MinMaxRect(
                -GroundSize * 0.5f, -ClearCorridorHalfWidth,
                GroundSize * 0.5f, ClearCorridorHalfWidth);

            const float edgeMargin = 1f;
            for (int i = 0; i < footprints.Count; i++)
            {
                Footprint current = footprints[i];
                Rect area = current.Area;

                if (area.xMin < -GroundSize * 0.5f + edgeMargin ||
                    area.xMax > GroundSize * 0.5f - edgeMargin ||
                    area.yMin < -GroundSize * 0.5f + edgeMargin ||
                    area.yMax > GroundSize * 0.5f - edgeMargin)
                {
                    throw new InvalidOperationException(
                        current.Name + " 超出 100x100 地面范围（至少保留 1 米边距）。");
                }

                if (Overlaps(area, verticalCorridor) || Overlaps(area, horizontalCorridor))
                {
                    throw new InvalidOperationException(
                        current.Name + " 压占了道路或人行道，请调整位置。");
                }

                for (int j = i + 1; j < footprints.Count; j++)
                {
                    Footprint other = footprints[j];
                    if (Overlaps(area, other.Area))
                    {
                        throw new InvalidOperationException(
                            current.Name + " 与 " + other.Name + " 重叠，请调整位置或尺寸。");
                    }
                }
            }

            Debug.Log(
                "[BlockoutBuilder] 布局校验通过：共 " + footprints.Count +
                " 个建筑/站点占位，无重叠、未压占道路，且均位于地面范围内。");
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
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            if (scenes.Exists(scene => scene.path == ScenePath))
                return;

            // 仅登记，不默认勾选，避免影响工程现有的构建内容。
            scenes.Add(new EditorBuildSettingsScene(ScenePath, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void FrameSceneView()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
                return;

            view.pivot = new Vector3(50f, 0f, 50f);
            view.rotation = Quaternion.Euler(45f, 0f, 0f);
            view.size = 175f;
            view.Repaint();
        }

        private static void MigrateObsoleteMaterials()
        {
            string oldPath = MaterialFolder + "/M_Blockout_Station_Orange.mat";
            string newPath = MaterialFolder + "/M_Blockout_Station_Blue.mat";
            Material oldMaterial = AssetDatabase.LoadAssetAtPath<Material>(oldPath);
            if (oldMaterial == null)
                return;

            if (AssetDatabase.LoadAssetAtPath<Material>(newPath) != null)
                AssetDatabase.DeleteAsset(oldPath);
            else
                AssetDatabase.MoveAsset(oldPath, newPath);
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
                    "M_Blockout_Grass",
                    new Color(0.30f, 0.37f, 0.29f),
                    0f,
                    0.08f),
                Asphalt = GetOrCreateMaterial(
                    "M_Blockout_Asphalt",
                    new Color(0.20f, 0.21f, 0.23f),
                    0.05f,
                    0.32f),
                Sidewalk = GetOrCreateMaterial(
                    "M_Blockout_Sidewalk",
                    new Color(0.50f, 0.51f, 0.52f),
                    0f,
                    0.14f),
                Marking = GetOrCreateMaterial(
                    "M_Blockout_RoadMarking",
                    new Color(0.92f, 0.92f, 0.88f),
                    0f,
                    0.20f,
                    new Color(1f, 1f, 0.95f),
                    0.08f),
                Plaza = GetOrCreateMaterial(
                    "M_Blockout_Plaza",
                    new Color(0.58f, 0.60f, 0.62f),
                    0.05f,
                    0.22f),
                BuildingPodium = GetOrCreateMaterial(
                    "M_Blockout_Building_Podium",
                    new Color(0.42f, 0.44f, 0.47f),
                    0.10f,
                    0.35f),
                BuildingTrim = GetOrCreateMaterial(
                    "M_Blockout_Building_Trim",
                    new Color(0.28f, 0.30f, 0.33f),
                    0.25f,
                    0.55f),
                Glass = GetOrCreateMaterial(
                    "M_Blockout_Glass",
                    new Color(0.28f, 0.40f, 0.48f),
                    0.35f,
                    0.86f),
                CommercialWall = GetOrCreateMaterial(
                    "M_Blockout_Commercial_Warm",
                    new Color(0.78f, 0.72f, 0.61f),
                    0.05f,
                    0.28f),
                CommercialWallAlt = GetOrCreateMaterial(
                    "M_Blockout_Commercial_Sage",
                    new Color(0.63f, 0.67f, 0.63f),
                    0.05f,
                    0.25f),
                MarketSign = GetOrCreateMaterial(
                    "M_Blockout_Commercial_MarketSign",
                    new Color(0.98f, 0.68f, 0.10f),
                    0.10f,
                    0.55f,
                    new Color(1f, 0.62f, 0.06f),
                    1.2f),
                HospitalSign = GetOrCreateMaterial(
                    "M_Blockout_Commercial_HospitalSign",
                    new Color(0.88f, 0.05f, 0.04f),
                    0.05f,
                    0.50f,
                    new Color(1f, 0.04f, 0.03f),
                    1.5f),
                BuildingMaterials = new[]
                {
                    GetOrCreateMaterial("M_Blockout_Building_White",
                        new Color(0.88f, 0.89f, 0.90f), 0f, 0.28f),
                    GetOrCreateMaterial("M_Blockout_Building_LightGray",
                        new Color(0.73f, 0.75f, 0.77f), 0f, 0.32f),
                    GetOrCreateMaterial("M_Blockout_Building_WarmGray",
                        new Color(0.61f, 0.62f, 0.64f), 0f, 0.30f),
                },
                StationBlue = GetOrCreateMaterial(
                    "M_Blockout_Station_Blue",
                    new Color(0.12f, 0.38f, 0.58f),
                    0.18f,
                    0.62f),
                StationDark = GetOrCreateMaterial(
                    "M_Blockout_Station_Dark",
                    new Color(0.10f, 0.11f, 0.12f),
                    0.35f,
                    0.62f),
                StationSign = GetOrCreateMaterial(
                    "M_Blockout_Station_Sign",
                    new Color(0.04f, 0.72f, 0.86f),
                    0.10f,
                    0.65f,
                    new Color(0.10f, 0.90f, 1f),
                    3.2f),
                Parcel = GetOrCreateMaterial(
                    "M_Blockout_Parcel",
                    new Color(0.62f, 0.46f, 0.29f),
                    0f,
                    0.20f),
                LampHead = GetOrCreateMaterial(
                    "M_Blockout_LampHead",
                    new Color(0.98f, 0.83f, 0.15f),
                    0.10f,
                    0.65f,
                    new Color(1f, 0.78f, 0.08f),
                    2.5f),
                TreeTrunk = GetOrCreateMaterial(
                    "M_Blockout_TreeTrunk",
                    new Color(0.36f, 0.23f, 0.13f),
                    0f,
                    0.08f),
                TreeLeaf = GetOrCreateMaterial(
                    "M_Blockout_TreeLeaf",
                    new Color(0.17f, 0.42f, 0.19f),
                    0f,
                    0.08f),
                BlueGlass = GetOrCreateBlueGlassMaterial(),
                SilverMetal = GetOrCreateMaterial(
                    "M_Blockout_SilverMetal",
                    new Color(0.86f, 0.88f, 0.90f),
                    0.38f,
                    0.72f),
                SilverDark = GetOrCreateMaterial(
                    "M_Blockout_SilverDark",
                    new Color(0.32f, 0.35f, 0.38f),
                    0.45f,
                    0.55f),
                TechGlow = GetOrCreateMaterial(
                    "M_Blockout_TechGlow",
                    new Color(0.05f, 0.85f, 1f),
                    0.10f,
                    0.70f,
                    new Color(0.05f, 0.90f, 1f),
                    3.5f),
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
                    throw new InvalidOperationException("找不到 URP Lit / Standard 着色器，无法创建材质。");

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

        private static Material GetOrCreateBlueGlassMaterial()
        {
            Material material = GetOrCreateMaterial(
                "M_Blockout_BlueGlass_Transparent",
                new Color(0.10f, 0.42f, 0.72f, 0.55f),
                0.15f,
                0.92f);

            ConfigureTransparentMaterial(material);
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");

            if (material.HasProperty("_Surface"))
            {
                // URP Lit 的透明表面参数。
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_AlphaClip", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else if (material.HasProperty("_Mode"))
            {
                // Standard 着色器的兼容路径。
                material.SetFloat("_Mode", 3f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
        }

        private readonly struct BuildingSpec
        {
            public readonly string Name;
            public readonly float X;
            public readonly float Z;
            public readonly float Width;
            public readonly float Depth;
            public readonly float Height;
            public readonly int MaterialIndex;

            public BuildingSpec(
                string name,
                float x,
                float z,
                float width,
                float depth,
                float height,
                int materialIndex)
            {
                Name = name;
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
                Height = height;
                MaterialIndex = materialIndex;
            }
        }

        private readonly struct TowerSpec
        {
            public readonly string Name;
            public readonly float X;
            public readonly float Z;
            public readonly float Width;
            public readonly float Depth;
            public readonly float Height;

            public TowerSpec(string name, float x, float z, float width, float depth, float height)
            {
                Name = name;
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
                Height = height;
            }
        }

        private readonly struct SkybridgeSpec
        {
            public readonly string Name;
            public readonly float X;
            public readonly float Z;
            public readonly float Length;
            public readonly float Width;
            public readonly float Height;

            public SkybridgeSpec(string name, float x, float z, float length, float width, float height)
            {
                Name = name;
                X = x;
                Z = z;
                Length = length;
                Width = width;
                Height = height;
            }
        }

        private readonly struct MallSpec
        {
            public readonly string Name;
            public readonly float X;
            public readonly float Z;
            public readonly float Width;
            public readonly float Depth;
            public readonly float Height;

            public MallSpec(string name, float x, float z, float width, float depth, float height)
            {
                Name = name;
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
                Height = height;
            }
        }

        private readonly struct CommercialSpec
        {
            public readonly string Name;
            public readonly float X;
            public readonly float Z;
            public readonly float Width;
            public readonly float Depth;
            public readonly float Height;
            public readonly int Style;

            public CommercialSpec(
                string name,
                float x,
                float z,
                float width,
                float depth,
                float height,
                int style)
            {
                Name = name;
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
                Height = height;
                Style = style;
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
            public Material Sidewalk;
            public Material Marking;
            public Material Plaza;
            public Material BuildingPodium;
            public Material BuildingTrim;
            public Material Glass;
            public Material CommercialWall;
            public Material CommercialWallAlt;
            public Material MarketSign;
            public Material HospitalSign;
            public Material[] BuildingMaterials;
            public Material StationBlue;
            public Material StationDark;
            public Material StationSign;
            public Material Parcel;
            public Material LampHead;
            public Material TreeTrunk;
            public Material TreeLeaf;
            public Material BlueGlass;
            public Material SilverMetal;
            public Material SilverDark;
            public Material TechGlow;
        }
    }
}
