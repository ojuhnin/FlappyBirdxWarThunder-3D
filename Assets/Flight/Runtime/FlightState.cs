using UnityEngine;

namespace FlappyFlight
{
    public struct FlightCommand
    {
        public float Pitch, Roll, Yaw, Throttle;
    }

    // Deterministic steering state, independent from camera transforms and animated bones.
    public sealed class FlightState
    {
        public float Pitch { get; private set; }
        public float Yaw { get; private set; }
        public float Bank { get; private set; }
        public float DesiredPitch { get; private set; }
        public float DesiredYaw { get; private set; }
        public float Speed { get; private set; }
        public float TargetSpeed { get; private set; }
        public float PitchVelocity { get; private set; }
        public float YawVelocity { get; private set; }
        public float BankVelocity { get; private set; }
        public bool FreeLook { get; private set; }
        private bool pitchWasManual, yawWasManual;
        private float handover = 1;
        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, -Bank);
        public Quaternion AimRotation => Quaternion.Euler(DesiredPitch, DesiredYaw, 0);
        public Vector3 Forward => Rotation * Vector3.forward;

        public void Reset(Quaternion rotation, FlightSettings settings)
        {
            Vector3 forward = rotation * Vector3.forward;
            Pitch = DesiredPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg,
                -settings.pitchLimit, settings.pitchLimit);
            Yaw = DesiredYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            Bank = PitchVelocity = YawVelocity = BankVelocity = 0;
            Speed = TargetSpeed = Mathf.Clamp(settings.cruiseSpeed, settings.minimumSpeed, settings.maximumSpeed);
            FreeLook = pitchWasManual = yawWasManual = false;
            handover = 1;
        }

        public void Look(Vector2 pixels, FlightSettings settings)
        {
            if (FreeLook) return;
            DesiredYaw = Mathf.Repeat(DesiredYaw + pixels.x * settings.degreesPerPixel, 360);
            DesiredPitch = Mathf.Clamp(DesiredPitch + pixels.y * settings.degreesPerPixel *
                (settings.invertMouseY ? 1 : -1), -settings.pitchLimit, settings.pitchLimit);
        }

        public void SetFreeLook(bool enabled)
        {
            if (FreeLook == enabled) return;
            FreeLook = enabled;
            if (!enabled)
            {
                // Rebase the target, never rotate/teleport the body during a camera transition.
                DesiredYaw = Yaw;
                DesiredPitch = Pitch;
                handover = 0;
            }
        }

        public void Step(FlightCommand command, FlightSettings settings, float dt)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            bool pitchManual = Mathf.Abs(command.Pitch) > .05f;
            bool yawManual = Mathf.Abs(command.Yaw) > .05f || Mathf.Abs(command.Roll) > .05f;
            // After a manual axis is released, aim at its current motion, not a stale mouse heading.
            if (!pitchManual && pitchWasManual)
                DesiredPitch = Mathf.Clamp(Pitch + PitchVelocity / settings.rateResponse,
                    -settings.pitchLimit, settings.pitchLimit);
            if (!yawManual && yawWasManual)
                DesiredYaw = Yaw + YawVelocity / settings.rateResponse;
            handover = Mathf.MoveTowards(handover, 1, dt / settings.handoverSeconds);
            float assist = settings.assistance * Mathf.SmoothStep(0, 1, handover);
            float yawError = Mathf.DeltaAngle(Yaw, DesiredYaw);
            float pitchTargetRate = pitchManual ? command.Pitch * settings.pitchRate :
                Mathf.Clamp((DesiredPitch - Pitch) * settings.directionGain * assist, -settings.pitchRate, settings.pitchRate);
            float yawTargetRate = yawManual ? Mathf.Clamp(command.Yaw * settings.yawRate +
                Mathf.Sin(Bank * Mathf.Deg2Rad) * settings.bankTurnRate, -settings.yawRate, settings.yawRate) :
                Mathf.Clamp(yawError * settings.directionGain * assist, -settings.yawRate, settings.yawRate);
            float bankTarget = Mathf.Clamp(yawError * .9f, -settings.automaticBankLimit, settings.automaticBankLimit);
            if (yawManual) bankTarget = 0;
            float bankTargetRate = Mathf.Abs(command.Roll) > .05f ? command.Roll * settings.rollRate :
                Mathf.Clamp((bankTarget - Bank) * settings.bankGain * assist, -settings.rollRate, settings.rollRate);
            float response = 1 - Mathf.Exp(-settings.rateResponse * dt);
            PitchVelocity = Mathf.Lerp(PitchVelocity, pitchTargetRate, response);
            YawVelocity = Mathf.Lerp(YawVelocity, yawTargetRate, response);
            BankVelocity = Mathf.Lerp(BankVelocity, bankTargetRate, response);
            float nextPitch = Pitch + PitchVelocity * dt;
            Pitch = Mathf.Clamp(nextPitch, -settings.pitchLimit, settings.pitchLimit);
            if (Pitch != nextPitch) PitchVelocity = 0;
            Yaw = Mathf.Repeat(Yaw + YawVelocity * dt, 360);
            float nextBank = Bank + BankVelocity * dt;
            Bank = Mathf.Clamp(nextBank, -settings.manualBankLimit, settings.manualBankLimit);
            if (Bank != nextBank) BankVelocity = 0;
            if (pitchManual) DesiredPitch = Pitch;
            if (yawManual) DesiredYaw = Yaw;
            pitchWasManual = pitchManual;
            yawWasManual = yawManual;
            TargetSpeed = Mathf.Clamp(TargetSpeed + command.Throttle * settings.throttleChange * dt,
                settings.minimumSpeed, settings.maximumSpeed);
            Speed = Mathf.MoveTowards(Speed, TargetSpeed, (TargetSpeed >= Speed ? settings.acceleration : settings.deceleration) * dt);
        }
    }
}
