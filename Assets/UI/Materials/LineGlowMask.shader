Shader "Unlit/LineGlowStencil" {
	Properties {
        _GlowAmount ("Glow Amount", Range(0, 2)) = 1
		[PerRendererData] _MainTex ("Texture", 2D) = "white" {}
         
        _Stencil ("Stencil Mask", Float) = 4
        _StencilReadMask ("Stencil Read Mask", Float) = 1
	}
	SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Transparent+5"}
		LOD 200
        
		Pass {

            Stencil {
                Ref [_Stencil]
                Comp Equal
                //Pass Zero

                ReadMask [_StencilReadMask]
            }

			//Blend SrcAlpha OneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

            #pragma shader_feature USE_X
			
			#include "UnityCG.cginc"


			struct appdata {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
                fixed4 color : COLOR0;
			};

			sampler2D _MainTex;
            float _GlowAmount;
			float4 _MainTex_ST;
			
			v2f vert (appdata v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * pow(2, _GlowAmount);
				
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target {
				return tex2D(_MainTex, i.uv) * i.color;
			}
			ENDCG
		}
	}
}
