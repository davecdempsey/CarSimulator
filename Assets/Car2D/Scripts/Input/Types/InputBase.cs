using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InControl;

namespace Car2D
{
    public class CarActions: PlayerActionSet
    {
        public PlayerAction EBreak;
        public PlayerAction ShiftUp;
        public PlayerAction ShiftDown;

        public PlayerAction EngineUp;
        public PlayerAction EngineDown;
        public PlayerAction EngineLeft;
        public PlayerAction EngineRight;

        public PlayerTwoAxisAction Engine;

        public PlayerAction Left;
        public PlayerAction Right;
        public PlayerAction Up;
        public PlayerAction Down;

        public PlayerTwoAxisAction Move;

        // Use this for initialization
        public CarActions()
        {
            EBreak = CreatePlayerAction("E Break");
            ShiftUp = CreatePlayerAction("Shift Up");
            ShiftDown = CreatePlayerAction("Shift Down");

            EngineUp = CreatePlayerAction("Engine Up");
            EngineDown = CreatePlayerAction("Engine Down");
            EngineLeft = CreatePlayerAction("Engine Left");
            EngineRight = CreatePlayerAction("Engine Right");
            Engine = CreateTwoAxisPlayerAction(EngineLeft, EngineRight, EngineDown, EngineUp);

            Left = CreatePlayerAction("Move Left");
            Right = CreatePlayerAction("Move Right");
            Up = CreatePlayerAction("Move Up");
            Down = CreatePlayerAction("Move Down");
            Move = CreateTwoAxisPlayerAction(Left, Right, Down, Up);
        }
    }


    public class InputBase: ScriptableObject
    {
        public InputBuffer _inputBuffer;
        public InputBuffer inputBuffer {
            get {
                if (_inputBuffer == null) {
                    _inputBuffer = new InputBuffer();
                }
                return _inputBuffer;
            }
        }

        public virtual void UpdateFrom() {}
    }
}
