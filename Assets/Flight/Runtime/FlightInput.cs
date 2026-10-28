using UnityEngine;
using UnityEngine.InputSystem;

namespace FlappyFlight
{
    public sealed class FlightInput : MonoBehaviour
    {
        public InputActionAsset bindings;
        private InputActionAsset instance;
        private InputActionMap map;
        public bool HasBindings => bindings != null;
        public InputAction Action(string name) { EnsureEnabled(); return map?.FindAction(name, true); }
        public void EnsureEnabled()
        {
            if (instance != null || bindings == null) return;
            instance = Instantiate(bindings);
            map = instance.FindActionMap("Flight", true);
            map.Enable();
        }
        public FlightCommand ReadCommand()
        {
            EnsureEnabled();
            if (map == null) return default;
            return new FlightCommand
            {
                Pitch = map.FindAction("Pitch").ReadValue<float>(),
                Roll = map.FindAction("Roll").ReadValue<float>(),
                Yaw = map.FindAction("Yaw").ReadValue<float>(),
                Throttle = (map.FindAction("Faster").IsPressed() ? 1 : 0) - (map.FindAction("Slower").IsPressed() ? 1 : 0)
            };
        }
        public Vector2 LookDelta => Action("Look")?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool Pressed(string name) => Action(name)?.WasPressedThisFrame() ?? false;
        public bool Held(string name) => Action(name)?.IsPressed() ?? false;
        private void OnEnable() { if (Application.isPlaying) EnsureEnabled(); }
        private void OnDisable()
        {
            if (instance == null) return;
            instance.Disable();
            if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance);
            instance = null;
            map = null;
        }
    }
}
