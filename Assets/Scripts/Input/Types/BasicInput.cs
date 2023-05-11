using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Car2D
{
    [CreateAssetMenu(menuName = "Car2D/Input/Basic")]
    public class BasicInput: InputBase
    {
        // Update is called once per frame
        public override void Update()
        {
            Vector2 axis = Vector2.zero;
            if(Input.GetKey(KeyCode.UpArrow)) {
                axis.y = 1;
            } else if(Input.GetKey(KeyCode.DownArrow)) {
                axis.y = -1;
            }

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
