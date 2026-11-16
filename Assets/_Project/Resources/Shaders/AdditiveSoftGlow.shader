// 批次 6f：additive 光晕 shader —— 霓虹/招牌辉光（假 bloom）。
// SoftCircle 径向渐变 + 加色混合 = 灯光溢出感；顶点色通道透传（SpriteRenderer 逐实例着色）。
// 供 OnlineWorldBuilder.ApplyNeonGlowPass 使用；Resources 加载保证打进构建。
Shader "Gangland/AdditiveSoftGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                return fixed4(tex.rgb * i.color.rgb, tex.a * i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
