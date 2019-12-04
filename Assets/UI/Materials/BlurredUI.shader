Shader "Custom/BlurredUI" {
    Properties
    {
        _Radius("Radius", Range(1, 255)) = 1
    }
    Category
    {
        Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }
   
        SubShader
        {
            GrabPass
            {
                Tags{ "LightMode" = "Always" }
            }
            Pass
            {
                Tags{ "LightMode" = "Always" }
                Blend SrcAlpha OneMinusSrcAlpha
                //Blend One One
 
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma fragmentoption ARB_precision_hint_fastest
                #include "UnityCG.cginc"
                struct appdata_t
                {
                    float4 vertex : POSITION;
                    float2 texcoord: TEXCOORD0;
                    fixed4 color : COLOR;
               };
                struct v2f
                {
                    float4 pos : SV_POSITION;
                    float4 grabPos : TEXCOORD0;
                    fixed4 color : COLOR;
                };
                v2f vert(appdata_t v)
                {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.grabPos = ComputeGrabScreenPos(o.pos);

                    o.color = v.color;

                    return o;
                }
                sampler2D _GrabTexture;
                float4 _GrabTexture_TexelSize;
                float _Radius;
                half4 frag(v2f i) : COLOR
                {
                    half4 sum = half4(0,0,0,0);
                    #define GRABXYPIXEL(kernelx, kernely) tex2Dproj( _GrabTexture, float4(i.grabPos.x + _GrabTexture_TexelSize.x * kernelx, i.grabPos.y + _GrabTexture_TexelSize.y * kernely, i.grabPos.z, i.grabPos.w))

                    int measurments = 1;
                   
                    for (float range = 0.2f; range <= _Radius; range += 0.2f)
                    {
                        sum += GRABXYPIXEL(range, range);
                        sum += GRABXYPIXEL(range, -range);
                        sum += GRABXYPIXEL(-range, range);
                        sum += GRABXYPIXEL(-range, -range);
                        measurments += 4;
                    }

                    sum *= i.color / measurments;
                    sum.a = i.color.a;

                    return sum;
                }
                ENDCG
            }
        }
    }
}