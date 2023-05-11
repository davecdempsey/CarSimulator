using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Car2D
{
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

        public virtual void Update() {}
    }
}
