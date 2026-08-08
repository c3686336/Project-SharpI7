Shader "Unlit/Electric"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _Tint("Color",Color)=(1,1,1,1)
        _Brightness("Brightness of Color",Float)=1
        _ScrollSpeed("Scroll Speed",vector)=(0,1,0,0)

        _BoltNumber("Number of bolt",float)=4
        _BoltPower("Bolt Power",float)=50
        _NumberLinePerBolt("Number Line of One Bolt",float)=2
        _BoltAngle("Angle of Bolt",float)=0

        _BoltNoiseScale("Distortion Scale of Bolt",float)=50
        _BoltNoiseVector("Vector of Bolt Distortion",vector)=(-1,1,0,0)
        _LerpBoltInUV("Lerp Ratio Distortion in UV Bolt",Range(0,1))=0.1

        _DissolveSpeed("Dissolve Speed",float)=20
        _DissolveMinMax("Dissolve Range",vector)=(2,5,0,0)

        _MaxHeightCutOff("Max Height Cut Off",Float)=0
        _MinHeightCutOff("Min Height Cut Off",Float)=0
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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;
            float _BoltNoiseScale;
            float2 _BoltNoiseVector;
            float _LerpBoltInUV;
            float _BoltAngle;
            float _BoltPower;
            float _NumberLinePerBolt;
            float _BoltNumber;
            float2 _ScrollSpeed;
            float4 _Tint;
            float _DissolveSpeed;
            float2 _DissolveMinMax;
            float _Brightness;
            float _MinHeightCutOff;
            float _MaxHeightCutOff;

            // polar coordinates
            void Unity_PolarCoordinates_float(float2 UV, float2 Center, float RadialScale, float LengthScale, out float2 Out)
            {
                float2 delta = UV - Center;
                float radius = length(delta) * 2 * RadialScale;
                float angle = atan2(delta.x, delta.y) * 1.0/6.28 * LengthScale;
                Out = float2(radius, angle);
            }
            // simple noise
            inline float unity_noise_randomValue (float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233)))*43758.5453);
            }

            inline float unity_noise_interpolate (float a, float b, float t)
            {
                return (1.0-t)*a + (t*b);
            }

            inline float unity_valueNoise (float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                uv = abs(frac(uv) - 0.5);
                float2 c0 = i + float2(0.0, 0.0);
                float2 c1 = i + float2(1.0, 0.0);
                float2 c2 = i + float2(0.0, 1.0);
                float2 c3 = i + float2(1.0, 1.0);
                float r0 = unity_noise_randomValue(c0);
                float r1 = unity_noise_randomValue(c1);
                float r2 = unity_noise_randomValue(c2);
                float r3 = unity_noise_randomValue(c3);

                float bottomOfGrid = unity_noise_interpolate(r0, r1, f.x);
                float topOfGrid = unity_noise_interpolate(r2, r3, f.x);
                float t = unity_noise_interpolate(bottomOfGrid, topOfGrid, f.y);
                return t;
            }

            void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
            {
                float t = 0.0;

                float freq = pow(2.0, float(0));
                float amp = pow(0.5, float(3-0));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(1));
                amp = pow(0.5, float(3-1));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                freq = pow(2.0, float(2));
                amp = pow(0.5, float(3-2));
                t += unity_valueNoise(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;

                Out = t;
            }

            // remap
            void Unity_Remap_float(float In, float2 InMinMax, float2 OutMinMax, out float Out)
            {
                Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
            }
            // triangle wave
            void Unity_TriangleWave_float(float In, out float Out)
            {
                Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
            }
            // create shape one bolt
            void CreateOneBoltShape(float2 uv,float2 offset,float numLinePerBolt,float angleBolt,float boltPower,out float output)
            {
                float2 polarCoordinates;
                Unity_PolarCoordinates_float(uv,offset,1,numLinePerBolt,polarCoordinates);
                
                float triangleWaveOut;
                // rotate in polarcoordinate by plus bolt angle
                // triangle wave
                Unity_TriangleWave_float(polarCoordinates.y+_BoltAngle,triangleWaveOut);
                // saturate to clip in range (0,1)
                float saturateOut=saturate(triangleWaveOut);

                // final is power
                output=pow(saturateOut,_BoltPower);
            }
            // smooth step
            void Unity_Smoothstep_float4(float4 Edge1, float4 Edge2, float4 In, out float4 Out)
            {
                Out = smoothstep(Edge1, Edge2, In);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // first is distortion of bolt
                float outOfNoise;
                Unity_SimpleNoise_float(i.uv+_Time.y*_BoltNoiseVector,_BoltNoiseScale,outOfNoise);
                // lerp distortion in uv
                float2 uvInputPolarCoordinates=lerp(i.uv,outOfNoise,_LerpBoltInUV);

                // base calculate bolt and plus them
                // because saturate bolt shape in 0,1 => so range of bolt is 0,1 => pos one bolt offset can be 1/(_BoltNumber+1) and bolt will start from 0
                float finalShapeAllBolt;
                for (int x = 0; x < _BoltNumber; x++)
                {
                    // calculate one polar
                    // calculate center of bolt by scroll speed and offset center by x
                    // alway x is o.5 to set middle of bolt is center

                    float2 offset= _Time.y*_ScrollSpeed.xy+float2(0.5,x*(1/_BoltNumber));
                    // frac
                    float shapeOneBolt;
                    CreateOneBoltShape(uvInputPolarCoordinates,frac(offset),_NumberLinePerBolt,_BoltAngle,_BoltPower,shapeOneBolt);
                    finalShapeAllBolt+=shapeOneBolt;
                }
                
                // calculate alpha of all map
                float2 polarCoordinatesOut;
                Unity_PolarCoordinates_float(i.uv, float2(0.5,0.5), 1, 1, polarCoordinatesOut);

                float remapOut;
                Unity_Remap_float(sin(_Time.y*_DissolveSpeed),float2(-1,1),_DissolveMinMax,remapOut);

                float finalData=finalShapeAllBolt*pow(saturate(1-polarCoordinatesOut.y),remapOut);

                // mask
                fixed4 maskTex=tex2D(_MaskTex,i.uv);
                fixed4 col=tex2D(_MainTex,finalData.xx)*_Tint*_Brightness*maskTex.a;
                // cut off
                float alphaOut=smoothstep(_MinHeightCutOff, _MaxHeightCutOff, i.uv.y)*col.r;

                return fixed4(col.rgb,alphaOut);
            }
            ENDCG
        }
    }
}
