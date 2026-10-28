using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TestPlayground.Editor
{
    // Editor-only: the saved scene has no dependency on this generator at runtime.
    [InitializeOnLoad]
    public static class TestPlaygroundBuilder
    {
        public const string ScenePath = "Assets/Scenes/TestPlayground.unity";
        private const string AssetRoot = "Assets/TestPlayground/Generated";
        private const string RootName = "TestPlayground_Generated";
        private const string RequestPath = "Temp/TestPlayground.Generate";
        private const string ReportPath = "Logs/TestPlayground-validation.txt";
        private static readonly List<string> Checks = new List<string>();
        private static Material floor, blue, green, sand, stone, violet, paint;

        static TestPlaygroundBuilder()
        {
            // A one-shot local request, used only when explicitly placed by the author.
            // Ordinary imports/project opens never generate or modify a scene.
            if (File.Exists(RequestPath))
                EditorApplication.update += ProcessRequestedBuild;
        }

        private static void ProcessRequestedBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            EditorApplication.update -= ProcessRequestedBuild;
            if (!File.Exists(RequestPath)) return;
            File.Delete(RequestPath);
            try { GenerateAndVerify(); }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(ReportPath, "FAILED\n" + exception);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Test Playground/Criar ou reconstruir cenario")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode antes de gerar o cenario.");

            if (!Application.isBatchMode && !EditorSceneManager.EnsureUntitledSceneHasBeenSaved(
                "A Unity precisa salvar a cena sem nome antes de criar TestPlayground em paralelo. Suas outras cenas serao preservadas."))
                throw new OperationCanceledException("Geracao cancelada: a cena sem nome foi preservada.");

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool closeAfter = !scene.IsValid() || !scene.isLoaded;
            if (closeAfter)
            {
                // Batch mode initially contains a disposable, empty, untitled scene.
                // Unity forbids NewScene(Additive) while that scene has no saved path.
                bool emptyBatchScene = Application.isBatchMode && SceneManager.sceneCount == 1 &&
                    string.IsNullOrEmpty(previous.path) && previous.rootCount == 0;
                scene = File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, emptyBatchScene ? OpenSceneMode.Single : OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
                if (emptyBatchScene) closeAfter = false;
            }

            try
            {
                SceneManager.SetActiveScene(scene);
                GameObject[] existingRoots = scene.GetRootGameObjects();
                if (File.Exists(ScenePath) && !existingRoots.Any(item => item.name == RootName))
                    throw new InvalidOperationException("TestPlayground.unity ja existe sem a raiz do gerador; arquivo preservado.");

                PrepareAssets();
                // Only the reserved generated root is replaced. User roots remain intact.
                foreach (GameObject item in existingRoots.Where(item => item.name == RootName))
                    Object.DestroyImmediate(item);

                Transform root = Group(RootName, null);
                BuildGround(root);
                BuildRamps(root);
                BuildPlatforms(root);
                BuildStairs(root);
                BuildCollisions(root);
                BuildCorridors(root);
                BuildGaps(root);
                BuildFlight(root);
                BuildLightingAndCamera(root);
                Validate(scene);

                Directory.CreateDirectory("Assets/Scenes");
                foreach (string guid in AssetDatabase.FindAssets("", new[] { AssetRoot }))
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
                }
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Nao foi possivel salvar " + ScenePath);
                Debug.Log("TestPlayground salvo: " + ScenePath + ". " + Checks.Count + " verificacoes geometricas aprovadas.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (closeAfter && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Tools/Test Playground/Abrir cena")]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) Generate();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().First(item => item.name == RootName);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1, 0),
                    Quaternion.Euler(57, -35, 0), 75);
        }

        [MenuItem("Tools/Test Playground/Validar cena salva e repeticao")]
        public static void GenerateAndVerify()
        {
            Generate();
            string first = ReadSavedSceneSignature(false);
            Generate();
            string second = ReadSavedSceneSignature(true);
            Require(first == second, "Reconstrucao: mesmos nomes, transforms, componentes e referencias, sem duplicacao.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, "PASS - Unity " + Application.unityVersion + "\n" +
                "Pipeline ativo: " + (GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.GetType().Name : "Built-in") + "\n" +
                string.Join("\n", Checks.Select(item => "PASS: " + item)) + "\n" +
                "Nao executado: Play Mode com personagem, testes de movimentacao/voo e inspecao visual humana.\n");
            Debug.Log("TestPlayground: validacao e repeticao aprovadas. Relatorio: " + ReportPath);
        }

        private static string ReadSavedSceneSignature(bool capture)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool closeAfter = !scene.IsValid() || !scene.isLoaded;
            Scene previous = SceneManager.GetActiveScene();
            if (closeAfter) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            bool reopened = closeAfter;
            if (!closeAfter && !scene.isDirty && SceneManager.sceneCount == 1)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                reopened = true;
            }
            try
            {
                SceneManager.SetActiveScene(scene);
                Validate(scene);
                if (reopened) Checks.Add("Cena salva reaberta pela Unity antes da validacao.");
                Transform root = scene.GetRootGameObjects().Single(item => item.name == RootName).transform;
                string signature = string.Join("\n", root.GetComponentsInChildren<Transform>(true)
                    .Select(item => HierarchyPath(item) + "|" + item.localPosition.ToString("F5") + "|" +
                        item.localRotation.ToString("F5") + "|" + item.localScale.ToString("F5") + "|" +
                        string.Join(",", item.GetComponents<Component>().Select(component => component.GetType().Name)) + "|" +
                        string.Join(",", item.GetComponents<Renderer>().SelectMany(renderer => renderer.sharedMaterials)
                            .Select(material => AssetDatabase.GetAssetPath(material))))
                    .OrderBy(item => item, StringComparer.Ordinal));
                if (capture) CaptureOverview(root);
                return signature;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void PrepareAssets()
        {
            EnsureFolder("Assets/TestPlayground");
            EnsureFolder(AssetRoot);
            EnsureFolder(AssetRoot + "/Materials");
            EnsureFolder(AssetRoot + "/Meshes");
            EnsureFolder(AssetRoot + "/Textures");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || GraphicsSettings.currentRenderPipeline == null ||
                !GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"))
                throw new InvalidOperationException("Este projeto utiliza URP. O gerador nao modifica o render pipeline.");

            floor = MaterialAsset("Ground_Grid_1m", new Color(.43f, .45f, .47f), shader);
            blue = MaterialAsset("Ramps_SoftBlue", new Color(.49f, .65f, .73f), shader);
            green = MaterialAsset("Platforms_Sage", new Color(.58f, .69f, .58f), shader);
            sand = MaterialAsset("Stairs_Sand", new Color(.76f, .68f, .51f), shader);
            stone = MaterialAsset("Collision_WarmGrey", new Color(.65f, .61f, .57f), shader);
            violet = MaterialAsset("Flight_Lavender", new Color(.66f, .60f, .76f), shader);
            paint = MaterialAsset("Markings_Chalk", new Color(.78f, .81f, .80f), shader);

            // Five metres per texture repeat, 64 texels per metre; no external image.
            const int size = 320;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Grid_1m", wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool major = x < 3 || y < 3 || x >= size - 1 || y >= size - 1;
                bool minor = x % 64 < 2 || y % 64 < 2;
                pixels[y * size + x] = major ? new Color(.61f, .64f, .66f) :
                    minor ? new Color(.50f, .53f, .55f) : new Color(.39f, .42f, .44f);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            Texture2D saved = Persist(texture, AssetRoot + "/Textures/Grid_1m.asset");
            floor.SetColor("_BaseColor", Color.white);
            floor.SetTexture("_BaseMap", saved);
            floor.SetTextureScale("_BaseMap", new Vector2(16, 16));
            EditorUtility.SetDirty(floor);
        }

        private static Material MaterialAsset(string name, Color color, Shader shader)
        {
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            material.SetFloat("_Metallic", 0);
            return Persist(material, AssetRoot + "/Materials/" + name + ".mat");
        }

        private static T Persist<T>(T generated, string path) where T : Object
        {
            T saved = AssetDatabase.LoadAssetAtPath<T>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, saved);
            EditorUtility.SetDirty(saved);
            Object.DestroyImmediate(generated);
            return saved;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static Transform Group(string name, Transform parent)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            return item.transform;
        }

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = center;
            item.transform.localScale = size;
            item.isStatic = true;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static GameObject Block(string name, Transform parent, float x, float z, float width,
            float depth, float top, Material material)
        {
            return Box(name, parent, new Vector3(x, top / 2, z), new Vector3(width, top, depth), material);
        }

        private static void BuildGround(Transform root)
        {
            Transform sector = Group("00_Ground_and_CentralArea", root);
            Box("Ground_80x80m_TopY0", sector, new Vector3(0, -.5f, 0), new Vector3(80, 1, 80), floor);
            Transform spawn = Group("PlayerSpawn", sector);
            spawn.localPosition = new Vector3(0, .05f, 0);
            Outline("Central_Clear_16x16m", sector, 0, 0, 16, 16, paint);
            Label("00  /  AREA LIVRE\n16 x 16 m", sector, new Vector3(0, .025f, -6), 1.8f);
            Label("TEST PLAYGROUND   /   1 QUADRADO = 1 m", sector, new Vector3(0, .025f, -38), 1.1f);
        }

        private static void BuildRamps(Transform root)
        {
            Transform sector = Group("01_Ramps_15_30_45deg", root);
            Outline("Sector_Boundary", sector, -24.5f, 23.5f, 23, 23, blue);
            float[] angles = { 15, 30, 45 };
            float[] runs = { 10, 8, 6 };
            for (int i = 0; i < angles.Length; i++)
            {
                float x = -32 + i * 7;
                float rise = Mathf.Tan(angles[i] * Mathf.Deg2Rad) * runs[i];
                Transform lane = Group("Ramp_" + angles[i] + "deg", sector);
                Ramp("Slope_" + angles[i] + "deg", lane, new Vector3(x, 0, 15), 4, runs[i], rise, 0, blue);
                Block("Landing_Grounded", lane, x, 15 + runs[i] + 1.5f, 4, 3, rise, blue);
                Label(angles[i] + " graus", lane, new Vector3(x, .025f, 13.2f), 3);
            }
            Label("01  /  RAMPAS", sector, new Vector3(-24.5f, .025f, 33.5f), 1.8f);
        }

        private static void BuildPlatforms(Transform root)
        {
            Transform sector = Group("02_Platforms_Connected", root);
            Outline("Sector_Boundary", sector, 24.5f, 23.5f, 23, 23, green);
            Ramp("Access_FromGround", sector, new Vector3(16, 0, 14), 6, 5, 1.5f, 0, green);
            Block("Platform_A_Top1.5m", sector, 16, 21, 6, 4, 1.5f, green);
            Block("Ramp_B_Foundation", sector, 16, 25, 6, 4, 1.5f, green);
            Ramp("Connection_A_B", sector, new Vector3(16, 1.5f, 23), 6, 4, 1.5f, 0, green);
            Block("Platform_B_Top3m", sector, 16, 29, 6, 4, 3, green);
            Block("Ramp_C_Foundation", sector, 21, 29, 4, 4, 3, green);
            Ramp("Connection_B_C", sector, new Vector3(19, 3, 29), 4, 4, 1.5f, 90, green);
            Block("Platform_C_Top4.5m", sector, 26, 29, 6, 4, 4.5f, green);
            Block("Platform_Isolated_Top0.75m", sector, 25, 18, 4, 4, .75f, green);
            Block("Platform_Isolated_Top2.25m", sector, 33, 19, 4, 6, 2.25f, green);
            Label("02  /  PLATAFORMAS", sector, new Vector3(24.5f, .025f, 33.5f), 1.6f);
        }

        private static void BuildStairs(Transform root)
        {
            Transform sector = Group("03_Stairs_VariedSteps", root);
            Outline("Sector_Boundary", sector, -24.5f, -24.5f, 23, 23, sand);
            float[] rises = { .15f, .25f, .40f };
            float[] treads = { .40f, .60f, .80f };
            int[] counts = { 10, 8, 6 };
            for (int lane = 0; lane < rises.Length; lane++)
            {
                float x = -32 + lane * 7;
                Transform flight = Group("Stairs_Rise" + Metres(rises[lane]) + "_Tread" + Metres(treads[lane]), sector);
                for (int step = 0; step < counts[lane]; step++)
                    Block("Step_" + (step + 1).ToString("00"), flight, x, -16 - (step + .5f) * treads[lane],
                        4, treads[lane], (step + 1) * rises[lane], sand);
                Block("Landing_Grounded", flight, x, -16 - counts[lane] * treads[lane] - 1.5f,
                    4, 3, counts[lane] * rises[lane], sand);
                Label(Metres(rises[lane]) + " / " + Metres(treads[lane]) + " m", flight,
                    new Vector3(x, .025f, -14.2f), 3.2f);
            }
            Label("03  /  ESCADAS", sector, new Vector3(-24.5f, .025f, -33.5f), 1.8f);
        }

        private static void BuildCollisions(Transform root)
        {
            Transform sector = Group("04_Collisions_Boxes_Walls_Pillars", root);
            Outline("Sector_Boundary", sector, -24.5f, 0, 23, 18, stone);
            for (int i = 0; i < 3; i++)
                Block("Cube_" + (i + 1) + "m", sector, -32 + i * 7, -5, i + 1, i + 1, i + 1, stone);
            Block("Wall_Low_7x0.5x1m", sector, -31, 1, 7, .5f, 1, stone);
            Block("Wall_Tall_6x0.75x2m", sector, -21, 1, 6, .75f, 2, stone);
            Pillar("Pillar_Small", sector, -33, 6, .4f, 2, stone);
            Pillar("Pillar_Medium", sector, -27, 6, .6f, 3.5f, stone);
            Pillar("Pillar_Large", sector, -20, 6, 1, 5, stone);
            Label("04  /  COLISOES", sector, new Vector3(-24.5f, .025f, -7.7f), 1.6f);
        }

        private static void BuildCorridors(Transform root)
        {
            Transform sector = Group("05_Corridors_and_Clearances", root);
            Outline("Sector_Boundary", sector, 24.5f, 0, 23, 18, stone);
            float[] widths = { .9f, 1.4f, 2.2f };
            float[] clearances = { 1.4f, 2, 3 };
            float[] centers = { 17, 24, 32 };
            for (int i = 0; i < widths.Length; i++)
            {
                Transform lane = Group("Corridor_ClearWidth" + Metres(widths[i]) + "m", sector);
                float offset = widths[i] / 2 + .25f;
                Block("Wall_Left", lane, centers[i] - offset, 0, .5f, 12, clearances[i] + 1, stone);
                Block("Wall_Right", lane, centers[i] + offset, 0, .5f, 12, clearances[i] + 1, stone);
                Box("Passage_ClearHeight" + Metres(clearances[i]) + "m", lane,
                    new Vector3(centers[i], clearances[i] + .2f, 2), new Vector3(widths[i], .4f, 1), stone);
                Label(Metres(widths[i]) + " m", lane, new Vector3(centers[i], .025f, -7.5f), 3.5f);
            }
            Label("05  /  PASSAGENS", sector, new Vector3(24.5f, .025f, 7.7f), 1.6f);
        }

        private static void BuildGaps(Transform root)
        {
            Transform sector = Group("06_Gaps_0.5_1_1.5m", root);
            Outline("Sector_Boundary", sector, 0, -24.5f, 20, 23, green);
            float[] gaps = { .5f, 1, 1.5f };
            for (int i = 0; i < gaps.Length; i++)
            {
                float x = -6 + i * 6;
                Transform lane = Group("GapLane_" + Metres(gaps[i]) + "m", sector);
                Ramp("Entry_FromGround", lane, new Vector3(x, 0, -16), 3, 2, 1, 180, green);
                for (int platform = 0; platform < 3; platform++)
                    Block("Island_" + (platform + 1), lane, x, -20 - platform * (4 + gaps[i]), 3, 4, 1, green);
                Label("VAO " + Metres(gaps[i]) + " m", lane, new Vector3(x, .025f, -14.1f), 3.2f);
            }
            Label("06  /  VAOS", sector, new Vector3(0, .025f, -34.5f), 1.8f);
        }

        private static void BuildFlight(Transform root)
        {
            Transform sector = Group("07_Flight_Arches_and_Pillars", root);
            Outline("Sector_Boundary", sector, 24.5f, -24.5f, 23, 23, violet);
            for (int i = 0; i < 3; i++)
            {
                float radius = 2 + i * .5f;
                float spring = 2 + i;
                float z = -17 - i * 7.5f;
                Transform gate = Group("Arch_" + (i + 1) + "_Apex" + Metres(spring + radius) + "m", sector);
                Block("Support_Left", gate, 25 - radius - .3f, z, .6f, .8f, spring, violet);
                Block("Support_Right", gate, 25 + radius + .3f, z, .6f, .8f, spring, violet);
                Mesh mesh = ArchMesh(radius, .6f, spring, .8f);
                MeshSolid("ArchRing_OpenCenter", gate, new Vector3(25, 0, z), mesh, violet, false);
            }
            Pillar("FlightPillar_Height2.5m", sector, 16, -18, .55f, 2.5f, violet);
            Pillar("FlightPillar_Height4m", sector, 16, -26, .7f, 4, violet);
            Pillar("FlightPillar_Height6m", sector, 34, -30, .85f, 6, violet);
            Label("07  /  VOO", sector, new Vector3(24.5f, .025f, -34.5f), 1.8f);
        }

        private static void Pillar(string name, Transform parent, float x, float z, float radius,
            float height, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = new Vector3(x, height / 2, z);
            item.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            item.isStatic = true;
            item.GetComponent<Renderer>().sharedMaterial = material;
            // The default primitive uses a capsule; use the actual flat-ended mesh instead.
            Object.DestroyImmediate(item.GetComponent<Collider>());
            MeshCollider collider = item.AddComponent<MeshCollider>();
            collider.sharedMesh = item.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = true;
        }

        private static void Ramp(string name, Transform parent, Vector3 start, float width,
            float run, float rise, float yaw, Material material)
        {
            Vector3 a = new Vector3(-width / 2, 0, 0), b = new Vector3(width / 2, 0, 0);
            Vector3 c = new Vector3(-width / 2, 0, run), d = new Vector3(width / 2, 0, run);
            Vector3 e = new Vector3(-width / 2, rise, run), f = new Vector3(width / 2, rise, run);
            var vertices = new List<Vector3>();
            Triangle(vertices, a, e, b); Triangle(vertices, b, e, f);
            Triangle(vertices, a, b, c); Triangle(vertices, b, d, c);
            Triangle(vertices, c, d, e); Triangle(vertices, d, f, e);
            Triangle(vertices, a, c, e); Triangle(vertices, b, f, d);
            string key = "Ramp_W" + Metres(width) + "_L" + Metres(run) + "_H" + Metres(rise);
            Mesh mesh = SaveMesh(key, vertices);
            GameObject item = MeshSolid(name, parent, start, mesh, material, true);
            item.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private static Mesh ArchMesh(float innerRadius, float thickness, float spring, float depth)
        {
            const int segments = 16;
            var vertices = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI / segments, b = (i + 1) * Mathf.PI / segments;
                Vector3 ia = ArcPoint(innerRadius, a, spring, -depth / 2);
                Vector3 ib = ArcPoint(innerRadius, b, spring, -depth / 2);
                Vector3 oa = ArcPoint(innerRadius + thickness, a, spring, -depth / 2);
                Vector3 ob = ArcPoint(innerRadius + thickness, b, spring, -depth / 2);
                Vector3 shift = new Vector3(0, 0, depth);
                Quad(vertices, ia, ib, ob, oa);                       // Front, -Z.
                Quad(vertices, ia + shift, oa + shift, ob + shift, ib + shift);
                Quad(vertices, ia, ia + shift, ib + shift, ib);      // Inner opening.
                Quad(vertices, oa, ob, ob + shift, oa + shift);      // Outer curve.
                if (i == 0) Quad(vertices, ia, oa, oa + shift, ia + shift);
                if (i == segments - 1) Quad(vertices, ib, ib + shift, ob + shift, ob);
            }
            return SaveMesh("Arch_R" + Metres(innerRadius) + "_Spring" + Metres(spring), vertices);
        }

        private static Vector3 ArcPoint(float radius, float angle, float spring, float z)
        {
            return new Vector3(Mathf.Cos(angle) * radius, spring + Mathf.Sin(angle) * radius, z);
        }

        private static void Triangle(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c)
        {
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
        }

        private static void Quad(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(vertices, a, b, c); Triangle(vertices, a, c, d);
        }

        private static Mesh SaveMesh(string name, List<Vector3> vertices)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(Enumerable.Range(0, vertices.Count).ToArray(), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return Persist(mesh, AssetRoot + "/Meshes/" + name + ".asset");
        }

        private static GameObject MeshSolid(string name, Transform parent, Vector3 position,
            Mesh mesh, Material material, bool convex)
        {
            Transform item = Group(name, parent);
            item.localPosition = position;
            item.gameObject.isStatic = true;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            MeshCollider collider = item.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            // Static, non-convex arches retain their openings; a convex hull would fill them.
            collider.convex = convex;
            return item.gameObject;
        }

        private static void Outline(string name, Transform parent, float x, float z, float width,
            float depth, Material material)
        {
            const float line = .065f, y = .012f;
            var vertices = new List<Vector3>();
            AddFlatStrip(vertices, -width / 2, -depth / 2, width / 2, -depth / 2 + line, y);
            AddFlatStrip(vertices, -width / 2, depth / 2 - line, width / 2, depth / 2, y);
            AddFlatStrip(vertices, -width / 2, -depth / 2 + line, -width / 2 + line, depth / 2 - line, y);
            AddFlatStrip(vertices, width / 2 - line, -depth / 2 + line, width / 2, depth / 2 - line, y);
            string key = "Outline_" + Metres(width) + "x" + Metres(depth);
            Transform item = Group(name + "_Paint_NoCollider", parent);
            item.localPosition = new Vector3(x, 0, z);
            item.gameObject.isStatic = true;
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh(key, vertices);
            MeshRenderer renderer = item.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void AddFlatStrip(List<Vector3> vertices, float x0, float z0, float x1, float z1, float y)
        {
            Quad(vertices, new Vector3(x0, y, z0), new Vector3(x0, y, z1),
                new Vector3(x1, y, z1), new Vector3(x1, y, z0));
        }

        private static void Label(string text, Transform parent, Vector3 position, float charactersPerMetre)
        {
            Transform item = Group("Label_" + text.Replace('\n', '_') + "_NoCollider", parent);
            item.localPosition = position;
            item.localRotation = Quaternion.Euler(90, 0, 0);
            item.gameObject.isStatic = true;
            TextMesh label = item.gameObject.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.characterSize = 10 / (64 * charactersPerMetre);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(.9f, .92f, .90f);
            label.text = text;
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = label.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void BuildLightingAndCamera(Transform root)
        {
            Transform sector = Group("08_Lighting_and_Overview", root);
            Light sun = Group("Sun_Key", sector).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            sun.color = new Color(1, .96f, .89f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .65f;
            sun.shadowBias = .03f;
            Light fill = Group("Sky_Fill", sector).gameObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(38, 145, 0);
            fill.color = new Color(.76f, .85f, 1);
            fill.intensity = .45f;
            fill.shadows = LightShadows.None;
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.57f, .62f, .68f);
            RenderSettings.ambientEquatorColor = new Color(.40f, .43f, .48f);
            RenderSettings.ambientGroundColor = new Color(.29f, .31f, .34f);
            RenderSettings.fog = false;

            Camera camera = Group("OverviewCamera", sector).gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(62, 76, -82);
            camera.transform.LookAt(new Vector3(0, 1, 0));
            camera.orthographic = true;
            camera.orthographicSize = 55;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 300;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.20f, .23f, .27f);
            camera.gameObject.AddComponent<AudioListener>();
        }

        private static void Validate(Scene scene)
        {
            Checks.Clear();
            Transform root = scene.GetRootGameObjects().Single(item => item.name == RootName).transform;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            Require(transforms.Select(HierarchyPath).Distinct().Count() == transforms.Length,
                "Nomes e caminhos unicos na hierarquia gerada.");
            Require(root.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                "Estruturas fixas sem Rigidbody; sem scripts de runtime.");
            Require(root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0,
                "Cena independente de scripts do gerador no Play Mode.");
            Transform spawn = transforms.Single(item => item.name == "PlayerSpawn");
            Require(spawn.childCount == 0 && spawn.GetComponents<Component>().Length == 1 &&
                spawn.position == new Vector3(0, .05f, 0), "PlayerSpawn vazio no centro, a 5 cm do chao.");
            Collider ground = colliders.Single(item => item.name == "Ground_80x80m_TopY0");
            Physics.SyncTransforms();
            Require(Vector3.Distance(ground.bounds.size, new Vector3(80, 1, 80)) < .001f &&
                Mathf.Abs(ground.bounds.max.y) < .001f, "Chao 80 x 80 m, superficie em Y = 0.");
            Material groundMaterial = ground.GetComponent<Renderer>().sharedMaterial;
            Require(groundMaterial.GetTexture("_BaseMap") is Texture2D grid && grid.width == 320 &&
                groundMaterial.GetTextureScale("_BaseMap") == new Vector2(16, 16),
                "Grade: 5 m por repeticao, 16 repeticoes em 80 m, linhas a cada 64/320 = 1 m.");
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool decoration = renderer.name.EndsWith("_NoCollider", StringComparison.Ordinal);
                Require(decoration || renderer.GetComponent<Collider>() != null,
                    "Colisor presente: " + HierarchyPath(renderer.transform));
                Require(renderer.sharedMaterials.All(material => material != null && material.shader != null && material.shader.isSupported),
                    "Material e shader suportados: " + renderer.name);
            }
            foreach (Collider collider in colliders)
            {
                Require(collider.enabled && !collider.isTrigger && collider.gameObject.isStatic,
                    "Colisor solido estatico: " + HierarchyPath(collider.transform));
                Require(collider.bounds.min.x >= -40.01f && collider.bounds.max.x <= 40.01f &&
                    collider.bounds.min.z >= -40.01f && collider.bounds.max.z <= 40.01f,
                    "Dentro do terreno: " + collider.name);
                if (collider != ground)
                    Require(collider.bounds.max.x <= -8 || collider.bounds.min.x >= 8 ||
                        collider.bounds.max.z <= -8 || collider.bounds.min.z >= 8,
                        "Area central livre: " + collider.name);
            }
            foreach (float angle in new[] { 15f, 30f, 45f })
            {
                Collider ramp = colliders.Single(item => item.name == "Slope_" + angle + "deg");
                var ray = new Ray(new Vector3(ramp.bounds.center.x, 30, ramp.bounds.center.z), Vector3.down);
                Require(ramp.Raycast(ray, out RaycastHit hit, 60) &&
                    Mathf.Abs(Vector3.Angle(hit.normal, Vector3.up) - angle) < .1f &&
                    Mathf.Abs(ramp.bounds.min.y) < .001f,
                    "Rampa " + angle + " graus: inclinacao medida no colisor e base no chao.");
            }
            foreach (Transform lane in transforms.Where(item => item.name.StartsWith("GapLane_", StringComparison.Ordinal)))
            {
                Collider[] islands = lane.GetComponentsInChildren<Collider>().Where(item => item.name.StartsWith("Island_", StringComparison.Ordinal))
                    .OrderByDescending(item => item.bounds.center.z).ToArray();
                float expected = float.Parse(lane.name.Substring(8).TrimEnd('m'), System.Globalization.CultureInfo.InvariantCulture);
                Require(islands.Length == 3 && Mathf.Abs(islands[0].bounds.min.z - islands[1].bounds.max.z - expected) < .001f &&
                    Mathf.Abs(islands[1].bounds.min.z - islands[2].bounds.max.z - expected) < .001f,
                    "Vaos medidos entre faces: " + Metres(expected) + " m.");
            }
            foreach (MeshCollider arch in colliders.OfType<MeshCollider>().Where(item => item.name == "ArchRing_OpenCenter"))
            {
                Vector3 center = arch.bounds.center;
                // A ray through the hole must remain clear; a ray through the crown must hit.
                float spring = arch.bounds.min.y;
                var opening = new Ray(new Vector3(center.x, spring + .25f, center.z - 2), Vector3.forward);
                var crown = new Ray(new Vector3(center.x, arch.bounds.max.y - .1f, center.z - 2), Vector3.forward);
                Require(!arch.convex && !arch.Raycast(opening, out _, 4) && arch.Raycast(crown, out _, 4),
                    "Arco com vao preservado e coroa solida: " + arch.transform.parent.name);
            }
            // Positive volume intersections are forbidden. Shared support/contact faces are allowed.
            // AABBs are only a broad phase; actual shapes resolve wedges and hollow arch rings.
            for (int a = 0; a < colliders.Length; a++)
            for (int b = a + 1; b < colliders.Length; b++)
            {
                if (!colliders[a].bounds.Intersects(colliders[b].bounds)) continue;
                Collider first = colliders[a], second = colliders[b];
                if (first is MeshCollider firstMesh && !firstMesh.convex)
                { first = colliders[b]; second = colliders[a]; }
                if (first is MeshCollider nonConvex && !nonConvex.convex) continue;
                bool overlap = Physics.ComputePenetration(first, first.transform.position, first.transform.rotation,
                    second, second.transform.position, second.transform.rotation, out _, out float distance);
                Require(!overlap || distance < .002f, "Sem interpenetracao: " + colliders[a].name + " / " + colliders[b].name);
            }
            Require(root.GetComponentsInChildren<Camera>().Length == 1, "Uma camera de visao geral.");
            Checks.Add("Objetos gerados: " + transforms.Length + "; colisores: " + colliders.Length + ".");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("TestPlayground: " + message);
            Checks.Add(message);
        }

        private static string HierarchyPath(Transform item)
        {
            return item.parent == null ? item.name : HierarchyPath(item.parent) + "/" + item.name;
        }

        private static string Metres(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void CaptureOverview(Transform root)
        {
            // A preview is supplementary: failure to render must not prevent scene creation.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Camera camera = root.GetComponentInChildren<Camera>();
            RenderTexture previous = RenderTexture.active;
            bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            RenderTexture target = RenderTexture.GetTemporary(1600, 1000, 24);
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                camera.targetTexture = target;
                // The first render initializes URP resources; the second captures the scene.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/TestPlayground-overview.png", image.EncodeToPNG());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("TestPlayground: cena salva, mas a captura opcional falhou: " + exception.Message);
            }
            finally
            {
                camera.targetTexture = null;
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
