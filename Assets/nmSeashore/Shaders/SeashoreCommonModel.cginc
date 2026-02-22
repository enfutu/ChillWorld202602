#ifndef NMSEASHORE_COMMON_MODEL
#define NMSEASHORE_COMMON_MODEL

sampler2D _FormMask, _WaterGradient, _Normal;
float _NormalAmount, _ReflectionIntensity, _AttenuationScale;
#if APPLY_SUN_SPECULAR
float _SunBrightnessMultiplier, _SourceAngleCos, _SpecularSoftEdge;
#endif

float _TimeScale, _ManualTimeOffset, _SideScrollSpeed, _SideScrollOffset;
float _WavePower, _WaveWidth, _WaveBreakThreshold, _Steepness;
float _BackFormLength, _FarWaveHeight, _FarWaveDecayDistance;
int _SwellZoneBuffer, _FrontWaveNoiseLevel;
float _FrontWaveWidth, _BackwashVelocity;
int _NumberOfWaves, _NoiseOctaves;
float _Interval;
float _MaxTessellationFactor, _TessellationFalloffThreshold, _TessellationFalloffExponent;
float _BeachSlope, _SeaLevel;
float _TimeDistortionPower, _TimeDistortionWidth;
int _TimeDistortionNoiseLevel;

float _WaterDistortionScale, _InvIOR;

// index番目の波の力
inline float wavePower(int index, float x)
{
	#if DEBUG_TRIANGLE
	return abs(frac(x / _WaveWidth + index * 0.3) - 0.5) * 2;
	#endif
	return fbm1d(x / _WaveWidth, index, _NoiseOctaves);
}

inline float breakerOffset(float num)
{
	return (_NumberOfWaves - num) * _Interval;
}

inline float currentTime(float2 pos)
{
	float timeDistortion = fbm1d(pos.y / _WaveWidth * _TimeDistortionWidth, 0, _TimeDistortionNoiseLevel) * _TimeDistortionPower;
	return _Time.y * _TimeScale + _ManualTimeOffset + timeDistortion;
}

inline float scrollTime()
{
	return (_Time.y * _TimeScale + _ManualTimeOffset) * _SideScrollSpeed + _SideScrollOffset;
}

float heightOffset(float offset)
{
	float getup = smoothstep(breakerOffset(2), breakerOffset(1), offset);
	float swell = -smoothstep(_Interval, 0, offset) * (_WaveBreakThreshold + 0.1);
	return getup + swell;
}

struct wave
{
	float power;
	float height;
	float comp;
};

// このIDの波の稜線がどの位置にあるのかを返す
inline float idToPos(int id, float time)
{
	return (id - time) * _Interval;
}

void waveCurve(inout wave w, int id, float2 pos)
{
	w.power = wavePower(id, pos.y);	// 正面から見たときのこの列の波の力
	
	float pitCenter = breakerOffset(2.1) * 0.5;
	float pit = (1 - SMOOTH(saturate(abs(pos.x / pitCenter - 1))));
	float depth = heightOffset(pos.x) + pit * (1 - _WaveBreakThreshold) * 0.5;	// 波形全体が沈み込むオフセット
	float p0 = max(w.power - depth, 0);	// 沖合から現れる波の立ち上がり
	float threshold = _WaveBreakThreshold * (1 - pit * 0.2);
	
	float dest = (1 - bottomSmooth(1 - min((1 - w.power) / (1 - threshold), 1))) * threshold;	// 砕波した波の最終的な変形先
	
	float compAmount = smoothstep(breakerOffset(2.5) + w.power * _Interval, breakerOffset(2.9) + w.power * _Interval, pos.x);	// 砕波される度合い
	float slope = max(threshold, dest);
	float p = min(lerp(p0, slope, compAmount), p0);	// 計算後の波の強さ
	
	w.comp = max(p0 - p, 0);	// 波の強さに対して潰れている度合い
	w.height = SMOOTH(p) * _WavePower * (1 - pit * 0.5);	// スムージングと波全体の高さ設定
	w.height *= (pos.x * 0.5) / breakerOffset(0) + 0.5;
}

inline float frontForm(float x, float o, float len, float b)
{
	return frac(1 - saturate((x - o + len) / b));
}

