Shader "Custom/PlasticPulse" {
    Properties {
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Brightness ("Brightness", Range(0.25, 4)) = 1.0
    }

    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        
        //Use this pass to stop Z clipping
        Pass {
            Zwrite On
            ColorMask 0
        }

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert fullforwardshadows
        #pragma target 3.0
        
        struct Input {
            float2 uv_MainTex;
            float4 vertexColor; // Vertex color stored here by vert() method
        };

        half _Brightness;

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.vertexColor.rgb = v.color * _Brightness;
            o.vertexColor.a = v.color.a;
        }

        half _Glossiness;
        half _Metallic;

        void surf (Input IN, inout SurfaceOutputStandard o) 
        {
            o.Albedo = IN.vertexColor; // Combine normal color with the vertex color
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = IN.vertexColor.a;
        }
        ENDCG
    } 
    FallBack "Diffuse"
 }