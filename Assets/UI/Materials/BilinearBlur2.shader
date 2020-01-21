Shader "Custom/BilinearBlur2" {
    Properties {
        _Radius("Radius", Range(1, 10)) = 1
		_MainTex ("Texture", 2D) = "white" {}
    }
 
    SubShader {
        Tags{ "Queue" = "Transparent+100" "IgnoreProjector"="True" "RenderType" = "Transparent" }
        GrabPass {
            Tags {"LightMode"="Always"}
        }
        //Tags{ "Queue" = "Transparent+2500" "IgnoreProjector"="True" "RenderType" = "Transparent" }

        Pass {
            //Tags {"LightMode"="Always"}
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_grab
            #pragma fragment frag_quarter
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _GrabTexture;
            float4 _MainTex_TexelSize;
            float4 _GrabTexture_TexelSize;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f_grab {
                float2 uv : TEXCOORD0;
                float4 grabPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
            
            v2f_grab vert_grab (appdata v) {
                v2f_grab o;
                o.color = v.color;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;// TRANSFORM_TEX(v.uv, _MainTex);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }
        
            // Quarter downsampler
            half4 frag_quarter(v2f_grab IN) : SV_Target {
        
                float4 d = _GrabTexture_TexelSize.xyxy * float4(1, 1, -1, -1);
                half4 s  = tex2D(_GrabTexture, IN.grabPos + d.xy);
                s += tex2D(_GrabTexture, IN.grabPos + d.xw);
                s += tex2D(_GrabTexture, IN.grabPos + d.zy);
                s += tex2D(_GrabTexture, IN.grabPos + d.zw);
                return s * 0.25;
            }

            ENDCG
        }

        GrabPass {
            Tags {"LightMode"="Always"}
        }

        Pass {
            //Tags {"LightMode"="Always"}
            //Blend SrcAlpha OneMinusSrcAlpha
            //ZTest Always Cull Off ZWrite On
            CGPROGRAM
            #pragma vertex vert_grab
            #pragma fragment frag_blur_h           
            #pragma fragmentoption ARB_precision_hint_fastest
            //#pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;
            float4 _GrabTexture_ST;
            float _Radius;

            static const float offsets[5] = {0,1.409090909,3.318181818,5.227272727,7.136363636};
            static const float weights[5] = {0.168191624,0.237193316,0.081323423,0.012557293,0.000734345};

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f_grab {
                float2 uv : TEXCOORD0;
                float4 grabPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
            
            v2f_grab vert_grab (appdata v) {
                v2f_grab o;
                o.color = v.color;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _GrabTexture);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }
        
            half4 frag_blur_h(v2f_grab IN) : SV_Target {
                half4 c = 0;
                for (int i = 0; i < 5; i++) {
                    c += tex2Dproj(_GrabTexture, IN.grabPos + float4( _Radius*_GrabTexture_TexelSize.x*offsets[i], 0, 0, 0)) * weights[i];
                    c += tex2Dproj(_GrabTexture, IN.grabPos + float4(-_Radius*_GrabTexture_TexelSize.x*offsets[i], 0, 0, 0)) * weights[i];
                }
                return c;
            }
        
            ENDCG
        }
        GrabPass {
            Tags {"LightMode"="Always"}
        }
        Pass {
            //Blend SrcAlpha OneMinusSrcAlpha
            //ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_grab
            #pragma fragment frag_blur_v
            #pragma fragmentoption ARB_precision_hint_fastest
            //#pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;
            float4 _GrabTexture_ST;
            float _Radius;

            static const float offsets[5] = {0,1.409090909,3.318181818,5.227272727,7.136363636};
            static const float weights[5] = {0.168191624,0.237193316,0.081323423,0.012557293,0.000734345};

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f_grab {
                float2 uv : TEXCOORD0;
                float4 grabPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
            
            v2f_grab vert_grab (appdata v) {
                v2f_grab o;
                o.color = v.color;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _GrabTexture);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }
        
            half4 frag_blur_v(v2f_grab IN) : SV_Target {
                half4 c = 0;
                for (int i = 0; i < 5; i++) {
                    c += tex2Dproj(_GrabTexture, IN.grabPos + float4(0,  _Radius*_GrabTexture_TexelSize.y*offsets[i], 0, 0)) * weights[i];
                    c += tex2Dproj(_GrabTexture, IN.grabPos + float4(0, -_Radius*_GrabTexture_TexelSize.y*offsets[i], 0, 0)) * weights[i];
                }
                return c;
            }
            ENDCG
        }
    }
}