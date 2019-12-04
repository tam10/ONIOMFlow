Shader "Unlit/Wireframe" {
    Properties {

        [IntRange] _Divisions ("Divisions", Range(1,10)) = 5
        _Thickness ("Thickness", Range(0.001, 0.5)) = 0.05
        _GlowRadius ("Glow Radius", Range(0.001, 1)) = 0.8
        _GridFrontAlphaMultiplier ("Grid Front Alpha Multiplier", Range(0.0, 1.0)) = 0.5

        [HDR] _TransparentColour ("Transparent Colour", color) = (0., 0., 0., 0.)
        [HDR] _BackgroundColour ("Background Colour", color) = (0.0, 0., 0.0, 0.0)
        [HDR] _GridColour ("Grid Colour", color) = (1., 1., 1., 0.8)
        [HDR] _GlowColour0 ("Glow Colour 0", color) = (0.6, 0., 0.6, 0.4)
        [HDR] _GlowColour1 ("Glow Colour 1", color) = (0.3, 0., 0.9, 0.4)
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Alphatest"}
        Zwrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 200

        //Show the back
        Pass {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            int _Divisions;
            half _Thickness;
            float4 _GridColour;
            float4 _BackgroundColour;
            float4 _GlowColour0;
            float4 _GlowColour1;
            half _GlowRadius;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half3 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                //Choose the color using the dot product of the norm and camera view direction
                half3 worldPos = mul(unity_ObjectToWorld, o.vertex).xyz;
                half3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                half3 worldNormal = normalize(UnityObjectToWorldNormal(v.normal));
                half g = abs(dot(worldNormal, worldViewDir));
                o.color = v.color * lerp(_GlowColour0, _GlowColour1, g);

                return o;
            }
            
            float4 frag (v2f i) : SV_Target {

                //Create a 2D sawtooth function between 0 and 1 scaled by 1 + _Thickness.
                //Everything between 1 and 1 + _Thickness is lerped between _GlowColour and _GridColour
                //Everything between 1 and 1 - _GlowRadius is lerped between _GlowColour and _BackgroundColour

                half xFrac = 2 * (1 + _Thickness) * abs(frac(_Divisions * i.uv.x) - 0.5);
                half yFrac = 2 * (1 + _Thickness) * abs(frac(_Divisions * i.uv.y) - 0.5);
                half t = max(xFrac, yFrac);

                float4 col;
                if (t > 1.0) {
                    col = lerp(i.color, _GridColour, (t - 1.0) / _Thickness);
                } else if (t > 1.0 - _GlowRadius) {
                    col = lerp(i.color, _BackgroundColour, (1.0 - t) / _GlowRadius);
                } else {
                    col = _BackgroundColour;
                }
                return col;

            }
            ENDCG
        }

        //Show the front
        Pass {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            int _Divisions;
            half _Thickness;
            float4 _GridColour;
            float4 _TransparentColour;
            half _GridFrontAlphaMultiplier;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target {

                float xFrac = 2 * (1 + _Thickness) * abs(frac(_Divisions * i.uv.x) - 0.5);
                float yFrac = 2 * (1 + _Thickness) * abs(frac(_Divisions * i.uv.y) - 0.5);
                float t = max(xFrac, yFrac);

                float4 col;
                if (t > 1.0) {
                    col = lerp(_TransparentColour, _GridColour, (t - 1.0) / _Thickness);
                    col.a = col.a * _GridFrontAlphaMultiplier;
                } else {
                    col = _TransparentColour;
                }
                return col;

            }
            ENDCG
        }
    }
}