inline float getFrontFormLength(wave w)
{
	return log2(w.comp + 1) * (_Interval * 0.5);
}

// x : 水面上の泡
// y : 前線の泡
// z : fragments ? フェードアウト係数 : 水面の高さ
// w : スムースネス
float4 shoreline(float2 pos, bool fragments = true)
{
	float4 result;
	float timer = currentTime(pos);
	
	for(int i = -1; i < _SwellZoneBuffer; i++)
	{
		int id = floor(timer) - i;
		float t = -idToPos(id, timer);	// 波打ち際において波頭の位置情報は原点からの経過時間
		
		wave w;
		waveCurve(w, id, float2(-t, pos.y));
		
		#if DEBUG_TRIANGLE
		// 最も波が伸びる状態
		float power = 1.5;
		#else
		// 折り返し型グラデーション
		float power = absfbm1d(pos.y / _FrontWaveWidth, id, _FrontWaveNoiseLevel) + 1.0;
		#endif
		
		float d = power / (_BackwashVelocity * 2);	// 波の前線が最も伸びた状態になったときの経過時間
		float amount = saturate(t / d);
		float smoothAmount = SMOOTH(amount);
		float td = min(t, d);	// 波が伸び切ったときにキャップが働く時間軸
		
		float nearshore = getFrontFormLength(w); // 砕波帯から入ってくる波の形
		float foreshore = power * _Interval * 0.5; // 波打ち際の波が伸び切ったときの形
		float shoreOffset = power * td - POW2(td) * _BackwashVelocity;	// 波が最大到達点まで伸びるカーブ
		float b = nearshore * 0.5 * (1 - amount);
		
		float swashLine = lerp(nearshore + t, foreshore + shoreOffset, smoothAmount);	// 波のカーブを入ってくる波の形と繋げる
		float backwashLine = swashLine - POW2(max((t - d) * 0.125, 0));
		
		float shoreMask = step(-pos.x, backwashLine);	// 水面が戻っていく動きに対応したマスク
		result.y += max(0, frontForm(pos.x, -swashLine + b, b, b * (1 - amount)) - amount);
		
		UNITY_BRANCH if(fragments == false)
		{
			result.z = max(result.z, max((1 - smoothAmount) * (1 - result.y), 0.01) * shoreMask);
		}
		else
		{
			float shoreAndFormMask = step(-pos.x, swashLine);	// 取り残される泡を含むマスク
			
			float distance = nearshore + t;
			result.x = max(result.x, saturate(1 - distance / _BackFormLength) * shoreAndFormMask);
			
			float endTime = -t + _Interval * _SwellZoneBuffer;
			float frontToBack = saturate((pos.x + swashLine) / (swashLine - backwashLine));
			result.w = max(result.w, frontToBack * saturate(endTime * 0.1));
			result.z = max(result.z, shoreMask * saturate(endTime * 0.2 - 2));
		}
	}
	
	UNITY_BRANCH if(fragments == false)
	{
		result.z *= result.z;
	}
	
	return result;
}

float vertexHeight(float2 pos)
{
	float timer = currentTime(pos);
	
	// 砕波帯の高さ変化
	wave w;
	
	#if DEBUG_WAVE_VISUALIZE
	waveCurve(w, ceil(_ManualTimeOffset), pos);
	return w.height;
	#endif
	
	int id = floor(timer + ((pos.x + _Interval * 0.5) / _Interval));
	float wx = idToPos(id, timer);
	waveCurve(w, id, float2(wx, pos.y));
	
	float steepness = 1 / _Steepness;
	steepness = steepness / (1 + steepness * POW2(wx)) * _Steepness;
	
	float distance = pos.x - wx;
	float rationalBump = w.height / (1 + w.height * steepness * POW2(distance));
	float triangleWave = 1 - abs(distance) / _Interval * 2;
	float smooth = SMOOTH(triangleWave);
	
	float nearWaves = rationalBump * smooth;
	
	// 白波に変化する前の波
	float t = (pos.x + timer * _Interval) / _Interval;
	float farWavesTriangle = abs(frac(t) - 0.5) * 2;	// 三角波
	farWavesTriangle *= farWavesTriangle;
	float farWaves = SMOOTH(farWavesTriangle);
	int farWavesID = round(t);
	
	// 沖合の波の描画範囲
	float farWavesStartLine = breakerOffset(1);
	float farWavesFalloff = (min(pos.x, farWavesStartLine + _FarWaveDecayDistance) - farWavesStartLine) - _FarWaveDecayDistance;
	farWavesFalloff *= farWavesFalloff;
	farWavesFalloff /= POW2(_FarWaveDecayDistance);
	
	float farWavesBlend = smoothstep(farWavesStartLine - _Interval, farWavesStartLine, pos.x) * farWavesFalloff;
	
	// 波打ち際の波
	float shorelineBlend = smoothstep(-_Interval, 0, -pos.x);
	float frontHeight = shoreline(pos, false).z * 0.5;
	
	// 全フェーズの波を合成
	float result;
	result = nearWaves + (farWaves * wavePower(farWavesID, pos.y) * farWavesBlend * _FarWaveHeight) + _SeaLevel;
	result = lerp(result, -pos.x * _BeachSlope + frontHeight, shorelineBlend);
	return result;
}

