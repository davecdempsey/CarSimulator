using UnityEngine;

namespace Car2D
{
	public class Tire: MonoBehaviour
	{
		[SerializeField]
		private Transform _transform;
		public new Transform transform {
			get {
				if(_transform == null) {
					_transform = GetComponent<Transform>();
				}
				return _transform;
			}
		}

		[SerializeField]
		private float m_restingweight;
		public float RestingWeight {
			get {
				return m_restingweight;
			}
			set {
				m_restingweight = value;
			}
		}

		[SerializeField]
		private float m_activeWeight;
		public float ActiveWeight {
			get {
				return m_activeWeight;
			}
			set {
				m_activeWeight = value;
			}
		}

		[SerializeField]
		private float m_grip;
		public float Grip {
			get {
				return m_grip;
			}
			set {
				m_grip = value;
			}
		}

		[SerializeField]
		private float m_frictionForce;
		public float FrictionForce {
			get {
				return m_frictionForce;
			}
			set {
				m_frictionForce = value;
			}
		}

		[SerializeField]
		private float m_angularVelocity;
		public float AngularVelocity {
			get {
				return m_angularVelocity;
			}
			set {
				m_angularVelocity = value;
			}
		}

		[SerializeField]
		private float m_torque;
		public float Torque {
			get {
				return m_torque;
			}
			set {
				m_torque = value;
			}
		}


		public float Radius = 0.5f;

		float TrailDuration = 5;
		bool TrailActive;

		[SerializeField]
		private GameObject skidmarkPrefab;

		[SerializeField]
		private GameObject skidmark;

		public void SetTrailActive (bool active)
		{
			if (active && !TrailActive) {
				// These should be pooled and re-used
				skidmark = Instantiate(skidmarkPrefab);

				skidmark.GetComponent<TrailRenderer>().time = TrailDuration;
				skidmark.GetComponent<TrailRenderer>().sortingOrder = 0;
				skidmark.transform.parent = this.transform;
				skidmark.transform.localPosition = Vector2.zero;

				//Fix issue where skidmarks draw at 0,0,0 at slow speeds
				skidmark.GetComponent<TrailRenderer>().Clear();
			} else if (!active && TrailActive) {
				skidmark.transform.parent = null;
				Destroy(skidmark, TrailDuration);
			}
			TrailActive = active;
		}
	}
}
