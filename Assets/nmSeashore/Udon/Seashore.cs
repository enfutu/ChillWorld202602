
using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Enums;
using System;

namespace nmSeashore
{
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class Seashore : UdonSharpBehaviour
	{
		public Renderer shore;
		public Renderer beach;
		public new AudioSource audio;
		
		private Material material;
		private Material beachMaterial;
		
		// マテリアルと共有するパラメータ
		private float _TimeScale, _ManualTimeOffset, _SideScrollSpeed, _SideScrollOffset;
		private float _WavePower, _WaveWidth, _WaveBreakThreshold, _Steepness;
		private float _FarWaveHeight, _FarWaveDecayDistance;
		private int _NumberOfWaves, _NoiseOctaves;
		private int _SwellZoneBuffer, _FrontWaveNoiseLevel;
		private float _FrontWaveWidth, _BackwashVelocity;
		private float _Interval;
		private float _BeachSlope, _SeaLevel;
		private float _TimeDistortionPower, _TimeDistortionWidth;
		private int _TimeDistortionNoiseLevel;

		private float maxShorelineLength;
		private float invWaveWidth, invFrontWaveWidth, invInterval, invSteepness;

		[UdonSynced]
		private long globalTimeOffset;
		private float localTimeOffset;
		
		[UdonSynced]
		private float seed;

		private const float EXPONENT_PRECISION_LIMIT = 0x8000;	// 分解能0.001953125まで許容
		private double Repeat(double x) => x % EXPONENT_PRECISION_LIMIT;	// Mathf.Repeatだと精度問題が解消できないため自前で用意（マイナスへのはみ出しは考慮しない）

		[SerializeField]
		private UnityEngine.Object[] includeUnderwaterObjects;
		[SerializeField]
		private GameObject[] excludeWaterVolumes;
		private bool underwater;
		private PostProcessVolume underwaterPostProcess;

		VRCPlayerApi localPlayer;
		private float[] stashedPlayerLocomotions;

		public override void OnDeserialization()
		{
			localTimeOffset = (float)((DateTime.UtcNow.Ticks - globalTimeOffset) * 1e-7) - Time.time;
			sideScrollOffset = _SideScrollOffset;	// setterの呼び出し
		}

		private void Start()
		{
			localPlayer = Networking.LocalPlayer;
			underwater = false;

			if(Networking.IsOwner(gameObject) == true)
			{
				// 最初にマスターが入った時間を基準にする
				globalTimeOffset = DateTime.UtcNow.Ticks;
				localTimeOffset = -Time.time;	// ここに入った時点で時間がズレているので0基準になるように補正
				
				seed = UnityEngine.Random.Range(-EXPONENT_PRECISION_LIMIT * 0.5f, EXPONENT_PRECISION_LIMIT * 0.5f);
				RequestSerialization();
			}

			// シーン内の他オブジェクトから自動登録するための情報
			string tag = localPlayer.GetPlayerTag("nmSeashoreReferenceName");
			if(tag == null || tag == "")
			{
				localPlayer.SetPlayerTag("nmSeashoreReferenceName", name);
			}
			stashedPlayerLocomotions = new float[5];
			SendCustomEventDelayedSeconds(nameof(updateDefaultPlayerLocomotions), 1.0f, EventTiming.LateUpdate);    // 別のStartで初期値を変更されるかもしれないので保存を遅延

			// 範囲指定付きのPostProcessVolumeがあればリストから抽出
			for(int i = 0; i < includeUnderwaterObjects.Length; i++)
			{
				if(includeUnderwaterObjects[i].GetType() == typeof(PostProcessVolume))
				{
					PostProcessVolume postProcess = (PostProcessVolume)includeUnderwaterObjects[i];
					if(postProcess.GetComponent<Collider>() == null) { continue; }

					UnityEngine.Object[] newArray = new UnityEngine.Object[includeUnderwaterObjects.Length - 1];
					int index = 0;
					for(int j = 0; j < includeUnderwaterObjects.Length; j++)
					{
						if(i == j) { continue; }
						newArray[index] = includeUnderwaterObjects[j];
						index++;
					}

					underwaterPostProcess = postProcess;
					includeUnderwaterObjects = newArray;
					break;
				}
			}

			syncMaterial();
		}

		public void syncMaterial()
		{
			// インスペクター上で直接マテリアルを入力すると
			// エディタでのプレイ終了後に元データのパラメータが上書きされるため
			// Rendererから複製インスタンスを取ってそれを変更する必要がある
			// プロパティを取るときに内部的には複製が働くため、これで動作する
			material = shore.material;
			
			#if UNITY_STANDALONE_WIN
			// DirectionalLightが配置されていない場合にDepthを強制的に付与
			if(material.name.Contains("SeashoreTransparent") == true && VRCCameraSettings.ScreenCamera.DepthTextureMode == DepthTextureMode.None)
			{
				VRCCameraSettings.ScreenCamera.DepthTextureMode = DepthTextureMode.Depth;
			}
			#else
			if(material.name.Contains("SeashoreTransparentNoTessNoGrab") == true)
			{
				// Androidプラットフォームではデフォルトで深度テクスチャが作成されないので明示的に設定する必要あり
				VRCCameraSettings.ScreenCamera.DepthTextureMode = DepthTextureMode.Depth;
			}
			else
			{
				VRCCameraSettings.ScreenCamera.DepthTextureMode = DepthTextureMode.None;
			}
			#endif

			// アニメーションをマニュアル動作に変更するためプロパティを抽出
			_TimeScale = material.GetFloat("_TimeScale");
			_ManualTimeOffset = 0f;
			material.SetFloat("_TimeScale", 0f);
			
			// 波の変形処理を一致させるのに必要なパラメータ
			_SideScrollSpeed = material.GetFloat("_SideScrollSpeed");
			_WavePower = material.GetFloat("_WavePower");
			_WaveWidth = material.GetFloat("_WaveWidth");
			_WaveBreakThreshold = material.GetFloat("_WaveBreakThreshold");
			_Steepness = material.GetFloat("_Steepness");
			_FarWaveHeight = material.GetFloat("_FarWaveHeight");
			_FarWaveDecayDistance = material.GetFloat("_FarWaveDecayDistance");
			_NumberOfWaves = material.GetInteger("_NumberOfWaves");
			_SwellZoneBuffer = material.GetInteger("_SwellZoneBuffer");
			_FrontWaveNoiseLevel = material.GetInteger("_FrontWaveNoiseLevel");
			_FrontWaveWidth = material.GetFloat("_FrontWaveWidth");
			_BackwashVelocity = material.GetFloat("_BackwashVelocity");
			_NoiseOctaves = material.GetInteger("_NoiseOctaves");
			_Interval = material.GetFloat("_Interval");
			_BeachSlope = material.GetFloat("_BeachSlope");
			_SeaLevel = material.GetFloat("_SeaLevel");
			_TimeDistortionPower = material.GetFloat("_TimeDistortionPower");
			_TimeDistortionWidth = material.GetFloat("_TimeDistortionWidth");
			_TimeDistortionNoiseLevel = material.GetInteger("_TimeDistortionNoiseLevel");

			// 計算処理を簡略化するための事前計算
			maxShorelineLength = -9.0f / (16.0f * _BackwashVelocity) - _Interval * 0.75f;
			invWaveWidth = 1.0f / _WaveWidth;
			invFrontWaveWidth = 1.0f / _FrontWaveWidth;
			invInterval = 1.0f / _Interval;
			invSteepness = 1.0f / _Steepness;
			
			// 砂浜マテリアルがある場合は海面のパラメータを複製
			if(beach != null)
			{
				beachMaterial = beach.material;
				beachMaterial.SetFloat("_TimeScale", 0f);
				beachMaterial.SetFloat("_SideScrollSpeed", _SideScrollSpeed);
				beachMaterial.SetFloat("_TimeDistortionPower", _TimeDistortionPower);
				beachMaterial.SetFloat("_TimeDistortionWidth", _TimeDistortionWidth);
				beachMaterial.SetInteger("_TimeDistortionNoiseLevel", _TimeDistortionNoiseLevel);
				beachMaterial.SetFloat("_Interval", _Interval);
				beachMaterial.SetInteger("_NoiseOctaves", _NoiseOctaves);
				beachMaterial.SetFloat("_BackFormLength", material.GetFloat("_BackFormLength"));
				beachMaterial.SetFloat("_WavePower", _WavePower);
				beachMaterial.SetFloat("_WaveWidth", _WaveWidth);
				beachMaterial.SetFloat("_WaveBreakThreshold", _WaveBreakThreshold);
				beachMaterial.SetInteger("_NumberOfWaves", _NumberOfWaves);
				beachMaterial.SetInteger("_SwellZoneBuffer", _SwellZoneBuffer);
				beachMaterial.SetInteger("_FrontWaveNoiseLevel", _FrontWaveNoiseLevel);
				beachMaterial.SetFloat("_FrontWaveWidth", _FrontWaveWidth);
				beachMaterial.SetFloat("_BackwashVelocity", _BackwashVelocity);
			}

			sideScrollOffset = material.GetFloat("_SideScrollOffset");	// setterの呼び出しのためbeachMaterial設定より後
		}

		private void LateUpdate()
		{
			if(localPlayer == null) { return; }

			_ManualTimeOffset = _TimeScale * (float)Repeat(Time.timeAsDouble + localTimeOffset);
			material.SetFloat("_ManualTimeOffset", _ManualTimeOffset);
			
			if(beachMaterial != null)
			{
				beachMaterial.SetFloat("_ManualTimeOffset", _ManualTimeOffset);
			}

			Vector3 playerPos;
			Vector3 screenCameraPos = VRCCameraSettings.ScreenCamera.Position;
			Vector3 photoCameraPos = Vector3.zero;
			bool screenCameraIsUnderWater = screenCameraPos.y < vertexHeight(screenCameraPos);
			bool photoCameraIsUnderWater = false;

			if(VRCCameraSettings.PhotoCamera != null && VRCCameraSettings.PhotoCamera.Active == true)
			{
				photoCameraPos = VRCCameraSettings.PhotoCamera.Position;
				photoCameraIsUnderWater = photoCameraPos.y < vertexHeight(photoCameraPos);
				playerPos = photoCameraPos;
			}
			else
			{
				playerPos = screenCameraPos;
			}

			if(underwaterPostProcess != null)
			{
				if(screenCameraIsUnderWater == true && photoCameraIsUnderWater == true)
				{
					underwaterPostProcess.enabled = true;
					underwaterPostProcess.isGlobal = true;
				}
				else if(screenCameraIsUnderWater == true && photoCameraIsUnderWater == false)
				{
					underwaterPostProcess.enabled = true;
					underwaterPostProcess.isGlobal = false;
					underwaterPostProcess.transform.position = screenCameraPos;
				}
				else if(screenCameraIsUnderWater == false && photoCameraIsUnderWater == true)
				{
					underwaterPostProcess.enabled = true;
					underwaterPostProcess.isGlobal = false;
					underwaterPostProcess.transform.position = photoCameraPos;
				}
				else
				{
					underwaterPostProcess.enabled = false;
				}
			}

			if(underwater == false)
			{
				if(playerPos.y < vertexHeight(playerPos))
				{
					activateUnderwaterObjects(true);
					underwater = true;
				}
			}
			else
			{
				if(playerPos.y > vertexHeight(playerPos))
				{
					activateUnderwaterObjects(false);
					underwater = false;
				}
			}

			if(audio != null)
			{
				Vector3 localPos = transform.InverseTransformPoint(playerPos);
				audio.transform.localPosition = new Vector3(Mathf.Max(localPos.x * 0.5f, 0f), 0f, localPos.z);

				if(underwater == true)
				{
					audio.spatialBlend = 0f;
				}
				else
				{
					audio.spatialBlend = 1.0f - 1.0f / (1.0f + Vector3.SqrMagnitude(localPos - audio.transform.localPosition) * 0.00007f);
				}
			}
		}

		private void activateUnderwaterObjects(bool value)
		{
			foreach(UnityEngine.Object obj in includeUnderwaterObjects)
			{
				if(obj == null) { continue; }
				
				string type = obj.GetType().Name;
				switch(type)
				{
				case "GameObject":
					((GameObject)obj).SetActive(value);
					break;
				// 基底クラスBehaviourのenabledはUdonに公開されていないため個別に対応
				// オブジェクト内の他コンポーネントに依存するオーディオフィルタはGameObjectの切替では使えないので
				// コンポーネントのみを配列に登録する必要がある
				case "AudioLowPassFilter": ((AudioLowPassFilter)obj).enabled = value; break;
				case "AudioHighPassFilter": ((AudioHighPassFilter)obj).enabled = value; break;
				case "AudioChorusFilter": ((AudioChorusFilter)obj).enabled = value; break;
				case "AudioDistortionFilter": ((AudioDistortionFilter)obj).enabled = value; break;
				case "AudioEchoFilter": ((AudioEchoFilter)obj).enabled = value; break;
				case "AudioReverbFilter": ((AudioReverbFilter)obj).enabled = value; break;
				case "PostProcessVolume": ((PostProcessVolume)obj).enabled = value; break;
				}
			}
		}
		
		public void updateDefaultPlayerLocomotions()
		{
			stashedPlayerLocomotions[0] = localPlayer.GetWalkSpeed();
			stashedPlayerLocomotions[1] = localPlayer.GetStrafeSpeed();
			stashedPlayerLocomotions[2] = localPlayer.GetRunSpeed();
			stashedPlayerLocomotions[3] = localPlayer.GetJumpImpulse();
			stashedPlayerLocomotions[4] = localPlayer.GetGravityStrength();
		}

		public void restoreDefaultPlayerLocomotions()
		{
			localPlayer.SetWalkSpeed(stashedPlayerLocomotions[0]);
			localPlayer.SetStrafeSpeed(stashedPlayerLocomotions[1]);
			localPlayer.SetRunSpeed(stashedPlayerLocomotions[2]);
			localPlayer.SetJumpImpulse(stashedPlayerLocomotions[3]);
			localPlayer.SetGravityStrength(stashedPlayerLocomotions[4]);
		}

		public bool playerIsImmobilize()
		{
			// 移動制御を奪う処理が補間処理を行っている場合、その途中で切り替えを行うと補間途中の状態を保存してしまうことがあるので
			// 元の移動速度に完全に戻っているかどうかで判断するようにしている
			return localPlayer.GetWalkSpeed() != stashedPlayerLocomotions[0] || localPlayer.GetStrafeSpeed() != stashedPlayerLocomotions[1] || localPlayer.GetRunSpeed() != stashedPlayerLocomotions[2];
		}

		public void OnBuoyancyEquipped(bool value)
		{
			if(value == false)
			{
				buoyancyDetached();
			}
			else
			{
				buoyancyAttached();
			}
		}

		private void buoyancyAttached()
		{
			foreach(GameObject volume in excludeWaterVolumes)
			{
				volume.SetActive(false);
			}
		}
		
		// 動作保留をするためのSendCustomEventDelayedが引数に対応してないので分離
		public void buoyancyDetached()
		{
			if(localPlayer.GetWalkSpeed() <= 0.001f || localPlayer.GetStrafeSpeed() <= 0.001f || localPlayer.GetRunSpeed() <= 0.001f)
			{
				SendCustomEventDelayedFrames(nameof(buoyancyDetached), 1);
			}
			
			foreach(GameObject volume in excludeWaterVolumes)
			{
				volume.SetActive(true);
			}
		}

		public float sideScrollOffset
		{
			get => _SideScrollOffset;
			set
			{
				_SideScrollOffset = value;
				material.SetFloat("_SideScrollOffset", value + seed);	// _SideScrollOffsetは実質的な波の形状変化なのでこれでseed値として成立する
				if(beachMaterial != null)
				{
					beachMaterial.SetFloat("_SideScrollOffset", value + seed);
				}
			}
		}

		public float currentScrollPosition => _ManualTimeOffset * _SideScrollSpeed + _SideScrollOffset;

		// HLSL代替関数
		private float frac(float x) => x - Mathf.Floor(x);
		private float step(float y, float x) => x < y ? 0.0f : 1.0f;
		private float smoothstep(float min, float max, float x) => SMOOTH(Mathf.Clamp01((x - min) / (max - min)));	// Mathf.SmoothStepとHLSLのsmoothstepでは範囲外の値の扱いが異なるので、独自に定義

		private float POW2(float x) => x * x;
		private float SMOOTH(float x) => x * x * (3.0f - 2.0f * x);
		
		// fiHashの改造品
		// https://www.shadertoy.com/view/43jSRR

		// The MIT License
		// Copyright © 2024 Giorgi Azmaipharashvili
		// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
		float hash(Vector2Int p)
		{
			uint ux = (uint)(p.x + 0x7FFFFFFFU) * 141421356U;
			uint uy = (uint)(p.y + 0x7FFFFFFFU) * 2718281828U;
			return (ux ^ uy) * 3141592653U * 0.00000000023283064365386963f;	// uint.MaxValueの逆数を単精度に丸めた値
		}

		float noise1D(float x, int seed)
		{
			Vector2Int q = new Vector2Int(Mathf.FloorToInt(x), seed);
			float t = SMOOTH(x - q.x);
			
			float h0 = hash(q);
			q.x++;
			float h1 = hash(q);
	
			return Mathf.Lerp(h0, h1, t);
		}

		private float fbm1d(float x, int seed, int level)
		{
			float result = 0f;
			float m = 0.5f;
			float n = 2.0f;
			
			for(int i = 0; i < level; i++)
			{
				result += noise1D(x * n, seed) * m;
				m *= 0.5f;
				n *= 2.0f;
			}
	
			// 0-1の範囲に正規化して返す
			return result * (1.0f / (1.0f - m * 2.0f));
		}
		
		private float absfbm1d(float x, int seed, int level)
		{
			float result = 0f;
			float m = 1.0f;
			float n = 1.0f;
	
			for(int i = 0; i < level; i++)
			{
				m *= 0.5f;
				n *= 2.0f;
				result += Mathf.Abs(noise1D(x * n, seed) - 0.5f) * m;
			}
	
			// 値域は0-0.5f
			return result * (1.0f / (1.0f - m));
		}

		private float bottomSmooth(float x) => (Mathf.Sqrt(x * x + 0.04f) - 0.2f) * 1.2198039293289185f;	// sqrt(1 + 0.2 * 0.2) + 0.2

		private float wavePower(int index, float x) => fbm1d(x * invWaveWidth, index, _NoiseOctaves);
		private float breakerOffset(float num) => (_NumberOfWaves - num) * _Interval;
		private float currentTime(Vector2 pos) => _ManualTimeOffset + fbm1d(pos.y * invWaveWidth * _TimeDistortionWidth, 0, _TimeDistortionNoiseLevel) * _TimeDistortionPower;
		private float scrollTime() => _ManualTimeOffset * _SideScrollSpeed + _SideScrollOffset + seed;
			
		private float heightOffset(float offset)
		{
			float getup = smoothstep(breakerOffset(2.0f), breakerOffset(1.0f), offset);
			float swell = -smoothstep(_Interval, 0f, offset) * (_WaveBreakThreshold + 0.1f);
			return getup + swell;
		}
		
		// このIDの波の稜線がどの位置にあるのかを返す
		private float idToPos(int id, float time)
		{
			return (id - time) * _Interval;
		}
		
		// structが使えないので個別に参照渡し
		private void waveCurve(int id, Vector2 pos, out float w_power, out float w_comp, out float w_height)
		{
			w_power = wavePower(id, pos.y);	// 正面から見たときのこの列の波の力
			
			float pitCenter = breakerOffset(2.1f) * 0.5f;
			float pit = 1 - SMOOTH(Mathf.Clamp01(Mathf.Abs(pos.x / pitCenter - 1.0f)));
			float depth = heightOffset(pos.x) + pit * (1.0f - _WaveBreakThreshold) * 0.5f;	// 波形全体が沈み込むオフセット
			float p0 = Mathf.Max(w_power - depth, 0f);	// 沖合から現れる波の立ち上がり
			float threshold = _WaveBreakThreshold * (1.0f - pit * 0.2f);
			
			float dest = (1.0f - bottomSmooth(1.0f - Mathf.Min((1 - w_power) / (1.0f - threshold), 1.0f))) * threshold;	// 砕波した波の最終的な変形先
			
			float compAmount = smoothstep(breakerOffset(2.5f) + w_power * _Interval, breakerOffset(2.9f) + w_power * _Interval, pos.x);	// 砕波される度合い
			float slope = Mathf.Max(threshold, dest);
			float p = Mathf.Min(Mathf.Lerp(p0, slope, compAmount), p0);	// 計算後の波の強さ
				
			w_comp = Mathf.Max(p0 - p, 0f);	// 波の強さに対して潰れている度合い
			w_height = SMOOTH(p) * _WavePower * (1.0f - pit * 0.5f);	// スムージングと波全体の高さ設定
			w_height *= (pos.x * 0.5f) / breakerOffset(0) + 0.5f;
		}
		
		private float frontForm(float x, float o, float len, float b)
		{
			return frac(1 - Mathf.Clamp01((x - o + len) / b));
		}
		
		private float getFrontFormLength(float comp)
		{
			return Mathf.Log(comp + 1.0f, 2.0f) * (_Interval * 0.5f);
		}

		private bool shoreline(Vector2 pos, ref float height)
		{
			float timer = currentTime(pos);
			float y = 0.0f;
			bool result = false;
			
			for(int i = -1; i < Math.Min(_SwellZoneBuffer, 2); i++)
			{
				int id = Mathf.FloorToInt(timer) - i;
				float t = -idToPos(id, timer);	// 波打ち際において波頭の位置情報は原点からの経過時間
				
				waveCurve(id, new Vector2(-t, pos.y), out float w_power, out float w_comp, out float w_height);
					
				#if DEBUG_TRIANGLE
				float power = 1.5f;
				#else
				// 折り返し型グラデーション
				float power = absfbm1d(pos.y * invFrontWaveWidth, id, _FrontWaveNoiseLevel) + 1.0f;
				#endif
				
				float d = power / (_BackwashVelocity * 2.0f);	// 波の前線が最も伸びた状態になったときの経過時間
				float amount = Mathf.Clamp01(t / d);
				float smoothAmount = SMOOTH(amount);
				float td = Mathf.Min(t, d);	// 波が伸び切ったときにキャップが働く時間軸
				
				float nearshore = getFrontFormLength(w_comp); // 砕波帯から入ってくる波の形
				float foreshore = power * _Interval * 0.5f; // 波打ち際の波が伸び切ったときの形
				float shoreOffset = power * td - POW2(td) * _BackwashVelocity;	// 波が最大到達点まで伸びるカーブ
				float b = nearshore * 0.5f * (1.0f - amount);
				
				float swashLine = Mathf.Lerp(nearshore + t, foreshore + shoreOffset, smoothAmount);	// 波のカーブを入ってくる波の形と繋げる
				float backwashLine = swashLine - POW2(Mathf.Max((t - d) * 0.125f, 0f));

				float shoreMask = step(-pos.x, backwashLine);	// 水面が戻っていく動きに対応したマスク
				y += Mathf.Max(0f, frontForm(pos.x, -swashLine + b, b, b * (1.0f - amount)) - amount);
				height = Mathf.Max(height, Mathf.Max((1.0f - smoothAmount) * (1.0f - y), 0.01f) * shoreMask);

				result = shoreMask != 0 || result;	// 水面上であればtrue
			}
			height = POW2(height) * 0.5f;
			return result;
		}
		
		// 水面のy座標を返す関数
		// 水面が見つからない場合はfloat.MinValueを返す
		public float vertexHeight(Vector3 pos)
		{
			Vector3 objectPos = transform.InverseTransformPoint(pos);
			pos = new Vector2(objectPos.x, objectPos.z);

			// 範囲外
			if(pos.x < maxShorelineLength)
			{
				return float.MinValue;
			}
			
			pos.y += scrollTime();
			
			float timer = currentTime(pos);
			
			// 範囲ごとの波の影響力が0であれば
			// 処理の関係上全て実行しなければならないシェーダーとは異なり
			// 変形取得処理をif文で省略できる
			float shorelineBlend = smoothstep(-_Interval, 0f, -pos.x);
			float frontHeight = 0f;

			// 波打ち際の波
			if(shorelineBlend > 0.0f)
			{
				if(shoreline(pos, ref frontHeight) == false)
				{
					return float.MinValue;
				}
			}
			
			float farWavesStartLine = breakerOffset(1.0f);
			float farWavesFalloff = (Mathf.Min(pos.x, farWavesStartLine + _FarWaveDecayDistance) - farWavesStartLine) - _FarWaveDecayDistance;
			farWavesFalloff *= farWavesFalloff;
			farWavesFalloff /= POW2(_FarWaveDecayDistance);
			float farWavesBlend = smoothstep(farWavesStartLine - _Interval, farWavesStartLine, pos.x) * farWavesFalloff;
			
			float nearWaves = 0f;
			float farWaves = 0f;

			// 砕波帯の波
			if(shorelineBlend < 1.0f && farWavesBlend < 1.0f)
			{
				int id = Mathf.FloorToInt(timer + (pos.x + _Interval * 0.5f) * invInterval);
				float w_x = idToPos(id, timer);
				waveCurve(id, new Vector2(w_x, pos.y), out float w_power, out float w_comp, out float w_height);
				
				float steepness = invSteepness / (1.0f + invSteepness * POW2(w_x)) * _Steepness;
				
				float distance = pos.x - w_x;
				float rationalBump = w_height / (1.0f + w_height * steepness * POW2(distance));
				float triangleWave = 1.0f - Mathf.Abs(distance) * invInterval * 2.0f;
				float smooth = SMOOTH(triangleWave);
				
				nearWaves = rationalBump * smooth;
			}
			
			// 白波に変化する前の波
			if(farWavesBlend > 0.0f)
			{
				float t = (pos.x + timer * _Interval) * invInterval;
				float farWavesTriangle = Mathf.Abs(frac(t) - 0.5f) * 2.0f;	// 三角波
				farWavesTriangle *= farWavesTriangle;
				int farWavesID = Mathf.RoundToInt(t);
				farWaves = SMOOTH(farWavesTriangle) * wavePower(farWavesID, pos.y) * farWavesBlend * _FarWaveHeight;
			}
			
			// 全フェーズの波を合成
			float result;
			result = nearWaves + farWaves + _SeaLevel;
			result = Mathf.Lerp(result, -pos.x * _BeachSlope + frontHeight, shorelineBlend);

			// グローバル座標に変換して返す
			return transform.TransformPoint(new Vector3(objectPos.x, result, objectPos.z)).y;
		}
	}
}
