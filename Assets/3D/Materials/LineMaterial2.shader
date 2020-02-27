Shader "Unlit/LineMaterial2" {
    SubShader {    
    	Tags {"Queue" = "Geometry" "RenderType"="Opaque"}
        Pass {
        	Cull Off
			ZWrite On
			ZTest LEqual
    		Lighting Off
         	Blend SrcAlpha OneMinusSrcAlpha 
                    
			CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "UnityCG.cginc"


 			struct Input {
            	float4 vertex : POSITION;
            	float4 colour : COLOR;
            	float4 texcoord : TEXCOORD0;
        	};

			struct Output {
			    float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
				half4 colour : COLOR; 
			};

			half oscHDR (half c) {
				if (c > 1) {
					c = (c - 1) * (_SinTime.w * _SinTime.w) + 1;
				}
				return c;
			}

			Output vert (Input v) {
			    Output o;
			    
			    o.position = UnityObjectToClipPos(v.vertex);
			    o.uv = v.texcoord;
 				o.colour = v.colour;
				 
				o.colour.r = oscHDR(o.colour.r);
				o.colour.g = oscHDR(o.colour.g);
				o.colour.b = oscHDR(o.colour.b);
			    
			    return o;
			}

			half4 frag (Output o) : COLOR {
				half4 c = o.colour;
				return c;
			}


            ENDCG
        }
    }
}