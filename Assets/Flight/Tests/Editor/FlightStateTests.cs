using NUnit.Framework;
using UnityEngine;

namespace FlappyFlight.Tests
{
    public sealed class FlightStateTests
    {
        private FlightSettings settings;
        private FlightState state;
        [SetUp] public void Setup()
        {
            settings = ScriptableObject.CreateInstance<FlightSettings>();
            state = new FlightState();
            state.Reset(Quaternion.identity, settings);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(settings); }
        private void Run(FlightCommand command, float seconds, float dt = .02f)
        { for (int i = 0; i < Mathf.RoundToInt(seconds / dt); i++) state.Step(command, settings, dt); }

        [Test] public void MouseTargetIsIndependentAndBodyTurnsAtBoundedRates()
        {
            Quaternion before = state.Rotation;
            state.Look(new Vector2(750, 250), settings);
            Assert.That(Quaternion.Angle(before, state.Rotation), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(before, state.AimRotation), Is.GreaterThan(60));
            state.Step(default, settings, .02f);
            Assert.That(Mathf.Abs(state.YawVelocity), Is.LessThanOrEqualTo(settings.yawRate));
            Assert.That(Mathf.Abs(state.PitchVelocity), Is.LessThanOrEqualTo(settings.pitchRate));
            Assert.That(Quaternion.Angle(before, state.Rotation), Is.LessThan(4));
            Run(default, 6);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(state.Yaw, state.DesiredYaw)), Is.LessThan(.1f));
            Assert.That(Mathf.Abs(state.Pitch - state.DesiredPitch), Is.LessThan(.1f));
        }

        [TestCase(1f)] [TestCase(-1f)]
        public void ManualPitchWinsAndReleaseDoesNotReturnToOldMouseTarget(float sign)
        {
            state.Look(new Vector2(0, 400 * sign), settings);
            Run(new FlightCommand { Pitch = sign }, .7f);
            Assert.That(state.Pitch * sign, Is.GreaterThan(15));
            float released = state.Pitch;
            state.Step(default, settings, .02f);
            Assert.That(Mathf.Abs(state.Pitch - released), Is.LessThan(settings.pitchRate * .02f + .01f));
            Run(default, 5);
            Assert.That(state.Pitch * sign, Is.GreaterThan(15));
            Assert.That(Mathf.Abs(state.PitchVelocity), Is.LessThan(.2f));
        }

        [TestCase(1f)] [TestCase(-1f)]
        public void ManualYawOverridesOppositeAimAndReleaseKeepsNewHeading(float sign)
        {
            state.Look(new Vector2(-600 * sign, 0), settings);
            Run(new FlightCommand { Yaw = sign }, .8f);
            float heading = Mathf.DeltaAngle(0, state.Yaw);
            Assert.That(heading * sign, Is.GreaterThan(25));
            Run(default, 5);
            Assert.That(Mathf.DeltaAngle(0, state.Yaw) * sign, Is.GreaterThan(25));
            Assert.That(Mathf.Abs(state.YawVelocity), Is.LessThan(.2f));
        }

        [Test] public void RollBanksAndTurnsThenStabilizesWithoutPersistentOscillation()
        {
            Run(new FlightCommand { Roll = 1 }, .7f);
            Assert.That(state.Bank, Is.GreaterThan(40));
            Assert.That(state.Yaw, Is.GreaterThan(5));
            Run(default, 6);
            Assert.That(Mathf.Abs(state.Bank), Is.LessThan(.2f));
            Assert.That(Mathf.Abs(state.BankVelocity), Is.LessThan(.2f));
        }

        [Test] public void FreeLookDoesNotAcceptMouseFlightInputAndKeyboardRemainsEffective()
        {
            state.Look(new Vector2(120, 40), settings);
            Quaternion target = state.AimRotation;
            state.SetFreeLook(true);
            state.Look(new Vector2(1200, 1200), settings);
            Assert.That(Quaternion.Angle(target, state.AimRotation), Is.LessThan(.001f));
            Run(new FlightCommand { Pitch = -.7f, Roll = .3f, Yaw = .6f, Throttle = 1 }, .6f);
            Assert.That(state.Pitch, Is.LessThan(-10));
            Assert.That(state.Bank, Is.GreaterThan(5));
            Assert.That(state.TargetSpeed, Is.GreaterThan(settings.cruiseSpeed));
            Quaternion beforeRelease = state.Rotation;
            state.SetFreeLook(false);
            Assert.That(Quaternion.Angle(beforeRelease, state.Rotation), Is.LessThan(.001f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(state.Yaw, state.DesiredYaw)), Is.LessThan(.001f));
            Assert.That(state.DesiredPitch, Is.EqualTo(state.Pitch).Within(.001f));
        }

        [Test] public void SpeedNeverStopsAndAcceleratesProgressively()
        {
            state.Step(new FlightCommand { Throttle = 1 }, settings, .02f);
            Assert.That(state.Speed, Is.GreaterThan(settings.cruiseSpeed).And.LessThan(settings.cruiseSpeed + .1f));
            Run(new FlightCommand { Throttle = 1 }, 10);
            Assert.That(state.Speed, Is.EqualTo(settings.maximumSpeed).Within(.001f));
            Run(new FlightCommand { Throttle = -1 }, 20);
            Assert.That(state.Speed, Is.EqualTo(settings.minimumSpeed).Within(.001f));
            Run(default, 4);
            Assert.That(state.Speed, Is.GreaterThan(0));
        }

        [Test] public void ResetClearsEveryControlStateAndRestoresAirborneCruise()
        {
            Run(new FlightCommand { Pitch = 1, Roll = 1, Yaw = 1, Throttle = 1 }, 1);
            state.SetFreeLook(true);
            state.Reset(Quaternion.Euler(-10, 45, 0), settings);
            Assert.That(state.FreeLook, Is.False);
            Assert.That(state.Pitch, Is.EqualTo(-10).Within(.01f));
            Assert.That(state.Yaw, Is.EqualTo(45).Within(.01f));
            Assert.That(state.BankVelocity + state.YawVelocity + state.PitchVelocity, Is.EqualTo(0));
            Assert.That(state.Speed, Is.EqualTo(settings.cruiseSpeed));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void SimulatedUpdateRatesStayCloseToHighRateReference(int hz)
        {
            FlightState reference = Scenario(1f / 720);
            FlightState actual = Scenario(1f / hz);
            Assert.That(Quaternion.Angle(reference.Rotation, actual.Rotation), Is.LessThan(2));
            Assert.That(Mathf.Abs(reference.Speed - actual.Speed), Is.LessThan(.1f));
            Assert.That(actual.Speed, Is.GreaterThan(0));
        }
        private FlightState Scenario(float dt)
        {
            var result = new FlightState(); result.Reset(Quaternion.identity, settings);
            for (int frame = 0; frame < Mathf.RoundToInt(8 / dt); frame++)
            {
                float time = frame * dt;
                result.Look(new Vector2(time < 2 ? 80 * dt : 0, 0), settings);
                var command = new FlightCommand
                { Pitch = time >= 2 && time < 3 ? -.4f : 0, Yaw = time >= 3 && time < 4 ? .3f : 0,
                  Roll = time >= 4 && time < 5 ? -.3f : 0, Throttle = time < 1 ? 1 : time >= 6 && time < 7 ? -1 : 0 };
                result.Step(command, settings, dt);
            }
            return result;
        }
    }
}
