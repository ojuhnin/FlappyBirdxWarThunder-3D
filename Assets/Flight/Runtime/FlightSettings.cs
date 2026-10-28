using UnityEngine;

namespace FlappyFlight
{
    [CreateAssetMenu(menuName = "Flappy Flight/Flight Settings")]
    public sealed class FlightSettings : ScriptableObject
    {
        [Header("Prototype metres / seconds")]
        [Min(.1f)] public float minimumSpeed = 3;
        [Min(.1f)] public float cruiseSpeed = 5.5f;
        [Min(.1f)] public float maximumSpeed = 10;
        [Min(.1f)] public float throttleChange = 2.5f;
        [Min(.1f)] public float acceleration = 3;
        [Min(.1f)] public float deceleration = 4;
        [Header("Degrees / second")]
        [Min(1)] public float pitchRate = 55;
        [Min(1)] public float yawRate = 65;
        [Min(1)] public float rollRate = 110;
        [Range(10, 85)] public float pitchLimit = 65;
        [Range(10, 85)] public float manualBankLimit = 75;
        [Range(0, 75)] public float automaticBankLimit = 50;
        [Min(0)] public float bankTurnRate = 38;
        [Header("Assistance")]
        [Range(0, 1)] public float assistance = 1;
        [Min(.1f)] public float directionGain = 4;
        [Min(.1f)] public float bankGain = 5;
        [Min(.1f)] public float rateResponse = 10;
        [Min(.01f)] public float handoverSeconds = .35f;
        [Header("Mouse (delta is already per-frame, not a rate)")]
        [Range(.01f, 1)] public float degreesPerPixel = .12f;
        public bool invertMouseY;
        private void OnValidate()
        {
            minimumSpeed = Mathf.Max(.1f, minimumSpeed);
            maximumSpeed = Mathf.Max(minimumSpeed, maximumSpeed);
            cruiseSpeed = Mathf.Clamp(cruiseSpeed, minimumSpeed, maximumSpeed);
        }
    }
}
