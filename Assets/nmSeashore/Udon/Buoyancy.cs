
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace nmSeashore
{
	public enum VolumeGeometryType
	{
		Sphere,
		Torus
	}
	
	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class Buoyancy : UdonSharpBehaviour
	{
		public Seashore seashore = null;
		public float radius = 0.5f;
		public float innerRadius = 0f;	// トーラスのみ使用
		public float mass = 20.0f;
		public float airDensity = 1.2f;	// 概ね外気温21℃程度の地表
		public float waterDensity = 1025f;	// 海水は真水より若干高い。水温29℃のプールであれば約996
		public VolumeGeometryType geometry = VolumeGeometryType.Sphere;
		public bool applyRotation = false;
		
		protected float depth;
		protected Vector3 velocity;
		protected Vector3 acceleration;
		private Quaternion destRotation;
		
		private float h;
		private Vector3 waterNormal;
		private float timer;
		private bool lazy;

		private bool floated;

		private bool _active;
		public bool active
		{
			get => _active;
			set
			{
				_active = value;

				velocity = Vector3.zero;
				acceleration = Vector3.zero;
			}
		}
		
		private Rigidbody _rigidbody;
		public new Rigidbody rigidbody
		{
			get => _rigidbody;
			set
			{
				_rigidbody = value;
				
				if(value != null)
				{
					rigidbody.mass = mass;
					rigidbody.useGravity = false;	// 重力影響を自前で計算するので常にオフ
				}
			}
		}

		protected bool substepping;

		protected virtual void Start()
		{
			floated = false;
			_active = true;
			substepping = false;
			timer = Random.value;
			lazy = false;
			
			// 子クラスで既に設定されている場合は無視
			if(rigidbody == null)
			{
				rigidbody = GetComponent<Rigidbody>();
			}
		}

		private void updateWaterSurface(Vector3 pos, float dt)
		{
			if(lazy == true)
			{
				timer += dt;
			}
			else
			{
				// vertexHeightの取得は重い処理のため距離LOD的な間引きを行う
				float step = 10.0f + 50.0f / (1.0f + Vector3.SqrMagnitude(pos - Networking.LocalPlayer.GetPosition()) * 0.005f);
				timer += dt * step;
			}

			if(timer >= 1.0f)
			{
				timer = Mathf.Repeat(timer, 1.0f);
				
				h = seashore.vertexHeight(pos);

				// 直下に水面がない場合は再び水面上に移動するまで判定処理の大幅な間引きを行う
				// 浅い水深では即座に浮力が必要になることはないので復帰はざっくりでいい
				lazy = h == float.MinValue;

				if(applyRotation == true && floated == true)
				{
					// 水面の法線を取得
					float delta = 0.01f;
					float hdx = seashore.vertexHeight(new Vector3(pos.x + delta, pos.y,  pos.z)) - h;
					if(hdx == float.MinValue) { waterNormal = Vector3.up; return; }
					float hdy = seashore.vertexHeight(new Vector3(pos.x, pos.y, pos.z + delta)) - h;
					if(hdy == float.MinValue) { waterNormal = Vector3.up; return; }
					Vector3 pdx = new Vector3(pos.x + delta, pos.y + hdx, pos.z);
					Vector3 pdy = new Vector3(pos.x, pos.y + hdy, pos.z + delta);
					Vector3 tangent = (pdx - pos).normalized;
					Vector3 binormal = (pdy - pos).normalized;
					waterNormal = Vector3.Cross(binormal, tangent).normalized;
				}
			}
		}

		protected virtual void Update()
		{
			if(mass == 0f) { return; }
			if(radius == 0f) { return; }	// 半径0だと0除算になる。エラーを回避しても浮力を受ける体積が存在しないため自由落下として計算される
			
			// インスペクタ上から手動登録しなくてもいいようにするための処理
			if(seashore == null)
			{
				string name = Networking.LocalPlayer.GetPlayerTag("nmSeashoreReferenceName");
				if(name == "" || name == null) { return; }	// 初期状態のGetPlayerTagはClientSimでは空文字列が返ってきてVRC上ではnullが返ってくる

				GameObject obj = GameObject.Find(name);
				if(obj == null) { return; }

				seashore = obj.GetComponent<Seashore>();
				if(seashore == null) { return; }
			}
			
			if(substepping == true)
			{
				int step = Mathf.CeilToInt(Time.deltaTime * 100.0f);
				float dt = Time.deltaTime / step;
				for(int i = 0; i < step; i++)
				{
					Tick(dt);
				}
			}
			else
			{
				float dt = Mathf.Min(Time.deltaTime, 0.1f) * 0.2f;
				Tick(Time.deltaTime);
			}
		}
		
		private void Tick(float dt)
		{
			Vector3 pos;
			Quaternion rot;
			if(rigidbody == null)
			{
				pos = transform.position;
				rot = transform.rotation;
			}
			else
			{
				pos = rigidbody.position;
				rot = rigidbody.rotation;
			}
			
			// 中心点の水位のみを水深として扱う
			updateWaterSurface(pos, dt);
			depth = h - pos.y;
			
			// 形状に応じて沈んでる部分の体積と抵抗を受ける正面断面積を出す
			float volume;
			float area;
			switch(geometry)
			{
			default:
				depth = Mathf.Clamp(depth + radius, 0f, radius * 2.0f);
				volume = Mathf.PI * depth * depth * (radius - depth / 3.0f);	// 球冠体積
				area = Mathf.PI * radius * radius;
				break;
			case VolumeGeometryType.Torus:
				float minorRadius = (radius - innerRadius) * 0.5f;	// トーラスの断面半径
				depth = Mathf.Clamp(depth + minorRadius, 0f, minorRadius * 2.0f);	// トーラスの沈み具合を考えるのが難しいので穴の中心に太さに応じた球の判定を置いている

				// 向きにかかわらず水平なトーラスが沈んだ場合の体積として計算する
				float theta = 2.0f * Mathf.Acos((minorRadius - depth) / minorRadius);
				float sideArea = 0.5f * minorRadius * minorRadius * (theta - Mathf.Sin(theta));
				float length = minorRadius * Mathf.PI * 2.0f;
				volume = sideArea * length;
				area = minorRadius * length * 2.0f;
				break;
			}

			if(active == false) { return; }
			
			// 浮力はY軸についてのみ処理
			float buoyancy = waterDensity * -Physics.gravity.y * volume;	// アルキメデスの原理
			float density = Mathf.Lerp(airDensity, waterDensity, depth / (radius * 2.0f));

			// 着水時の速度減衰
			if(floated == false && depth > 0f)
			{
				if(rigidbody != null && rigidbody.isKinematic == false)
				{
					rigidbody.drag = 1.5f;
				}
				floated = true;
			}
			else if(floated == true && depth == 0)
			{
				if(rigidbody != null)
				{
					rigidbody.drag = 0f;
				}
				floated = false;
			}
			
			if(applyRotation == true && floated == true)
			{
				Vector3 normal = Vector3.Dot(rot * Vector3.up, waterNormal) > 0f ? waterNormal : -waterNormal;	// 表裏対応（本当は任意の軸に対してもやりたいがよくわからなかった）
				Quaternion dest = Quaternion.FromToRotation(rot * Vector3.up, normal);
				destRotation = dest * rot;

				if(rigidbody != null)
				{
					rigidbody.angularVelocity = Vector3.zero;
				}
			}
			
			if(rigidbody == null || rigidbody.isKinematic == true)
			{
				float drag = density * area * Mathf.Abs(velocity.y) * velocity.y * -0.5f * 0.47f;
				acceleration.y = Mathf.Clamp((mass * Physics.gravity.y + buoyancy + drag) / mass, -100.0f, 100.0f);	// massが極端に小さいと値が暴走しやすいため最大速度を制限

				velocity += acceleration * dt;

				if(floated == true)
				{
					velocity *= Mathf.Pow(0.97f, dt * 100.0f);
					if(applyRotation == true)
					{
						transform.rotation = Quaternion.Slerp(transform.rotation, destRotation, 5.0f * dt);
					}
				}

				transform.position += velocity * dt;
			}
			else
			{
				float drag = density * area * Mathf.Abs(rigidbody.velocity.y) * rigidbody.velocity.y * -0.5f * 0.47f;
				acceleration.y = Mathf.Clamp(mass * Physics.gravity.y + buoyancy + drag, -100.0f * mass, 100.0f * mass);
			}
		}

		private void FixedUpdate()
		{
			if(rigidbody == null) { return; }
			if(rigidbody.isKinematic == true) { return; }

			rigidbody.AddForce(acceleration, ForceMode.Force);

			if(applyRotation == true && floated == true)
			{
				rigidbody.MoveRotation(Quaternion.Slerp(rigidbody.rotation, destRotation, 5.0f * Time.fixedDeltaTime));
			}
		}
	}
}
