using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Car2D
{
    [CreateAssetMenu(menuName = "Car2D/Car Model")]
    public class CarModel: ScriptableObject
    {
		[Range(0f, 1f)]
		public float CGHeight = 0.55f;

		[Range(0f, 2f)]
		public float InertiaScale = 1f;

		public float BrakePower = 12000;

		public float EBrakePower = 5000;

		[SerializeField]
		[Range(0f, 1f)]
		public float WeightTransfer = 0.35f;

		[Range(0f, 1f)]
		public float MaxSteerAngle = 0.75f;

		[Range(0f, 20f)]
		public float CornerStiffnessFront = 5.0f;

		[Range(0f, 20f)]
		public float CornerStiffnessRear = 5.2f;

		[Range(0f, 20f)]
		public float AirResistance = 2.5f;

		[Range(0f, 20f)]
		public float RollingResistance = 8.0f;

		[Range(0f, 1f)]
		public float EBrakeGripRatioFront = 0.9f;

		[Range(0f, 5f)]
		public float TotalTireGripFront = 2.5f;

		[Range(0f, 1f)]
		public float EBrakeGripRatioRear = 0.4f;

		[SerializeField]
		[Range(0f, 5f)]
		public float TotalTireGripRear = 2.5f;

		[Range(0f, 5f)]
		public float SteerSpeed = 2.5f;

		[Range(0f, 5f)]
		public float SteerAdjustSpeed = 1f;

		[Range(0f, 1000f)]
		public float SpeedSteerCorrection = 300f;

		[Range(0f, 20f)]
		public float SpeedTurningStability = 10f;

		[Range(0f, 10f)]
		public float AxleDistanceCorrection = 2f;
	}
}

