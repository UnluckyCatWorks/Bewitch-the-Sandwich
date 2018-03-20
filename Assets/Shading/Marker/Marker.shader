﻿Shader "Unlit/Marker"
{
	Properties
	{
		[PerRendererData]
		_Alpha ("Alpha value", Range(0, 1)) = 1.0
		_Color ("Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Pass
		{
			Cull Off
			//Blend One OneMinusSrcAlpha
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			// #pragma multi_compile_fog
			#include "UnityCG.cginc"
			struct appdata 
			{
				float4 vertex : POSITION;
			};
			struct v2f 
			{
				float4 vertex : SV_POSITION;
				float height : TEXCOORD1;
				// UNITY_FOG_COORDS(1)
			};
			fixed4 _Color;
			float _Alpha;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.height = v.vertex.y;

				//UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			fixed4 frag (v2f i) : SV_Target
			{
				// apply fog
				// UNITY_APPLY_FOG(i.fogCoord, col);

				fixed4 color = _Color;
				color.a *= smoothstep (3, 0, i.height) * _Alpha;
				return color;
			}
			ENDCG
		}
	}
}
