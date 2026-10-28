using System;
using UnityEngine;

namespace FlappyFlight
{
    [RequireComponent(typeof(Rigidbody), typeof(FlightInput))]
    public sealed class FlappyFlightController : MonoBehaviour
    {
        public FlightSettings settings;
        public Transform visualRoot;
        public Transform cameraTarget;
        public Transform aimOrigin;
        [Header("Fallback aerial spawn; scene FlightSpawn overrides these")]
        public Vector3 spawnPosition = new Vector3(0, 5, -4);
        public Vector3 spawnEuler;
        public Transform spawnPoint;
        public bool captureCursorOnStart = true;
        public bool automaticSimulation = true;
        [Min(10)] public float horizontalSafetyBoundary = 55;
        public Vector2 altitudeSafetyRange = new Vector2(-3, 45);
        public FlightState State { get; } = new FlightState();
        public Rigidbody Body { get; private set; }
        public FlightInput Input { get; private set; }
        public bool CursorCaptured { get; private set; }
        public int ResetCount { get; private set; }
        public string LastResetReason { get; private set; } = "Start";
        public event Action ResetPerformed;
        public event Action<bool> FreeLookChanged;
        private FlightCommand command;
        private bool resetPending, discardNextMouse;
        private string pendingReason;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Input = GetComponent<FlightInput>();
        }
        private void Start()
        {
            if (settings == null) { Debug.LogError("FlightSettings missing.", this); enabled = false; return; }
            RestartFlight("Start");
            if (captureCursorOnStart) CaptureCursor();
        }
        private void Update()
        {
            if (!automaticSimulation || settings == null) return;
            if (Input.Pressed("ReleaseCursor")) ReleaseCursor();
            else if (!CursorCaptured && Input.Pressed("CaptureCursor")) CaptureCursor();
            if (Input.Pressed("Reset")) RequestReset("R");
            SetFreeLook(CursorCaptured && Input.Held("FreeLook"));
            Vector2 look = Input.LookDelta;
            if (discardNextMouse) { look = Vector2.zero; discardNextMouse = false; }
            if (CursorCaptured && !State.FreeLook) State.Look(Vector2.ClampMagnitude(look, 800), settings);
            // Input System owns focus/device filtering. A second Application.isFocused gate
            // discards valid input from its isolated runtime and Editor Game View policies.
            command = Input.ReadCommand();
        }
        private void FixedUpdate()
        {
            if (automaticSimulation && settings != null) Simulate(command, Time.fixedDeltaTime);
        }
        public void Simulate(FlightCommand input, float dt)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (resetPending) { RestartFlight(pendingReason); return; }
            State.Step(input, settings, dt);
            Body.MoveRotation(State.Rotation);
            Body.linearVelocity = State.Forward * State.Speed;
            Vector3 p = Body.position;
            if (Mathf.Abs(p.x) > horizontalSafetyBoundary || Mathf.Abs(p.z) > horizontalSafetyBoundary ||
                p.y < altitudeSafetyRange.x || p.y > altitudeSafetyRange.y)
                RequestReset("Airspace boundary");
        }
        public void SetFreeLook(bool enabled)
        {
            if (State.FreeLook == enabled) return;
            State.SetFreeLook(enabled);
            FreeLookChanged?.Invoke(enabled);
        }
        public void RequestReset(string reason) { resetPending = true; pendingReason = reason; }
        public void RestartFlight(string reason)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            Vector3 point = spawnPoint != null ? spawnPoint.position : spawnPosition;
            Quaternion heading = spawnPoint != null ? spawnPoint.rotation : Quaternion.Euler(spawnEuler);
            State.Reset(heading, settings);
            Body.position = point;
            Body.rotation = State.Rotation;
            Body.linearVelocity = State.Forward * State.Speed;
            Body.angularVelocity = Vector3.zero;
            Body.WakeUp();
            resetPending = false;
            command = default;
            discardNextMouse = true;
            LastResetReason = reason;
            ResetCount++;
            ResetPerformed?.Invoke();
        }
        private void OnCollisionEnter(Collision collision)
        {
            RequestReset("Collision: " + collision.collider.name);
        }
        public void CaptureCursor()
        {
            CursorCaptured = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            discardNextMouse = true;
        }
        public void ReleaseCursor()
        {
            CursorCaptured = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetFreeLook(false);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { command = default; ReleaseCursor(); }
        }
        private void OnDisable() { if (Application.isPlaying) ReleaseCursor(); }
    }
}
