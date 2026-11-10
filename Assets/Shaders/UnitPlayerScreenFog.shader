Shader "Hidden/RTSokak/UnitPlayerScreenFog"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            float4 _MainTex_TexelSize;

            float4 _FogColor;
            float _FogStart;
            float _FogEnd;
            float _FogIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // Platform UV flip for depth blit
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                    o.uv.y = 1 - o.uv.y;
                #endif
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(raw);

                // Sky / no geometry → full fog (so horizon matches)
                float isSky = raw < 1e-5 ? 1.0 : 0.0;

                float fogFactor = saturate((depth - _FogStart) / max(0.001, _FogEnd - _FogStart));
                fogFactor = lerp(fogFactor, 1.0, isSky);
                fogFactor = pow(fogFactor, 0.85); // чуть раньше набирает плотность
                fogFactor *= saturate(_FogIntensity);

                col.rgb = lerp(col.rgb, _FogColor.rgb, fogFactor);
                return col;
            }
            ENDCG
        }
    }
}
