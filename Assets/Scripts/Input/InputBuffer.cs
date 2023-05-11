using UnityEngine;

namespace Car2D
{
    [System.Serializable]
    public class InputBuffer
    {
        public Vector2 input = Vector2.zero;

        public bool shiftUpPressed = false;
        public bool shiftDownPressed = false;
        public bool eBrakePressed = false;
    }
}

