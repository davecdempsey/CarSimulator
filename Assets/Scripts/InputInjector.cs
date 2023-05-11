using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Car2D
{
    public class InputInjector: MonoBehaviour
    {
        [SerializeField]
        private Car car;

        [SerializeField]
        private InputBase input;

        // Update is called once per frame
        void Update()
        {
            input.Update();
            car.InputMovement(input.inputBuffer.input);
            car.PressEBreak(input.inputBuffer.eBrakePressed);
            car.PressShiftUp(input.inputBuffer.shiftUpPressed);
            car.PressShiftDown(input.inputBuffer.shiftDownPressed);
        }
    }
}
