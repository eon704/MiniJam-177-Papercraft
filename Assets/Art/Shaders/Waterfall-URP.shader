Shader "Custom/Waterfall-URP"
{
    Properties
    {
        _MainTex        ("Normal Map", 2D)                    = "bump" {}
        _WaterColor     ("Water Color", Color)                = (0.2, 0.55, 0.75, 0.75)
        _FoamColor      ("Foam Color", Color)                 = (0.8, 0.95, 1.0, 1.0)
        _FlowSpeed      ("Flow Speed", Range(0, 5))           = 1.5
        _NormalStrength ("Normal Strength", Range(0, 3))      = 0.6
        _TileX          ("Tile X", Range(0.1, 10))            = 1.0
        _TileY          ("Tile Y", Range(0.1, 10))            = 2.0
        _FoamWidth      ("Foam Width", Range(0, 0.5))           = 0.15
        _FoamNoise      ("Foam Noise Scale", Range(0.1, 10))  = 2.0
        _FoamThreshold  ("Foam Threshold", Range(0, 1))       = 0.15
        _FoamSharpness  ("Foam Sharpness", Range(0.01, 0.5))  = 0.3
        _FoamSpeed      ("Foam Drift Speed", Range(0, 2))     = 0.3
        _BodyFoam       ("Body Foam Amount", Range(0, 1))    = 0.0
        _Turbulence     ("Turbulence", Range(0, 1))           = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;  // r = top-edge foam gate, g = bottom-edge foam gate
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 vertColor   : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // No CBUFFER — required for MaterialPropertyBlock overrides (disables SRP Batcher)
            float4 _MainTex_ST;
            half4  _WaterColor;
            half4  _FoamColor;
            float  _FlowSpeed;
            float  _NormalStrength;
            float  _TileX;
            float  _TileY;
            float  _FoamWidth;
            float  _FoamNoise;
            float  _FoamThreshold;
            float  _FoamSharpness;
            float  _FoamSpeed;
            float  _BodyFoam;
            float  _Turbulence;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.vertColor   = IN.color;
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                uv.x *= _TileX;
                uv.y *= _TileY;

                // scroll downward: += moves texture down → water falls down
                float scroll = _Time.y * _FlowSpeed;
                uv.y += scroll;

                // second UV layer: slight turbulence + slower scroll
                float2 uv2 = uv * 0.7;
                uv2.x += _Turbulence * sin(_Time.y * 0.5 + IN.uv.y * 3.0);
                uv2.y += scroll * 0.6;

                // sample normal map, blend two layers
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2));
                half3 blendedNormal = normalize(n1 + n2);

                // diffuse lighting
                float3 normalWS = normalize(IN.normalWS + blendedNormal * _NormalStrength);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);

                half4 col = _WaterColor;
                col.rgb *= NdotL * mainLight.color;

                // ── Foam ────────────────────────────────────────────────────
                float rawY = IN.uv.y; // 0 = bottom, 1 = top (not scrolled)

                // Edge gradients: 1 AT the edge, fades to 0 inward
                // smoothstep(a, b, x): a > b → inverted: 1 when x<=b, 0 when x>=a
                float topEdge    = smoothstep(_FoamWidth, 0.0, 1.0 - rawY); // 1 at rawY=1, 0 below
                float bottomEdge = smoothstep(_FoamWidth, 0.0, rawY);        // 1 at rawY=0, 0 above
                // vertex color gates which edges show foam (r=top, g=bottom, interpolated)
                float edgeGrad = topEdge * IN.vertColor.r + bottomEdge * IN.vertColor.g;
                edgeGrad = saturate(edgeGrad);

                // Foam noise: two independent layers from .g channel (DXT5nm has data there)
                float foamScroll = _Time.y * _FlowSpeed * _FoamSpeed;
                float2 fn1 = IN.uv * _FoamNoise;
                fn1.y += foamScroll;
                float2 fn2 = IN.uv * _FoamNoise * 1.4 + float2(0.17, 0.35);
                fn2.y += foamScroll * 0.65;

                half f1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, fn1).g;
                half f2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, fn2).g;

                // Multiply layers → sharper organic clumps
                half foamPattern = saturate(f1 * f2 * 2.5);

                // Edge foam: only inside edge zone
                float foamCombined = foamPattern * edgeGrad;
                float edgeFoam = smoothstep(_FoamThreshold, _FoamThreshold + _FoamSharpness, foamCombined);

                // Body foam: scattered throughout the flow, scaled by _BodyFoam
                float bodyFoam = smoothstep(_FoamThreshold + 0.25, _FoamThreshold + 0.25 + _FoamSharpness, foamPattern) * _BodyFoam;

                float foamMask = saturate(edgeFoam + bodyFoam);

                // Apply foam: fully opaque
                col.rgb = lerp(col.rgb, _FoamColor.rgb, foamMask);
                col.a   = saturate(col.a + foamMask);
                // ────────────────────────────────────────────────────────────

                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
