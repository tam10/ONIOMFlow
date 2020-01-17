Shader "Custom/Rim" {
    Properties {
        [HDR] _Color ("Color", Color) = (1.,1.,1.,1.)
    }
    SubShader {
        Zwrite Off
        
        LOD 200
        //Blend SrcAlpha OneMinusSrcAlpha
        Blend SrcAlpha OneMinusSrcAlpha
 
        Pass {
            Tags { "RenderType"="Transparent" "Queue"="Transparent" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                float4 vertex : SV_POSITION;
                float4 vertexColor : COLOR;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            float4 _Color;
            v2f vert (float4 color : COLOR, float4 vertex : POSITION, float3 normal : NORMAL) {
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex);
                o.vertexColor = color * _Color;

                o.worldPos = mul(unity_ObjectToWorld, vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(normal);
                
                return o;
            }

                
            float4 frag (v2f i) : SV_Target {
                float4 col;
                half3 worldViewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half a = 1 - (dot(i.worldNormal, worldViewDir));

                col = i.vertexColor;
                col.a = col.a * a * a;
                return col;

            }
            ENDCG
        }

    }
    //FallBack "Diffuse"
}