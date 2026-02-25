// Made with Amplify Shader Editor v1.9.9.5
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "GPU Grass/Surface Mask"
{
	Properties
	{
		_GrassHeightMap( "Grass Detail Map", 2D ) = "white" {}
		[Header(Mask R)] _NormalMaskRMin( "Normal Mask Min", Range( 0, 1 ) ) = 0
		_NormalMaskRMax( "Normal Mask Max", Range( 0, 1 ) ) = 0
		_HeightRMin( "Detail Height Min", Float ) = 0
		_HeightRMax( "Detail Height Max", Float ) = 1
		[Header(Mask G)] _NormalMaskGMin( "Normal Mask Min", Range( 0, 1 ) ) = 0
		_NormalMaskGMax( "Normal Mask Max", Range( 0, 1 ) ) = 0
		_HeightGMin( "Detail Height Min", Float ) = 0
		_HeightGMax( "Detail Height Max", Float ) = 1
		[Header(Mask B)] _NormalMaskBMin( "Normal Mask Min", Range( 0, 1 ) ) = 0
		_NormalMaskBMax( "Normal Mask Max", Range( 0, 1 ) ) = 0
		_HeightBMin( "Detail Height Min", Float ) = 0
		_HeightBMax( "Detail Height Max", Float ) = 1
		[Enum(UnityEngine.Rendering.CullMode)] _Culling( "Culling", Float ) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

	}

	SubShader
	{
		

		Tags { "RenderType"="Opaque" }

	LOD 0

		

		Blend Off
		AlphaToMask Off
		Cull [_Culling]
		ColorMask RGBA
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		

		CGINCLUDE
			#pragma target 3.5

			float4 ComputeClipSpacePosition( float2 screenPosNorm, float deviceDepth )
			{
				float4 positionCS = float4( screenPosNorm * 2.0 - 1.0, deviceDepth, 1.0 );
			#if UNITY_UV_STARTS_AT_TOP
				positionCS.y = -positionCS.y;
			#endif
				return positionCS;
			}
		ENDCG

		
		Pass
		{
			Name "Unlit"

			CGPROGRAM
				#define ASE_VERSION 19905

				#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
					#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
				#endif
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing
				#include "UnityCG.cginc"

				#define ASE_NEEDS_FRAG_COLOR
				#define ASE_NEEDS_TEXTURE_COORDINATES0


				struct appdata
				{
					float4 vertex : POSITION;
					float4 ase_color : COLOR;
					float4 ase_texcoord : TEXCOORD0;
					float3 ase_normal : NORMAL;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f
				{
					float4 pos : SV_POSITION;
					float4 ase_color : COLOR;
					float4 ase_texcoord : TEXCOORD0;
					float4 ase_texcoord1 : TEXCOORD1;
					float3 ase_normal : NORMAL;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				uniform float _Culling;
				uniform sampler2D _GrassHeightMap;
				uniform float4 _GrassHeightMap_ST;
				uniform float _HeightRMin;
				uniform float _HeightRMax;
				uniform float _HeightGMin;
				uniform float _HeightGMax;
				uniform float _HeightBMin;
				uniform float _HeightBMax;
				uniform float _NormalMaskRMin;
				uniform float _NormalMaskRMax;
				uniform float _NormalMaskGMin;
				uniform float _NormalMaskGMax;
				uniform float _NormalMaskBMin;
				uniform float _NormalMaskBMax;


				
				v2f vert ( appdata v )
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID( v );
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
					UNITY_TRANSFER_INSTANCE_ID( v, o );

					float3 ase_positionWS = mul( unity_ObjectToWorld, float4( ( v.vertex ).xyz, 1 ) ).xyz;
					o.ase_texcoord.xyz = ase_positionWS;
					
					o.ase_color = v.ase_color;
					o.ase_texcoord1.xy = v.ase_texcoord.xy;
					o.ase_normal = v.ase_normal;
					
					//setting value to unused interpolator channels and avoid initialization warnings
					o.ase_texcoord.w = 0;
					o.ase_texcoord1.zw = 0;

					float3 vertexValue = float3( 0, 0, 0 );
					#if ASE_ABSOLUTE_VERTEX_POS
						vertexValue = v.vertex.xyz;
					#endif
					vertexValue = vertexValue;
					#if ASE_ABSOLUTE_VERTEX_POS
						v.vertex.xyz = vertexValue;
					#else
						v.vertex.xyz += vertexValue;
					#endif

					o.pos = UnityObjectToClipPos( v.vertex );
					return o;
				}

				half4 frag( v2f IN  ) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID( IN );
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
					half4 finalColor;

					float4 ScreenPosNorm = float4( IN.pos.xy * ( _ScreenParams.zw - 1.0 ), IN.pos.zw );
					float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, IN.pos.z ) * IN.pos.w;
					float4 ScreenPos = ComputeScreenPos( ClipPos );

					float3 ase_positionWS = IN.ase_texcoord.xyz;
					float3 worldToView2 = mul( UNITY_MATRIX_V, float4( ase_positionWS, 1 ) ).xyz;
					float4 appendResult4 = (float4(IN.ase_color.r , IN.ase_color.g , IN.ase_color.b , worldToView2.z));
					float2 uv_GrassHeightMap = IN.ase_texcoord1.xy * _GrassHeightMap_ST.xy + _GrassHeightMap_ST.zw;
					float4 tex2DNode6 = tex2D( _GrassHeightMap, uv_GrassHeightMap );
					float3 appendResult62 = (float3(saturate(  (0.0 + ( tex2DNode6.r - _HeightRMin ) * ( 1.0 - 0.0 ) / ( _HeightRMax - _HeightRMin ) ) ) , saturate(  (0.0 + ( tex2DNode6.g - _HeightGMin ) * ( 1.0 - 0.0 ) / ( _HeightGMax - _HeightGMin ) ) ) , saturate(  (0.0 + ( tex2DNode6.b - _HeightBMin ) * ( 1.0 - 0.0 ) / ( _HeightBMax - _HeightBMin ) ) )));
					float3 objToViewDir28 = normalize( mul( UNITY_MATRIX_IT_MV, float4( IN.ase_normal, 0.0 ) ).xyz );
					float dotResult17 = dot( float3( 0,0,1 ) , objToViewDir28 );
					float temp_output_57_0 = abs( dotResult17 );
					float3 appendResult61 = (float3(saturate(  (0.0 + ( temp_output_57_0 - _NormalMaskRMin ) * ( 1.0 - 0.0 ) / ( _NormalMaskRMax - _NormalMaskRMin ) ) ) , saturate(  (0.0 + ( temp_output_57_0 - _NormalMaskGMin ) * ( 1.0 - 0.0 ) / ( _NormalMaskGMax - _NormalMaskGMin ) ) ) , saturate(  (0.0 + ( temp_output_57_0 - _NormalMaskBMin ) * ( 1.0 - 0.0 ) / ( _NormalMaskBMax - _NormalMaskBMin ) ) )));
					float4 appendResult15 = (float4(( appendResult62 * sign( dotResult17 ) * appendResult61 ) , 1.0));
					

					finalColor = ( appendResult4 * appendResult15 );

					return finalColor;
				}
			ENDCG
		}
	}
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
	
	Fallback Off
}
/*ASEBEGIN
Version=19905
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;44;-2784,1024;Inherit;False;Property;_NormalMaskBMax;Normal Mask Max;10;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;43;-2784,944;Inherit;False;Property;_NormalMaskBMin;Normal Mask Min;9;1;[Header];Create;False;1;Mask B;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;42;-2784,864;Inherit;False;Property;_NormalMaskGMax;Normal Mask Max;6;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;41;-2784,784;Inherit;False;Property;_NormalMaskGMin;Normal Mask Min;5;1;[Header];Create;False;1;Mask G;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;40;-2784,688;Inherit;False;Property;_NormalMaskRMax;Normal Mask Max;2;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;39;-2784,608;Inherit;False;Property;_NormalMaskRMin;Normal Mask Min;1;1;[Header];Create;False;1;Mask R;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;25;-3104,400;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.AbsOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;57;-2544,496;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;6;-2816,-336;Inherit;True;Property;_GrassHeightMap;Grass Detail Map;0;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;11;-2752,-64;Inherit;False;Property;_HeightRMax;Detail Height Max;4;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;10;-2752,-144;Inherit;False;Property;_HeightRMin;Detail Height Min;3;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;54;-2752,16;Inherit;False;Property;_HeightGMin;Detail Height Min;7;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;53;-2752,96;Inherit;False;Property;_HeightGMax;Detail Height Max;8;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;56;-2752,176;Inherit;False;Property;_HeightBMin;Detail Height Min;11;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;55;-2752,256;Inherit;False;Property;_HeightBMax;Detail Height Max;12;0;Create;False;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;38;-2400,560;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;45;-2400,752;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;46;-2400,928;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TransformDirectionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;28;-2912,400;Inherit;False;Object;View;True;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;9;-2416,-288;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;49;-2416,-112;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;51;-2416,64;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;5;-2224,-816;Inherit;False;660;410.2;(RGB) - Grass Types Heights   (A) - World Heightmap;4;1;2;3;4;Output Data;1,1,1,1;0;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;23;-2208,560;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;47;-2208,752;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;48;-2208,928;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;17;-2672,400;Inherit;False;2;0;FLOAT3;0,0,1;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;12;-2240,-288;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;50;-2240,-112;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;52;-2240,64;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;61;-2000,560;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SignOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;58;-1968,400;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;1;-2176,-592;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;62;-2032,16;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;59;-1824,368;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.VertexColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;3;-1968,-768;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TransformPositionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;2;-2000,-592;Inherit;False;World;View;False;Fast;True;1;0;FLOAT3;0,0,0;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;15;-1648,368;Inherit;False;FLOAT4;4;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;4;-1744,-592;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;8;-1456,-112;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;63;-1632,512;Inherit;False;Property;_Culling;Culling;13;1;[Enum];Create;True;0;0;1;UnityEngine.Rendering.CullMode;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;-1280,-112;Float;False;True;-1;3;AmplifyShaderEditor.MaterialInspector;0;5;GPU Grass/Surface Mask;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;False;True;0;1;False;;0;False;;0;1;False;;0;False;;True;0;False;;0;False;;False;False;False;False;False;False;False;False;False;True;0;False;;True;True;2;True;_Culling;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;True;True;1;False;;True;0;False;;True;True;0;False;;0;False;;True;1;RenderType=Opaque=RenderType;True;3;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;0;;0;0;Standard;1;Vertex Position;1;0;0;1;True;False;;False;0
WireConnection;57;0;17;0
WireConnection;38;0;57;0
WireConnection;38;1;39;0
WireConnection;38;2;40;0
WireConnection;45;0;57;0
WireConnection;45;1;41;0
WireConnection;45;2;42;0
WireConnection;46;0;57;0
WireConnection;46;1;43;0
WireConnection;46;2;44;0
WireConnection;28;0;25;0
WireConnection;9;0;6;1
WireConnection;9;1;10;0
WireConnection;9;2;11;0
WireConnection;49;0;6;2
WireConnection;49;1;54;0
WireConnection;49;2;53;0
WireConnection;51;0;6;3
WireConnection;51;1;56;0
WireConnection;51;2;55;0
WireConnection;23;0;38;0
WireConnection;47;0;45;0
WireConnection;48;0;46;0
WireConnection;17;1;28;0
WireConnection;12;0;9;0
WireConnection;50;0;49;0
WireConnection;52;0;51;0
WireConnection;61;0;23;0
WireConnection;61;1;47;0
WireConnection;61;2;48;0
WireConnection;58;0;17;0
WireConnection;62;0;12;0
WireConnection;62;1;50;0
WireConnection;62;2;52;0
WireConnection;59;0;62;0
WireConnection;59;1;58;0
WireConnection;59;2;61;0
WireConnection;2;0;1;0
WireConnection;15;0;59;0
WireConnection;4;0;3;1
WireConnection;4;1;3;2
WireConnection;4;2;3;3
WireConnection;4;3;2;3
WireConnection;8;0;4;0
WireConnection;8;1;15;0
WireConnection;0;0;8;0
ASEEND*/
//CHKSM=45DF6A2F11A4C24004F07E99842762A780ECEEC6