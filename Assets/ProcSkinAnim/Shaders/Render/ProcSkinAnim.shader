Shader "ProcSkinAnim/ProcSkinAnim" {

	Properties {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}

        [Space]
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        [Gamma] _Metallic("Metallic", Range(0, 1)) = 0
        [KeywordEnum(None, Front, Back)] _Cull("Culling", Int) = 0
	}

    CGINCLUDE
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull [_Cull]

        Pass
        {
            Tags { "LightMode"="Deferred" }
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_prepassfinal noshadowmask nodynlightmap nodirlightmap nolightmap
            #include "ProcSkinAnim.cginc"
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_shadowcaster noshadowmask nodynlightmap nodirlightmap nolightmap
            #pragma instancing_options procedural:setup
            #define UNITY_PASS_SHADOWCASTER
            #include "ProcSkinAnim.cginc"
            ENDCG
        }
    }


}
