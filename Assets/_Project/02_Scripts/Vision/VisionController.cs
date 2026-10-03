using System.Collections.Generic;
using DG.Tweening;
using LastLight.Core;
using LastLight.Maze;
using LastLight.Player;
using UnityEngine;

namespace LastLight.Vision
{
    /// <summary>
    /// 손전등 조명 컨트롤러.
    /// - 배터리 잔량에 비례해 빛이 닿는 거리를 조절 (거리는 타일 칸 수 기준이라 기기 화면 비율과 무관)
    /// - 플레이어가 바라보는 방향으로 부채꼴을 비추고, 거리에 따라 서서히 어두워지며, 주황빛 색을 입힘
    /// - 부채꼴 안에서 광선을 쏴 벽에 막히는 거리를 구해 셰이더로 전달(벽 너머는 어둡게)
    /// 배터리팩 획득 시 잠깐 반경이 커졌다 돌아오는 펄스 연출은 DOTween으로 처리.
    /// 전제: 카메라는 Orthographic이고 회전하지 않음.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VisionController : MonoBehaviour
    {
        // VisionMask.shader의 LOS_COUNT와 반드시 같은 값이어야 한다.
        private const int RayCount = 64;

        // 플레이어 주변 원형 빛용 전방향 광선 개수. VisionMask.shader의 RING_COUNT와 같은 값이어야 한다.
        private const int RingRayCount = 48;

        // 동시에 반영 가능한 아이템 발광 최대 개수. VisionMask.shader의 MAX_ITEM_LIGHTS와 같은 값이어야 한다.
        private const int MaxItemLights = 24;

        // 동시에 반영 가능한 횃불 개수. VisionMask.shader의 MAX_TORCH_LIGHTS와 같은 값이어야 한다.
        private const int MaxTorchLights = 16;

        /// <summary>배터리팩 등 발광 아이템이 등록/해제하는 창구. 씬에 하나만 존재.</summary>
        public static VisionController Instance { get; private set; }

        // 아이템이 스스로 등록/해제하므로 여기서는 참조만 들고 있는다(소유하지 않음)
        private readonly List<Transform> _itemLights = new List<Transform>(MaxItemLights);
        private readonly Vector4[] _itemLightPositions = new Vector4[MaxItemLights];
        private readonly List<Transform> _torchLights = new List<Transform>(MaxTorchLights);
        private readonly Vector4[] _torchLightPositions = new Vector4[MaxTorchLights];

        [Header("References")]
        [SerializeField] private BatterySystem battery;
        [SerializeField] private PlayerController player;
        [SerializeField] private MazeBuilder maze; // 벽에 빛이 막히도록 격자 정보를 읽어옴
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material visionMaskMaterial; // VisionMask 셰이더를 사용하는 머티리얼 인스턴스

        [Header("Reach (타일 칸 수 기준, 배터리 잔량에 비례)")]
        [Tooltip("배터리 100%일 때 빛이 닿는 거리(타일 칸 수).")]
        [SerializeField] private float maxRadiusTiles = 7f;
        [Tooltip("배터리 0%일 때도 남는 최소 거리(타일 칸 수).")]
        [SerializeField] private float minRadiusTiles = 2.5f;

        [Header("Falloff (거리별 밝기 감소)")]
        [Tooltip("반경의 몇 %지점부터 어두워지기 시작할지(0.25 = 25%). 작을수록 가까운 곳만 밝다.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float falloffStart = 0.25f;
        [Tooltip("클수록 어두워지는 곡선이 급격해진다. 1~2.5 사이를 추천.")]
        [SerializeField] private float falloffPower = 1.6f;

        [Header("Light Color (손전등 빛 색)")]
        [SerializeField] private Color lightColor = new Color(1f, 0.88f, 0.68f, 1f);
        [Tooltip("1 = 타일 원래 밝기, 1보다 크면 더 밝게(빛이 가장 센 곳).")]
        [SerializeField] private float peakIntensity = 1.3f;
        [Tooltip("빛이 안 닿는 곳의 최소 밝기. 검정이면 완전한 어둠.")]
        [SerializeField] private Color ambientColor = Color.black;

        [Header("Personal Light (플레이어 주변 약한 원형 빛, 방향 무관)")]
        [Tooltip("주변 빛이 닿는 거리(타일 칸 수). 0이면 끔.")]
        [SerializeField] private float personalRadiusTiles = 1.5f;
        [Tooltip("주변 빛의 세기. 손전등(Peak Intensity)보다 훨씬 약해야 한다.")]
        [SerializeField] private float personalIntensity = 0.35f;
        [Tooltip("클수록 가장자리가 빨리 어두워진다.")]
        [SerializeField] private float personalPower = 1.5f;

        [Header("Item Glow (배터리팩 등 아이템 발광, 손전등과 무관하게 항상 켜짐)")]
        [SerializeField] private Color itemGlowColor = new Color(0.35f, 1f, 0.85f, 1f);
        [Tooltip("발광이 닿는 거리(타일 칸 수).")]
        [SerializeField] private float itemGlowRadiusTiles = 0.9f;
        [Tooltip("발광의 기본 세기.")]
        [SerializeField] private float itemGlowIntensity = 0.6f;
        [Tooltip("숨쉬듯 커졌다 작아지는 속도.")]
        [SerializeField] private float itemGlowPulseSpeed = 2f;
        [Tooltip("펄스로 인한 크기 배율 범위(최소~최대).")]
        [SerializeField] private Vector2 itemGlowPulseRange = new Vector2(0.8f, 1.15f);

        [Header("Torch Light (횃불 등 길목 조명, 배터리팩과 별도 색 채널)")]
        [SerializeField] private Color torchColor = new Color(1f, 0.55f, 0.2f, 1f);
        [Tooltip("빛이 닿는 거리(타일 칸 수).")]
        [SerializeField] private float torchRadiusTiles = 1.1f;
        [Tooltip("기본 세기.")]
        [SerializeField] private float torchIntensity = 0.5f;
        [Tooltip("불꽃처럼 흔들리는 속도.")]
        [SerializeField] private float torchFlickerSpeed = 3f;
        [Tooltip("흔들림으로 인한 크기 배율 범위(최소~최대).")]
        [SerializeField] private Vector2 torchFlickerRange = new Vector2(0.7f, 1.05f);

        [Header("Light Origin (손전등이 실제로 뿜어져 나오는 기준점)")]
        [Tooltip("플레이어 위치 대비 손전등 시작점 오프셋(월드 유닛). Y를 음수로 하면 아래로 내려감. 실제 플레이어 위치/충돌에는 영향 없음.")]
        [SerializeField] private Vector2 lightOriginOffset = new Vector2(0f, -0.2f);

        [Header("Cone (손전등 부채꼴 각도)")]
        [Tooltip("부채꼴 전체 각도(도). 45~60 사이를 추천.")]
        [SerializeField] private float coneAngleDeg = 50f;
        [Tooltip("부채꼴 경계가 부드럽게 어두워지는 폭(도).")]
        [SerializeField] private float coneSoftEdgeDeg = 10f;

        [Header("Wall Occlusion (벽에 막히는 빛)")]
        [Tooltip("벽 경계에서 빛이 어두워지는 폭(타일 단위). 0.25 = 타일 1/4칸.")]
        [SerializeField] private float losSoftEdgeTiles = 0.25f;

        [Header("Pickup Pulse")]
        [SerializeField] private float pulseScale = 1.15f;
        [SerializeField] private float pulseDuration = 0.25f;

        [Header("Flicker (손전등 고장 함정 등에서 사용)")]
        [Tooltip("깜빡이는 동안 완전히 꺼질 확률(매 토글마다).")]
        [Range(0f, 1f)]
        [SerializeField] private float flickerOffChance = 0.35f;
        [Tooltip("꺼지지 않을 때 밝기가 최소 이 값까지 떨어질 수 있다(1 = 정상 밝기).")]
        [Range(0f, 1f)]
        [SerializeField] private float flickerDimMin = 0.3f;
        [Tooltip("깜빡임 토글 간격 범위(초). 짧을수록 더 빠르게 깜빡인다.")]
        [SerializeField] private float flickerIntervalMin = 0.05f;
        [SerializeField] private float flickerIntervalMax = 0.18f;

        private float _flickerTimer;
        private float _flickerToggleTimer;
        private float _flickerMultiplier = 1f;

        private static readonly int CenterProp = Shader.PropertyToID("_Center");
        private static readonly int DirectionProp = Shader.PropertyToID("_Direction");
        private static readonly int RadiusProp = Shader.PropertyToID("_Radius");
        private static readonly int FalloffStartProp = Shader.PropertyToID("_FalloffStart");
        private static readonly int FalloffPowerProp = Shader.PropertyToID("_FalloffPower");
        private static readonly int ConeCosInnerProp = Shader.PropertyToID("_ConeCosInner");
        private static readonly int ConeCosOuterProp = Shader.PropertyToID("_ConeCosOuter");
        private static readonly int AspectProp = Shader.PropertyToID("_AspectRatio");
        private static readonly int LosEnabledProp = Shader.PropertyToID("_LosEnabled");
        private static readonly int LosRangeProp = Shader.PropertyToID("_LosRange");
        private static readonly int LosSoftEdgeProp = Shader.PropertyToID("_LosSoftEdge");
        private static readonly int LosDistProp = Shader.PropertyToID("_LosDist");
        private static readonly int LightColorProp = Shader.PropertyToID("_LightColor");
        private static readonly int PeakProp = Shader.PropertyToID("_Peak");
        private static readonly int AmbientColorProp = Shader.PropertyToID("_AmbientColor");
        private static readonly int PersonalRadiusProp = Shader.PropertyToID("_PersonalRadius");
        private static readonly int PersonalIntensityProp = Shader.PropertyToID("_PersonalIntensity");
        private static readonly int PersonalPowerProp = Shader.PropertyToID("_PersonalPower");
        private static readonly int LosRingDistProp = Shader.PropertyToID("_LosRingDist");
        private static readonly int ItemLightColorProp = Shader.PropertyToID("_ItemLightColor");
        private static readonly int ItemLightRadiusProp = Shader.PropertyToID("_ItemLightRadius");
        private static readonly int ItemLightIntensityProp = Shader.PropertyToID("_ItemLightIntensity");
        private static readonly int ItemGlowPulseProp = Shader.PropertyToID("_ItemGlowPulse");
        private static readonly int ItemLightCountProp = Shader.PropertyToID("_ItemLightCount");
        private static readonly int ItemLightPosProp = Shader.PropertyToID("_ItemLightPos");
        private static readonly int TorchLightColorProp = Shader.PropertyToID("_TorchLightColor");
        private static readonly int TorchLightRadiusProp = Shader.PropertyToID("_TorchLightRadius");
        private static readonly int TorchLightIntensityProp = Shader.PropertyToID("_TorchLightIntensity");
        private static readonly int TorchFlickerPulseProp = Shader.PropertyToID("_TorchFlickerPulse");
        private static readonly int TorchLightCountProp = Shader.PropertyToID("_TorchLightCount");
        private static readonly int TorchLightPosProp = Shader.PropertyToID("_TorchLightPos");

        private float _pulseMultiplier = 1f;
        private Tween _pulseTween;
        private float _aspectRatio;

        // 광선 결과 캐시: 위치/방향/미로/줌이 바뀌었을 때만 다시 계산한다.
        private readonly float[] _losDist = new float[RayCount];
        private readonly float[] _losRingDist = new float[RingRayCount];
        private int _lastGridVersion = -1;
        private Vector2 _lastLosPos;
        private Vector2 _lastLosFacing;
        private float _lastOrthoSize = -1f;

        // 몬스터 AI 등 외부에서 IsPointLit()을 조회할 때 쓰는 캐시. LateUpdate에서 매 프레임 갱신.
        private Vector2 _cachedLightOrigin;
        private Vector2 _cachedFacing = Vector2.up;
        private float _cachedFlashRadiusWorld;
        private float _cachedPersonalRadiusWorld;
        private float _cachedConeCosInner = 1f;
        private float _cachedConeCosOuter = 1f;

        private void Awake()
        {
            Instance = this;

            if (targetCamera == null)
                targetCamera = Camera.main;

            _aspectRatio = (float)Screen.width / Screen.height;

            if (visionMaskMaterial != null)
            {
                visionMaskMaterial.SetFloat(AspectProp, _aspectRatio);
                ApplyStaticParams();
            }

            if (battery != null)
                battery.OnPickup += HandlePickupPulse;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (battery != null)
                battery.OnPickup -= HandlePickupPulse;

            _pulseTween?.Kill();
        }

        // 플레이 중에 인스펙터 값을 바꾸면 바로 반영되도록(튜닝 편의)
        private void OnValidate()
        {
            if (!Application.isPlaying || visionMaskMaterial == null) return;

            ApplyStaticParams();
            _lastGridVersion = -1; // 부채꼴 각도가 바뀌었을 수 있으니 광선을 다시 계산
        }

        private void LateUpdate()
        {
            if (battery == null || player == null || targetCamera == null || visionMaskMaterial == null)
                return;

            UpdateAspectIfChanged();

            Transform playerTransform = player.transform;
            float tileWorld = maze != null ? maze.CellSize : 1f;

            // 손전등 계산은 전부 이 지점을 기준으로 한다. 플레이어 실제 좌표(이동/충돌)는 그대로 두고
            // 손전등이 뿜어져 나오는 지점만 살짝 옮기는 것.
            Vector3 lightOrigin = playerTransform.position + (Vector3)lightOriginOffset;

            // 월드 거리 -> 셰이더 UV 거리 변환 계수 (직교 카메라: 화면 세로 = 2 * orthographicSize 유닛)
            float uvPerUnit = 1f / (2f * targetCamera.orthographicSize);

            // 손전등 시작점의 화면상 위치를 0~1 UV로 변환해 셰이더 중심점으로 전달
            Vector3 centerViewport = targetCamera.WorldToViewportPoint(lightOrigin);
            visionMaskMaterial.SetVector(CenterProp, new Vector4(centerViewport.x, centerViewport.y, 0f, 0f));

            // 방향은 두 지점(손전등 시작점 / 그 앞 한 지점)을 뷰포트로 변환해 UV 상의 방향 벡터로 계산.
            Vector3 aheadWorld = lightOrigin + (Vector3)player.FacingDirection;
            Vector3 aheadViewport = targetCamera.WorldToViewportPoint(aheadWorld);

            Vector2 dirUV = (Vector2)(aheadViewport - centerViewport);
            dirUV.x *= _aspectRatio; // Center diff와 동일한 화면비 보정을 적용해야 각도가 맞음
            dirUV = dirUV.sqrMagnitude > 1e-8f ? dirUV.normalized : Vector2.up;

            visionMaskMaterial.SetVector(DirectionProp, new Vector4(dirUV.x, dirUV.y, 0f, 0f));

            // 도달 거리: 타일 칸 수 -> 월드 유닛 -> 화면 UV
            // 손전등이 꺼져 있으면 부채꼴/주변 빛 반경을 0으로 만들어 화면 전체를 어둡게 한다(배터리팩 발광은 별개라 그대로 켜짐).
            UpdateFlicker();

            float onMultiplier = battery.IsOn ? 1f : 0f;
            float radiusTiles = Mathf.Lerp(minRadiusTiles, maxRadiusTiles, battery.Normalized) * _pulseMultiplier * onMultiplier * _flickerMultiplier;
            float flashRadiusWorld = radiusTiles * tileWorld;
            float personalRadiusWorld = personalRadiusTiles * tileWorld;
            visionMaskMaterial.SetFloat(RadiusProp, flashRadiusWorld * uvPerUnit);
            visionMaskMaterial.SetFloat(PersonalRadiusProp, personalRadiusWorld * uvPerUnit);
            visionMaskMaterial.SetFloat(ItemLightRadiusProp, itemGlowRadiusTiles * tileWorld * uvPerUnit);
            visionMaskMaterial.SetFloat(TorchLightRadiusProp, torchRadiusTiles * tileWorld * uvPerUnit);

            // 몬스터 AI가 IsPointLit()으로 조회할 수 있게 이번 프레임 값을 캐시
            _cachedLightOrigin = lightOrigin;
            _cachedFacing = player.FacingDirection;
            _cachedFlashRadiusWorld = flashRadiusWorld;
            _cachedPersonalRadiusWorld = personalRadiusWorld;

            UpdateLineOfSight(lightOrigin, player.FacingDirection, tileWorld, uvPerUnit);
            UpdateItemGlow();
            UpdateTorchGlow();
        }

        /// <summary>
        /// 이 월드 좌표가 '손전등 부채꼴'만으로 밝혀져 있는지 조회한다(주변 원형 빛은 포함하지 않음).
        /// 몬스터의 손전등 처치 판정 전용 — 손전등을 직접 비춰야만 타격으로 치게 하려는 목적.
        /// </summary>
        public bool IsPointLitByFlashlight(Vector3 worldPos)
        {
            if (maze == null || maze.OpenGrid == null || maze.VisibleWallGrid == null)
                return false;

            Vector2 origin = _cachedLightOrigin;
            Vector2 toPoint = (Vector2)worldPos - origin;
            float dist = toPoint.magnitude;
            if (dist > _cachedFlashRadiusWorld) return false;

            Vector2 dir = dist > 1e-4f ? toPoint / dist : _cachedFacing;

            float dot = Vector2.Dot(dir, _cachedFacing);
            if (dot < _cachedConeCosOuter) return false;

            float inner = _cachedFlashRadiusWorld * falloffStart;
            float t = Mathf.Clamp01((dist - inner) / Mathf.Max(_cachedFlashRadiusWorld - inner, 1e-4f));
            float radial = Mathf.Pow(1f - t, falloffPower);
            float angular = Mathf.Clamp01(Mathf.InverseLerp(_cachedConeCosOuter, _cachedConeCosInner, dot));

            if (radial * angular <= 0.02f) return false;

            float visibleDist = GridRaycast.Cast(maze.OpenGrid, maze.VisibleWallGrid, maze.GridOrigin, maze.CellSize,
                                                 origin, dir, dist + 0.05f);
            return visibleDist >= dist - 0.05f;
        }

        /// <summary>
        /// 이 월드 좌표가 지금 손전등(부채꼴) 또는 플레이어 주변 원형 빛으로 밝혀져 있는지 조회한다.
        /// 몬스터 AI가 "빛에 노출됐는지" 판단하는 용도. 벽에 막혀 있으면 밝아 보여도 false를 반환한다.
        /// 매 프레임 캐시된 값만 읽고 광선은 이 지점 하나만 새로 쏘므로, 몬스터 수만큼 호출해도 가볍다.
        /// </summary>
        public bool IsPointLit(Vector3 worldPos)
        {
            if (maze == null || maze.OpenGrid == null || maze.VisibleWallGrid == null)
                return false;

            Vector2 origin = _cachedLightOrigin;
            Vector2 toPoint = (Vector2)worldPos - origin;
            float dist = toPoint.magnitude;
            if (dist < 1e-4f) return true;

            Vector2 dir = toPoint / dist;

            // 1) 손전등 부채꼴 + 거리 감쇠
            float flash = 0f;
            if (dist <= _cachedFlashRadiusWorld)
            {
                float dot = Vector2.Dot(dir, _cachedFacing);
                if (dot >= _cachedConeCosOuter)
                {
                    float inner = _cachedFlashRadiusWorld * falloffStart;
                    float t = Mathf.Clamp01((dist - inner) / Mathf.Max(_cachedFlashRadiusWorld - inner, 1e-4f));
                    float radial = Mathf.Pow(1f - t, falloffPower);
                    float angular = Mathf.Clamp01(Mathf.InverseLerp(_cachedConeCosOuter, _cachedConeCosInner, dot));
                    flash = radial * angular;
                }
            }

            // 2) 플레이어 주변 원형 빛
            float personal = 0f;
            if (dist <= _cachedPersonalRadiusWorld)
            {
                float pt = 1f - Mathf.Clamp01(dist / Mathf.Max(_cachedPersonalRadiusWorld, 1e-4f));
                personal = Mathf.Pow(pt, personalPower) * personalIntensity;
            }

            if (Mathf.Max(flash, personal) <= 0.02f) return false;

            // 3) 벽 차단 확인: 목표 지점까지 닿기 전에 벽이 있으면 실제로는 빛이 안 닿은 것
            float visibleDist = GridRaycast.Cast(maze.OpenGrid, maze.VisibleWallGrid, maze.GridOrigin, maze.CellSize,
                                                 origin, dir, dist + 0.05f);
            return visibleDist >= dist - 0.05f;
        }

        /// <summary>
        /// 등록된 아이템들의 화면상 위치와 공통 펄스 값을 셰이더에 전달한다.
        /// 벽 막힘 계산은 하지 않는다(발광 반경이 작아 부담을 줄이려는 의도적인 단순화).
        /// 펄스는 아이템마다 트윈을 도는 대신 공유 sin 값 하나로 계산해 비용이 아이템 수와 무관하다.
        /// </summary>
        private void UpdateItemGlow()
        {
            int count = _itemLights.Count;
            visionMaskMaterial.SetFloat(ItemLightCountProp, count);
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                Transform t = _itemLights[i];
                if (t == null) continue; // 파괴됐는데 아직 해제 콜백이 안 온 경우 방어

                Vector3 vp = targetCamera.WorldToViewportPoint(t.position);
                _itemLightPositions[i] = new Vector4(vp.x, vp.y, 0f, 0f);
            }

            visionMaskMaterial.SetVectorArray(ItemLightPosProp, _itemLightPositions);

            float pulse = (Mathf.Sin(Time.time * itemGlowPulseSpeed) + 1f) * 0.5f;
            visionMaskMaterial.SetFloat(ItemGlowPulseProp, Mathf.Lerp(itemGlowPulseRange.x, itemGlowPulseRange.y, pulse));
        }

        /// <summary>횃불 등 길목 조명의 위치/깜빡임을 셰이더에 전달. 구조는 UpdateItemGlow()와 동일하되 채널만 분리.</summary>
        private void UpdateTorchGlow()
        {
            int count = _torchLights.Count;
            visionMaskMaterial.SetFloat(TorchLightCountProp, count);
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                Transform t = _torchLights[i];
                if (t == null) continue;

                Vector3 vp = targetCamera.WorldToViewportPoint(t.position);
                _torchLightPositions[i] = new Vector4(vp.x, vp.y, 0f, 0f);
            }

            visionMaskMaterial.SetVectorArray(TorchLightPosProp, _torchLightPositions);

            float pulse = (Mathf.Sin(Time.time * torchFlickerSpeed) + 1f) * 0.5f;
            visionMaskMaterial.SetFloat(TorchFlickerPulseProp, Mathf.Lerp(torchFlickerRange.x, torchFlickerRange.y, pulse));
        }

        /// <summary>
        /// 광선을 쏴서 벽에 막히는 거리(화면 UV 단위)를 셰이더에 전달한다.
        /// - 부채꼴 광선(RayCount개): 위치나 방향이 바뀔 때만 다시 계산
        /// - 전방향 광선(RingRayCount개, 주변 원형 빛용): 위치가 바뀔 때만 다시 계산
        /// 가만히 서 있으면 계산 0.
        /// </summary>
        private void UpdateLineOfSight(Vector2 position, Vector2 facing, float tileWorld, float uvPerUnit)
        {
            bool useLos = maze != null && maze.OpenGrid != null && maze.VisibleWallGrid != null;
            visionMaskMaterial.SetFloat(LosEnabledProp, useLos ? 1f : 0f);
            if (!useLos) return;

            float orthoSize = targetCamera.orthographicSize;

            bool baseChanged = maze.GridVersion != _lastGridVersion
                               || Mathf.Abs(orthoSize - _lastOrthoSize) > 1e-4f;
            bool moved = baseChanged || (position - _lastLosPos).sqrMagnitude > 1e-6f;
            bool turned = baseChanged || Vector2.Dot(facing, _lastLosFacing) < 0.99999f;
            if (!moved && !turned) return;

            _lastGridVersion = maze.GridVersion;
            _lastOrthoSize = orthoSize;
            _lastLosPos = position;
            _lastLosFacing = facing;

            bool[,] grid = maze.OpenGrid;
            bool[,] visibleWall = maze.VisibleWallGrid;
            Vector2 origin = maze.GridOrigin;
            float cellSize = maze.CellSize;

            // ---- 부채꼴 광선 ----
            // 반경이 가장 커질 때(배터리 만땅 + 펄스)까지만 쏘면 충분 (+1칸 여유)
            float maxDistWorld = (maxRadiusTiles * pulseScale + 1f) * tileWorld;

            float range = (coneAngleDeg * 0.5f + coneSoftEdgeDeg) * Mathf.Deg2Rad;
            float baseAngle = Mathf.Atan2(facing.y, facing.x);

            for (int i = 0; i < RayCount; i++)
            {
                float a = baseAngle + Mathf.Lerp(-range, range, i / (float)(RayCount - 1));
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                float distWorld = GridRaycast.Cast(grid, visibleWall, origin, cellSize, position, dir, maxDistWorld);
                _losDist[i] = distWorld * uvPerUnit;
            }

            visionMaskMaterial.SetFloatArray(LosDistProp, _losDist);
            visionMaskMaterial.SetFloat(LosRangeProp, range);
            visionMaskMaterial.SetFloat(LosSoftEdgeProp, losSoftEdgeTiles * cellSize * uvPerUnit);

            // ---- 전방향 광선 (플레이어 주변 원형 빛) ----
            if (moved && personalRadiusTiles > 0f && personalIntensity > 0f)
            {
                float ringMaxDist = (personalRadiusTiles + 1f) * tileWorld;

                for (int i = 0; i < RingRayCount; i++)
                {
                    // 셰이더의 각도 매핑과 같아야 한다: 인덱스 i = 월드 각도 -PI + 2*PI * i / RingRayCount
                    float a = -Mathf.PI + Mathf.PI * 2f * i / RingRayCount;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                    float distWorld = GridRaycast.Cast(grid, visibleWall, origin, cellSize, position, dir, ringMaxDist);
                    _losRingDist[i] = distWorld * uvPerUnit;
                }

                visionMaskMaterial.SetFloatArray(LosRingDistProp, _losRingDist);
            }
        }

        /// <summary>발광 아이템(배터리팩 등)이 켜질 때 스스로 호출. 최대 개수를 넘으면 조용히 무시한다.</summary>
        public void RegisterItemLight(Transform t)
        {
            if (t == null || _itemLights.Contains(t)) return;
            if (_itemLights.Count >= MaxItemLights) return;

            _itemLights.Add(t);
        }

        /// <summary>아이템이 사라지거나 획득됐을 때 스스로 호출.</summary>
        public void UnregisterItemLight(Transform t)
        {
            _itemLights.Remove(t);
        }

        /// <summary>횃불이 켜질 때 스스로 호출.</summary>
        public void RegisterTorchLight(Transform t)
        {
            if (t == null || _torchLights.Contains(t)) return;
            if (_torchLights.Count >= MaxTorchLights) return;

            _torchLights.Add(t);
        }

        /// <summary>횃불이 꺼지거나 파괴될 때 스스로 호출.</summary>
        public void UnregisterTorchLight(Transform t)
        {
            _torchLights.Remove(t);
        }

        private void UpdateAspectIfChanged()
        {
            float aspect = (float)Screen.width / Screen.height;
            if (Mathf.Abs(aspect - _aspectRatio) < 1e-4f) return;

            _aspectRatio = aspect;
            visionMaskMaterial.SetFloat(AspectProp, aspect);
        }

        // 자주 바뀌지 않는 값(색, 밝기, 각도)은 시작할 때/인스펙터 수정 시에만 셰이더로 전달
        private void ApplyStaticParams()
        {
            visionMaskMaterial.SetFloat(FalloffStartProp, falloffStart);
            visionMaskMaterial.SetFloat(FalloffPowerProp, falloffPower);
            visionMaskMaterial.SetColor(LightColorProp, lightColor);
            visionMaskMaterial.SetFloat(PeakProp, peakIntensity);
            visionMaskMaterial.SetColor(AmbientColorProp, ambientColor);
            visionMaskMaterial.SetFloat(PersonalIntensityProp, personalIntensity);
            visionMaskMaterial.SetFloat(PersonalPowerProp, personalPower);
            visionMaskMaterial.SetColor(ItemLightColorProp, itemGlowColor);
            visionMaskMaterial.SetFloat(ItemLightIntensityProp, itemGlowIntensity);
            visionMaskMaterial.SetColor(TorchLightColorProp, torchColor);
            visionMaskMaterial.SetFloat(TorchLightIntensityProp, torchIntensity);
            ApplyConeAngle();
        }

        private void ApplyConeAngle()
        {
            float halfAngleRad = coneAngleDeg * 0.5f * Mathf.Deg2Rad;
            float outerAngleRad = (coneAngleDeg * 0.5f + coneSoftEdgeDeg) * Mathf.Deg2Rad;

            _cachedConeCosInner = Mathf.Cos(halfAngleRad);
            _cachedConeCosOuter = Mathf.Cos(outerAngleRad);

            visionMaskMaterial.SetFloat(ConeCosInnerProp, _cachedConeCosInner);
            visionMaskMaterial.SetFloat(ConeCosOuterProp, _cachedConeCosOuter);
        }


        /// <summary>N초 동안 손전등을 깜빡이게 한다. 이미 깜빡이는 중이면 더 긴 시간으로 갱신한다.</summary>
        public void TriggerFlicker(float duration)
        {
            _flickerTimer = Mathf.Max(_flickerTimer, duration);
        }

        /// <summary>
        /// 일정하지 않은 간격으로 밝기를 껐다 켰다 해서 '고장난 손전등' 느낌을 낸다.
        /// 매 프레임 랜덤을 돌리면 지글거리는 노이즈처럼 보여서, 짧은 간격마다 한 번씩만 값을 다시 뽑는다.
        /// </summary>
        private void UpdateFlicker()
        {
            if (_flickerTimer <= 0f)
            {
                _flickerMultiplier = 1f;
                return;
            }

            _flickerTimer -= Time.deltaTime;
            _flickerToggleTimer -= Time.deltaTime;

            if (_flickerToggleTimer <= 0f)
            {
                _flickerToggleTimer = UnityEngine.Random.Range(flickerIntervalMin, flickerIntervalMax);
                _flickerMultiplier = UnityEngine.Random.value < flickerOffChance
                    ? 0f
                    : UnityEngine.Random.Range(flickerDimMin, 1f);
            }
        }

        private void HandlePickupPulse()
        {
            _pulseTween?.Kill();
            _pulseMultiplier = 1f;

            _pulseTween = DOTween.Sequence()
                .Append(DOTween.To(() => _pulseMultiplier, x => _pulseMultiplier = x, pulseScale, pulseDuration * 0.4f)
                    .SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => _pulseMultiplier, x => _pulseMultiplier = x, 1f, pulseDuration * 0.6f)
                    .SetEase(Ease.InOutQuad));
        }
    }
}