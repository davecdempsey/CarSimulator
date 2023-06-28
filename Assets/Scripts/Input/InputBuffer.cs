using UnityEngine;

namespace Car2D
{
    [System.Serializable]
    public class InputBuffer
    {
        public Vector2 input = Vector2.zero;
        public Vector2 rawInput = Vector2.zero;
        public float steerInput = 0.0f;
        public bool directionalSteering = true;

        public bool trottlePressed = false;
        public bool reverseIsPressed = false;
        public bool shiftUpPressed = false;
        public bool shiftDownPressed = false;
        public bool eBrakePressed = false;

        public void UpdateWith(InputBuffer inputBuffer)
        {
            input = inputBuffer.input;
            rawInput = inputBuffer.rawInput;
            steerInput = inputBuffer.steerInput;
            directionalSteering = inputBuffer.directionalSteering;

            trottlePressed = inputBuffer.trottlePressed;
            reverseIsPressed = inputBuffer.reverseIsPressed;
            shiftUpPressed = inputBuffer.shiftUpPressed;
            shiftDownPressed = inputBuffer.shiftDownPressed;
            eBrakePressed = inputBuffer.eBrakePressed;
        }
    }
}

