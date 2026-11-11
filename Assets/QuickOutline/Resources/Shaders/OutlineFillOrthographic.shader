// Built-in RP outline fill (perspective & orthographic)
Shader "Custom/Outline Fill Orthographic"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
        _OutlineColor("Outline Color", Color) = (1, 0.08, 0.08, 1)
        _OutlineWidth("Outline Width", Range(0, 10)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+110"
            "RenderType" = "Transparent"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "Fill"
            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _OutlineWidth;
            fixed4 _OutlineColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 smoothNormal : TEXCOORD3;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 normal = any(v.smoothNormal) ? v.smoothNormal : v.normal;
                float nLen = length(normal);
                float3 safeNormal = nLen > 1e-4 ? (normal / nLen) : float3(0.0, 1.0, 0.0);

                // View-space position and normal
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, safeNormal));

                // unity_OrthoParams.w: 1.0 = orthographic camera, 0.0 = perspective camera
                float isOrtho = unity_OrthoParams.w;
                float screenH = _ScreenParams.y > 0.0 ? _ScreenParams.y : 1080.0;
                float projY = max(0.0001, abs(UNITY_MATRIX_P[1][1]));

                float unitsPerPixel;
                if (isOrtho > 0.5)
                {
                    // For orthographic camera, unity_OrthoParams.y is the camera's world height (2 * orthoSize).
                    // Fallback to 2.0 / projY if unity_OrthoParams.y is zero.
                    float orthoHeight = unity_OrthoParams.y > 0.0 ? unity_OrthoParams.y : (2.0 / projY);
                    unitsPerPixel = orthoHeight / screenH;
                }
                else
                {
                    // For perspective camera, visible height at depth -viewPos.z is 2.0 * dist / projY.
                    float dist = max(0.1, -viewPos.z);
                    unitsPerPixel = (dist * 2.0) / (projY * screenH);
                }

                // Clear, visible outline in screen pixels (clamped to prevent bloated outlines)
                float targetPixels = clamp(_OutlineWidth * 1.5, 1.5, 8.0);
                float extrusion = targetPixels * unitsPerPixel;

                // 3D extrusion in view space preserves full volume: top, bottom, and sides expand uniformly
                viewPos += viewNormal * extrusion;

                o.pos = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
    Fallback Off
}
