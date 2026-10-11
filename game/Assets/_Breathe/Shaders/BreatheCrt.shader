// Full-screen look: low virtual resolution, ordered dither + posterize, warm/cold palette push, scanlines, light CRT bend and grain.
// Driven by global shader values set from PostFx (so no asset is mutated at runtime). With _BrParams.x == 0 the pass is a plain copy.
Shader "Breathe/CrtDither"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "BreatheCrt"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BrParams;   // x virtual lines (vertical resolution), y colour levels per channel, z dither strength, w scanline strength
            float4 _BrTint;     // rgb colour multiplier, a = saturation
            float4 _BrFx;       // x barrel bend, y chroma split in virtual pixels, z grain, w time

            static const float bayer4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_BrParams.x < 1.0) return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                float2 cc = uv * 2.0 - 1.0;
                cc *= 1.0 + _BrFx.x * dot(cc, cc);
                uv = cc * 0.5 + 0.5;
                if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return float4(0, 0, 0, 1);

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 res = float2(_BrParams.x * aspect, _BrParams.x);
                float2 px = floor(uv * res);
                float2 puv = (px + 0.5) / res;
                float2 split = float2(_BrFx.y / res.x, 0);
                float3 c;
                c.r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, puv + split, 0).r;
                c.g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, puv, 0).g;
                c.b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, puv - split, 0).b;

                float lum = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(lum.xxx, c, _BrTint.a) * _BrTint.rgb;

                int2 ip = int2(px) & 3;
                float threshold = bayer4[ip.y * 4 + ip.x] / 16.0 - 0.5;
                float levels = max(2.0, _BrParams.y);
                c = floor(saturate(c + threshold * _BrParams.z / levels) * levels + 0.5) / levels;

                float scan = 1.0 - _BrParams.w * fmod(px.y, 2.0);
                float grain = (Hash(px + floor(_BrFx.w * 12.0)) - 0.5) * _BrFx.z;
                return float4(saturate(c * scan + grain), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
