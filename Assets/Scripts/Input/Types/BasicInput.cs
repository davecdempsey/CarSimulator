using UnityEngine;
using InControl;

namespace Car2D
{
    public class BasicCarActions: CarActions
    {
        public static BasicCarActions CreateHaloBindings()
        {
            var actions = new BasicCarActions();

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

    [CreateAssetMenu(menuName = "Car2D/Input/Basic")]
    public class BasicInput: InputBase
    {
        private BasicCarActions _basicCarActions;
        public BasicCarActions basicCarActions {
            get {
                if(_basicCarActions == null) {
                    _basicCarActions = BasicCarActions.CreateHaloBindings();
                }
                return _basicCarActions;
            }
        }

        // Update is called once per frame
        public override void UpdateFrom()
        {
            UpdateFromInControl();
        }

        private void UpdateFromInControl()
        {
            inputBuffer.directionalSteering = false;
            inputBuffer.reverseIsPressed = basicCarActions.Engine.Y < 0.0f;
            inputBuffer.trottlePressed = basicCarActions.Engine.Y > 0.0f;

            Vector2 axis = basicCarActions.Move;

            if(axis.x > 0) {
                axis.x = -1;
            } else if(axis.x < 0) {
                axis.x = 1;
            }

            inputBuffer.rawInput = basicCarActions.Move;

            inputBuffer.steerInput = axis.x;

            inputBuffer.eBrakePressed = basicCarActions.EBreak.WasPressed;
            inputBuffer.shiftUpPressed = basicCarActions.ShiftUp.WasPressed;
            inputBuffer.shiftDownPressed = basicCarActions.ShiftDown.WasPressed;
        }

        public void UpdateFromUnityInput()
        {
            inputBuffer.reverseIsPressed = Input.GetKey(KeyCode.DownArrow);
            inputBuffer.trottlePressed = Input.GetKey(KeyCode.UpArrow);

            Vector2 axis = Vector2.zero;
            if(Input.GetKey(KeyCode.LeftArrow)) {
                axis.x = 1;
            } else if(Input.GetKey(KeyCode.RightArrow)) {
                axis.x = -1;
            }
            inputBuffer.input = axis;

            inputBuffer.eBrakePressed = Input.GetKey(KeyCode.Space);
            inputBuffer.shiftUpPressed = Input.GetKeyDown(KeyCode.A);
            inputBuffer.shiftDownPressed = Input.GetKeyDown(KeyCode.Z);
        }
    }
}
