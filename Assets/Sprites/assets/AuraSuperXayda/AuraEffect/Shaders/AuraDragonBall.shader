Shader "Unlit/AuraDragonBall"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Emission("Emission",Float)=1
        _FlowTex1("Flow Texture 1",2D) = "white" {}
        _ScaleTex1Tex2("Scale Texture 1/2",vector)=(1,1,1,1)
        _FlowTex2("Flow Texture 2",2D) = "white" {}
        _DistortionConfig("Distortion Speed XY and Power Z",vector)=(1,1,1,0)
        _MaxHeightCutOff("Max Height Cut Off",Float)=0
        _MinHeightCutOff("Min Height Cut Off",Float)=0
        _Tint("Color",Color)=(1,1,1,1)

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off


        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Tint;
            float4 _DistortionConfig;
            sampler2D _FlowTex1;
            sampler2D _FlowTex2;
            float4 _FlowTex2_ST;
            float _Distortion;
            float _Emission;
            float _MinHeightCutOff;
            float _MaxHeightCutOff;
            float4 _ScaleTex1Tex2;



            
            // smooth step
            void Unity_Smoothstep_float4(float4 Edge1, float4 Edge2, float4 In, out float4 Out)
            {
                Out = smoothstep(Edge1, Edge2, In);
            }
            // float _Constant_TAU = 6.28318530;
            // polar coordinate
            void Unity_PolarCoordinates_float(float2 UV, float2 Center, float RadialScale, float LengthScale, out float2 Out)
            {
                float2 delta = UV - Center;
                float radius = length(delta) * 2 * RadialScale;
                float angle = atan2(delta.x, delta.y) * 1.0/6.28 * LengthScale;
                Out = float2(radius, angle);
            }
            // remap
            void Unity_Remap_float2(float2 In, float2 InMinMax, float2 OutMinMax, out float2 Out)
            {
                Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
            }
            void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
            {
                Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color=v.color*_Tint;
                o.positionWS = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // first config uv
                float2 uvMake;
                float2 remapOut;
                Unity_Remap_float2(i.uv,float2(0,1),float2(-1,1),remapOut);
                uvMake.y=atan2(remapOut.x,remapOut.y)/6.28318530+0.5;
                float2 polar1;
                Unity_PolarCoordinates_float(i.uv,float2(0.5,0.5),1,1,polar1);
                uvMake=polar1.x;
                // noise with time for config uv
                float2 offset=_DistortionConfig.xy/_FlowTex2_ST.xy*_Time.y;
                float2 uvNoise;
                float2 uvPolarOut;
                Unity_PolarCoordinates_float(i.uv,float2(0.5,0.3),1,1,uvPolarOut);
                uvNoise=uvPolarOut+offset;

                fixed4 colFlow1=tex2D(_FlowTex1,uvNoise*_ScaleTex1Tex2.xy);
                fixed4 colFlow2=tex2D(_FlowTex2,uvNoise*_ScaleTex1Tex2.zw);
                fixed4 colNoise=colFlow1+colFlow2;
                float4 outRemapNoise;
                Unity_Remap_float4(colNoise,float2(0,1),float2(-0.5,0.5),outRemapNoise);
                

                uvMake.x=uvMake.x-outRemapNoise*_DistortionConfig.z;

                // main col
                fixed4 colMain=tex2D(_MainTex,uvMake);
                // cal alpha
                float alphaOut=smoothstep(_MinHeightCutOff, _MaxHeightCutOff, i.uv.y)*colMain.a*i.color.a;
                UNITY_APPLY_FOG(i.fogCoord, colMain);
                return fixed4(colMain.rgb*_Emission*i.color,alphaOut);
            }
            ENDCG
        }
    }
}
