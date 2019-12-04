//3D Gradient Perlin Noise using Normal

// https://github.com/przemyslawzaworski/Unity3D-CG-programming
// https://www.shadertoy.com/view/XslGRr

// License Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
 
Shader "FBM Generator version 2"
{
    Properties
    {
        _octaves ("Octaves",Int) = 7
        _lacunarity("Lacunarity", Range( 1.0 , 5.0)) = 2.5
        _gain("Gain", Range( 0.0 , 1.0)) = 0.1
        _value("Value", Range( -2.0 , 2.0)) = -0.05
        _amplitude("Amplitude", Range( 0.0 , 5.0)) = 0.9
        _frequency("Frequency", Range( 0.0 , 6.0)) = 3.0
        _power("Power", Range( 0.1 , 10.0)) = 4.0
        [HDR]
        _color ("Color", Color) = (1.0,1.0,1.0,1.0)      
    }
    Category {
        Tags {"RenderType" = "Transparent" "Queue" = "Transparent"}
        Blend SrcColor OneMinusSrcAlpha
        
        Subshader {

            Pass
            {
                CGPROGRAM
                #pragma vertex vertex_shader
                #pragma fragment pixel_shader
                #pragma target 3.0
            
                struct SHADERDATA
                {
                    float4 vertex : SV_POSITION;
                    float3 normal: NORMAL;
                    float2 uv : TEXCOORD0;
                    fixed4 color : COLOR;
                };
    
                float _octaves,_lacunarity,_gain,_value,_amplitude,_frequency, _power, _scale;
                float4 _color;
            

                float hash( float n )
                {
                    return frac(sin(n)*43758.5453123);
                }
            
                float noise( float3 norm )
                {

                    for( int octave = 0; octave < _octaves; octave++ ) {

                        float3 p = floor(norm * _frequency);
                        float3 f = frac(norm * _frequency);
                    
                        f = f*f*(3.0-2.0*f);
                        float n = p.x + p.y*57.0 + 113.0*p.z;

                        float noise = lerp(lerp(lerp( hash(n+0.0), hash(n+1.0),f.x),
                                    lerp( hash(n+57.0), hash(n+58.0),f.x),f.y),
                                lerp(lerp( hash(n+113.0), hash(n+114.0),f.x),
                                    lerp( hash(n+170.0), hash(n+171.0),f.x),f.y),f.z);

                        _value += _amplitude * noise;
                        _frequency *= _lacunarity + _SinTime.w * 0.1;
                        _amplitude *= _gain;
                    }
                    _value = clamp(_value, -1.0, 1.0);

                    return pow(_value * 0.5 + 0.5, _power);
                }
            
                SHADERDATA vertex_shader (float4 vertex:POSITION, float2 uv:TEXCOORD0, float3 normal:NORMAL, float4 color:COLOR)
                {
                    SHADERDATA vs;
                    vs.vertex = UnityObjectToClipPos (vertex);
                    vs.normal = normal;
                    vs.color = color;
                    vs.uv = uv;
                    return vs;
                }
    
                float4 pixel_shader (SHADERDATA ps) : SV_TARGET
                {  
                    //float c = noise(ps.normal + float3(_SinTime.y, 0.0, _CosTime.y)) ;
                    float c = noise(ps.normal + float3(_SinTime.y, ps.uv.y, _CosTime.y)) ;
                    float4 col = _color + ps.color;
                    //float4 col = _color + 2 * float4(1.0, 0.0, 0.0, 1.0);
                    col.a *= 0.25;
                    return float4(c,c,c,c) * col;
                }
    
                ENDCG
    
            }
        }
    }
}