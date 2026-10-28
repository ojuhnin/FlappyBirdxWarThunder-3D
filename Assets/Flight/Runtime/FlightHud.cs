using UnityEngine;

namespace FlappyFlight
{
    [RequireComponent(typeof(Camera), typeof(FlightChaseCamera))]
    public sealed class FlightHud : MonoBehaviour
    {
        public FlappyFlightController player;
        public FlightChaseCamera chase;
        public bool showHelp = true;
        [Min(5)] public float bodyMarkerDistance = 30;
        private Texture2D ring;
        private GUIStyle textStyle;
        private void OnGUI()
        {
            if (player == null || player.settings == null || chase == null) return;
            EnsureResources();
            Color previous = GUI.color;
            if (chase.ShowsFlightReticle)
            {
                GUI.color = new Color(.9f, .98f, 1, .8f);
                GUI.DrawTexture(new Rect(Screen.width / 2f - 8, Screen.height / 2f - 8, 16, 16), ring);
                Camera view = GetComponent<Camera>();
                Vector3 origin = player.aimOrigin != null ? player.aimOrigin.position : player.transform.position;
                Vector3 nose = view.WorldToScreenPoint(origin + player.transform.forward * bodyMarkerDistance);
                if (nose.z > 0 && nose.x > 5 && nose.x < Screen.width - 5 && nose.y > 5 && nose.y < Screen.height - 5)
                {
                    GUI.color = new Color(1, .71f, .25f, .95f);
                    GUI.DrawTexture(new Rect(nose.x - 2, Screen.height - nose.y - 2, 4, 4), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;
            string status = player.State.FreeLook ? "OLHAR LIVRE" : chase.Returning ? "RETORNANDO A CAMERA" : "VOO ASSISTIDO";
            GUI.Label(new Rect(16, 12, 650, 26), status + "   " + player.State.Speed.ToString("0.0") +
                " m/s  |  alvo " + player.State.TargetSpeed.ToString("0.0") + " m/s", textStyle);
            if (showHelp)
                GUI.Label(new Rect(16, Screen.height - 66, Screen.width - 32, 60),
                    "Mouse: direcao  |  W/S: nariz baixo/cima  |  A/D: rolagem  |  Q/E: guinada\n" +
                    "Shift/Ctrl: velocidade  |  C: olhar livre  |  R: reiniciar no ar  |  Esc: liberar mouse" +
                    (player.CursorCaptured ? "" : "  |  Clique para capturar"), textStyle);
            GUI.color = previous;
        }
        private void EnsureResources()
        {
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 14, normal = { textColor = new Color(.93f, .95f, 1) } };
            if (ring != null) return;
            ring = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "FlightAimRing" };
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(16, 16));
                ring.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1.5f - Mathf.Abs(radius - 12))));
            }
            ring.Apply();
        }
        private void OnDestroy() { if (ring != null) Destroy(ring); }
    }
}
