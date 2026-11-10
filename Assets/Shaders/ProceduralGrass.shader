Shader "RTSokak/AnimatedGrassTexture"
{
    Properties
    {
        _MainTex ("Grass Texture", 2D) = "white" {}
        _GrassColor ("Base Grass Tint", Color) = (1,1,1,1)
        
        [Header(Wind Animation)]
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.0
        _WindStrength ("Wind Strength", Range(0, 0.5)) = 0.05
        _WindDirection ("Wind Direction XZ", Vector) = (1, 0, 1, 0)
        
        [Header(Color Variation)]
        _ColorVariationScale ("Color Variation Scale", Range(1, 50)) = 10.0
        _ColorVariationStrength ("Color Variation Strength", Range(0, 1)) = 0.3
        _ColorTint1 ("Variation Tint 1", Color) = (0.9, 1.0, 0.9, 1)
        _ColorTint2 ("Variation Tint 2", Color) = (1.0, 0.95, 0.85, 1)
        
        [Header(Advanced)]
        _BrightnessVariation ("Brightness Variation", Range(0, 1)) = 0.2
        _TextureScale ("Texture Tiling", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        float4 _GrassColor;
        
        float _WindSpeed;
        float _WindStrength;
        float4 _WindDirection;
        
        float _ColorVariationScale;
        float _ColorVariationStrength;
        float4 _ColorTint1;
        float4 _ColorTint2;
        
        float _BrightnessVariation;
        float _TextureScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        // Perlin-like noise function
        float hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        float noise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            
            float a = hash(i);
            float b = hash(i + float2(1.0, 0.0));
            float c = hash(i + float2(0.0, 1.0));
            float d = hash(i + float2(1.0, 1.0));
            
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Vertex shader для анимации ветра
        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            
            // Волна ветра в направлении _WindDirection
            float windTime = _Time.y * _WindSpeed;
            float windPhase = dot(worldPos.xz, _WindDirection.xz) + windTime;
            float windWave = sin(windPhase) * 0.5 + 0.5;
            
            // Вторая волна для более сложного движения
            float windPhase2 = dot(worldPos.xz * 1.3, _WindDirection.xz * 0.8) + windTime * 1.5;
            float windWave2 = sin(windPhase2) * 0.3;
            
            // Комбинируем волны
            float totalWind = (windWave + windWave2) * _WindStrength;
            
            // Применяем ветер (только по X и Z, больше на высоких вершинах)
            float windInfluence = v.texcoord.y; // UV.y = высота на Plane
            v.vertex.x += totalWind * windInfluence * _WindDirection.x;
            v.vertex.z += totalWind * windInfluence * _WindDirection.z;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Базовая текстура с tiling
            float2 uv = IN.uv_MainTex * _TextureScale;
            fixed4 texColor = tex2D(_MainTex, uv);
            
            // Цветовая вариация на основе world position
            float2 worldUV = IN.worldPos.xz / _ColorVariationScale;
            float colorNoise = noise(worldUV);
            float colorNoise2 = noise(worldUV * 2.3 + float2(5.7, 3.2));
            
            // Смешиваем два цветовых оттенка
            float4 colorVariation = lerp(_ColorTint1, _ColorTint2, colorNoise);
            
            // Применяем вариацию цвета
            fixed4 c = texColor * _GrassColor;
            c.rgb = lerp(c.rgb, c.rgb * colorVariation.rgb, _ColorVariationStrength);
            
            // Вариация яркости для устранения повторений
            float brightnessNoise = noise(worldUV * 3.7 + float2(11.3, 7.9));
            float brightnessMultiplier = lerp(1.0 - _BrightnessVariation, 1.0 + _BrightnessVariation, brightnessNoise);
            c.rgb *= brightnessMultiplier;
            
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    
    FallBack "Diffuse"
}
