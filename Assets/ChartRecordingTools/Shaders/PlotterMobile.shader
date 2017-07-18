/**
ChartRecordingTools

Copyright (c) 2017 Sokuhatiku

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

Shader "UI/ChartRecorder/PlotterMobile"
{
	Properties
	{
		_Color("Color", COLOR) = (1,1,1,1)
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest[unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			Name "Default"
		CGPROGRAM
			#pragma target 3.5
//			#pragma enable_d3d11_debug_symbols

			#pragma vertex vert
	        #pragma fragment frag

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			fixed4 _Color;
			float4 _ClippingRect;
			float4x4 _S2LMatrix;
			float4x4 _L2WMatrix;

			struct AppData
			{
				float4 pos : POSITION;			
			};

			struct VSOut 
			{
				float4 pos : SV_POSITION;
				float4 wPos : TEXCOORD0;
	        };

			VSOut vert (AppData i)
			{
				VSOut o;

				//o.wPos = mul(_L2WMatrix, mul(_S2LMatrix, i.pos));
				//o.pos = mul(UNITY_MATRIX_VP, o.wPos);

				float4 originalPosition = float4(i.pos.x, i.pos.y, 0, 1);

				o.wPos = mul(_S2LMatrix, originalPosition);
				o.pos  = mul(UNITY_MATRIX_VP, mul(_L2WMatrix, o.wPos));

//				o.pos = mul(UNITY_MATRIX_VP, i.pos);
//				o.pos = mul(_L2WMatrix, i.pos);
//				o.pos = UnityObjectToClipPos(i.pos);
				o.pos = mul(UNITY_MATRIX_VP, i.pos);

				return o;
			}

			fixed4 frag (VSOut i) : COLOR
	        {
				//clip(
				//	min(
				//		1,
				//		UnityGet2DClipping(i.wPos.xy, _ClippingRect) - 0.5
				//	)
				//);
				return _Color;
	        }
		ENDCG
		}
	}
}
