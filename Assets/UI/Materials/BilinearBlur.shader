Shader "Custom/BilinearBlur" {
    Properties {
        _Radius("Radius", Range(1, 10)) = 1
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _GrabTexture;
    float4 _MainTex_TexelSize;
    float4 _GrabTexture_TexelSize;

    //const float3 offset = float3(0.0, 1.3846153846, 3.2307692308);
    //const float3 weight = float3(0.2270270270, 0.3162162162, 0.0702702703);

    struct appdata {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        float4 color : COLOR;
    };

    struct v2f {
        float2 uv : TEXCOORD0;
        float4 grabPassUV : TEXCOORD1;
        float4 vertex : SV_POSITION;
        float4 color : COLOR;
    };
    
    v2f vert (appdata v) {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        o.grabPassUV = ComputeGrabScreenPos(o.vertex);
        o.color = v.color;
        return o;
    }

    half4 blur_v(v2f IN) : SV_TARGET {
        float3 offset = float3(0.0, 1.3846153846, 3.2307692308);
        float3 weight = float3(0.2270270270, 0.3162162162, 0.0702702703);
        half4 c;
        for (int i = 0; i < 3; i++) {
            c += tex2D(_GrabTexture, IN.grabPassUV + float2(0,  _GrabTexture_TexelSize.y*offset[i])) * weight[i];
            c += tex2D(_GrabTexture, IN.grabPassUV + float2(0, -_GrabTexture_TexelSize.y*offset[i])) * weight[i];
        }
        return c;
    }

    half4 blur_h(v2f IN) : SV_TARGET {
        float3 offset = float3(0.0, 1.3846153846, 3.2307692308);
        float3 weight = float3(0.2270270270, 0.3162162162, 0.0702702703);
        half4 c;
        for (int i = 0; i < 3; i++) {
            c += tex2D(_GrabTexture, IN.grabPassUV + float2( _GrabTexture_TexelSize.x*offset[i], 0)) * weight[i];
            c += tex2D(_GrabTexture, IN.grabPassUV + float2(-_GrabTexture_TexelSize.x*offset[i], 0)) * weight[i];
        }
        return c;
    }



    half4 gaussian_filter(float2 uv, float2 stride)
    {
        half4 s = tex2D(_GrabTexture, uv) * 0.227027027;

        float2 d1 = stride * 1.3846153846;
        s += tex2D(_GrabTexture, uv + d1) * 0.3162162162;
        s += tex2D(_GrabTexture, uv - d1) * 0.3162162162;

        float2 d2 = stride * 3.2307692308;
        s += tex2D(_GrabTexture, uv + d2) * 0.0702702703;
        s += tex2D(_GrabTexture, uv - d2) * 0.0702702703;

        return s;
    }

    // Quarter downsampler
    half4 frag_quarter(v2f_img i) : SV_Target
    {
        float4 d = _GrabTexture_TexelSize.xyxy * float4(1, 1, -1, -1);
        half4 s;
        s  = tex2D(_GrabTexture, i.uv + d.xy);
        s += tex2D(_GrabTexture, i.uv + d.xw);
        s += tex2D(_GrabTexture, i.uv + d.zy);
        s += tex2D(_GrabTexture, i.uv + d.zw);
        return s * 0.25;
    }

    // Separable Gaussian filters
    half4 frag_blur_h(v2f_img i) : SV_Target
    {
        return gaussian_filter(i.uv, float2(_GrabTexture_TexelSize.x, 0));
    }

    half4 frag_blur_v(v2f_img i) : SV_Target
    {
        return gaussian_filter(i.uv, float2(0, _GrabTexture_TexelSize.y));
    }


    ENDCG

    SubShader {
        Tags{ "Queue" = "Transparent+2100" "IgnoreProjector"="True" "RenderType" = "Transparent" }
        GrabPass {
            "_GrabTexture"
        }
        Tags{ "Queue" = "Transparent+2500" "IgnoreProjector"="True" "RenderType" = "Transparent" }
        Pass {
            //Blend SrcAlpha OneMinusSrcAlpha
            //ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment blur_v
            #pragma target 3.0
            ENDCG
        }
        Pass {
            //Blend SrcAlpha OneMinusSrcAlpha
            //ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment blur_h
            #pragma target 3.0
            ENDCG
        }
        //Pass
        //{
        //    ZTest Always Cull Off ZWrite Off
        //    CGPROGRAM
        //    #pragma vertex vert_img
        //    #pragma fragment frag_quarter
        //    ENDCG
        //}
        //Pass
        //{
        //    ZTest Always Cull Off ZWrite Off
        //    CGPROGRAM
        //    #pragma vertex vert_img
        //    #pragma fragment frag_blur_h
        //    #pragma target 3.0
        //    ENDCG
        //}
        //Pass
        //{
        //    ZTest Always Cull Off ZWrite Off
        //    CGPROGRAM
        //    #pragma vertex vert_img
        //    #pragma fragment frag_blur_v
        //    #pragma target 3.0
        //    ENDCG
        //}
    }
}