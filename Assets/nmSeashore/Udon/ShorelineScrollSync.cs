
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace nmSeashore
{
	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class ShorelineScrollSync : UdonSharpBehaviour
	{
		[SerializeField] private Seashore seashore;

		private Transform[] children;
		private float[] x;
		private Vector2 distance;

		void Start()
		{
			BoxCollider bounds = GetComponent<BoxCollider>();
			distance.x = bounds.center.x - bounds.size.x * 0.5f;
			distance.y = bounds.size.x;

			unpackChildren();

			children = new Transform[transform.childCount];
			x = new float[children.Length];
			for(int i = 0; i < children.Length; i++)
			{
				children[i] = transform.GetChild(i);
				x[i] = children[i].transform.localPosition.x;
			}
		}

		private void Update()
		{
			for(int i = 0; i < children.Length; i++)
			{
				float position = Mathf.Repeat(x[i] - seashore.currentScrollPosition, distance.y) + distance.x;
				children[i].transform.localPosition = new Vector3(position, children[i].transform.localPosition.y, children[i].transform.localPosition.z);
			}
		}

		private void unpackChildren()
		{
			Transform[] transforms = transform.GetComponentsInChildren<Transform>();
			for(int i = 0; i < transforms.Length; i++)
			{
				if(transform == transforms[i]) { Debug.Log("parent!");continue; }

				transforms[i].SetParent(transform);
			}
		}
	}
}