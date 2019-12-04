Shader "Unlit/LineMask" {
	Properties {

		[PerRendererData] _MainTex ("Texture", 2D) = "white" {}

        _StencilMask ("Stencil Mask", Float) = 4
        _StencilWriteMask ("Stencil Write Mask", Float) = 1

	}
	SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Transparent+4"}
        ColorMask 0
		LOD 200
        
        Pass{
            Stencil {
                Ref [_StencilMask]
                Comp Always
                Pass Replace

                WriteMask [_StencilWriteMask]
            }
        }

    }
}

