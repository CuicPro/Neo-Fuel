Shader "Custom/BiomeBlend"
{
    Properties
    {
        _BiomeTexture1 ("Plains Texture", 2D) = "white" {}
        _BiomeTexture2 ("Forest Texture", 2D) = "white" {}
        _BiomeTexture3 ("Desert Texture", 2D) = "white" {}
        _BiomeTexture4 ("Mountains Texture", 2D) = "white" {}
        _BiomeTexture5 ("Snow Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // RGBA = poids des 4 premiers biomes
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 vertex : SV_POSITION;
            };

            sampler2D _BiomeTexture1;
            sampler2D _BiomeTexture2;
            sampler2D _BiomeTexture3;
            sampler2D _BiomeTexture4;
            sampler2D _BiomeTexture5;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = i.color.r;
                float g = i.color.g;
                float b = i.color.b;
                float a = i.color.a;
                float e = 1.0 - (r + g + b + a); // Snow biome = reste

                fixed4 tex1 = tex2D(_BiomeTexture1, i.uv);
                fixed4 tex2 = tex2D(_BiomeTexture2, i.uv);
                fixed4 tex3 = tex2D(_BiomeTexture3, i.uv);
                fixed4 tex4 = tex2D(_BiomeTexture4, i.uv);
                fixed4 tex5 = tex2D(_BiomeTexture5, i.uv);

                fixed4 blended = tex1 * r + tex2 * g + tex3 * b + tex4 * a + tex5 * e;
                return blended;
            }
            ENDCG
        }
    }
}
