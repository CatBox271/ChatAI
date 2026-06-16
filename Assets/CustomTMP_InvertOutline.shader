Shader "Custom/TMP_InvertOutline"
{
    Properties
    {
        _FaceColor("Text Face Color", Color) = (1,1,1,1)
        _FaceDilate("Face Dilate", Range(-1,1)) = 0

        _OutlineSoftness("Outline Softness", Range(0,1)) = 0
        _OutlineWidth("Outline Thickness", Range(0,1)) = 0

        _UnderlayColor("Border Color", Color) = (0,0,0, 0.5)
        _UnderlayOffsetX("Border OffsetX", Range(-1,1)) = 0
        _UnderlayOffsetY("Border OffsetY", Range(-1,1)) = 0
        _UnderlayDilate("Border Dilate", Range(-1,1)) = 0
        _UnderlaySoftness("Border Softness", Range(0,1)) = 0

        _WeightNormal("Weight Normal", float) = 0
        _WeightBold("Weight Bold", float) = 0.5

        _ShaderFlags("Flags", float) = 0
        _ScaleRatioA("Scale RatioA", float) = 1
        _ScaleRatioB("Scale RatioB", float) = 1
        _ScaleRatioC("Scale RatioC", float) = 1

        _MainTex("Font Atlas", 2D) = "white" {}
        _TextureWidth("Texture Width", float) = 512
        _TextureHeight("Texture Height", float) = 512
        _GradientScale("Gradient Scale", float) = 5.0
        _ScaleX("Scale X", float) = 1.0
        _ScaleY("Scale Y", float) = 1.0
        _PerspectiveFilter("Perspective Filter", Range(0,1)) = 0.875
        _Sharpness("Sharpness", Range(0,1)) = 0

        _VertexOffsetX("Vertex OffsetX", float) = 0
        _VertexOffsetY("Vertex OffsetY", float) = 0
    }

        SubShader
        {
            Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
            ZWrite Off
            Cull Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            // 主要Pass
            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"
                #include "UnityUI.cginc"

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;
                };

                sampler2D _MainTex;
                float4 _FaceColor;
                float _FaceDilate;
                float _OutlineSoftness;
                float _OutlineWidth;
                float _UnderlaySoftness;
                float _UnderlayDilate;
                float _UnderlayOffsetX;
                float _UnderlayOffsetY;
                float4 _UnderlayColor;
                float _WeightNormal;
                float _WeightBold;
                float _ShaderFlags;
                float _ScaleRatioA;
                float _ScaleRatioB;
                float _ScaleRatioC;
                float _TextureWidth;
                float _TextureHeight;
                float _GradientScale;
                float _ScaleX;
                float _ScaleY;
                float _PerspectiveFilter;
                float _Sharpness;
                float _VertexOffsetX;
                float _VertexOffsetY;

                v2f vert(appdata v)
                {
                    v2f o;
                    v.vertex.xy += _VertexOffsetX * v.vertex.z + _VertexOffsetY;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    o.color = v.color;
                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    // 采样SDF纹理
                    float alpha = tex2D(_MainTex, i.uv).a;

                // 应用FaceDilate
                alpha = clamp(alpha - _FaceDilate, 0, 1);

                // 计算描边
                float outlineAlpha = 0;
                if (_OutlineWidth > 0)
                {
                    // 基础SDF采样
                    float baseAlpha = tex2D(_MainTex, i.uv).a;
                    // 内部软边
                    float innerGlow = saturate(_OutlineSoftness);
                    float dilate = _OutlineWidth * (1 + innerGlow);

                    // 计算描边alpha，在指定厚度内显示描边
                    outlineAlpha = saturate((baseAlpha - _FaceDilate) / max(dilate, 0.01));
                    outlineAlpha = smoothstep(0, 1, outlineAlpha);

                    // 减去文字本身的alpha，只保留描边部分
                    outlineAlpha = max(0, outlineAlpha - alpha);
                }
                else
                {
                    outlineAlpha = 0;
                }

                // 计算描边颜色（反色）
                float4 outlineColor = float4(1, 1, 1, 1) - _FaceColor;
                outlineColor.a = _FaceColor.a;

                // 计算文字颜色
                float4 textColor = _FaceColor * i.color;

                // 混合最终颜色
                float4 finalColor = textColor * alpha;
                finalColor += outlineColor * outlineAlpha;

                // 应用透明度
                finalColor.a = clamp(alpha + outlineAlpha, 0, 1);

                return finalColor;
            }
            ENDCG
        }
        }
            FallBack "TextMeshPro/Distance Field"
}