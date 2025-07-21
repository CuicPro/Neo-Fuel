Shader "Custom/BiomeBlendShaderSimple"
{
    Properties
    {
        _MainTex ("Main Texture (Optional)", 2D) = "white" {}
        _TextureScale ("Texture Scale", Float) = 1.0
        _ColorIntensity ("Color Intensity", Range(0,2)) = 1.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        float _TextureScale;
        float _ColorIntensity;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float4 color : COLOR; // Couleurs de vertex pour les biomes
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Utiliser la position mondiale pour les UVs (tiling)
            float2 tiledUV = IN.worldPos.xz * _TextureScale;
            
            // Échantillonner la texture principale (optionnelle)
            fixed4 texColor = tex2D(_MainTex, tiledUV);
            
            // Mélanger la texture avec la couleur de vertex (couleur des biomes)
            fixed4 finalColor = texColor * IN.color * _ColorIntensity;
            
            // S'assurer qu'on a une couleur visible même sans texture
            if (length(finalColor.rgb) < 0.1)
            {
                finalColor.rgb = IN.color.rgb * _ColorIntensity;
            }
            
            o.Albedo = finalColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

