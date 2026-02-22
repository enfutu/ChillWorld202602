
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.SDK3.Components;

namespace nmSeashore
{
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class WearableBuoyancy : Buoyancy
	{
		private const string TAG_NAME = "WearableBuoyancyEquipped";
		private Transform target;
		private Renderer targetRenderer;
		private Rigidbody targetRigidbody;
		private Collider targetCollider;

		private VRCPlayerApi localPlayer;
		private VRCPickup pickup;

		private Vector2 inputMove;
		private Vector3 playerVelocity;
		private bool swimMode;
		private float retriggerBlockTime;
		
		public override void InputMoveHorizontal(float value, UdonInputEventArgs args) => inputMove.x = value;
		public override void InputMoveVertical(float value, UdonInputEventArgs args) => inputMove.y = value;

		[UdonSynced]
		private float equipHeight;
		
		private string equipText
		{
			get
			{
				switch(VRCPlayerApi.GetCurrentLanguage())
				{
				case "ja" : return "装着する";
				default : return "Equip";
				}
			}
		}

		public override void OnLanguageChanged(string language)
		{
			switch(language)
			{
			case "ja" : InteractionText = "外す"; break;
			default : InteractionText = "Remove"; break;
			}
		}

		// 誰かが装備しているか
		[UdonSynced]
		private bool _equipped;
		private bool equipped
		{
			get => _equipped;
			set
			{
				_equipped = value;

				if(Networking.GetOwner(gameObject) == localPlayer)
				{
					substepping = value;

					if(value == true)
					{
						localPlayer.SetPlayerTag(TAG_NAME, name);
					}
					else
					{
						localPlayer.SetPlayerTag(TAG_NAME, "");
					}
				}

				updateEquipmentState();
				RequestSerialization();
			}
		}

		public override void OnDeserialization() => updateEquipmentState();
		private void updateEquipmentState()
		{
			if(guide == true)
			{
				renderer.enabled = true;
			}
			else
			{
				renderer.enabled = equipped;
			}
				
			targetVisibility(!equipped);
				
			if(equipped == true)
			{
				transform.rotation = Quaternion.identity;
				meshFilter.sharedMesh = targetMesh;
				renderer.materials = targetMaterials;
				rigidbody = null;

				// 外す操作ができるのは自分のみ
				trigger.enabled = Networking.GetOwner(gameObject) == localPlayer;
			}
			else
			{
				meshFilter.sharedMesh = null;	// エディタ上でのみ、Interactしたメッシュを即座に無効化するとInteract判定の表示が残るバグ（ClientSimのバグ？）があり、描画内容を消して対処している
				renderer.materials = guideMaterials;

				// 補間を無視してワープ移動させる処理
				targetRigidbody.velocity = Vector3.zero;
				targetRigidbody.angularVelocity = Vector3.zero;
				targetRigidbody.position = transform.position;
				targetRigidbody.rotation = transform.rotation;
				VRCObjectSync objectSync = target.GetComponent<VRCObjectSync>();
				if(objectSync != null)
				{
					// TeleportToを使うよりこちらのほうがいいらしい
					objectSync.FlagDiscontinuity();
				}

				rigidbody = targetRigidbody;
				trigger.enabled = false;
			}
		}

		private bool guide;

		public float defaultEquipmentHeight = 0.4f;

		public float speed = 10.0f;
		public float dragForce = 0.2f;

		public Material guideMaterial;
		private new Renderer renderer;	// 隠蔽元変数は非推奨らしいのでそのまま隠す
		private Collider trigger;
		private MeshFilter meshFilter;
		
		private Mesh targetMesh;
		private Material[] targetMaterials;
		private Material[] guideMaterials;

		override protected void Start()
		{
			target = transform.parent;
			targetRenderer = target.GetComponent<Renderer>();
			targetCollider = target.GetComponent<Collider>();
			targetRigidbody = target.GetComponent<Rigidbody>();

			// 階層関係にある状態だとInteract判定が実際のコリジョンの位置とずれるバグがある（実際のVRChatクライアント上のみ）
			target.DetachChildren();

			pickup = target.GetComponent<VRCPickup>();
			renderer = GetComponent<Renderer>();
			trigger = GetComponent<Collider>();
			meshFilter = GetComponent<MeshFilter>();

			localPlayer = Networking.LocalPlayer;
			inputMove = Vector2.zero;
			swimMode = false;
			retriggerBlockTime = 0;
			
			// 親オブジェクトに設定されているモデルを見て自動的に一致させる
			targetMesh = target.GetComponent<MeshFilter>().sharedMesh;
			GetComponent<MeshCollider>().sharedMesh = targetMesh;
			targetMaterials = target.GetComponent<Renderer>().materials;

			guideMaterials = new Material[targetMaterials.Length];
			for(int i = 0; i < guideMaterials.Length; i++)
			{
				guideMaterials[i] = guideMaterial;
			}
			guide = false;

			// 初期状態のGetPlayerTagはClientSimでは空文字列が返ってきてVRC上ではNullが返ってくる
			// あらかじめ空文字列を設定しておくことで動作を統一する
			localPlayer.SetPlayerTag(TAG_NAME, "");

			// インスタンスを立てた人の初期化処理
			if (Networking.IsOwner(gameObject))
			{
				equipped = false;	// setterの呼び出し
			}
			
			base.Start();
		}

		private float getHandHeight()
		{
			float result;
			switch(pickup.currentHand)
			{
			case VRC_Pickup.PickupHand.Left:
				result = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position.y;
				break;
			case VRC_Pickup.PickupHand.Right:
				result = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position.y;
				break;
			default:
				Debug.LogWarning("getHandHeightがピックアップオブジェクトを手に持っていない状態で呼ばれました");
				result = pickup.transform.position.y;
				break;
			}

			// 使い勝手がいいようにやや下寄り
			return (result - localPlayer.GetPosition().y) * 0.8f;
		}

		override protected void Update()
		{
			if(localPlayer == null) { return; }	// 終了時のエラー回避
			
			// 位置同期が自動化されているので実際にシミュレートを行うのはオーナーのみ
			// 通常時はターゲットオブジェクトのVRC Object Syncで、着用時はプレイヤー座標で同期される
			VRCPlayerApi owner = Networking.GetOwner(gameObject);
			if(owner == localPlayer)
			{
				base.Update();
			}
			
			if(guide == true)
			{
				float height;
				if(localPlayer.IsUserInVR() == false)
				{
					height = defaultEquipmentHeight * localPlayer.GetAvatarEyeHeightAsMeters();
				}
				else
				{
					height = getHandHeight();
				}
				
				transform.SetPositionAndRotation(localPlayer.GetPosition() + Vector3.up * height, localPlayer.GetRotation());
			}

			if(equipped == true)
			{
				// 装備されているとき、その装備対象はオブジェクトのオーナー
				// 他者からの見え方を含むのでLocalPlayerと混同しないように
				Quaternion rotation = owner.GetRotation();
				transform.position = owner.GetPosition() + equipHeight * owner.GetAvatarEyeHeightAsMeters() * Vector3.up;

				if(owner == localPlayer)
				{
					float r;
					
					switch(geometry)
					{
					default: r = radius; break;
					case VolumeGeometryType.Torus: r = (radius - innerRadius) * 0.5f; break;
					}
					
					if(swimMode == false && depth > r && retriggerBlockTime < 0f)
					{
						setSwimmode(true);

						if(swimMode == true)
						{
							retriggerBlockTime = 1.0f;
						}
					}

					if(swimMode == true)
					{
						Vector3 acceleration = (rotation * Vector3.right * inputMove.x + rotation * Vector3.forward * inputMove.y) * speed;
						playerVelocity += acceleration * Time.deltaTime;
						playerVelocity *= Mathf.Exp(-dragForce * Time.deltaTime);
						localPlayer.SetVelocity(playerVelocity + velocity);

						if(isGrounded(r) == true)
						{
							if(depth > r * 0.5f)
							{
								velocity.y = 0f;
							}
							else if(retriggerBlockTime < 0f)
							{
								playerVelocity = Vector3.zero;
								setSwimmode(false);
								retriggerBlockTime = 1.0f;
							}
						}
					}

					retriggerBlockTime -= Time.deltaTime;
				}
			}
		}

		private bool isGrounded(float radius)
		{
			// これはClientSimだと拾ってくれるが実際のクライアントは拾ってくれない
			//if(localPlayer.IsPlayerGrounded() == true) { return true; }

			int layerMask = 3410391;	// SDK3.8.2時点でのPlayerLocalのレイヤーマスク（カスタムレイヤーを除く）
			return Physics.CheckSphere(localPlayer.GetPosition() + 0.5f * radius * Vector3.up, radius, layerMask, QueryTriggerInteraction.Ignore);
		}

		public void enterGuide()
		{
			// ガイドは持っている人だけが見える
			active = false;
			
			if(localPlayer.GetPlayerTag(TAG_NAME) == "")
			{
				pickup.UseText = equipText;
				guide = true;
				meshFilter.sharedMesh = targetMesh;
				renderer.enabled = true;
			}
			else
			{
				pickup.UseText = "";
				guide = false;
			}
		}

		public void endGuide()
		{
			active = true;
			renderer.enabled = false;
			guide = false;
		}

		public void enterBuoy()
		{
			if(pickup.IsHeld == false) { return; }
			if(equipped == true) { return; }
			if(Networking.GetOwner(gameObject) != localPlayer) { return; }	// PickupによりOwnerを揃えられる。オーナー変更が通っていない間は装備しない
			if(localPlayer.GetPlayerTag(TAG_NAME) != "") { return; }
			
			if(localPlayer.IsUserInVR() == false)
			{
				equipHeight = defaultEquipmentHeight;
			}
			else
			{
				// 持ち手を参照するのでこれはPickupを離す前でないといけない
				equipHeight = getHandHeight() / localPlayer.GetAvatarEyeHeightAsMeters();
			}

			pickup.Drop();
			pickup.pickupable = false;
			rigidbody = null;	// 参照先を外すと自分が浮力の対象になる

			guide = false;
			
			renderer.materials = targetMaterials;

			seashore.OnBuoyancyEquipped(true);
			equipped = true;
		}

		private void setSwimmode(bool value)
		{
			if(swimMode == value) { return; }

			if(value == true)
			{
				// 標準移動制御が何らかの理由により奪われている場合は、パラメータが戻ってくるまで保留
				if(seashore.playerIsImmobilize() == true)
				{
					swimMode = false;
					active = false;
					return;
				}
				
				// 速度を維持
				playerVelocity = localPlayer.GetVelocity();
				playerVelocity.y = 0f;

				localPlayer.SetWalkSpeed(0f);
				localPlayer.SetStrafeSpeed(0f);
				localPlayer.SetRunSpeed(0f);
				localPlayer.SetJumpImpulse(0f);
				localPlayer.SetGravityStrength(0f);
				
				swimMode = true;
				active = true;
			}
			else
			{
				seashore.restoreDefaultPlayerLocomotions();
				swimMode = false;
				active = false;
			}
		}
		
		private void targetVisibility(bool value)
		{
			targetRenderer.enabled = value;
			targetCollider.enabled = value;
			targetRigidbody.isKinematic = !value;
		}

		// 外す
		public override void Interact()
		{
			setSwimmode(false);
			active = true;

			seashore.OnBuoyancyEquipped(false);
			pickup.pickupable = true;
			equipped = false;
		}

		public override void OnOwnershipTransferred(VRCPlayerApi player)
		{
			// 装着したままLeaveしたプレイヤーがいるとここに飛んでくる
			if(equipped == true && localPlayer == player)
			{
				equipped = false;
			}
		}
	}
}