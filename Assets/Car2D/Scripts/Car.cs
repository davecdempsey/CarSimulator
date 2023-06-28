using UnityEngine;
using System.Collections.Generic;

namespace Car2D
{
	public class Car: MonoBehaviour
	{
		public bool showStats = false;

		[SerializeField]
		public Rigidbody2D Rigidbody2D;

		[SerializeField]
		private Axle AxleFront;

		[SerializeField]
		private Axle AxleRear;

		[SerializeField]
		private float FrontTurnDirection;

		[SerializeField]
		private Engine Engine;

		[SerializeField]
		private CarModel model;

		[SerializeField]
		private Transform CenterOfGravity;

		float CGHeight => model.CGHeight;

		float InertiaScale => model.InertiaScale;

		float BrakePower => model.BrakePower;

		float EBrakePower => model.BrakePower;

		float WeightTransfer => model.WeightTransfer;

		float MaxSteerAngle => model.MaxSteerAngle;

		float CornerStiffnessFront => model.CornerStiffnessFront;

		float CornerStiffnessRear => model.CornerStiffnessRear;

		float AirResistance => model.AirResistance;

		float RollingResistance => model.RollingResistance;

		float EBrakeGripRatioFront => model.EBrakeGripRatioFront;

		float TotalTireGripFront => model.TotalTireGripFront;

		float EBrakeGripRatioRear => model.EBrakeGripRatioRear;

		float TotalTireGripRear => model.TotalTireGripRear;

		float SteerSpeed => model.SteerSpeed;

		float SteerAdjustSpeed => model.SteerAdjustSpeed;

		float SpeedSteerCorrection => model.SpeedSteerCorrection;

		float SpeedTurningStability => model.SpeedTurningStability;

		float AxleDistanceCorrection => model.AxleDistanceCorrection;

		public float SpeedKilometersPerHour {
			get {
				return Rigidbody2D.velocity.magnitude * 18f / 5f;
			}
		}

		// Variables that get initialized via code
		[SerializeField]
		float Inertia = 1;
		[SerializeField]
		float WheelBase = 1;
		[SerializeField]
		float TrackWidth = 1;

		// Private vars
		[SerializeField]
		float HeadingAngle;
		[SerializeField]
		float AbsoluteVelocity;
		[SerializeField]
		float AngularVelocity;
		[SerializeField]
		float SteerDirection;
		[SerializeField]
		float SteerAngle;

		Vector2 Velocity;
		Vector2 Acceleration;
		Vector2 LocalVelocity;
		Vector2 LocalAcceleration;

		float Throttle;
		float Brake;
		float EBrake;

		[SerializeField]
		private InputBuffer _inputBuffer;
		private InputBuffer inputBuffer {
			get {
				if(_inputBuffer == null) {
					_inputBuffer = new InputBuffer();
				}
				return _inputBuffer;
			}
		}

		private string className => this.GetType().ToString();
		private string debugCategory => "#" + className + "#";

		void Awake ()
		{
			Rigidbody2D = GetComponent<Rigidbody2D>();
			Init();
		}

		void Init ()
		{
			Velocity = Vector2.zero;
			AbsoluteVelocity = 0;

			// Dimensions
			AxleFront.DistanceToCG = Vector2.Distance(CenterOfGravity.position, AxleFront.axle.position);
			AxleRear.DistanceToCG = Vector2.Distance(CenterOfGravity.position, AxleRear.axle.position);

			// Extend the calculations past actual car dimensions for better simulation
			AxleFront.DistanceToCG *= AxleDistanceCorrection;
			AxleRear.DistanceToCG *= AxleDistanceCorrection;

			WheelBase = AxleFront.DistanceToCG + AxleRear.DistanceToCG;
			Inertia = Rigidbody2D.mass * InertiaScale;

			// Set starting angle of car
			Rigidbody2D.rotation = transform.rotation.eulerAngles.z;
			HeadingAngle = (Rigidbody2D.rotation + 90) * Mathf.Deg2Rad;
		}

		void Start ()
		{
			AxleFront.Init(Rigidbody2D, WheelBase);
			AxleRear.Init(Rigidbody2D, WheelBase);

			TrackWidth = Vector2.Distance(AxleRear.TireLeft.transform.position, AxleRear.TireRight.transform.position);
		}		

