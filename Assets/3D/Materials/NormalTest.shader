Shader "Custom/NormalTest" {
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                half3 normal : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (float4 vertex : POSITION, float3 normal : NORMAL)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.normal = UnityObjectToWorldNormal(normal);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = 1;
                c.rgb = i.normal*0.5+0.5;
                return c;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
} 