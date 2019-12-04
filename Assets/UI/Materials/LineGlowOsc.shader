Shader "Unlit/LineGlowOsc" {
	Properties {
        _GlowAmount ("Glow Amount", Range(0, 2)) = 1
        _GlowSpeed ("Glow Speed", Range(1, 4)) = 2
        [Toggle(USE_X)] _UseX ("Use UV.x", Float) = 0
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader {
		Tags { "RenderType" = "Background" "Queue" = "Transparent" }
		LOD 100

		Pass {
			ZWrite Off
			Blend SrcAlpha OneMinusSrcAlpha
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
            float _GlowSpeed;
            float _GlowAmount;
            float _UseX;
			float4 _MainTex_ST;
			
			v2f vert (appdata v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
				
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target {
				fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                #ifdef USE_X
                    return lerp(
                        col, 
                        (col * pow(2, _GlowAmount)), 
                        0.5 + 0.5 * sin( _GlowSpeed * 3.14 * (i.uv.y - _Time.y * 0.5) ) 
                    );
                #else
                    return lerp(
                        col, 
                        (col * pow(2, _GlowAmount)), 
                        0.5 + 0.5 * sin( _GlowSpeed * 3.14 * (i.uv.x - _Time.y * 0.5) ) 
                    );
                #endif
			}
			ENDCG
		}
	}
}
