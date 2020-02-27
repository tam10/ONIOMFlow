Shader "Custom/OrbitalDensity" {
    Properties {
        _Clarity ("Clarity", Range(1,4)) = 1
        _Cutoff ("Cutoff", Range(0, 1)) = 1
        _Power ("Power", Range(0, 20)) = 1
        _MainTex ("MainTex", 2D) = "white" {}
        _Tex3D ("Texture",3D) = "white" {}
    }
    SubShader {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        //Blend One OneMinusSrcAlpha
        //Blend DstAlpha OneMinusSrcColor
        //Blend One One
        Cull Off
        ZWrite Off
        LOD 200
        Pass {


            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #include "UnityCG.cginc"
            
            sampler3D _Tex3D;
            sampler2D _MainTex;

            half _Clarity;
            half _Cutoff;
            half _Power;

            float4x4 _Rotation;
            float4 _Scale;
            float4 _Position;
            
            struct v2f {
                float4 pos : SV_POSITION;
                float3 uv : TEXCOORD;
            };

            v2f vert (appdata_base v) {
                //float4x4 Rot = float4x4 (
                //    _Rotation[0][1], _Rotation[0][2], _Rotation[0][0], _Rotation[0][3],
                //    _Rotation[1][1], _Rotation[1][2], _Rotation[1][0], _Rotation[1][3],
                //    _Rotation[2][1], _Rotation[2][2], _Rotation[2][0], _Rotation[2][3],
                //    _Rotation[3][1], _Rotation[3][2], _Rotation[3][0], _Rotation[3][3]
                //
                //);
                v2f o;
                float4 vertex = v.vertex;
                vertex.w = 1.0;

                //o.pos = UnityObjectToClipPos ((v.vertex + (_Position + float4(0,0,-15,0))/ _Scale) );
                //o.pos = UnityObjectToClipPos ((vertex + _Position / _Scale + _Position) );

                //o.pos = UnityObjectToClipPos ((vertex + _Position / _Scale) );
                //o.pos = UnityObjectToClipPos (vertex + _Position * _Scale );
                //o.pos = UnityObjectToClipPos (vertex + _Position );
                //_Position.z -= 15;
                //o.pos = UnityObjectToClipPos (vertex + _Position * 75);
                o.pos = UnityObjectToClipPos (2 * (vertex + _Position * 37.5));
                //o.uv.w = -vertex.z;

                //o.pos = mul(UnityObjectToClipPos ((vertex + _Position / _Scale) ), _Rotation);
                //o.pos = mul(UnityObjectToClipPos ((vertex + _Position) ), _Rotation);
                //o.pos = UnityObjectToClipPos ((vertex + mul(_Position , _Rotation) ) );
                //o.pos = UnityObjectToClipPos (vertex);

                //o.pos = UnityObjectToClipPos ((vertex + mul(_Position, _Rotation)) );
                //o.pos = mul(UnityObjectToClipPos (vertex), _Rotation);
                //o.pos = UnityObjectToClipPos (vertex);

                //o.pos = UnityObjectToClipPos ((vertex + _Position * 400) );
                //o.pos = UnityObjectToClipPos (mul(_Rotation, (vertex * _Scale ) * 0.01 ).xyz);
                //o.pos = UnityObjectToClipPos (vertex);

                //o.uv = (mul(_Rotation, v.vertex).xyz*0.5+0.5)  * 0.000877;
                //o.uv = (mul(_Rotation, (v.vertex * _Scale) + _Translation).xyz*0.5+0.5);
                //o.uv = (mul(_Rotation, (v.vertex) + _Translation).xyz*0.5+0.5);
                //o.uv = (mul(_Rotation, v.vertex * 0.000094).xyz*0.5+0.5);
                //o.uv = (mul(_Rotation, (v.vertex) * _Scale * 0.01).xyz*0.5+0.5);
                //o.uv = (mul(Rot, ((v.vertex ) * _Scale ) * 0.01 ).xyz * 0.5 + 0.5);
                //o.uv = (mul(_Rotation, ((vertex ) * _Scale ) * 0.01 ).xyz * 0.5 + 0.5);
                
                //o.uv = (mul(_Rotation, ((vertex ) * (_Scale ) ) * 0.01 ) * 0.5 + 0.5);

                //o.uv = (mul(vertex, _Rotation ) * _Scale * 0.5 + 0.5);
                //o.uv = (mul(vertex * _Scale, _Rotation ) * 0.5 + 0.5);
                //o.uv = (mul(o.pos, _Rotation ) * _Scale * 0.5 + 0.5);;
                o.uv = (mul(vertex * _Scale / 1000, _Rotation) * 0.5 + 0.5);
                //o.uv = (vertex * _Scale * 0.01) * 0.5 + 0.5;
                
                //o.uv = mul(_Rotation, v.vertex).xyz*0.5+0.5;
                //o.uv = v.vertex.xyz*0.5+0.5;
                //o.uv = (v.vertex.xyz * 0.000877)*0.5+0.5;
                return o;
            }
            
            half4 frag (v2f i) : COLOR {
                //float4 outColor = tex3D (_Tex3D, i.uv) ;
                //return outColor;
                if (i.uv.x > 1) {return half4(0,0,0,0);}
                if (i.uv.x < 0) {return half4(0,0,0,0);}
                if (i.uv.y > 1) {return half4(0,0,0,0);}
                if (i.uv.y < 0) {return half4(0,0,0,0);}
                if (i.uv.z > 1) {return half4(0,0,0,0);}
                if (i.uv.z < 0) {return half4(0,0,0,0);}
                half4 color = tex3D (_Tex3D, i.uv);
                //color.a *= 5;// + float4(0,i.uv.x / 200 + 0.005,0,0);
                if (color.a < _Cutoff) {
                    return half4(0,0,0,0);
                }

                color.a = pow(color.a * _Power, _Clarity);
                return color;
            }
            
            ENDCG
            
        }
    }
    FallBack "VertexLit"
}