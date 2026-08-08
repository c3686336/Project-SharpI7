Shader "SharpI7/Chant Aura"
{
    Properties
    {
        _MainTex ("Energy Texture", 2D) = "white" {}
        _Color ("Aura Color", Color) = (1, 0.22, 0.02, 0.3)
        _ScrollSpeed ("Scroll Speed", Vector) = (0.04, 0.03, 0, 0)
        _Radius ("Radius", Range(0, 1)) = 0.92
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.28
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ScrollSpeed;
            float _Radius;
            float _Softness;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 scrollUv = i.uv + (_Time.y * _ScrollSpeed.xy);
                fixed4 energy = tex2D(_MainTex, scrollUv);
                float distanceFromCenter = length(i.uv - 0.5) * 2.0;
                float circleMask = 1.0 - smoothstep(_Radius - _Softness, _Radius, distanceFromCenter);
                float alpha = energy.r * _Color.a * circleMask;
                return fixed4(energy.rgb * _Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