		void Update ()
		{
			SetDirections();

			if (inputBuffer.trottlePressed && inputBuffer.reverseIsPressed) {
				Throttle = 0;
			} else if (inputBuffer.trottlePressed) { 
				Throttle = 1;
			} else if (inputBuffer.reverseIsPressed) {
				Throttle = -1;
			} else {
				Throttle = 0;
            }

			if(inputBuffer.shiftDownPressed) {
				Engine.ShiftDown();
			}

			if(inputBuffer.shiftUpPressed) {
				Engine.ShiftUp();
			}

			if(inputBuffer.eBrakePressed) {
				EBrake = 1;
			} else {
				EBrake = 0;
			}

			// Apply filters to our steer direction
			SteerDirection = SmoothSteering(inputBuffer.steerInput, inputBuffer.directionalSteering);
			SteerDirection = SpeedAdjustedSteering(SteerDirection);

            // Calculate the current angle the tires are pointing
            SteerAngle = SteerDirection * MaxSteerAngle;

            // Set front axle tires rotation
            AxleFront.TireRight.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * SteerAngle);
            AxleFront.TireLeft.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * SteerAngle);


			// Calculate weight center of four tires
			// This is just to draw that red dot over the car to indicate what tires have the most weight
			Vector2 pos = Vector2.zero;
			if (LocalAcceleration.magnitude > 1f) {

				float wfl = Mathf.Max(0, (AxleFront.TireLeft.ActiveWeight - AxleFront.TireLeft.RestingWeight));
				float wfr = Mathf.Max(0, (AxleFront.TireRight.ActiveWeight - AxleFront.TireRight.RestingWeight));
				float wrl = Mathf.Max(0, (AxleRear.TireLeft.ActiveWeight - AxleRear.TireLeft.RestingWeight));
				float wrr = Mathf.Max(0, (AxleRear.TireRight.ActiveWeight - AxleRear.TireRight.RestingWeight));

				pos = (AxleFront.TireLeft.transform.localPosition) * wfl +
					(AxleFront.TireRight.transform.localPosition) * wfr +
					(AxleRear.TireLeft.transform.localPosition) * wrl +
					(AxleRear.TireRight.transform.localPosition) * wrr;

				float weightTotal = wfl + wfr + wrl + wrr;

				if (weightTotal > 0) {
					pos /= weightTotal;
					pos.Normalize();
					pos.x = Mathf.Clamp(pos.x, -0.6f, 0.6f);
				} else {
					pos = Vector2.zero;
				}
			}

			// Update the "Center Of Gravity" dot to indicate the weight shift
			CenterOfGravity.localPosition = Vector2.Lerp(CenterOfGravity.localPosition, pos, 0.1f);

			// Skidmarks
			if (Mathf.Abs(LocalAcceleration.y) > 18 || EBrake == 1) {
				AxleRear.TireRight.SetTrailActive(true);
				AxleRear.TireLeft.SetTrailActive(true);
			} else {
				AxleRear.TireRight.SetTrailActive(false);
				AxleRear.TireLeft.SetTrailActive(false);
			}

