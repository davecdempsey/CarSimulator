using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InControl;
using System.Reflection;

namespace Car2D
{
    public class HaloCarActions: CarActions
    {
        public static HaloCarActions CreateHaloBindings()
        {
            var actions = new HaloCarActions();

            // EBreak
            actions.EBreak.AddDefaultBinding(Key.E);
            actions.EBreak.AddDefaultBinding(InputControlType.Action2);

            // ShiftUp
            actions.ShiftUp.AddDefaultBinding(Key.UpArrow);
            actions.ShiftUp.AddDefaultBinding(InputControlType.RightBumper);

            // ShiftUp
            actions.ShiftDown.AddDefaultBinding(Key.DownArrow);
            actions.ShiftDown.AddDefaultBinding(InputControlType.LeftBumper);

            // WASD
            actions.Up.AddDefaultBinding(Key.W);
            actions.Down.AddDefaultBinding(Key.S);
            actions.Left.AddDefaultBinding(Key.A);
            actions.Right.AddDefaultBinding(Key.D);

            // RIGHT JOYSTICK
            actions.Left.AddDefaultBinding(InputControlType.RightStickLeft);
            actions.Right.AddDefaultBinding(InputControlType.RightStickRight);
            actions.Up.AddDefaultBinding(InputControlType.RightStickUp);
            actions.Down.AddDefaultBinding(InputControlType.RightStickDown);

            // DPAD
            actions.Left.AddDefaultBinding(InputControlType.DPadLeft);
            actions.Right.AddDefaultBinding(InputControlType.DPadRight);
            actions.Up.AddDefaultBinding(InputControlType.DPadUp);
            actions.Down.AddDefaultBinding(InputControlType.DPadDown);


            // LEFT JOYSTICK
            actions.EngineUp.AddDefaultBinding(InputControlType.LeftStickUp);
            actions.EngineDown.AddDefaultBinding(InputControlType.LeftStickDown);
            actions.EngineLeft.AddDefaultBinding(InputControlType.LeftStickLeft);
            actions.EngineRight.AddDefaultBinding(InputControlType.LeftStickRight);

            return actions;
        }
    }

    [CreateAssetMenu(menuName = "Car2D/Input/Halo")]
    public class HaloInput: InputBase
    {
        [SerializeField]
        private Vector2 direction;

        [SerializeField]
        private float radianDirection;

        private HaloCarActions _haloCarActions;
        public HaloCarActions haloCarActions {
            get {
                if(_haloCarActions == null) {
                    _haloCarActions = HaloCarActions.CreateHaloBindings();
                }
                return _haloCarActions;
            }
        }

        private string className => this.GetType().ToString();
        private string debugCategory => "#" + className + "#";

        public override void UpdateFrom()
        {
            UpdateFromInControl();
        }

        public void UpdateFromInControl()
        {
            inputBuffer.directionalSteering = true;
            inputBuffer.reverseIsPressed = haloCarActions.Engine.Y < 0.0f;
            inputBuffer.trottlePressed = haloCarActions.Engine.Y > 0.0f;

            Vector2 axis = haloCarActions.Move;

            direction = axis;

            inputBuffer.rawInput = haloCarActions.Move;


            if(direction.magnitude > 0) {
                radianDirection = Vector3.Angle(direction, Vector3.up);

                if (direction.x > 0) {
                    radianDirection = radianDirection * -1.0f;
                }
            } else {
                radianDirection = 0.0f;
            }

            inputBuffer.steerInput = radianDirection * Mathf.Deg2Rad;

            inputBuffer.eBrakePressed = haloCarActions.EBreak.WasPressed;
            inputBuffer.shiftUpPressed = haloCarActions.ShiftUp.WasPressed;
            inputBuffer.shiftDownPressed = haloCarActions.ShiftDown.WasPressed;
        }

        public void UpdateFromUnityInput()
        {
            inputBuffer.reverseIsPressed = Input.GetKey(KeyCode.LeftShift);
            inputBuffer.trottlePressed = Input.GetKey(KeyCode.Space);

            Vector2 axis = Vector2.zero;
            if(Input.GetKey(KeyCode.A)) {
                axis.x = 1;
            } else if(Input.GetKey(KeyCode.D)) {
                axis.x = -1;
            }

            if(Input.GetKey(KeyCode.W)) {
                axis.y = 1;
            } else if(Input.GetKey(KeyCode.S)) {
                axis.y = -1;
            }

            direction = axis - Vector2.zero;

            inputBuffer.eBrakePressed = Input.GetKey(KeyCode.E);
            inputBuffer.shiftUpPressed = Input.GetKeyDown(KeyCode.UpArrow);
            inputBuffer.shiftDownPressed = Input.GetKeyDown(KeyCode.DownArrow);
        }
    }
}
