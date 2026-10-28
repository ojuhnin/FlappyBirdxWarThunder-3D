using System.Linq;
using FlappyFlight.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FlappyFlight.Tests
{
    public sealed class InputAndImportTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private GameObject root;
        private FlightInput input;
        private InputTestFixture fixture;
        [SetUp] public void Setup()
        {
            fixture = new InputTestFixture(); fixture.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            root = new GameObject("InputTestOnly"); input = root.AddComponent<FlightInput>();
            input.bindings = AssetDatabase.LoadAssetAtPath<InputActionAsset>(FlightSetup.InputPath);
            input.EnsureEnabled();
            InputSystem.Update();
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            fixture.TearDown();
        }
        [TestCase(Key.W, 1, 0, 0, 0)] [TestCase(Key.S, -1, 0, 0, 0)]
        [TestCase(Key.A, 0, -1, 0, 0)] [TestCase(Key.D, 0, 1, 0, 0)]
        [TestCase(Key.Q, 0, 0, -1, 0)] [TestCase(Key.E, 0, 0, 1, 0)]
        [TestCase(Key.LeftShift, 0, 0, 0, 1)] [TestCase(Key.RightCtrl, 0, 0, 0, -1)]
        public void ActualBindingsMapIndividualKeys(Key key, int pitch, int roll, int yaw, int throttle)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); InputSystem.Update();
            FlightCommand command = input.ReadCommand();
            Assert.That(command.Pitch, Is.EqualTo(pitch)); Assert.That(command.Roll, Is.EqualTo(roll));
            Assert.That(command.Yaw, Is.EqualTo(yaw)); Assert.That(command.Throttle, Is.EqualTo(throttle));
        }
        [Test] public void CombinedKeysAndMouseRemainIndependent()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.Q, Key.LeftShift, Key.C));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40, -15) }); InputSystem.Update();
            FlightCommand command = input.ReadCommand();
            Assert.That(command.Pitch, Is.EqualTo(1)); Assert.That(command.Roll, Is.EqualTo(1));
            Assert.That(command.Yaw, Is.EqualTo(-1)); Assert.That(command.Throttle, Is.EqualTo(1));
            Assert.That(input.Held("FreeLook"), Is.True); Assert.That(input.LookDelta.x, Is.EqualTo(40));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            Assert.That(input.ReadCommand().Pitch, Is.EqualTo(0)); Assert.That(input.Held("FreeLook"), Is.False);
        }
        [Test] public void ImportedModelRetainsMeshesUvsBlendShapesAndExternalUrpMaterials()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FlightSetup.ModelPath);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.EqualTo(57));
            int shapes = 0, triangles = 0;
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                shapes += mesh.blendShapeCount; triangles += mesh.triangles.Length / 3;
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/Art/FlappyBird/Materials/"));
                }
            }
            Assert.That(shapes, Is.EqualTo(184)); Assert.That(triangles, Is.EqualTo(11504));
            Assert.That(renderers.SelectMany(r => r.sharedMaterials).Distinct().Count(), Is.EqualTo(8));
        }
        [Test] public void PrefabPhysicsAndCameraTargetsAreOutsideVisualHierarchy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlightSetup.PlayerPath);
            FlappyFlightController player = prefab.GetComponent<FlappyFlightController>();
            Assert.That(player.visualRoot.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(0));
            Assert.That(player.visualRoot.GetComponentsInChildren<Collider>().Length, Is.EqualTo(0));
            Assert.That(player.cameraTarget.parent, Is.EqualTo(player.transform));
            Assert.That(player.aimOrigin.parent, Is.EqualTo(player.transform));
            Assert.That(prefab.GetComponent<Rigidbody>().useGravity, Is.False);
        }
    }
}
