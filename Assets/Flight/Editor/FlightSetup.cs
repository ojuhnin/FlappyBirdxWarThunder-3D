using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FlappyFlight.Editor
{
    public static class FlightSetup
    {
        public const string ScenePath = "Assets/Scenes/TestPlayground.unity";
        public const string PlayerPath = "Assets/Flight/Prefabs/FlappyBirdPlayer.prefab";
        public const string CameraPath = "Assets/Flight/Prefabs/FlightCameraRig.prefab";
        public const string ModelPath = "Assets/Art/FlappyBird/Models/FlappyBird.fbx";
        public const string SettingsPath = "Assets/Flight/Settings/PrototypeFlight.asset";
        public const string InputPath = "Assets/Flight/Input/FlappyFlight.inputactions";
        public const string RootName = "FlappyFlight_Integration";
        private const string MaterialFolder = "Assets/Art/FlappyBird/Materials";
        [Serializable] private sealed class Palette { public SourceMaterial[] materials = Array.Empty<SourceMaterial>(); }
        [Serializable] private sealed class SourceMaterial
        { public string name = ""; public float[] base_color = Array.Empty<float>(); public float roughness = .7f, metallic = 0; }

        [MenuItem("Tools/Flappy Flight/Configurar jogador no TestPlayground")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode para configurar o voo.");
            if (!File.Exists(ScenePath) || !File.Exists(ModelPath))
                throw new FileNotFoundException("O playground e o FBX devem existir antes da configuracao.");
            if (GraphicsSettings.currentRenderPipeline == null ||
                !GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"))
                throw new InvalidOperationException("URP esperado; configuracao do projeto preservada.");
            EnsureFolder("Assets/Flight/Prefabs");
            EnsureFolder("Assets/Flight/Settings");
            CreateMaterialsAndConfigureImport();
            FlightSettings settings = AssetDatabase.LoadAssetAtPath<FlightSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<FlightSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            CreatePlayerPrefab(settings);
            CreateCameraPrefab();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter)
            {
                bool empty = Application.isBatchMode && previous.rootCount == 0 && string.IsNullOrEmpty(previous.path);
                if (!Application.isBatchMode && !EditorSceneManager.EnsureUntitledSceneHasBeenSaved(
                    "Salve a cena sem nome antes de configurar o voo no playground.")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, empty ? OpenSceneMode.Single : OpenSceneMode.Additive);
                if (empty) closeAfter = false;
            }
            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject terrain = scene.GetRootGameObjects().Single(item => item.name == "TestPlayground_Generated");
                string terrainBefore = TerrainSignature(terrain);
                foreach (GameObject old in scene.GetRootGameObjects().Where(item => item.name == RootName))
                    Object.DestroyImmediate(old);
                var root = new GameObject(RootName);
                var spawn = new GameObject("FlightSpawn_Airborne");
                spawn.transform.SetParent(root.transform, false);
                spawn.transform.position = new Vector3(0, 5, -4);
                var playerObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), scene);
                playerObject.transform.SetParent(root.transform, false);
                playerObject.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
                FlappyFlightController player = playerObject.GetComponent<FlappyFlightController>();
                player.spawnPoint = spawn.transform;
                player.State.Reset(spawn.transform.rotation, settings);
                var cameraObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath), scene);
                cameraObject.transform.SetParent(root.transform, false);
                FlightChaseCamera chase = cameraObject.GetComponent<FlightChaseCamera>();
                chase.player = player;
                FlightHud hud = cameraObject.GetComponent<FlightHud>();
                hud.player = player;
                hud.chase = chase;
                chase.Snap();
                Transform overview = terrain.GetComponentsInChildren<Transform>(true).Single(item => item.name == "OverviewCamera");
                overview.GetComponent<Camera>().enabled = false;
                overview.GetComponent<AudioListener>().enabled = false;
                if (terrainBefore != TerrainSignature(terrain)) throw new InvalidOperationException("Geometria do playground alterada.");
                Validate(scene);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(chase);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Falha ao salvar cena de voo.");
                Debug.Log("FlappyFlight: cena pronta e salva em " + ScenePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (closeAfter && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Tools/Flappy Flight/Abrir teste de voo")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void CreateMaterialsAndConfigureImport()
        {
            Palette palette = JsonUtility.FromJson<Palette>(File.ReadAllText(MaterialFolder + "/SourcePalette.json"));
            var materials = new Dictionary<string, Material>();
            foreach (SourceMaterial source in palette.materials)
            {
                string path = MaterialFolder + "/" + source.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                Color linear = new Color(source.base_color[0], source.base_color[1], source.base_color[2], source.base_color[3]);
                material.SetColor("_BaseColor", linear.gamma);
                material.SetFloat("_Smoothness", 1 - source.roughness);
                material.SetFloat("_Metallic", source.metallic);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                materials.Add(source.name, material);
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapeNormals = ModelImporterNormals.Import;
            importer.optimizeMeshVertices = false;
            importer.optimizeMeshPolygons = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }

        private static void CreatePlayerPrefab(FlightSettings settings)
        {
            var root = new GameObject("FlappyBirdPlayer");
            try
            {
                Rigidbody body = root.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.mass = 1;
                body.linearDamping = 0;
                body.angularDamping = 0;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                FlightInput input = root.AddComponent<FlightInput>();
                input.bindings = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
                if (input.bindings == null) throw new FileNotFoundException(InputPath);
                FlappyFlightController player = root.AddComponent<FlappyFlightController>();
                player.settings = settings;
                var visual = new GameObject("VisualRoot_FutureRig");
                visual.transform.SetParent(root.transform, false);
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetParent(visual.transform, false);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                Renderer beak = renderers.First(item => item.name == "Bico_Superior");
                Renderer torso = renderers.First(item => item.name == "Corpo_Amarelo");
                Vector3 front = Vector3.ProjectOnPlane(beak.bounds.center - torso.bounds.center, Vector3.up).normalized;
                if (front.sqrMagnitude < .5f) throw new InvalidOperationException("Nao foi possivel identificar a frente do modelo.");
                visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(front, Vector3.up));
                visual.transform.localScale = Vector3.one * .18f;
                Bounds bounds = CombinedBounds(renderers);
                model.transform.localPosition -= visual.transform.InverseTransformVector(bounds.center - root.transform.position);
                foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
                player.visualRoot = visual.transform;
                player.cameraTarget = Child("CameraTarget_Stable", root.transform, new Vector3(0, .15f, 0));
                player.aimOrigin = Child("AimOrigin_Stable", root.transform, new Vector3(0, .1f, .45f));
                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 2;
                capsule.radius = .38f;
                capsule.height = 1.2f;
                BoxCollider wings = root.AddComponent<BoxCollider>();
                wings.center = new Vector3(0, .05f, -.12f);
                wings.size = new Vector3(1.55f, .25f, .8f);
                if (Vector3.Dot((beak.bounds.center - torso.bounds.center).normalized, Vector3.forward) < .8f)
                    throw new InvalidOperationException("Bico nao orientado para +Z.");
                Bounds fitted = CombinedBounds(renderers);
                if (Mathf.Abs(fitted.size.y - .9f) > .03f || fitted.size.x > 1.65f)
                    throw new InvalidOperationException("Escala do modelo importado inesperada: " + fitted.size);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateCameraPrefab()
        {
            var root = new GameObject("FlightCameraRig");
            try
            {
                root.tag = "MainCamera";
                Camera view = root.AddComponent<Camera>();
                view.fieldOfView = 68;
                view.nearClipPlane = .12f;
                view.farClipPlane = 250;
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = new Color(.22f, .29f, .37f);
                root.AddComponent<AudioListener>();
                root.AddComponent<FlightChaseCamera>();
                root.AddComponent<FlightHud>();
                PrefabUtility.SaveAsPrefabAsset(root, CameraPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        public static string TerrainSignature(GameObject terrain)
        {
            return string.Join("\n", terrain.GetComponentsInChildren<Collider>(true).Select(collider =>
                collider.name + "|" + collider.transform.position.ToString("F5") + "|" +
                collider.transform.rotation.ToString("F5") + "|" + collider.transform.lossyScale.ToString("F5") + "|" + collider.GetType().Name));
        }

        public static void Validate(Scene scene)
        {
            GameObject root = scene.GetRootGameObjects().Single(item => item.name == RootName);
            var player = root.GetComponentsInChildren<FlappyFlightController>(true).Single();
            var camera = root.GetComponentsInChildren<FlightChaseCamera>(true).Single();
            if (camera.player != player || root.GetComponentsInChildren<FlightHud>().Single().player != player)
                throw new InvalidOperationException("Referencias de camera/HUD incorretas.");
            if (player.visualRoot.GetComponentsInChildren<Rigidbody>().Length != 0 ||
                player.visualRoot.GetComponentsInChildren<Collider>().Length != 0)
                throw new InvalidOperationException("Fisica deve ficar na raiz do jogador.");
            Renderer[] renderers = player.visualRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 57) throw new InvalidOperationException("Uma das 57 malhas foi perdida.");
            foreach (Renderer renderer in renderers)
                if (renderer.sharedMaterials.Any(material => material == null || !material.shader.name.StartsWith("Universal Render Pipeline/")))
                    throw new InvalidOperationException("Material URP faltando: " + renderer.name);
            if (scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<Camera>(true)).Count(item => item.enabled) != 1)
                throw new InvalidOperationException("Mais de uma camera ativa.");
            if (scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<AudioListener>(true)).Count(item => item.enabled) != 1)
                throw new InvalidOperationException("Mais de um AudioListener ativo.");
            Physics.SyncTransforms();
            foreach (Collider environment in scene.GetRootGameObjects().Where(item => item.name != RootName)
                .SelectMany(item => item.GetComponentsInChildren<Collider>()))
                if (environment.bounds.Intersects(new Bounds(player.spawnPoint.position, Vector3.one * 2.4f)))
                    throw new InvalidOperationException("Spawn aereo obstruido: " + environment.name);
        }

        public static void BuildAndValidate()
        {
            Build();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            string first = IntegrationSignature(scene);
            Build();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Validate(scene);
            if (first != IntegrationSignature(scene)) throw new InvalidOperationException("Configuracao duplicada ou divergente apos repeticao.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/FlappyFlight-integration.txt", "PASS\nUnity " + Application.unityVersion +
                "\n57 meshes; 8 URP materials; 184 imported blend shapes expected.\n" +
                "Single player/camera/HUD/listener; aerial spawn clear; source terrain geometry unchanged.\n" +
                "Repeated setup + saved scene reopened without duplicates.\nPlay Mode tests have a separate XML report.\n");
            Capture(scene);
            Debug.Log("FlappyFlight: integration build PASS.");
        }

        private static string IntegrationSignature(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return string.Join("|", scene.GetRootGameObjects().Single(item => item.name == RootName)
                .GetComponentsInChildren<Transform>(true).Select(item => item.name).OrderBy(item => item, StringComparer.Ordinal));
        }
        private static Transform Child(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }
        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        public static void Capture(Scene scene)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            FlappyFlightController player = scene.GetRootGameObjects().Single(item => item.name == RootName)
                .GetComponentInChildren<FlappyFlightController>();
            player.State.Reset(Quaternion.identity, player.settings);
            FlightChaseCamera chase = scene.GetRootGameObjects().Single(item => item.name == RootName).GetComponentInChildren<FlightChaseCamera>();
            chase.Snap();
            Camera camera = chase.GetComponent<Camera>();
            bool asyncBefore = ShaderUtil.allowAsyncCompilation;
            RenderTexture previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1600, 1000, 24);
            var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                camera.targetTexture = target;
                camera.Render(); camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Logs/FlappyFlight-camera-preview.png", texture.EncodeToPNG());
                camera.transform.position = player.transform.position + new Vector3(2.2f, .8f, 2.7f);
                camera.transform.LookAt(player.transform.position);
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Logs/FlappyFlight-model-preview.png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                ShaderUtil.allowAsyncCompilation = asyncBefore;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
                chase.Snap();
            }
        }
    }
}
