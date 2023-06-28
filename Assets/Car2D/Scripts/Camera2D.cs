﻿using UnityEngine;
using System.Collections;

public class Camera2D: MonoBehaviour
{
	public new Transform transform;

	public Transform Area;

	public Transform target;


	Vector3 CurrentPosition;
	Vector3 CurrentScale;

	public void Set()
	{
		float height = Area.localScale.y * 100;
		float width = Area.localScale.x * 100;

		float w = Screen.width / width;
		float h = Screen.height / height;

		float ratio = w / h;
		float size = (height / 2) / 100f;

		if (w < h)
			size /= ratio;

		Camera.main.orthographicSize = size;

		Vector2 position = Area.transform.position;

		Vector3 camPosition = position;
		Vector3 point = Camera.main.WorldToViewportPoint(camPosition);
		Vector3 delta = camPosition - Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, point.z));
		Vector3 destination = transform.position + delta;

		transform.position = destination;
	}

    private void Update ()
    {
		transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
	}

	public void LateUpdate()
	{
		if (CurrentPosition != Area.transform.position || CurrentScale != Area.transform.localScale) {
			CurrentPosition = Area.transform.position;
			CurrentScale = Area.transform.localScale;
			Set ();	
		}

	}
}