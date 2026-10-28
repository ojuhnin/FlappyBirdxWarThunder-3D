using UnityEngine;

namespace FlappyFlight
{
    [RequireComponent(typeof(Camera))]
    public sealed class FlightChaseCamera : MonoBehaviour
    {
        public FlappyFlightController player;
        [Min(1)] public float distance = 4.2f;
        [Min(0)] public float height = .85f;
        [Range(35, 100)] public float fieldOfView = 68;
        [Min(.1f)] public float positionSharpness = 12;
        [Min(.1f)] public float returnSharpness = 9;
        [Min(.05f)] public float collisionRadius = .2f;
        [Min(.01f)] public float collisionPadding = .08f;
        [Min(.1f)] public float modelHideDistance = 1.05f;
        public LayerMask obstacleMask = ~0;
        public bool automaticCamera = true;
        public bool Returning { get; private set; }
        public bool ShowsFlightReticle => player != null && !player.State.FreeLook && !Returning;
        private Camera view;
        private Quaternion viewRotation;
        private float freeYaw, freePitch;
        private bool initialized;
        private readonly RaycastHit[] hits = new RaycastHit[48];
        private Renderer[] visualRenderers;
        private void OnEnable()
        {
            view = GetComponent<Camera>();
            if (player == null) return;
            player.ResetPerformed += Snap;
            player.FreeLookChanged += OnFreeLookChanged;
        }
        private void OnDisable()
        {
            if (player != null) { player.ResetPerformed -= Snap; player.FreeLookChanged -= OnFreeLookChanged; }
            SetVisualHidden(false);
        }
        private void OnFreeLookChanged(bool enabled)
        {
            if (enabled)
            {
                Vector3 direction = viewRotation * Vector3.forward;
                freeYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                freePitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
            }
            Returning = !enabled;
        }
        private void LateUpdate()
        {
            if (player == null || player.settings == null || !automaticCamera) return;
            Tick(Time.deltaTime, player.State.FreeLook && player.CursorCaptured ? player.Input.LookDelta : Vector2.zero);
        }
        public void Tick(float dt, Vector2 lookPixels)
        {
            if (player == null || player.settings == null) return;
            if (!initialized) Snap();
            if (player.State.FreeLook)
            {
                freeYaw += lookPixels.x * player.settings.degreesPerPixel;
                freePitch = Mathf.Clamp(freePitch - lookPixels.y * player.settings.degreesPerPixel, -75, 75);
                viewRotation = Quaternion.Euler(freePitch, freeYaw, 0);
            }
            else if (Returning)
            {
                viewRotation = Quaternion.Slerp(viewRotation, player.State.AimRotation, 1 - Mathf.Exp(-returnSharpness * dt));
                if (Quaternion.Angle(viewRotation, player.State.AimRotation) < .3f)
                { viewRotation = player.State.AimRotation; Returning = false; }
            }
            else viewRotation = player.State.AimRotation;
            Vector3 pivot = player.cameraTarget != null ? player.cameraTarget.position : player.transform.position;
            Vector3 desired = pivot - viewRotation * Vector3.forward * distance + Vector3.up * height;
            Vector3 smoothed = Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-positionSharpness * dt));
            transform.SetPositionAndRotation(ResolveOcclusion(pivot, smoothed), viewRotation);
            if (view == null) view = GetComponent<Camera>();
            view.fieldOfView = fieldOfView;
            SetVisualHidden(Vector3.Distance(transform.position, pivot) < modelHideDistance);
        }
        public Vector3 ResolveOcclusion(Vector3 pivot, Vector3 desired)
        {
            Vector3 offset = desired - pivot;
            float length = offset.magnitude;
            if (length < .001f) return pivot;
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, offset / length, hits, length,
                obstacleMask, QueryTriggerInteraction.Ignore);
            float safe = length;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.attachedRigidbody == player.Body || hits[i].collider.transform.IsChildOf(player.transform)) continue;
                safe = Mathf.Min(safe, Mathf.Max(.05f, hits[i].distance - collisionPadding));
            }
            return pivot + offset / length * safe;
        }
        public void Snap()
        {
            if (player == null || player.settings == null) return;
            viewRotation = player.State.AimRotation;
            Returning = false;
            Vector3 pivot = player.cameraTarget != null ? player.cameraTarget.position : player.transform.position;
            // Rigidbody teleports are immediate, including interpolated render transforms.
            if (player.Body != null && player.cameraTarget != null)
                pivot = player.Body.position + player.Body.rotation * player.cameraTarget.localPosition;
            transform.SetPositionAndRotation(ResolveOcclusion(pivot,
                pivot - viewRotation * Vector3.forward * distance + Vector3.up * height), viewRotation);
            initialized = true;
            SetVisualHidden(false);
        }
        private void SetVisualHidden(bool hidden)
        {
            if (player == null || player.visualRoot == null) return;
            if (visualRenderers == null) visualRenderers = player.visualRoot.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in visualRenderers) if (renderer != null) renderer.forceRenderingOff = hidden;
        }
    }
}
