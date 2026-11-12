// 批次 6c：全屏调色（暗角 + 轻对比 + 轻饱和 + 微暖调）。
// 供 ScreenGradeController 的 CommandBuffer Blit 使用（内置管线，gamma 空间调参）。
Shader "Gangland/ScreenGrade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.30
        _VignetteStart ("Vignette Start", Range(0, 1)) = 0.45
        _Contrast ("Contrast", Range(0, 0.5)) = 0.13
        _Saturation ("Saturation", Range(0, 0.5)) = 0.14
        _WarmTint ("Warm Tint", Range(0, 0.2)) = 0.045
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
            float _VignetteStrength;
            float _VignetteStart;
            float _Contrast;
            float _Saturation;
            float _WarmTint;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 轻对比：围绕 0.5 的线性 S 近似，中灰拉开
                col.rgb = saturate((col.rgb - 0.5) * (1.0 + _Contrast) + 0.5);

                // 轻饱和：向亮度方向分离后整体提饱和
                fixed lum = dot(col.rgb, fixed3(0.299, 0.587, 0.114));
                col.rgb = saturate(lerp(lum.xxx, col.rgb, 1.0 + _Saturation));

                // 微暖调：高光区偏暖（港区灯色氛围），暗部不动
                fixed warmWeight = saturate(_WarmTint * saturate(lum * 3.0) * 2.0);
                col.rgb = lerp(col.rgb, col.rgb * fixed3(1.07, 1.00, 0.90), warmWeight);

                // 暗角：边缘平滑压暗
                float2 d = i.uv - 0.5;
                float r = length(d) * 1.4143; // 画面中心 0 → 角落 1
                float vig = smoothstep(_VignetteStart, 1.0, r) * _VignetteStrength;
                col.rgb *= (1.0 - vig);

                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
