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
            input.UpdateFrom();
            car.ConsumeInputBuffer(input.inputBuffer);
        }
    }
}