// xy : 仮想平面のlocalPos
// z : 水面から平面までの深度
float3 ParallaxPlane(float3 surface, float3 viewDir, float3 planeNormal, float3 surfaceNormal)
{
	float3 dir = refract(viewDir, surfaceNormal, _InvIOR);
	float3 hit = -dot(surface, planeNormal) / dot(dir, planeNormal) * dir + surface;
	
	return float3(hit.xz, length(hit - surface));
}

// x : 水面上の泡
// y : 前線の泡
float2 formMask(float2 pos)
{
	float2 result = 0;
	float timer = currentTime(pos);
	
	#if DEBUG_WAVE_VISUALIZE
	wave w;
	waveCurve(w, ceil(_ManualTimeOffset), pos);
	return float2(w.comp, 0);
	#endif
	
	for(int i = 0; i < _NumberOfWaves; i++)
	{
		wave w;
		wave l;
		
		int id = floor(timer) + i;
		float wx = idToPos(id, timer);
		waveCurve(w, id, float2(wx, pos.y));
		waveCurve(l, id, pos);
		
		float frontFormLength = getFrontFormLength(w);
		float distance = (pos.x + frontFormLength) - wx;
		float back = smoothstep(0, distance * 0.02, l.comp * 10);
		
		// 高さ
		float front = frontForm(pos.x, wx, frontFormLength, frontFormLength * 0.5);
		result.y += front;
		
		float backForm = saturate(1 - distance / _BackFormLength) * step(wx - frontFormLength, pos.x) * back * saturate((wx + _Interval) * 0.1);
		result.x = max(result.x, backForm);
	}
	
	return result;
}

float3 waterSurfaceNormal(float2 pos, float mask)
{
	float t = _Time.x;
	
	float4 tex = tex2D(_Normal, pos * 0.02 + t * 0.7);
	tex += tex2D(_Normal, pos.yx * 0.02 - t * 0.3);
	tex += tex2D(_Normal, -pos * 0.01 - float2(0, t * 0.5));
	tex += tex2D(_Normal, -pos.yx * 0.01 - float2(-t * 0.5, 0));
	tex += tex2D(_Normal, -pos * 0.001 - float2(-t * -0.3, 0));
	tex += tex2D(_Normal, -pos.yx * 0.001 - float2(0, -t * 0.5));
	
	return lerp(float3(0, 0, 1), UnpackNormal(tex * 0.166667), _NormalAmount * mask);
}

float showBorderLine(float x)
{
	float thickness = 0.08;
	
	float result = step(1 - thickness, 1 - abs(x));
	result += step(1 - thickness, 1 - abs(x - breakerOffset(1) - _FarWaveDecayDistance));
	result += step(1 - thickness, 1 - abs(x - breakerOffset(1)));
	result += step(1 - thickness, 1 - abs(x + 9 / (16 * _BackwashVelocity) + _Interval * 0.75));	// 前線の最大到達点の計算 (1.5 * _Interval * 0.5) + (1.5 * (1.5 / (_BackwashVelocity * 2)) - POW2(1.5 / (_BackwashVelocity * 2)) * _BackwashVelocity) と同義
	return result;
}

#endif
