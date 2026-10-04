// Clears the game's distance fog inside Wisp Torch bubbles. OttoAura's FogBubbles class
// draws this pass right after the game's own fog pass, with:
//   _MainTex          the scene after the game's fog
//   _OttoAuraPreFog   the same scene before the fog
// For each pixel it measures how much of the view ray lies inside a bubble, works out how
// much fog the shorter outside stretch would have built up, and blends the unfogged scene
// back in by that much. The game's fog colour, sun glow included, is never recomputed.
// The formulas mirror WispFog/FogBubbleMath.cs; change both together.
Shader "Hidden/OttoAura/FogBubble"
{
    Properties
    {
        _MainTex ("Fogged scene", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile FOG_LINEAR FOG_EXP FOG_EXP2
            #include "UnityCG.cginc"

            // Must match FogBubbles.MaxBubbles.
            #define MAX_BUBBLES 16

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _OttoAuraPreFog;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            // World-space view directions through the four screen corners.
            float3 _OttoAuraRayTL;
            float3 _OttoAuraRayTR;
            float3 _OttoAuraRayBL;
            float3 _OttoAuraRayBR;
            float3 _OttoAuraCamPos;
            float3 _OttoAuraCamFwd;
            // x density, y linear start, z linear end, w camera near plane.
            float4 _OttoAuraFogParams;
            int _OttoAuraBubbleCount;
            // xyz center, w radius.
            float4 _OttoAuraBubbles[MAX_BUBBLES];

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvDepth : TEXCOORD1;
            };

            Varyings Vert(appdata_img v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.uvDepth = v.texcoord.xy;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                {
                    o.uvDepth.y = 1.0 - o.uvDepth.y;
                }
                #endif
                return o;
            }

            // FogBubbleMath.Transmittance: the share of the scene colour left at fog distance z.
            float Transmittance(float z)
            {
                float t;
                #if defined(FOG_LINEAR)
                    float span = _OttoAuraFogParams.z - _OttoAuraFogParams.y;
                    t = span > 0.0 ? (_OttoAuraFogParams.z - z) / span : 1.0;
                #elif defined(FOG_EXP)
                    t = exp2(-_OttoAuraFogParams.x * z);
                #else
                    float f = _OttoAuraFogParams.x * z;
                    t = exp2(-f * f);
                #endif
                return saturate(t);
            }

            // FogBubbleMath.LengthInside: how much of the ray from the camera to maxDistance
            // lies inside the sphere.
            float LengthInside(float3 dir, float maxDistance, float4 bubble)
            {
                float3 offset = _OttoAuraCamPos - bubble.xyz;
                float b = dot(offset, dir);
                float c = dot(offset, offset) - bubble.w * bubble.w;
                float discriminant = b * b - c;
                if (discriminant <= 0.0)
                {
                    return 0.0;
                }
                float root = sqrt(discriminant);
                float enter = max(-b - root, 0.0);
                float exit = min(-b + root, maxDistance);
                return max(exit - enter, 0.0);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 fogged = tex2D(_MainTex, i.uv);

                float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uvDepth);
                if (Linear01Depth(raw) >= 0.9999)
                {
                    // Sky. The game's skybox fog is left as it is.
                    return fogged;
                }

                float eyeDepth = LinearEyeDepth(raw);
                float3 dir = normalize(lerp(
                    lerp(_OttoAuraRayBL, _OttoAuraRayBR, i.uvDepth.x),
                    lerp(_OttoAuraRayTL, _OttoAuraRayTR, i.uvDepth.x),
                    i.uvDepth.y));
                float rayDistance = eyeDepth / max(dot(dir, _OttoAuraCamFwd), 1e-4);

                float inside = 0.0;
                [loop]
                for (int b = 0; b < MAX_BUBBLES; b++)
                {
                    if (b >= _OttoAuraBubbleCount)
                    {
                        break;
                    }
                    inside += LengthInside(dir, rayDistance, _OttoAuraBubbles[b]);
                }
                if (inside <= 0.0)
                {
                    return fogged;
                }

                // FogBubbleMath.FogKept.
                float fogDistance = max(eyeDepth - _OttoAuraFogParams.w, 0.0);
                float fullFog = 1.0 - Transmittance(fogDistance);
                if (fullFog <= 1e-4)
                {
                    return fogged;
                }
                float clearFraction = saturate(inside / rayDistance);
                float reducedFog = 1.0 - Transmittance(fogDistance * (1.0 - clearFraction));
                float kept = saturate(reducedFog / fullFog);

                half4 clear = tex2D(_OttoAuraPreFog, i.uv);
                return lerp(clear, fogged, kept);
            }
            ENDCG
        }
    }

    Fallback Off
}
