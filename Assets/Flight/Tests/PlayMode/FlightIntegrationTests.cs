using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FlappyFlight.Tests
{
    public sealed class FlightIntegrationTests
    {
        private FlappyFlightController player;
        private FlightChaseCamera chase;
        private GameObject playerObject, cameraObject, obstacle;
        private SimulationMode previousMode;
        private InputTestFixture inputFixture;
        private Keyboard keyboard;
        [UnitySetUp] public IEnumerator Setup()
        {
            inputFixture = new InputTestFixture(); inputFixture.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            previousMode = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
#if UNITY_EDITOR
            playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Flight/Prefabs/FlappyBirdPlayer.prefab"));
            cameraObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Flight/Prefabs/FlightCameraRig.prefab"));
#else
            Assert.Ignore("These integration tests use Editor-imported project prefabs.");
#endif
            player = playerObject.GetComponent<FlappyFlightController>();
            player.automaticSimulation = false; player.captureCursorOnStart = false;
            chase = cameraObject.GetComponent<FlightChaseCamera>();
            chase.enabled = false; chase.player = player; chase.automaticCamera = false; chase.enabled = true;
            cameraObject.GetComponent<FlightHud>().enabled = false;
            yield return null;
            chase.Snap();
            Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Object.Destroy(playerObject); Object.Destroy(cameraObject); if (obstacle != null) Object.Destroy(obstacle);
            Physics.simulationMode = previousMode;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            yield return null;
            inputFixture.TearDown();
        }
        private void Simulate(int steps)
        {
            for (int i = 0; i < steps; i++) { player.Simulate(default, .02f); Physics.Simulate(.02f); }
        }
        [UnityTest] public IEnumerator RealRigidbodyStartsInAirAndMovesContinuously()
        {
            Vector3 start = player.Body.position;
            Simulate(50);
            Assert.That(player.Body.position.z - start.z, Is.EqualTo(player.settings.cruiseSpeed).Within(.1f));
            Assert.That(player.Body.position.y, Is.EqualTo(5).Within(.01f));
            Assert.That(player.Body.linearVelocity.magnitude, Is.GreaterThan(0));
            yield return null;
        }
        [UnityTest] public IEnumerator RigidbodyActuallyRotatesWithTheSteeringState()
        {
            for (int i = 0; i < 35; i++)
            { player.Simulate(new FlightCommand { Yaw = 1 }, .02f); Physics.Simulate(.02f); }
            Assert.That(Quaternion.Angle(Quaternion.identity, player.Body.rotation), Is.GreaterThan(15));
            Assert.That(Quaternion.Angle(player.Body.rotation, player.State.Rotation), Is.LessThan(.5f));
            yield return null;
        }
        [UnityTest] public IEnumerator RuntimeHudMatchesNormalAndFreeLookStates()
        {
            FlightHud hud = cameraObject.GetComponent<FlightHud>();
            hud.player = player; hud.chase = chase; hud.enabled = true;
            Assert.That(chase.ShowsFlightReticle, Is.True);
            yield return null;
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                System.IO.Directory.CreateDirectory("Logs");
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Logs/FlappyFlight-runtime-hud.png"));
                for (int i = 0; i < 12; i++) yield return null;
            }
            player.SetFreeLook(true);
            Assert.That(chase.ShowsFlightReticle, Is.False);
        }
        [UnityTest] public IEnumerator RunningPlayerLoopRemainsStableAtRequestedRenderCadences()
        {
            int oldTarget = Application.targetFrameRate, oldVsync = QualitySettings.vSyncCount;
            SimulationMode oldMode = Physics.simulationMode;
            var measurements = new System.Collections.Generic.List<string>();
            try
            {
                QualitySettings.vSyncCount = 0;
                Physics.simulationMode = SimulationMode.FixedUpdate;
                player.automaticSimulation = true;
                chase.automaticCamera = true;
                foreach (int requested in new[] { 30, 60, 144 })
                {
                    Application.targetFrameRate = requested;
                    yield return null;
                    float start = Time.realtimeSinceStartup;
                    int frames = 0;
                    float largestFrame = 0;
                    while (Time.realtimeSinceStartup - start < 1.2f)
                    {
                        player.State.Look(new Vector2(30 * Time.deltaTime, 0), player.settings);
                        yield return null;
                        frames++;
                        largestFrame = Mathf.Max(largestFrame, Time.unscaledDeltaTime);
                        Assert.That(float.IsNaN(player.Body.position.x), Is.False);
                        Assert.That(player.Body.linearVelocity.magnitude, Is.InRange(player.settings.minimumSpeed - .1f, player.settings.maximumSpeed + .1f));
                        Assert.That(player.Body.position.y, Is.EqualTo(5).Within(.05f));
                        Assert.That(Mathf.Abs(player.State.Bank), Is.LessThanOrEqualTo(player.settings.manualBankLimit));
                    }
                    float elapsed = Time.realtimeSinceStartup - start;
                    measurements.Add("Requested=" + requested + " Hz; observed=" + (frames / elapsed).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                        " fps; frames=" + frames + "; max_frame_ms=" + (largestFrame * 1000).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                    Assert.That(Vector3.Dot(chase.transform.right, Vector3.up), Is.EqualTo(0).Within(.001f));
                }
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllLines("Logs/FlappyFlight-render-cadences.txt", measurements);
            }
            finally
            {
                Application.targetFrameRate = oldTarget;
                QualitySettings.vSyncCount = oldVsync;
                Physics.simulationMode = oldMode;
            }
        }
        [UnityTest] public IEnumerator RealPlayerLoopReceivesCombinedKeyboardDuringFreeLook()
        {
            player.automaticSimulation = true;
            player.CaptureCursor();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.Q, Key.LeftShift, Key.C));
            InputSystem.Update();
            yield return null;
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.State.FreeLook, Is.True);
            Assert.That(player.State.Pitch, Is.GreaterThan(3));
            Assert.That(player.State.Bank, Is.GreaterThan(3));
            Assert.That(player.State.TargetSpeed, Is.GreaterThan(player.settings.cruiseSpeed));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            yield return null;
            Assert.That(player.State.FreeLook, Is.False);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.State.Yaw, player.State.DesiredYaw)), Is.LessThan(3));
        }
        [UnityTest] public IEnumerator ActualResetKeyRestartsThePlayerLoop()
        {
            player.automaticSimulation = true;
            int resets = player.ResetCount;
            player.Body.position = new Vector3(8, 10, 8);
            // Queue for the next real Dynamic update: processing here would consume the edge
            // before the player's Update gets its frame (held-axis tests do not have that issue).
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            for (int i = 0; i < 8 && player.ResetCount == resets; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.ResetCount, Is.GreaterThan(resets));
            Assert.That(player.LastResetReason, Is.EqualTo("R"));
            Assert.That(Vector3.Distance(player.Body.position, player.spawnPosition), Is.LessThan(.01f));
        }
        [UnityTest] public IEnumerator GroundCollisionRestartsActualPhysicsAndCamera()
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "CollisionTestGround";
            obstacle.transform.position = new Vector3(0, -.5f, 0); obstacle.transform.localScale = new Vector3(10, 1, 10);
            int resets = player.ResetCount;
            player.Body.position = new Vector3(0, .2f, 0);
            Physics.SyncTransforms(); Physics.Simulate(.02f);
            player.Simulate(default, .02f);
            Assert.That(player.ResetCount, Is.GreaterThan(resets));
            Assert.That(player.LastResetReason, Does.StartWith("Collision:"));
            Assert.That(Vector3.Distance(player.Body.position, player.spawnPosition), Is.LessThan(.01f));
            Assert.That(player.Body.linearVelocity.magnitude, Is.EqualTo(player.settings.cruiseSpeed).Within(.01f));
            Assert.That(chase.Returning, Is.False);
            Assert.That(Vector3.Distance(chase.transform.position, player.Body.position), Is.InRange(3, 6));
            yield return null;
        }
        [UnityTest] public IEnumerator WallCollisionRestartsInsteadOfStoppingOnObstacle()
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "CollisionTestWall";
            obstacle.transform.position = player.spawnPosition + Vector3.forward * 2;
            obstacle.transform.localScale = new Vector3(6, 6, .5f);
            Physics.SyncTransforms(); int resets = player.ResetCount;
            Simulate(25);
            Assert.That(player.ResetCount, Is.GreaterThan(resets));
            Assert.That(player.Body.linearVelocity.magnitude, Is.GreaterThan(0));
            yield return null;
        }
        [UnityTest] public IEnumerator ResetClearsFreeLookAndCameraOrbit()
        {
            player.SetFreeLook(true); chase.Tick(.02f, new Vector2(1200, 200));
            Assert.That(player.State.FreeLook, Is.True);
            player.RequestReset("R"); player.Simulate(default, .02f);
            Assert.That(player.State.FreeLook, Is.False); Assert.That(chase.Returning, Is.False);
            Assert.That(Quaternion.Angle(chase.transform.rotation, player.State.AimRotation), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(player.Body.position, player.spawnPosition), Is.LessThan(.01f));
            yield return null;
        }
        [UnityTest] public IEnumerator FreeLookReturnIsSmoothAndNormalReticleMatchesDesiredDirection()
        {
            player.SetFreeLook(true); chase.Tick(.02f, new Vector2(1000, 100));
            player.Simulate(new FlightCommand { Yaw = 1, Pitch = -.5f }, .02f);
            Quaternion cameraBefore = chase.transform.rotation, bodyBefore = player.State.Rotation;
            player.SetFreeLook(false); chase.Tick(.02f, Vector2.zero);
            Assert.That(Quaternion.Angle(cameraBefore, chase.transform.rotation), Is.LessThan(35));
            Assert.That(Quaternion.Angle(bodyBefore, player.State.Rotation), Is.LessThan(.001f));
            Assert.That(chase.ShowsFlightReticle, Is.False);
            for (int i = 0; i < 120; i++) chase.Tick(.02f, Vector2.zero);
            Assert.That(chase.ShowsFlightReticle, Is.True);
            Assert.That(Quaternion.Angle(chase.transform.rotation, player.State.AimRotation), Is.LessThan(.01f));
            Assert.That(Mathf.Abs(Vector3.Dot(chase.transform.right, Vector3.up)), Is.LessThan(.001f));
            yield return null;
        }
        [UnityTest] public IEnumerator CameraSphereCastKeepsCameraInFrontOfWall()
        {
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "CameraTestWall";
            obstacle.transform.position = player.spawnPosition + new Vector3(0, 0, -2);
            obstacle.transform.localScale = new Vector3(6, 6, .5f);
            Physics.SyncTransforms();
            Vector3 pivot = player.spawnPosition + Vector3.up * .15f;
            Vector3 result = chase.ResolveOcclusion(pivot, pivot + new Vector3(0, .85f, -4.2f));
            Assert.That(result.z, Is.GreaterThan(obstacle.GetComponent<Collider>().bounds.max.z + chase.collisionRadius));
            yield return null;
        }
    }
}
