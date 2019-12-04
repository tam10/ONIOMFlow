Shader "Unlit/LineMaterial" {
	SubShader { 
		Tags { "RenderType"="Opaque" }
		Pass { 
			Blend SrcAlpha OneMinusSrcAlpha 
			ZWrite On ZTest LEqual Cull Off Fog { Mode Off } 
			BindChannels {
				Bind "vertex", vertex Bind "color", color 
			}
		} 
	} 
}
