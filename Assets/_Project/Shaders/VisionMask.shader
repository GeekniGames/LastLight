Shader "LastLight/VisionMask"
{
    Properties
    {
        // UGUI Image가 머티리얼에 _MainTex가 있다고 가정하고 값을 세팅하려 해서 없으면 경고가 뜬다.
        // 셰이더에서 실제로 샘플링하지는 않지만 속성만 선언해둔다.
        _MainTex ("Texture (unused)", 2D) = "white" {}

        _Center ("Center (Screen UV)", Vector) = (0.5, 0.5, 0, 0)
        _Direction ("Facing Direction (Aspect-Corrected, Normalized)", Vector) = (0, 1, 0, 0)

        // 손전등: 도달 거리와 거리별 밝기 감소
        _Radius ("Radius (UV)", Float) = 0.5
        _FalloffStart ("Falloff Start (0~1, 반경 대비 어두워지기 시작하는 위치)", Range(0, 0.95)) = 0.25
        _FalloffPower ("Falloff Power (클수록 급격히 어두워짐)", Float) = 1.6

        // 손전등 부채꼴
        _ConeCosInner ("Cone Cos (Inner, fully lit up to here)", Float) = 0.906 // cos(25deg)
        _ConeCosOuter ("Cone Cos (Outer, fully dark past here)", Float) = 0.819 // cos(35deg)

        // 플레이어 주변 약한 원형 빛 (방향과 무관, 벽에는 막힘)
        _PersonalRadius ("Personal Light Radius (UV)", Float) = 0.1
        _PersonalIntensity ("Personal Light Intensity", Float) = 0.35
        _PersonalPower ("Personal Light Falloff Power", Float) = 1.5

        // 벽에 막히는 빛(Line of Sight). 거리 배열은 스크립트가 SetFloatArray로 채운다.
        _LosEnabled ("LOS Enabled (0/1)", Float) = 0
        _LosRange ("LOS Half Range (radians)", Float) = 0.61
        _LosSoftEdge ("LOS Soft Edge (UV)", Float) = 0.02

        // 빛 색과 세기
        _LightColor ("Light Color", Color) = (1, 0.88, 0.68, 1)
        _Peak ("Peak Intensity (1 = 원래 밝기, 그 이상은 더 밝게)", Float) = 1.3
        _AmbientColor ("Ambient Light (빛이 안 닿는 곳의 최소 밝기)", Color) = (0, 0, 0, 1)

        // 아이템(배터리팩 등) 발광. 손전등/주변 빛과 무관하게 항상 켜짐. 벽에는 막히지 않음(작은 반경이라 단순화).
        _ItemLightColor ("Item Glow Color", Color) = (0.35, 1, 0.85, 1)
        _ItemLightRadius ("Item Glow Radius (UV)", Float) = 0.05
        _ItemLightIntensity ("Item Glow Intensity", Float) = 0.6
        _ItemGlowPulse ("Item Glow Pulse Multiplier", Float) = 1.0
        _ItemLightCount ("Item Light Count", Float) = 0

        // 횃불 등 길목 조명. 아이템 발광과 같은 방식이지만 색이 다른 별도 채널.
        _TorchLightColor ("Torch Light Color", Color) = (1, 0.55, 0.2, 1)
        _TorchLightRadius ("Torch Light Radius (UV)", Float) = 0.07
        _TorchLightIntensity ("Torch Light Intensity", Float) = 0.5
        _TorchFlickerPulse ("Torch Flicker Multiplier", Float) = 1.0
        _TorchLightCount ("Torch Light Count", Float) = 0

        _AspectRatio ("Aspect Ratio (W/H)", Float) = 1.7778
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // 곱하기 블렌드: 결과 = 2 * 출력색 * 화면색.
        // 그래서 출력색 0.5가 '원래 밝기 그대로', 0이 '완전한 어둠', 1이 '2배 밝게'가 된다.
        Blend DstColor SrcColor
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // VisionController의 RayCount / RingRayCount / MaxItemLights와 반드시 같은 값이어야 한다.
            #define LOS_COUNT 64
            #define RING_COUNT 48
            #define MAX_ITEM_LIGHTS 24
            #define MAX_TORCH_LIGHTS 16

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Center;
            float4 _Direction;
            float _Radius;
            float _FalloffStart;
            float _FalloffPower;
            float _ConeCosInner;
            float _ConeCosOuter;
            float _PersonalRadius;
            float _PersonalIntensity;
            float _PersonalPower;
            float _LosEnabled;
            float _LosRange;
            float _LosSoftEdge;
            float _LosDist[LOS_COUNT];
            float _LosRingDist[RING_COUNT];
            float4 _LightColor;
            float _Peak;
            float4 _AmbientColor;
            float4 _ItemLightColor;
            float _ItemLightRadius;
            float _ItemLightIntensity;
            float _ItemGlowPulse;
            float _ItemLightCount;
            float4 _ItemLightPos[MAX_ITEM_LIGHTS];
            float4 _TorchLightColor;
            float _TorchLightRadius;
            float _TorchLightIntensity;
            float _TorchFlickerPulse;
            float _TorchLightCount;
            float4 _TorchLightPos[MAX_TORCH_LIGHTS];
            float _AspectRatio;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 손전등 조명 마스크: 화면에 곱해지는 '빛 색'을 계산한다.
            // 최종 밝기 = max(손전등 밝기, 플레이어 주변 약한 빛) + 아이템 발광(더하기)
            // 손전등 밝기 = 거리별 감소 x 부채꼴 x 벽 막힘
            half4 frag(Varyings IN) : SV_Target
            {
                float2 diff = IN.uv - _Center.xy;
                diff.x *= _AspectRatio; // 화면비 보정 (원/각도가 찌그러지지 않게)

                float dist = length(diff);
                float2 diffDir = diff / max(dist, 1e-5); // 0 나눗셈 방지

                float3 ambient = _AmbientColor.rgb;
                float softEdge = max(_LosSoftEdge, 1e-4);

                // ---- A) 플레이어 주변 약한 원형 빛 (전방향, 벽에는 막힘) ----
                float personal = 0.0;
                if (_PersonalIntensity > 0.0 && dist < _PersonalRadius)
                {
                    float pt = 1.0 - saturate(dist / max(_PersonalRadius, 1e-4));
                    personal = pow(pt, _PersonalPower) * _PersonalIntensity;

                    if (_LosEnabled > 0.5)
                    {
                        // 월드 각도(-PI~PI) -> 전방향 광선 배열 인덱스 (0~RING_COUNT, 끝과 처음이 이어짐)
                        float theta = atan2(diffDir.y, diffDir.x);
                        float u = (theta + 3.14159265) / 6.2831853 * RING_COUNT;

                        int i0 = clamp((int)floor(u), 0, RING_COUNT - 1);
                        int i1 = (i0 + 1) % RING_COUNT;
                        float f = saturate(u - i0);

                        float visibleRing = lerp(_LosRingDist[i0], _LosRingDist[i1], f);
                        personal *= 1.0 - smoothstep(visibleRing - softEdge, visibleRing, dist);
                    }
                }

                // ---- B) 손전등 (부채꼴) ----
                float flash = 0.0;

                // 1) 거리별 밝기: 가까이는 1, _FalloffStart 이후 서서히 어두워져 _Radius에서 0
                float inner = _Radius * _FalloffStart;
                float t = saturate((dist - inner) / max(_Radius - inner, 1e-4));
                float radial = pow(1.0 - t, _FalloffPower);

                // 2) 진행 방향 기준 부채꼴
                float2 dir = _Direction.xy;
                float dotValue = dot(diffDir, dir);
                float angular = smoothstep(_ConeCosOuter, _ConeCosInner, dotValue);

                // 이미 어두운 픽셀(화면 대부분)은 광선 배열 조회를 건너뛴다
                if (radial > 0.0 && angular > 0.0)
                {
                    // 3) 벽 막힘
                    float los = 1.0;
                    if (_LosEnabled > 0.5)
                    {
                        // 진행 방향 기준 부호 있는 각도 -> 부채꼴 광선 배열 인덱스
                        float crossValue = dir.x * diffDir.y - dir.y * diffDir.x;
                        float theta2 = atan2(crossValue, dotValue);
                        float u2 = saturate((theta2 + _LosRange) / (2.0 * _LosRange)) * (LOS_COUNT - 1);

                        int j0 = (int)floor(u2);
                        int j1 = min(j0 + 1, LOS_COUNT - 1);
                        float visible = lerp(_LosDist[j0], _LosDist[j1], u2 - j0);

                        // 벽 경계 직전부터 서서히 어두워지게(빛이 벽 뒤로 새지 않도록 경계 안쪽에서 끝남)
                        los = 1.0 - smoothstep(visible - softEdge, visible, dist);
                    }

                    flash = radial * angular * los * _Peak;
                }

                float intensity = max(flash, personal);

                // ---- C) 아이템 발광 (배터리팩 등, 손전등/주변 빛과 무관하게 항상 켜짐) ----
                // 벽 막힘 계산은 하지 않는다: 반경이 작아 얇은 벽 하나 정도는 살짝 새어도 눈에 거슬리지 않고,
                // 아이템마다 광선을 쏘면 비용이 아이템 수만큼 늘어나므로 의도적으로 생략했다.
                float itemGlow = 0.0;
                int itemCount = (int)_ItemLightCount;
                if (itemCount > 0)
                {
                    float effRadius = max(_ItemLightRadius * _ItemGlowPulse, 1e-5);

                    for (int k = 0; k < MAX_ITEM_LIGHTS; k++)
                    {
                        float active = k < itemCount ? 1.0 : 0.0;
                        float2 idiff = IN.uv - _ItemLightPos[k].xy;
                        idiff.x *= _AspectRatio;

                        float ifall = saturate(1.0 - length(idiff) / effRadius) * active;
                        itemGlow = max(itemGlow, ifall * ifall * _ItemLightIntensity);
                    }
                }

                // ---- D) 횃불 등 길목 조명 (아이템 발광과 동일한 방식, 색만 다른 채널) ----
                float torchGlow = 0.0;
                int torchCount = (int)_TorchLightCount;
                if (torchCount > 0)
                {
                    float torchEffRadius = max(_TorchLightRadius * _TorchFlickerPulse, 1e-5);

                    for (int m = 0; m < MAX_TORCH_LIGHTS; m++)
                    {
                        float active = m < torchCount ? 1.0 : 0.0;
                        float2 tdiff = IN.uv - _TorchLightPos[m].xy;
                        tdiff.x *= _AspectRatio;

                        float tfall = saturate(1.0 - length(tdiff) / torchEffRadius) * active;
                        torchGlow = max(torchGlow, tfall * tfall * _TorchLightIntensity);
                    }
                }

                float3 lightRGB = ambient + _LightColor.rgb * intensity + _ItemLightColor.rgb * itemGlow + _TorchLightColor.rgb * torchGlow;

                // 곱하기 블렌드가 출력색을 2배로 쓰므로, 여기서 절반으로 낮춰서 내보낸다
                return half4(saturate(lightRGB * 0.5), 1);
            }
            ENDHLSL
        }
    }
}
