using UnityEngine;

namespace Car2D
{
	public class Axle: MonoBehaviour
	{
		public Transform axle;

		public float DistanceToCG { get; set; }
		public float WeightRatio { get; set; }
		public float SlipAngle { get; set; }
		public float FrictionForce {
			get {
				return (TireLeft.FrictionForce + TireRight.FrictionForce) / 2f;
			}
		}
		public float AngularVelocity {
			get {
				return Mathf.Min(TireLeft.AngularVelocity + TireRight.AngularVelocity);
			}
		}
		public float Torque {
			get {
				return (TireLeft.Torque + TireRight.Torque) / 2f;
			}
		}

		public float turnDirection => TireLeft.turnDirection;

		[SerializeField]
		private Tire m_leftTire;
		public Tire TireLeft {
			get {
				return m_leftTire;
			}
			private set {
				m_leftTire = value;
			}
		}

		[SerializeField]
		private Tire m_rightTire;
		public Tire TireRight {
			get {
				return m_rightTire;
			}
			private set {
				m_rightTire = value;
			}
		}

		public void Init (Rigidbody2D rb, float wheelBase)
		{

			// Weight distribution on each axle and tire
			WeightRatio = DistanceToCG / wheelBase;

			// Calculate resting weight of each Tire
			float weight = rb.mass * (WeightRatio * -Physics2D.gravity.y);
			TireLeft.RestingWeight = weight;
			TireRight.RestingWeight = weight;
		}
	}
}
