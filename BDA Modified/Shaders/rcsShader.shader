﻿// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/rcsShader"
{
	Properties
	{
		_RCSCOLOR("RCSCOLOR", Color) = (1, 1, 1, 1)
		_LIGHTDIR("LIGHTDIR", Vector) = (0, 0, 1)
	}

		SubShader
	{
		Pass
		{
			CGPROGRAM

			// DEFINES
			#pragma target 3.5
			#pragma vertex RCSVertexShader
			#pragma fragment RCSFragmentShader

			// INCLUDES
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
			#include "UnityStandardBRDF.cginc"

			// uniforms from properties
			float4 _RCSCOLOR;
			float3 _LIGHTDIR;

			struct VertexData
			{
				float4 position : POSITION;
				float3 normal : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct Interpolators
			{
				float4 position : SV_POSITION;
				float3 normal : TEXCOORD1;
			};

			// Main Vertex Program
			Interpolators RCSVertexShader(VertexData v)
			{
				Interpolators i;
				i.position = UnityObjectToClipPos(v.position);
				i.normal = UnityObjectToWorldNormal(v.normal);
                return i;
			}

			// Main Fragment Program
			float4 RCSFragmentShader(Interpolators i) : SV_TARGET
			{
				float3 reflectionDir = DotClamped(_LIGHTDIR, reflect(-_LIGHTDIR, i.normal));
				half returnStr = max(0, reflectionDir);
				float4 finalreturn = returnStr * _RCSCOLOR; //actually make it use _RCSCOLOR
				return finalreturn;
			}
			ENDCG
		} //PASS
	} //SUBSHADER
}