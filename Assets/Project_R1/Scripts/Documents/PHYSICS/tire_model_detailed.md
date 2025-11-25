# 3.3 타이어 모델 (Tire Model)

## 목차
- [3.3.1 개요](#331-개요)
- [3.3.2 Pacejka Magic Formula](#332-pacejka-magic-formula)
- [3.3.3 타이어 데이터 구조](#333-타이어-데이터-구조)
- [3.3.4 슬립 계산](#334-슬립-계산)
- [3.3.5 타이어 힘 계산](#335-타이어-힘-계산)
- [3.3.6 노면 상태 시스템](#336-노면-상태-시스템)
- [3.3.7 타이어 온도 시뮬레이션](#337-타이어-온도-시뮬레이션)
- [3.3.8 구현 참고사항](#338-구현-참고사항)

---

## 3.3.1 개요

### 목적
타이어는 차량과 노면 사이의 유일한 접점입니다. 타이어 모델의 품질이 전체 주행 느낌을 결정합니다.

### 설계 목표
- **Grid Legends 스타일**: 현실적이지만 관대함
- **예측 가능성**: 같은 상황 = 같은 결과
- **점진적 한계**: 급격한 그립 손실 없음
- **전략적 깊이**: 타이어 선택과 온도 관리

### 핵심 특징
```
┌─────────────────────────────────────┐
│ Pacejka Magic Formula (간소화)      │
│ + 노면 타입 시스템                  │
│ + 타이어 온도 시뮬레이션            │
│ + 타이어 컴파운드 (Dry/Int/Wet)    │
│ + 하중 민감도                       │
└─────────────────────────────────────┘
```

---

## 3.3.2 Pacejka Magic Formula

### 이론 배경

Pacejka Magic Formula는 타이어 마찰력을 모델링하는 산업 표준 공식입니다.

**원본 공식 (매우 복잡):**
```
F = D × sin(C × arctan(B × α - E × (B × α - arctan(B × α))))

여기서:
B = Stiffness factor
C = Shape factor
D = Peak value
E = Curvature factor
α = Slip angle or slip ratio
```

### 간소화 버전 (우리가 사용)

Grid Legends 스타일을 위해 E 파라미터를 제거한 단순화 버전:

```
F = D × sin(C × arctan(B × slip))

여기서:
D = Peak Force (최대 힘, N)
C = Shape Factor (커브 형태, 무차원)
B = Stiffness Factor (강성, 무차원)
slip = 슬립 비율 또는 슬립 각도 (무차원 또는 radian)
```

### 그래프 형태

```
타이어 힘 (N)
   ↑
8000│      ╱────── Peak (D)
    │     ╱ ╲
6000│    ╱   ╲
    │   ╱     ╲
4000│  ╱       ╲
    │ ╱         ╲
2000│╱           ─────
    └──────────────────────→ Slip
    0%   15%   30%   50%   100%
         ↑
      Peak Slip

특징:
- 0-15%: 선형 구간 (안정적)
- 15%: Peak (최대 그립)
- 15-30%: 완만한 감소 (컨트롤 가능)
- 30%+: 플래토 (예측 가능한 슬립)
```

### 파라미터 값 (Grid Legends 스타일)

```
차량 타입별 권장값:

┌──────────────┬──────┬──────┬──────┐
│   차량       │  D   │  C   │  B   │
├──────────────┼──────┼──────┼──────┤
│ 해치백       │ 6000 │ 1.6  │  8   │
│ 스포츠 쿠페  │ 7500 │ 1.65 │ 10   │
│ 슈퍼카       │ 9000 │ 1.7  │ 12   │
│ 레이스카     │ 12000│ 1.8  │ 15   │
└──────────────┴──────┴──────┴──────┘

파라미터 의미:
- D (Peak Force): 클수록 강력한 그립
- C (Shape): 클수록 날카로운 피크
- B (Stiffness): 클수록 민감한 반응
```

---

## 3.3.3 타이어 데이터 구조

### ScriptableObject 정의

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "TireData", menuName = "Vehicle/Tire Data")]
public class TireData : ScriptableObject
{
    [Header("=== Pacejka Parameters ===")]
    [Tooltip("D: Peak force in Newtons")]
    [Range(5000f, 15000f)]
    public float peakForce = 8000f;
    
    [Tooltip("C: Shape factor (커브 형태)")]
    [Range(1.3f, 2.0f)]
    public float shapeFactor = 1.65f;
    
    [Tooltip("B: Stiffness factor (반응 민감도)")]
    [Range(5f, 20f)]
    public float stiffnessFactor = 10f;
    
    [Header("=== Surface Grip Coefficients ===")]
    [Tooltip("Dry asphalt grip")]
    [Range(0.8f, 1.2f)]
    public float dryGrip = 1.0f;
    
    [Tooltip("Wet asphalt grip")]
    [Range(0.5f, 0.9f)]
    public float wetGrip = 0.7f;
    
    [Tooltip("Gravel/dirt grip")]
    [Range(0.3f, 0.7f)]
    public float gravelGrip = 0.5f;
    
    [Tooltip("Grass grip")]
    [Range(0.2f, 0.5f)]
    public float grassGrip = 0.3f;
    
    [Header("=== Load Sensitivity ===")]
    [Tooltip("하중 증가 시 그립 증가 비율")]
    [Range(1.0f, 2.0f)]
    public float loadSensitivity = 1.5f;
    
    [Tooltip("최대 설계 하중 (N)")]
    public float maxLoad = 5000f;
    
    [Header("=== Temperature System ===")]
    [Tooltip("최적 작동 온도 (°C)")]
    [Range(70f, 110f)]
    public float optimalTemperature = 90f;
    
    [Tooltip("현재 타이어 온도 (°C) - 런타임 변수")]
    [HideInInspector]
    public float currentTemperature = 20f;
    
    [Tooltip("슬립 시 가열 속도")]
    [Range(0.1f, 2.0f)]
    public float heatingRate = 0.5f;
    
    [Tooltip("자연 냉각 속도")]
    [Range(0.05f, 0.5f)]
    public float coolingRate = 0.2f;
    
    [Header("=== Tire Compound ===")]
    public TireCompound compound = TireCompound.Dry;
    
    [Header("=== Physical Properties ===")]
    [Tooltip("타이어 반지름 (m)")]
    public float radius = 0.35f;
    
    [Tooltip("타이어 폭 (m)")]
    public float width = 0.25f;
    
    [Tooltip("회전 관성 (kg·m²)")]
    public float rotationalInertia = 1.5f;
}

public enum TireCompound
{
    Dry,           // 드라이: 최대 그립, 빠른 가열, 비에서 약함
    Intermediate,  // 인터미디어트: 범용, 중간 성능
    Wet            // 웨트: 비에서 강함, 드라이에서 약함, 느린 가열
}
```

### 컴파운드별 특성

```
┌──────────────┬─────────┬─────────┬─────────┐
│   특성       │  Dry    │  Inter  │  Wet    │
├──────────────┼─────────┼─────────┼─────────┤
│ 드라이 그립  │  100%   │  90%    │  75%    │
│ 웨트 그립    │  60%    │  85%    │  130%   │
│ 최적 온도    │  90°C   │  70°C   │  50°C   │
│ 가열 속도    │  빠름   │  중간   │  느림   │
│ 내구성       │  낮음   │  중간   │  높음   │
└──────────────┴─────────┴─────────┴─────────┘

전략적 선택:
- Dry: 맑은 날씨에서 최고 성능, 비 오면 위험
- Inter: 날씨 변화가 예상될 때
- Wet: 폭우에서 필수, 드라이에서 느림
```

---

## 3.3.4 슬립 계산

### 4.1 종방향 슬립 (Longitudinal Slip)

가속과 브레이킹 시 발생하는 슬립입니다.

#### 이론

```
슬립 비율 정의:

slip = (V_wheel - V_car) / V_ref

여기서:
V_wheel = 바퀴 표면 속도 = ω × r
V_car = 차체 속도 (전진 방향)
V_ref = 참조 속도

참조 속도 선택:
- 가속 시 (V_wheel > V_car): V_ref = V_wheel
- 브레이크 시 (V_wheel < V_car): V_ref = |V_car|

결과:
- slip > 0: 가속 슬립 (휠스핀)
- slip < 0: 브레이크 슬립 (잠김)
- slip = 0: 완벽한 그립
```

#### 구현

```csharp
public class TireModel : MonoBehaviour
{
    [Header("References")]
    public TireData data;
    public Transform wheelTransform;
    
    // 상태 변수
    private float wheelAngularVelocity; // rad/s
    private Rigidbody vehicleRigidbody;
    
    /// <summary>
    /// 종방향 슬립 계산
    /// </summary>
    /// <returns>슬립 비율 (-1 ~ 1)</returns>
    public float CalculateLongitudinalSlip()
    {
        // 1. 바퀴 표면 속도 (m/s)
        float wheelSpeed = wheelAngularVelocity * data.radius;
        
        // 2. 차체 속도 (전진 방향만, m/s)
        Vector3 localVelocity = wheelTransform.InverseTransformDirection(
            vehicleRigidbody.velocity
        );
        float carSpeed = localVelocity.z; // 로컬 Z = 전진
        
        // 3. 정지 상태 처리
        if (Mathf.Abs(carSpeed) < 0.1f && Mathf.Abs(wheelSpeed) < 0.1f)
            return 0f;
        
        // 4. 슬립 계산
        float slip;
        
        if (wheelSpeed > carSpeed) // 가속 슬립
        {
            float vRef = Mathf.Max(Mathf.Abs(wheelSpeed), 0.1f);
            slip = (wheelSpeed - carSpeed) / vRef;
        }
        else // 브레이크 슬립
        {
            float vRef = Mathf.Max(Mathf.Abs(carSpeed), 0.1f);
            slip = (wheelSpeed - carSpeed) / vRef;
        }
        
        // 5. 클램핑 (-1 ~ 1)
        return Mathf.Clamp(slip, -1f, 1f);
    }
}
```

#### 예제 시나리오

```
시나리오 1: 휠스핀 (가속)
━━━━━━━━━━━━━━━━━━━━━━━━━
차량 속도: 50 km/h (13.9 m/s)
바퀴 회전: 100 rad/s
바퀴 반지름: 0.35 m
바퀴 속도: 100 × 0.35 = 35 m/s

slip = (35 - 13.9) / 35 = 0.60 (60% 슬립)
→ 강한 휠스핀, 그립 감소

시나리오 2: 브레이크 잠김
━━━━━━━━━━━━━━━━━━━━━━━━━
차량 속도: 80 km/h (22.2 m/s)
바퀴 회전: 0 rad/s (잠김)
바퀴 속도: 0 m/s

slip = (0 - 22.2) / 22.2 = -1.0 (-100% 슬립)
→ 완전 잠김, 최소 그립

시나리오 3: 최적 그립
━━━━━━━━━━━━━━━━━━━━━━━━━
차량 속도: 60 km/h (16.7 m/s)
바퀴 속도: 19.2 m/s
slip = (19.2 - 16.7) / 19.2 = 0.13 (13% 슬립)
→ Peak 그립 구간!
```

### 4.2 횡방향 슬립 (Lateral Slip)

코너링 시 발생하는 슬립입니다.

#### 이론

```
슬립 각도 정의:

α = arctan(V_lateral / V_forward)

여기서:
V_lateral = 측면 속도 (좌우)
V_forward = 전진 속도
α = 슬립 각도 (radian)

의미:
- α = 0: 직진 (슬립 없음)
- α > 0: 우측으로 미끄러짐
- α < 0: 좌측으로 미끄러짐
- |α| 클수록 심한 슬립
```

#### 구현

```csharp
/// <summary>
/// 횡방향 슬립 각도 계산
/// </summary>
/// <returns>슬립 각도 (radian, -π/2 ~ π/2)</returns>
public float CalculateLateralSlip()
{
    // 1. 로컬 좌표계로 속도 변환
    Vector3 localVelocity = wheelTransform.InverseTransformDirection(
        vehicleRigidbody.velocity
    );
    
    float lateralVelocity = localVelocity.x;  // 좌우
    float forwardVelocity = localVelocity.z;  // 전후
    
    // 2. 저속에서는 슬립 각도 0
    if (Mathf.Abs(forwardVelocity) < 0.5f)
        return 0f;
    
    // 3. 슬립 각도 계산 (atan2 사용)
    float slipAngle = Mathf.Atan2(lateralVelocity, Mathf.Abs(forwardVelocity));
    
    // 4. 클램핑 (±90도)
    return Mathf.Clamp(slipAngle, -Mathf.PI * 0.5f, Mathf.PI * 0.5f);
}
```

#### 예제 시나리오

```
시나리오 1: 날카로운 코너
━━━━━━━━━━━━━━━━━━━━━━━━━
전진 속도: 20 m/s
측면 속도: 5 m/s

α = arctan(5 / 20) = 0.245 rad ≈ 14°
→ 중간 슬립, 여전히 컨트롤 가능

시나리오 2: 드리프트
━━━━━━━━━━━━━━━━━━━━━━━━━
전진 속도: 15 m/s
측면 속도: 10 m/s

α = arctan(10 / 15) = 0.588 rad ≈ 34°
→ 강한 슬립, 드리프트 상태

시나리오 3: 직진
━━━━━━━━━━━━━━━━━━━━━━━━━
전진 속도: 30 m/s
측면 속도: 0.5 m/s

α = arctan(0.5 / 30) = 0.017 rad ≈ 1°
→ 거의 슬립 없음, 안정적
```

---

## 3.3.5 타이어 힘 계산

### 5.1 전체 흐름

```
타이어 힘 계산 파이프라인:

1. 슬립 계산
   ├─ 종방향 슬립 (가속/브레이크)
   └─ 횡방향 슬립 (코너링)
   
2. Pacejka Formula 적용
   ├─ 종방향 힘 Fx
   └─ 횡방향 힘 Fy
   
3. 수정자 적용
   ├─ 하중 (Load)
   ├─ 노면 타입 (Grip Coefficient)
   ├─ 온도 (Temperature)
   └─ 컴파운드 (Tire Compound)
   
4. 결합 슬립 처리
   └─ 가속 + 코너링 동시 시 그립 감소
   
5. 월드 좌표계 변환
   └─ 로컬 힘 → 글로벌 힘
```

### 5.2 구현

```csharp
/// <summary>
/// 타이어 힘 출력 구조체
/// </summary>
public struct TireForces
{
    public Vector3 longitudinal;  // 전후 힘
    public Vector3 lateral;       // 좌우 힘
    public Vector3 total;         // 총 힘
    public float slipRatio;       // 디버깅: 종방향 슬립
    public float slipAngle;       // 디버깅: 횡방향 슬립
    public bool isSlipping;       // 슬립 상태
}

/// <summary>
/// 메인 타이어 힘 계산 함수
/// </summary>
public TireForces CalculateForces(float normalLoad, SurfaceType surface)
{
    TireForces forces = new TireForces();
    
    // === 1. 슬립 계산 ===
    float longSlip = CalculateLongitudinalSlip();
    float latSlip = CalculateLateralSlip();
    
    forces.slipRatio = longSlip;
    forces.slipAngle = latSlip;
    
    // === 2. Pacejka Formula로 기본 힘 계산 ===
    float Fx_base = PacejkaFormula(longSlip, data.peakForce, data.shapeFactor, data.stiffnessFactor);
    float Fy_base = PacejkaFormula(latSlip, data.peakForce, data.shapeFactor, data.stiffnessFactor);
    
    // === 3. 하중 효과 ===
    float loadFactor = CalculateLoadEffect(normalLoad);
    Fx_base *= loadFactor;
    Fy_base *= loadFactor;
    
    // === 4. 노면 그립 계수 ===
    float gripCoeff = GetSurfaceGripCoefficient(surface);
    Fx_base *= gripCoeff;
    Fy_base *= gripCoeff;
    
    // === 5. 온도 효과 ===
    float tempFactor = CalculateTemperatureEffect();
    Fx_base *= tempFactor;
    Fy_base *= tempFactor;
    
    // === 6. 결합 슬립 (Combined Slip) ===
    float totalSlip = Mathf.Sqrt(longSlip * longSlip + latSlip * latSlip);
    
    if (totalSlip > 0.3f) // Grid Legends: 관대한 임계값
    {
        // 0.3 ~ 1.0 슬립일 때 선형으로 감소
        float excessSlip = Mathf.Clamp01((totalSlip - 0.3f) / 0.7f);
        float reduction = Mathf.Lerp(1.0f, 0.7f, excessSlip); // 최대 30% 감소
        
        Fx_base *= reduction;
        Fy_base *= reduction;
    }
    
    forces.isSlipping = totalSlip > 0.2f;
    
    // === 7. 방향 벡터로 변환 ===
    forces.longitudinal = wheelTransform.forward * Fx_base;
    forces.lateral = wheelTransform.right * Fy_base;
    forces.total = forces.longitudinal + forces.lateral;
    
    // === 8. 타이어 온도 업데이트 ===
    UpdateTemperature(totalSlip);
    
    return forces;
}
```

### 5.3 Pacejka Formula 함수

```csharp
/// <summary>
/// Pacejka Magic Formula (간소화 버전)
/// </summary>
/// <param name="slip">슬립 (비율 또는 각도)</param>
/// <param name="D">Peak force</param>
/// <param name="C">Shape factor</param>
/// <param name="B">Stiffness factor</param>
/// <returns>타이어 힘 (N)</returns>
private float PacejkaFormula(float slip, float D, float C, float B)
{
    // F = D × sin(C × arctan(B × slip))
    float x = B * slip;
    float y = D * Mathf.Sin(C * Mathf.Atan(x));
    return y;
}
```

### 5.4 하중 효과

```csharp
/// <summary>
/// 하중에 따른 그립 변화
/// </summary>
private float CalculateLoadEffect(float normalLoad)
{
    // 정규화 (0 ~ 1)
    float normalizedLoad = Mathf.Clamp01(normalLoad / data.maxLoad);
    
    // 비선형 관계: Load^sensitivity
    // sensitivity > 1 : 하중 증가 시 그립 많이 증가
    // sensitivity = 1 : 선형
    // sensitivity < 1 : 하중 증가 시 그립 적게 증가
    float loadFactor = Mathf.Pow(normalizedLoad, data.loadSensitivity);
    
    return Mathf.Clamp(loadFactor, 0.1f, 1.5f); // 안전 범위
}
```

**하중 효과 예시:**
```
예: loadSensitivity = 1.5

정규화 하중   →   Load Factor
━━━━━━━━━━━━━━━━━━━━━━━━━━
0.5 (50%)    →   0.35 (35%)  ← 하중 부족 시 그립 큰 폭 감소
1.0 (100%)   →   1.00 (100%)
1.2 (120%)   →   1.31 (131%) ← 하중 증가 시 보너스

의미: 
- 코너 외측 바퀴: 하중↑ → 그립↑
- 코너 내측 바퀴: 하중↓ → 그립↓↓
```

---

## 3.3.6 노면 상태 시스템

### 6.1 노면 타입 정의

```csharp
public enum SurfaceType
{
    DryAsphalt,   // 마른 아스팔트
    WetAsphalt,   // 젖은 아스팔트
    Gravel,       // 자갈
    Grass,        // 잔디
    Sand,         // 모래
    Snow,         // 눈 (선택사항)
    Ice           // 얼음 (선택사항)
}
```

### 6.2 그립 계수 계산

```csharp
/// <summary>
/// 노면 타입과 타이어 컴파운드에 따른 그립 계수
/// </summary>
private float GetSurfaceGripCoefficient(SurfaceType surface)
{
    // 1. 기본 노면 그립
    float baseGrip = surface switch
    {
        SurfaceType.DryAsphalt => data.dryGrip,
        SurfaceType.WetAsphalt => data.wetGrip,
        SurfaceType.Gravel => data.gravelGrip,
        SurfaceType.Grass => data.grassGrip,
        SurfaceType.Sand => 0.4f,
        SurfaceType.Snow => 0.3f,
        SurfaceType.Ice => 0.15f,
        _ => 1.0f
    };
    
    // 2. 타이어 컴파운드 보정
    float compoundMultiplier = 1.0f;
    
    switch (data.compound)
    {
        case TireCompound.Dry:
            if (surface == SurfaceType.WetAsphalt)
                compoundMultiplier = 0.6f; // 드라이 타이어는 비에서 -40%
            else if (surface == SurfaceType.DryAsphalt)
                compoundMultiplier = 1.0f; // 최적
            break;
            
        case TireCompound.Intermediate:
            if (surface == SurfaceType.WetAsphalt)
                compoundMultiplier = 0.9f; // 약간 불리
            else if (surface == SurfaceType.DryAsphalt)
                compoundMultiplier = 0.95f; // 약간 불리
            break;
            
        case TireCompound.Wet:
            if (surface == SurfaceType.WetAsphalt)
                compoundMultiplier = 1.3f; // 웨트 타이어는 비에서 +30%
            else if (surface == SurfaceType.DryAsphalt)
                compoundMultiplier = 0.75f; // 드라이에서 -25%
            break;
    }
    
    return baseGrip * compoundMultiplier;
}
```

### 6.3 노면 감지 (Raycast 통합)

```csharp
/// <summary>
/// 서스펜션 Raycast 결과로 노면 타입 감지
/// </summary>
public SurfaceType DetectSurfaceType(RaycastHit hit)
{
    // 방법 1: Collider Tag
    string tag = hit.collider.tag;
    
    SurfaceType surface = tag switch
    {
        "Road" => SurfaceType.DryAsphalt,
        "WetRoad" => SurfaceType.WetAsphalt,
        "Gravel" => SurfaceType.Gravel,
        "Grass" => SurfaceType.Grass,
        _ => SurfaceType.DryAsphalt
    };
    
    // 방법 2: PhysicMaterial (더 정교함)
    if (hit.collider.sharedMaterial != null)
    {
        string matName = hit.collider.sharedMaterial.name.ToLower();
        
        if (matName.Contains("wet")) 
            surface = SurfaceType.WetAsphalt;
        else if (matName.Contains("gravel"))
            surface = SurfaceType.Gravel;
        // ... 추가 매칭
    }
    
    return surface;
}
```

### 6.4 동적 노면 상태 (날씨 시스템 연동)

```csharp
/// <summary>
/// 날씨에 따른 노면 상태 변화
/// </summary>
public class DynamicSurfaceManager : MonoBehaviour
{
    [Header("Weather State")]
    public WeatherCondition currentWeather = WeatherCondition.Clear;
    
    [Header("Wetness")]
    [Range(0f, 1f)]
    public float trackWetness = 0f; // 0 = 완전 건조, 1 = 완전 젖음
    
    [Tooltip("비 올 때 젖는 속도")]
    public float wettingRate = 0.1f; // per second
    
    [Tooltip("건조 속도")]
    public float dryingRate = 0.05f; // per second
    
    void Update()
    {
        // 날씨에 따라 wetness 업데이트
        switch (currentWeather)
        {
            case WeatherCondition.Clear:
                trackWetness -= dryingRate * Time.deltaTime;
                break;
                
            case WeatherCondition.LightRain:
                trackWetness += wettingRate * 0.5f * Time.deltaTime;
                break;
                
            case WeatherCondition.HeavyRain:
                trackWetness += wettingRate * Time.deltaTime;
                break;
        }
        
        trackWetness = Mathf.Clamp01(trackWetness);
    }
    
    /// <summary>
    /// Wetness를 그립 계수로 변환
    /// </summary>
    public float GetWetnessGripMultiplier(TireCompound compound)
    {
        // 드라이 <-> 웨트 사이 보간
        float dryGrip = 1.0f;
        float wetGrip = compound switch
        {
            TireCompound.Dry => 0.6f,
            TireCompound.Intermediate => 0.85f,
            TireCompound.Wet => 1.3f,
            _ => 1.0f
        };
        
        return Mathf.Lerp(dryGrip, wetGrip, trackWetness);
    }
}

public enum WeatherCondition
{
    Clear,
    Cloudy,
    LightRain,
    HeavyRain,
    Fog
}
```

---

## 3.3.7 타이어 온도 시뮬레이션

### 7.1 온도 모델

```
타이어 온도 동역학:

dT/dt = Heating - Cooling

Heating = f(slip, load, speed)
Cooling = k × (T - T_ambient)

여기서:
T = 타이어 온도
T_ambient = 외기 온도
k = 냉각 계수
```

### 7.2 구현

```csharp
/// <summary>
/// 타이어 온도 업데이트 (매 FixedUpdate 호출)
/// </summary>
private void UpdateTemperature(float totalSlip)
{
    float dt = Time.fixedDeltaTime;
    
    // === 1. 발열 (Heating) ===
    
    // 슬립으로 인한 마찰열
    float slipHeating = totalSlip * data.heatingRate * dt;
    
    // 속도 보정 (빠를수록 많이 가열)
    float speed = vehicleRigidbody.velocity.magnitude;
    float speedFactor = Mathf.Clamp01(speed / 50f); // 50 m/s에서 최대
    slipHeating *= (0.5f + 0.5f * speedFactor);
    
    // === 2. 냉각 (Cooling) ===
    
    float ambientTemp = 20f; // 외기 온도 (°C)
    float tempDifference = data.currentTemperature - ambientTemp;
    
    // Newton's Law of Cooling
    float cooling = tempDifference * data.coolingRate * dt;
    
    // 속도에 의한 강제 냉각 (바람)
    float windCooling = speed * 0.01f * dt;
    cooling += windCooling;
    
    // === 3. 온도 변화 적용 ===
    
    data.currentTemperature += slipHeating - cooling;
    
    // 물리적 한계
    data.currentTemperature = Mathf.Clamp(data.currentTemperature, ambientTemp, 150f);
}
```

### 7.3 온도 효과

```csharp
/// <summary>
/// 온도가 그립에 미치는 영향
/// </summary>
private float CalculateTemperatureEffect()
{
    float temp = data.currentTemperature;
    float optimal = data.optimalTemperature;
    
    // 최적 온도에서 멀어질수록 그립 감소
    float tempDiff = Mathf.Abs(temp - optimal);
    
    // 온도 윈도우: ±20°C 이내는 90% 이상 유지
    float windowSize = 20f;
    
    if (tempDiff <= windowSize)
    {
        // 선형 감소: 최적에서 100%, ±20°에서 90%
        float ratio = tempDiff / windowSize;
        return Mathf.Lerp(1.0f, 0.9f, ratio);
    }
    else
    {
        // 윈도우 밖: 급격한 감소
        float excessTemp = tempDiff - windowSize;
        float penalty = excessTemp / 50f; // 50°C 초과 시 0%
        return Mathf.Lerp(0.9f, 0.6f, Mathf.Clamp01(penalty));
    }
}
```

### 7.4 온도 시각화 (디버그)

```csharp
/// <summary>
/// 타이어 온도를 색상으로 표시 (디버그용)
/// </summary>
public Color GetTemperatureColor()
{
    float temp = data.currentTemperature;
    float optimal = data.optimalTemperature;
    
    if (temp < optimal - 20f)
        return Color.blue;      // 너무 차가움
    else if (temp < optimal - 10f)
        return Color.cyan;      // 약간 차가움
    else if (temp <= optimal + 10f)
        return Color.green;     // 최적 범위
    else if (temp <= optimal + 20f)
        return Color.yellow;    // 약간 뜨거움
    else
        return Color.red;       // 너무 뜨거움
}
```

---

## 3.3.8 구현 참고사항

### 8.1 최적화 팁

```csharp
/// <summary>
/// 성능 최적화 버전 (캐싱 활용)
/// </summary>
public class TireModelOptimized : TireModel
{
    // 캐시된 계산 결과
    private float cachedLongSlip;
    private float cachedLatSlip;
    private float cachedGripCoeff;
    private int lastUpdateFrame;
    
    public override TireForces CalculateForces(float normalLoad, SurfaceType surface)
    {
        // 같은 프레임에서 여러 번 호출 방지
        if (lastUpdateFrame == Time.frameCount)
        {
            return cachedForces;
        }
        
        // ... 계산 로직 ...
        
        lastUpdateFrame = Time.frameCount;
        cachedForces = forces;
        return forces;
    }
}
```

### 8.2 디버그 시각화

```csharp
/// <summary>
/// 타이어 상태 디버그 시각화
/// </summary>
void OnDrawGizmos()
{
    if (!Application.isPlaying) return;
    
    // 1. 슬립 벡터
    Gizmos.color = Color.red;
    Vector3 slipDirection = wheelTransform.right * cachedLatSlip * 5f;
    Gizmos.DrawRay(wheelTransform.position, slipDirection);
    
    // 2. 힘 벡터
    Gizmos.color = Color.green;
    Vector3 forceDirection = cachedForces.total.normalized * 2f;
    Gizmos.DrawRay(wheelTransform.position, forceDirection);
    
    // 3. 접지점
    if (isGrounded)
    {
        Gizmos.color = GetTemperatureColor();
        Gizmos.DrawSphere(groundContactPoint, 0.1f);
    }
}
```

### 8.3 에디터 툴

```csharp
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(TireModel))]
public class TireModelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        TireModel tire = (TireModel)target;
        
        if (!Application.isPlaying)
            return;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("=== Runtime Status ===", EditorStyles.boldLabel);
        
        // 온도 표시
        EditorGUILayout.LabelField($"Temperature: {tire.data.currentTemperature:F1}°C");
        
        // 슬립 표시
        EditorGUILayout.LabelField($"Long Slip: {tire.cachedLongSlip:F3}");
        EditorGUILayout.LabelField($"Lat Slip: {tire.cachedLatSlip:F3}");
        
        // 힘 표시
        EditorGUILayout.LabelField($"Total Force: {tire.cachedForces.total.magnitude:F0} N");
        
        // 그래프 (간단한 바)
        Rect rect = EditorGUILayout.GetControlRect(false, 20f);
        float slipAmount = Mathf.Abs(tire.cachedLongSlip);
        EditorGUI.ProgressBar(rect, slipAmount, $"Slip: {slipAmount:P0}");
        
        // 온도 바
        rect = EditorGUILayout.GetControlRect(false, 20f);
        float tempRatio = tire.data.currentTemperature / 150f;
        EditorGUI.ProgressBar(rect, tempRatio, $"Temp: {tire.data.currentTemperature:F0}°C");
        
        Repaint();
    }
}
#endif
```

### 8.4 단위 테스트

```csharp
#if UNITY_EDITOR
using NUnit.Framework;

public class TireModelTests
{
    [Test]
    public void TestPacejkaFormula()
    {
        // Arrange
        float slip = 0.15f; // 15% 슬립 (일반적 피크)
        float D = 8000f;
        float C = 1.65f;
        float B = 10f;
        
        // Act
        TireModel tire = new TireModel();
        float force = tire.PacejkaFormula(slip, D, C, B);
        
        // Assert
        Assert.Greater(force, 7000f); // 피크 근처여야 함
        Assert.Less(force, 8500f);
    }
    
    [Test]
    public void TestSlipCalculation_Stationary()
    {
        // 정지 상태에서 슬립은 0
        TireModel tire = CreateTestTire();
        tire.wheelAngularVelocity = 0f;
        tire.vehicleRigidbody.velocity = Vector3.zero;
        
        float slip = tire.CalculateLongitudinalSlip();
        
        Assert.AreEqual(0f, slip, 0.01f);
    }
    
    [Test]
    public void TestTemperatureHeating()
    {
        // 슬립 시 온도 상승
        TireModel tire = CreateTestTire();
        float initialTemp = tire.data.currentTemperature;
        
        // 강한 슬립 시뮬레이션
        tire.UpdateTemperature(0.5f); // 50% 슬립
        
        Assert.Greater(tire.data.currentTemperature, initialTemp);
    }
    
    private TireModel CreateTestTire()
    {
        GameObject obj = new GameObject("TestTire");
        TireModel tire = obj.AddComponent<TireModel>();
        tire.data = ScriptableObject.CreateInstance<TireData>();
        // ... 초기화 ...
        return tire;
    }
}
#endif
```

### 8.5 설정 프리셋

```csharp
/// <summary>
/// 타이어 프리셋 (빠른 세팅용)
/// </summary>
public static class TirePresets
{
    public static TireData CreateStreetTire()
    {
        var data = ScriptableObject.CreateInstance<TireData>();
        data.peakForce = 6000f;
        data.shapeFactor = 1.6f;
        data.stiffnessFactor = 8f;
        data.compound = TireCompound.Dry;
        data.optimalTemperature = 80f;
        return data;
    }
    
    public static TireData CreateSportTire()
    {
        var data = ScriptableObject.CreateInstance<TireData>();
        data.peakForce = 8000f;
        data.shapeFactor = 1.65f;
        data.stiffnessFactor = 10f;
        data.compound = TireCompound.Dry;
        data.optimalTemperature = 90f;
        return data;
    }
    
    public static TireData CreateRaceTire()
    {
        var data = ScriptableObject.CreateInstance<TireData>();
        data.peakForce = 12000f;
        data.shapeFactor = 1.8f;
        data.stiffnessFactor = 15f;
        data.compound = TireCompound.Dry;
        data.optimalTemperature = 100f;
        return data;
    }
}
```

---

## 요약 체크리스트

구현 시 확인사항:

### 필수 구현
- [ ] TireData ScriptableObject 생성
- [ ] 종방향 슬립 계산 (`CalculateLongitudinalSlip`)
- [ ] 횡방향 슬립 계산 (`CalculateLateralSlip`)
- [ ] Pacejka Formula 함수 (`PacejkaFormula`)
- [ ] 타이어 힘 계산 (`CalculateForces`)
- [ ] 노면 타입 감지 (`DetectSurfaceType`)

### 권장 구현
- [ ] 타이어 온도 시스템
- [ ] 하중 효과 (`CalculateLoadEffect`)
- [ ] 결합 슬립 처리
- [ ] 타이어 컴파운드 시스템

### 선택적 구현
- [ ] 동적 날씨 시스템
- [ ] 온도 시각화
- [ ] 에디터 툴
- [ ] 단위 테스트

### 최적화
- [ ] 계산 결과 캐싱
- [ ] 같은 프레임 중복 호출 방지
- [ ] Gizmos 조건부 렌더링

---

## 다음 문서

이어질 문서들:
1. **3.2 서스펜션 시스템** - Raycast 기반 서스펜션
2. **3.4 엔진 모델** - 토크, 기어, 구동계
3. **3.5 공기역학** - 다운포스, 드래그
4. **3.6 차체 통합** - 모든 힘을 Rigidbody에 적용

---

**문서 버전**: 1.0  
**작성일**: 2024  
**상태**: 구현 준비 완료