			// Automatic transmission
			Engine.UpdateAutomaticTransmission(Rigidbody2D);
		}

		void FixedUpdate ()
		{
			// Update from rigidbody to retain collision responses
			Velocity = Rigidbody2D.velocity;
			HeadingAngle = (Rigidbody2D.rotation + 90) * Mathf.Deg2Rad;

			float sin = Mathf.Sin(HeadingAngle);
			float cos = Mathf.Cos(HeadingAngle);

			// Get local velocity
			LocalVelocity.x = cos * Velocity.x + sin * Velocity.y;
			LocalVelocity.y = cos * Velocity.y - sin * Velocity.x;

			// Weight transfer
			float transferX = WeightTransfer * LocalAcceleration.x * CGHeight / WheelBase;
			float transferY = WeightTransfer * LocalAcceleration.y * CGHeight / TrackWidth * 20;        //exagerate the weight transfer on the y-axis

			// Weight on each axle
			float weightFront = Rigidbody2D.mass * (AxleFront.WeightRatio * -Physics2D.gravity.y - transferX);
			float weightRear = Rigidbody2D.mass * (AxleRear.WeightRatio * -Physics2D.gravity.y + transferX);

			// Weight on each tire
			AxleFront.TireLeft.ActiveWeight = weightFront - transferY;
			AxleFront.TireRight.ActiveWeight = weightFront + transferY;
			AxleRear.TireLeft.ActiveWeight = weightRear - transferY;
			AxleRear.TireRight.ActiveWeight = weightRear + transferY;

			// Velocity of each tire
			AxleFront.TireLeft.AngularVelocity = AxleFront.DistanceToCG * AngularVelocity;
			AxleFront.TireRight.AngularVelocity = AxleFront.DistanceToCG * AngularVelocity;
			AxleRear.TireLeft.AngularVelocity = -AxleRear.DistanceToCG * AngularVelocity;
			AxleRear.TireRight.AngularVelocity = -AxleRear.DistanceToCG * AngularVelocity;

			// Slip angle
			AxleFront.SlipAngle = Mathf.Atan2(LocalVelocity.y + AxleFront.AngularVelocity, Mathf.Abs(LocalVelocity.x)) - Mathf.Sign(LocalVelocity.x) * SteerAngle;
			AxleRear.SlipAngle = Mathf.Atan2(LocalVelocity.y + AxleRear.AngularVelocity, Mathf.Abs(LocalVelocity.x));

			// Brake and Throttle power
			float activeBrake = Mathf.Min(Brake * BrakePower + EBrake * EBrakePower, BrakePower);
			float activeThrottle = (Throttle * Engine.GetTorque(Rigidbody2D)) * (Engine.GearRatio * Engine.EffectiveGearRatio);

			// Torque of each tire (rear wheel drive)
			AxleRear.TireLeft.Torque = activeThrottle / AxleRear.TireLeft.Radius;
			AxleRear.TireRight.Torque = activeThrottle / AxleRear.TireRight.Radius;

			// Grip and Friction of each tire
			AxleFront.TireLeft.Grip = TotalTireGripFront * (1.0f - EBrake * (1.0f - EBrakeGripRatioFront));
			AxleFront.TireRight.Grip = TotalTireGripFront * (1.0f - EBrake * (1.0f - EBrakeGripRatioFront));
			AxleRear.TireLeft.Grip = TotalTireGripRear * (1.0f - EBrake * (1.0f - EBrakeGripRatioRear));
			AxleRear.TireRight.Grip = TotalTireGripRear * (1.0f - EBrake * (1.0f - EBrakeGripRatioRear));

			AxleFront.TireLeft.FrictionForce = Mathf.Clamp(-CornerStiffnessFront * AxleFront.SlipAngle, -AxleFront.TireLeft.Grip, AxleFront.TireLeft.Grip) * AxleFront.TireLeft.ActiveWeight;
			AxleFront.TireRight.FrictionForce = Mathf.Clamp(-CornerStiffnessFront * AxleFront.SlipAngle, -AxleFront.TireRight.Grip, AxleFront.TireRight.Grip) * AxleFront.TireRight.ActiveWeight;
			AxleRear.TireLeft.FrictionForce = Mathf.Clamp(-CornerStiffnessRear * AxleRear.SlipAngle, -AxleRear.TireLeft.Grip, AxleRear.TireLeft.Grip) * AxleRear.TireLeft.ActiveWeight;
			AxleRear.TireRight.FrictionForce = Mathf.Clamp(-CornerStiffnessRear * AxleRear.SlipAngle, -AxleRear.TireRight.Grip, AxleRear.TireRight.Grip) * AxleRear.TireRight.ActiveWeight;

			// Forces
			float tractionForceX = AxleRear.Torque - activeBrake * Mathf.Sign(LocalVelocity.x);
			float tractionForceY = 0;

			float dragForceX = -RollingResistance * LocalVelocity.x - AirResistance * LocalVelocity.x * Mathf.Abs(LocalVelocity.x);
			float dragForceY = -RollingResistance * LocalVelocity.y - AirResistance * LocalVelocity.y * Mathf.Abs(LocalVelocity.y);

			float totalForceX = dragForceX + tractionForceX;
			float totalForceY = dragForceY + tractionForceY + Mathf.Cos(SteerAngle) * AxleFront.FrictionForce + AxleRear.FrictionForce;

			//adjust Y force so it levels out the car heading at high speeds
			if (AbsoluteVelocity > 10) {
				totalForceY *= (AbsoluteVelocity + 1) / (21f - SpeedTurningStability);
			}

			// If we are not pressing gas, add artificial drag - helps with simulation stability
			if (Throttle == 0) {
				Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.005f);
			}

			// Acceleration
			LocalAcceleration.x = totalForceX / Rigidbody2D.mass;
			LocalAcceleration.y = totalForceY / Rigidbody2D.mass;

			Acceleration.x = cos * LocalAcceleration.x - sin * LocalAcceleration.y;
			Acceleration.y = sin * LocalAcceleration.x + cos * LocalAcceleration.y;

			// Velocity and speed
			Velocity.x += Acceleration.x * Time.deltaTime;
			Velocity.y += Acceleration.y * Time.deltaTime;

			AbsoluteVelocity = Velocity.magnitude;

			// Angular torque of car
			float angularTorque = (AxleFront.FrictionForce * AxleFront.DistanceToCG) - (AxleRear.FrictionForce * AxleRear.DistanceToCG);

			// Car will drift away at low speeds
			if (AbsoluteVelocity < 0.5f && activeThrottle == 0) {
				LocalAcceleration = Vector2.zero;
				AbsoluteVelocity = 0;
				Velocity = Vector2.zero;
				angularTorque = 0;
				AngularVelocity = 0;
				Acceleration = Vector2.zero;
				Rigidbody2D.angularVelocity = 0;
			}

			var angularAcceleration = angularTorque / Inertia;

			// Update 
			AngularVelocity += angularAcceleration * Time.deltaTime;

			// Simulation likes to calculate high angular velocity at very low speeds - adjust for this
			if (AbsoluteVelocity < 1 && Mathf.Abs(SteerAngle) < 0.05f) {
				AngularVelocity = 0;
			} else if (SpeedKilometersPerHour < 0.75f) {
				AngularVelocity = 0;
			}

			HeadingAngle += AngularVelocity * Time.deltaTime;
			Rigidbody2D.velocity = Velocity;

			Rigidbody2D.MoveRotation(Mathf.Rad2Deg * HeadingAngle - 90);
		}

		private void SetDirections()
        {
			FrontTurnDirection = AxleFront.turnDirection;
		}

		float SmoothSteering (float steerInput, bool directionalSteering)
		{
			float steer = 0;

			if(Mathf.Abs(steerInput) > 0.001f) {
				if (directionalSteering) {
					float steerDiff = SteerDiff(steerInput);
					steer = Mathf.Clamp(SteerDirection + steerDiff * Time.deltaTime * SteerSpeed, -1.0f, 1.0f);
				} else {
					steer = Mathf.Clamp(SteerDirection + steerInput * Time.deltaTime * SteerSpeed, -1.0f, 1.0f);
				}
			} else {
				if (SteerDirection > 0) {
					steer = Mathf.Max(SteerDirection - Time.deltaTime * SteerAdjustSpeed, 0);
				} else if (SteerDirection < 0) {
					steer = Mathf.Min(SteerDirection + Time.deltaTime * SteerAdjustSpeed, 0);
				}
			}

			return steer;
		}

		float SteerDiff(float steerInput)
        {
			float delta = Mathf.DeltaAngle(steerInput * Mathf.Rad2Deg, FrontTurnDirection) * Mathf.Deg2Rad * -1.0f;

			if (delta > 0) {
				float testDelta = delta - (360.0f * Mathf.Deg2Rad);
				if (Mathf.Abs(delta) > Mathf.Abs(testDelta)) {
					return testDelta;
                }
            } else {
				float testDelta = delta + (360.0f * Mathf.Deg2Rad);
				if(Mathf.Abs(delta) > Mathf.Abs(testDelta)) {
					return testDelta;
				}
			}
			return delta;
		}

		float SpeedAdjustedSteering (float steerInput)
		{
			float activeVelocity = Mathf.Min(AbsoluteVelocity, 250.0f);
			float steer = steerInput * (1.0f - (activeVelocity / SpeedSteerCorrection));
			return steer;
		}

        void OnGUI()
        {
            if(showStats) {
                GUI.Label(new Rect(5, 5, 300, 20), "Speed: " + SpeedKilometersPerHour.ToString());
                GUI.Label(new Rect(5, 25, 300, 20), "RPM: " + Engine.GetRPM(Rigidbody2D).ToString());
                GUI.Label(new Rect(5, 45, 300, 20), "Gear: " + (Engine.CurrentGear + 1).ToString());
                GUI.Label(new Rect(5, 65, 300, 20), "LocalAcceleration: " + LocalAcceleration.ToString());
                GUI.Label(new Rect(5, 85, 300, 20), "Acceleration: " + Acceleration.ToString());
                GUI.Label(new Rect(5, 105, 300, 20), "LocalVelocity: " + LocalVelocity.ToString());
                GUI.Label(new Rect(5, 125, 300, 20), "Velocity: " + Velocity.ToString());
                GUI.Label(new Rect(5, 145, 300, 20), "SteerAngle: " + SteerAngle.ToString());
                GUI.Label(new Rect(5, 165, 300, 20), "Throttle: " + Throttle.ToString());
                GUI.Label(new Rect(5, 185, 300, 20), "Brake: " + Brake.ToString());

                GUI.Label(new Rect(5, 205, 300, 20), "HeadingAngle: " + HeadingAngle.ToString());
                GUI.Label(new Rect(5, 225, 300, 20), "AngularVelocity: " + AngularVelocity.ToString());

                GUI.Label(new Rect(5, 245, 300, 20), "TireFL Weight: " + AxleFront.TireLeft.ActiveWeight.ToString());
                GUI.Label(new Rect(5, 265, 300, 20), "TireFR Weight: " + AxleFront.TireRight.ActiveWeight.ToString());
                GUI.Label(new Rect(5, 285, 300, 20), "TireRL Weight: " + AxleRear.TireLeft.ActiveWeight.ToString());
                GUI.Label(new Rect(5, 305, 300, 20), "TireRR Weight: " + AxleRear.TireRight.ActiveWeight.ToString());

                GUI.Label(new Rect(5, 325, 300, 20), "TireFL Friction: " + AxleFront.TireLeft.FrictionForce.ToString());
                GUI.Label(new Rect(5, 345, 300, 20), "TireFR Friction: " + AxleFront.TireRight.FrictionForce.ToString());
                GUI.Label(new Rect(5, 365, 300, 20), "TireRL Friction: " + AxleRear.TireLeft.FrictionForce.ToString());
                GUI.Label(new Rect(5, 385, 300, 20), "TireRR Friction: " + AxleRear.TireRight.FrictionForce.ToString());

                GUI.Label(new Rect(5, 405, 300, 20), "TireFL Grip: " + AxleFront.TireLeft.Grip.ToString());
                GUI.Label(new Rect(5, 425, 300, 20), "TireFR Grip: " + AxleFront.TireRight.Grip.ToString());
                GUI.Label(new Rect(5, 445, 300, 20), "TireRL Grip: " + AxleRear.TireLeft.Grip.ToString());
                GUI.Label(new Rect(5, 465, 300, 20), "TireRR Grip: " + AxleRear.TireRight.Grip.ToString());

                GUI.Label(new Rect(5, 485, 300, 20), "AxleF SlipAngle: " + AxleFront.SlipAngle.ToString());
                GUI.Label(new Rect(5, 505, 300, 20), "AxleR SlipAngle: " + AxleRear.SlipAngle.ToString());

                GUI.Label(new Rect(5, 525, 300, 20), "AxleF Torque: " + AxleFront.Torque.ToString());
                GUI.Label(new Rect(5, 545, 300, 20), "AxleR Torque: " + AxleRear.Torque.ToString());
            }
        }

        private void OnDrawGizmos()
        {
			if (inputBuffer.rawInput.magnitude == 0) {
				return;
            }
			Gizmos.DrawRay(transform.localPosition, inputBuffer.rawInput);
		}

		public void ConsumeInputBuffer(InputBuffer inputBuffer)
        {
			this.inputBuffer.UpdateWith(inputBuffer);
        }
	}
}
