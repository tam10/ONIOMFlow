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
				float4 colour : COLOR; 
			};


			Output vert (Input v) {
			    Output o;
			    
			    o.position = UnityObjectToClipPos(v.vertex);
			    o.uv = v.texcoord;
 				o.colour = v.colour;
			    
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