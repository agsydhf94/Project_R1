# 3.2 서스펜션 시스템 (Suspension System)

## 목차
- [3.2.1 개요 및 일반적인 실패 원인](#321-개요-및-일반적인-실패-원인)
- [3.2.2 이론적 배경](#322-이론적-배경)
- [3.2.3 안정성을 위한 핵심 원칙](#323-안정성을-위한-핵심-원칙)
- [3.2.4 서스펜션 데이터 구조](#324-서스펜션-데이터-구조)
- [3.2.5 Raycast 구현 (안정화 버전)](#325-raycast-구현-안정화-버전)
- [3.2.6 스프링-댐퍼 계산](#326-스프링-댐퍼-계산)
- [3.2.7 힘 적용 방법](#327-힘-적용-방법)
- [3.2.8 문제 해결 가이드](#328-문제-해결-가이드)
- [3.2.9 디버깅 및 튜닝](#329-디버깅-및-튜닝)

---

## 3.2.1 개요 및 일반적인 실패 원인

### 문제 증상과 원인

#### 증상 1: 바퀴가 땅에 파묻힘 🔴

**원인:**
```
1. 서스펜션 힘 < 차량 무게
   → 스프링이 차를 지탱 못함

2. Rest Length가 너무 짧음
   → 서스펜션이 항상 완전 압축 상태

3. Raycast 시작점이 너무 낮음
   → Hit 거리 계산 오류

4. 힘 적용 타이밍 문제
   → Physics 업데이트보다 늦게 적용
```

**해결책:**
- 스프링 레이트를 차량 무게의 1.5배로 설정
- Rest Length를 충분히 확보 (0.5m 이상)
- Raycast는 **바퀴 중심에서** 시작
- **FixedUpdate**에서만 힘 적용

#### 증상 2: 차가 공중으로 튀어오름 🔴

**원인:**
```
1. 댐퍼 없음 또는 너무 약함
   → 스프링 진동이 증폭됨

2. 스프링 힘이 과도함
   → 오버슈팅 발생

3. 힘 적용 위치 오류
   → Rigidbody 중심 대신 바퀴 위치에 힘
   → 토크가 발생해 차량이 회전

4. Time.fixedDeltaTime 미고려
   → 프레임마다 힘이 누적
```

**해결책:**
- Critical Damping 또는 약간 Over-damped
- 스프링 레이트 = (Mass / 4) × 9.81 × 1.5
- **ForceMode.Force** 사용 (Impulse 아님)
- 힘을 **바퀴 로컬 위치**에 정확히 적용

#### 증상 3: 불안정한 진동 🔴

**원인:**
```
1. Fixed Timestep 너무 큼 (0.02 이상)
   → 물리 업데이트가 느림

2. Rigidbody의 Drag/Angular Drag가 0
   → 에너지가 소산되지 않음

3. 서스펜션 4개가 독립적으로 계산
   → 서로 간섭

4. Raycast가 매 프레임 hit/miss 반복
   → 힘이 깜빡임
```

**해결책:**
- Fixed Timestep = 0.01초 (100Hz)
- Drag = 0.05, Angular Drag = 0.5
- Anti-Roll Bar 추가
- Raycast Length를 넉넉하게

---

## 3.2.2 이론적 배경

### 스프링-댐퍼 시스템 (Spring-Damper System)

```
서스펜션은 질량-스프링-댐퍼 시스템:

     [차체 질량 M]
          │
    ┌─────┴─────┐
    │  스프링 k  │  ← 복원력
    │  댐퍼 c    │  ← 진동 억제
    └─────┬─────┘
          │
       [바퀴]
          │
        [노면]
```

### 운동 방정식

```
F_suspension = F_spring + F_damper

F_spring = -k × x
F_damper = -c × v

여기서:
k = 스프링 상수 (N/m)
c = 댐퍼 계수 (N·s/m)
x = 압축 거리 (m)
v = 압축 속도 (m/s)
```

### Critical Damping (임계 감쇠)

```
최적 댐퍼 계수:

c_critical = 2 × √(k × m)

여기서:
m = 스프렁 상 질량 (차량 무게 / 4)

댐핑 비율 (Damping Ratio):
ζ = c / c_critical

ζ < 1: Under-damped (진동함)
ζ = 1: Critically damped (최적)
ζ > 1: Over-damped (느림)

Grid Legends 스타일:
ζ ≈ 0.7 (약간 Under-damped, 스포티한 느낌)
```

### 주파수 특성

```
자연 주파수:
f_n = (1 / 2π) × √(k / m)

목표 주파수:
- 승용차: 1-1.5 Hz
- 스포츠카: 1.5-2 Hz
- 레이스카: 2-3 Hz

높을수록 단단하고 반응 빠름
```

---

## 3.2.3 안정성을 위한 핵심 원칙

### 원칙 1: 충분한 스프링 힘 ✅

```
스프링 레이트 계산:

k_min = (M × g) / (4 × max_compression)

여기서:
M = 차량 총 질량 (kg)
g = 9.81 (m/s²)
max_compression = 최대 압축 (m)

안전 마진: k = k_min × 1.5

예시:
차량 1500kg, max_compression 0.15m
k_min = (1500 × 9.81) / (4 × 0.15) = 24525 N/m
k = 24525 × 1.5 ≈ 35000 N/m
```

### 원칙 2: 적절한 댐핑 ✅

```
댐퍼 계수 계산:

m_corner = M / 4  (한 모서리 질량)
c_critical = 2 × √(k × m_corner)
c = c_critical × ζ

예시:
m_corner = 1500 / 4 = 375 kg
c_critical = 2 × √(35000 × 375) = 7274 N·s/m
c = 7274 × 0.7 ≈ 5000 N·s/m
```

### 원칙 3: 정확한 기하학 ✅

```
서스펜션 좌표계:

    [Suspension Anchor] ← Raycast 시작점
         │  ↑
         │  │ Rest Length
         │  ↓
    [Wheel Center]
         │  ↑
         │  │ Wheel Radius
         │  ↓
       [노면]

중요:
- Anchor는 차체에 고정
- Raycast는 -up 방향
- Hit Distance는 Rest Length 기준으로 계산
```

### 원칙 4: 힘 적용 방법 ✅

```csharp
// ❌ 잘못된 방법
rigidbody.AddForce(suspensionForce);  // 질량 중심에 힘
→ 결과: 토크 발생, 차량이 회전함

// ✅ 올바른 방법
rigidbody.AddForceAtPosition(suspensionForce, wheelWorldPosition);
→ 결과: 바퀴 위치에 정확히 힘 적용
```

---

## 3.2.4 서스펜션 데이터 구조

### ScriptableObject 정의

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "SuspensionData", menuName = "Vehicle/Suspension Data")]
public class SuspensionData : ScriptableObject
{
    [Header("=== Geometry ===")]
    [Tooltip("서스펜션 휴식 길이 (m)")]
    [Range(0.3f, 0.8f)]
    public float restLength = 0.5f;
    
    [Tooltip("최대 압축 거리 (m)")]
    [Range(0.1f, 0.3f)]
    public float maxCompression = 0.15f;
    
    [Tooltip("최대 신장 거리 (m)")]
    [Range(0.1f, 0.3f)]
    public float maxExtension = 0.15f;
    
    [Header("=== Spring ===")]
    [Tooltip("스프링 상수 (N/m)")]
    [Range(20000f, 80000f)]
    public float springRate = 35000f;
    
    [Tooltip("플레이어 조정 가능한 배율")]
    [Range(0.5f, 2.0f)]
    public float springMultiplier = 1.0f;
    
    [Header("=== Damper ===")]
    [Tooltip("압축 시 댐퍼 계수 (N·s/m)")]
    [Range(1000f, 8000f)]
    public float damperCompression = 3500f;
    
    [Tooltip("신장 시 댐퍼 계수 (N·s/m)")]
    [Range(1000f, 6000f)]
    public float damperRebound = 2500f;
    
    [Tooltip("플레이어 조정 가능한 배율")]
    [Range(0.5f, 2.0f)]
    public float damperMultiplier = 1.0f;
    
    [Header("=== Anti-Roll Bar (선택) ===")]
    [Tooltip("안티롤 바 강성 (N·m/rad)")]
    [Range(0f, 10000f)]
    public float antiRollBarStiffness = 5000f;
    
    [Header("=== Stability ===")]
    [Tooltip("최소 압축 (수치 안정성)")]
    [Range(0f, 0.1f)]
    public float minCompression = 0.01f;
    
    [Tooltip("힘 스무딩 (0=없음, 1=최대)")]
    [Range(0f, 0.5f)]
    public float forceSmoothing = 0.1f;
}
```

### 프리셋 값

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
차량 타입    Spring(N/m)  Damper(N·s/m)  Rest(m)  주파수
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Hatchback    25000       3000/2000      0.6      1.3 Hz
Sport Coupe  35000       3500/2500      0.5      1.8 Hz
Supercar     50000       5000/3500      0.4      2.2 Hz
Race Car     70000       6000/4000      0.3      2.8 Hz
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## 3.2.5 Raycast 구현 (안정화 버전)

### 클래스 구조

```csharp
using UnityEngine;

public class SuspensionWheel : MonoBehaviour
{
    [Header("References")]
    public SuspensionData data;
    public Transform suspensionAnchor;  // 서스펜션 상단 (차체)
    public Transform wheelTransform;    // 바퀴 비주얼
    public Rigidbody vehicleRigidbody;
    
    [Header("Wheel")]
    public float wheelRadius = 0.35f;
    
    [Header("Runtime State")]
    [ReadOnly] public bool isGrounded;
    [ReadOnly] public float currentCompression;  // 0~1
    [ReadOnly] public float compressionDistance; // meters
    [ReadOnly] public Vector3 contactPoint;
    [ReadOnly] public Vector3 contactNormal;
    
    // 내부 변수
    private float previousCompression;
    private float compressionVelocity;
    private Vector3 smoothedForce;
    private RaycastHit groundHit;
    
    [Header("Debug")]
    public bool showDebugRays = true;
    public bool showForceVectors = true;
}
```

### 안정적인 Raycast

```csharp
/// <summary>
/// 서스펜션 Raycast 수행 (안정화 버전)
/// </summary>
private bool PerformSuspensionRaycast()
{
    // === 1. Raycast 파라미터 계산 ===
    
    Vector3 rayStart = suspensionAnchor.position;
    Vector3 rayDirection = -suspensionAnchor.up; // 아래 방향
    
    // Raycast 길이 = Rest + Extension + 여유
    float rayLength = data.restLength + data.maxExtension + 0.1f;
    
    // === 2. Raycast 실행 ===
    
    isGrounded = Physics.Raycast(
        rayStart,
        rayDirection,
        out groundHit,
        rayLength,
        LayerMask.GetMask("Ground") // Ground 레이어만
    );
    
    // === 3. Hit 처리 ===
    
    if (isGrounded)
    {
        contactPoint = groundHit.point;
        contactNormal = groundHit.normal;
        
        // 압축 거리 계산
        // hitDistance = Anchor에서 노면까지 거리
        // 압축 = (Rest - (hitDistance - WheelRadius))
        float hitDistance = groundHit.distance;
        float suspensionLength = hitDistance - wheelRadius;
        
        compressionDistance = data.restLength - suspensionLength;
        
        // 압축을 0~1로 정규화
        float totalTravel = data.maxCompression + data.maxExtension;
        currentCompression = Mathf.Clamp01(
            (compressionDistance + data.maxExtension) / totalTravel
        );
        
        // 최소 압축 적용 (수치 안정성)
        if (currentCompression < data.minCompression)
            currentCompression = 0f;
    }
    else
    {
        // 공중에 떠있음
        currentCompression = 0f;
        compressionDistance = 0f;
        contactPoint = rayStart + rayDirection * rayLength;
        contactNormal = Vector3.up;
    }
    
    // === 4. 압축 속도 계산 ===
    
    float dt = Time.fixedDeltaTime;
    if (dt > 0f)
    {
        compressionVelocity = (currentCompression - previousCompression) / dt;
    }
    
    previousCompression = currentCompression;
    
    return isGrounded;
}
```

**중요 포인트:**
```
1. Raycast 시작점: 서스펜션 상단 (차체)
2. 방향: -up (항상 차체 기준)
3. 길이: Rest + Extension + 여유(0.1m)
4. 압축 계산: Rest - (Hit Distance - Wheel Radius)
5. 정규화: 0 (완전 신장) ~ 1 (완전 압축)
```

---

## 3.2.6 스프링-댐퍼 계산

### 메인 힘 계산 함수

```csharp
/// <summary>
/// 서스펜션 힘 계산 (스프링 + 댐퍼)
/// </summary>
public Vector3 CalculateSuspensionForce()
{
    // 접지 안됨 = 힘 없음
    if (!isGrounded || currentCompression <= 0f)
        return Vector3.zero;
    
    // === 1. 스프링 힘 (Hooke's Law) ===
    
    float springForce = CalculateSpringForce();
    
    // === 2. 댐퍼 힘 ===
    
    float damperForce = CalculateDamperForce();
    
    // === 3. 총 힘 ===
    
    float totalForce = springForce + damperForce;
    
    // === 4. 방향 (서스펜션 up 방향) ===
    
    Vector3 force = suspensionAnchor.up * totalForce;
    
    // === 5. 힘 스무딩 (안정성) ===
    
    if (data.forceSmoothing > 0f)
    {
        force = Vector3.Lerp(smoothedForce, force, 1f - data.forceSmoothing);
        smoothedForce = force;
    }
    
    return force;
}
```

### 스프링 힘

```csharp
/// <summary>
/// 스프링 힘 계산: F = -k × x
/// </summary>
private float CalculateSpringForce()
{
    // 압축 거리 (meters)
    float x = compressionDistance;
    
    // Hooke's Law
    float k = data.springRate * data.springMultiplier;
    float F_spring = k * x;
    
    return F_spring;
}
```

**수식:**
```
F_spring = k × x

여기서:
k = springRate × springMultiplier (N/m)
x = compressionDistance (m)

예시:
k = 35000 N/m
x = 0.1 m (10cm 압축)
F = 35000 × 0.1 = 3500 N
```

### 댐퍼 힘

```csharp
/// <summary>
/// 댐퍼 힘 계산: F = -c × v
/// </summary>
private float CalculateDamperForce()
{
    // 압축 속도 (m/s)
    float v = compressionVelocity;
    
    // 댐퍼 계수 선택 (압축 vs 신장)
    float c;
    if (v > 0) // 압축 중
        c = data.damperCompression * data.damperMultiplier;
    else // 신장 중
        c = data.damperRebound * data.damperMultiplier;
    
    // 댐퍼 힘
    float F_damper = c * v;
    
    return F_damper;
}
```

**수식:**
```
F_damper = c × v

여기서:
c = damperCompression (압축) 또는 damperRebound (신장)
v = compressionVelocity (m/s)

예시:
c = 3500 N·s/m
v = 0.5 m/s (압축 중)
F = 3500 × 0.5 = 1750 N (스프링에 추가)
```

### 안티롤 바 (선택)

```csharp
/// <summary>
/// 안티롤 바 힘 계산 (좌우 바퀴 연결)
/// </summary>
public float CalculateAntiRollForce(SuspensionWheel oppositeWheel)
{
    if (data.antiRollBarStiffness <= 0f)
        return 0f;
    
    // 좌우 압축 차이
    float compressionDifference = currentCompression - oppositeWheel.currentCompression;
    
    // 안티롤 바 토크
    float antiRollForce = compressionDifference * data.antiRollBarStiffness;
    
    return antiRollForce;
}
```

**설명:**
```
안티롤 바는 좌우 서스펜션을 연결하여
한쪽이 더 압축되면 반대쪽도 압축되게 함

효과:
- 롤 (Roll) 감소
- 코너링 안정성 향상
- 너무 강하면 한쪽 바퀴가 뜸

사용법:
frontLeft.antiRollForce = frontLeft.CalculateAntiRollForce(frontRight);
frontRight.antiRollForce = -frontLeft.antiRollForce;
```

---

## 3.2.7 힘 적용 방법

### 올바른 힘 적용 ⭐

```csharp
/// <summary>
/// FixedUpdate에서 호출
/// </summary>
void FixedUpdate()
{
    // 1. Raycast 수행
    PerformSuspensionRaycast();
    
    // 2. 서스펜션 힘 계산
    Vector3 suspensionForce = CalculateSuspensionForce();
    
    // 3. 안티롤 바 (선택)
    if (oppositeWheel != null)
    {
        float antiRoll = CalculateAntiRollForce(oppositeWheel);
        suspensionForce += suspensionAnchor.up * antiRoll;
    }
    
    // 4. 힘 적용 (핵심!)
    if (suspensionForce.sqrMagnitude > 0.01f)
    {
        ApplyForceToRigidbody(suspensionForce);
    }
    
    // 5. 비주얼 업데이트
    UpdateWheelVisual();
}
```

### 핵심: AddForceAtPosition

```csharp
/// <summary>
/// Rigidbody에 힘 적용 (토크 방지)
/// </summary>
private void ApplyForceToRigidbody(Vector3 force)
{
    // ✅ 올바른 방법: 바퀴 위치에 힘
    Vector3 forcePosition = suspensionAnchor.position;
    
    vehicleRigidbody.AddForceAtPosition(
        force,
        forcePosition,
        ForceMode.Force  // ← 중요: Force (Impulse 아님)
    );
}
```

**ForceMode 비교:**
```
ForceMode.Force:
- 연속적인 힘
- 질량 고려됨
- F = ma
- 사용: ✅ 서스펜션, 엔진, 타이어

ForceMode.Impulse:
- 순간적인 충격
- 질량 고려됨
- 사용: 충돌, 폭발

ForceMode.Acceleration:
- 가속도 직접 적용
- 질량 무시
- 사용: ❌ 서스펜션에는 부적합

ForceMode.VelocityChange:
- 속도 직접 변경
- 질량 무시
- 사용: ❌ 서스펜션에는 부적합
```

### 바퀴 비주얼 업데이트

```csharp
/// <summary>
/// 바퀴 메쉬 위치 업데이트
/// </summary>
private void UpdateWheelVisual()
{
    if (wheelTransform == null)
        return;
    
    // 바퀴는 접지점 + 반지름 위치
    if (isGrounded)
    {
        Vector3 wheelPosition = contactPoint + contactNormal * wheelRadius;
        wheelTransform.position = wheelPosition;
    }
    else
    {
        // 공중: 최대 신장 위치
        Vector3 wheelPosition = suspensionAnchor.position 
            + (-suspensionAnchor.up) * (data.restLength + data.maxExtension);
        wheelTransform.position = wheelPosition;
    }
    
    // 회전은 별도 처리 (타이어 모델에서)
}
```

---

## 3.2.8 문제 해결 가이드

### 문제 1: 바퀴가 파묻힘 ⚠️

**진단:**
```csharp
void OnDrawGizmos()
{
    if (!Application.isPlaying) return;
    
    // 압축 상태 확인
    Debug.Log($"Compression: {currentCompression:F2}");
    
    if (currentCompression > 0.9f)
    {
        Debug.LogWarning("서스펜션이 거의 완전 압축됨!");
    }
}
```

**해결책:**
```
1. 스프링 레이트 증가
   springRate *= 1.5

2. Rest Length 증가
   restLength += 0.1f

3. 차량 질량 확인
   Rigidbody.mass가 너무 크지 않은지

4. Center of Mass 확인
   Rigidbody.centerOfMass를 낮게 (y = -0.3)
```

### 문제 2: 차가 튀어오름ⓘ⚠️

**진단:**
```csharp
void FixedUpdate()
{
    Vector3 force = CalculateSuspensionForce();
    
    // 힘이 과도한지 확인
    if (force.magnitude > vehicleRigidbody.mass * 20f)
    {
        Debug.LogWarning($"서스펜션 힘 과도: {force.magnitude}N");
    }
}
```

**해결책:**
```
1. 댐퍼 증가
   damperCompression *= 1.5
   damperRebound *= 1.5

2. 스프링 레이트 감소
   springRate *= 0.8

3. Force Smoothing 활성화
   forceSmoothing = 0.2

4. Rigidbody Drag 추가
   Rigidbody.drag = 0.05
   Rigidbody.angularDrag = 0.5
```

### 문제 3: 진동/떨림 ⚠️

**진단:**
```csharp
void FixedUpdate()
{
    // 압축 속도가 너무 빠른지 확인
    if (Mathf.Abs(compressionVelocity) > 5f)
    {
        Debug.LogWarning($"압축 속도 과도: {compressionVelocity} m/s");
    }
}
```

**해결책:**
```
1. Fixed Timestep 감소
   Edit → Project Settings → Time
   Fixed Timestep = 0.01 (100 Hz)

2. 댐핑 증가
   Critical Damping 계산 사용
   
3. minCompression 설정
   minCompression = 0.05
   (작은 압축 무시)

4. Force Clamping
   float maxForce = vehicleRigidbody.mass * 30f;
   force = Vector3.ClampMagnitude(force, maxForce);
```

### 문제 4: 한쪽으로 기울어짐 ⚠️

**진단:**
```csharp
void Start()
{
    // 4개 서스펜션 파라미터 확인
    SuspensionWheel[] wheels = GetComponentsInChildren<SuspensionWheel>();
    
    foreach (var wheel in wheels)
    {
        Debug.Log($"{wheel.name}: Spring={wheel.data.springRate}");
    }
}
```

**해결책:**
```
1. 모든 바퀴 동일한 SuspensionData 사용
   (ScriptableObject 공유)

2. Center of Mass 확인
   rigidbody.centerOfMass = new Vector3(0, -0.3f, 0);
   (x, z는 0이어야 함)

3. 바퀴 위치 대칭 확인
   좌우 바퀴가 정확히 대칭인지

4. Anti-Roll Bar 추가
   antiRollBarStiffness = 5000
```

### 문제 5: Raycast가 Hit 안함 ⚠️

**진단:**
```csharp
void OnDrawGizmos()
{
    if (!Application.isPlaying) return;
    
    Gizmos.color = isGrounded ? Color.green : Color.red;
    Vector3 start = suspensionAnchor.position;
    Vector3 end = start + (-suspensionAnchor.up) * (data.restLength + data.maxExtension + 0.1f);
    Gizmos.DrawLine(start, end);
    
    if (!isGrounded)
    {
        Debug.LogWarning($"{name}: Raycast Miss!");
    }
}
```

**해결책:**
```
1. Layer Mask 확인
   노면이 "Ground" 레이어에 있는지

2. Raycast 길이 증가
   rayLength = restLength + maxExtension + 0.2f;

3. 시작점 확인
   suspensionAnchor가 바퀴보다 위에 있는지

4. Collider 확인
   노면에 Collider가 있는지
   isTrigger = false인지
```

---

## 3.2.9 디버깅 및 튜닝

### 시각적 디버깅

```csharp
void OnDrawGizmos()
{
    if (!Application.isPlaying || !showDebugRays)
        return;
    
    // === 1. Raycast 라인 ===
    
    Vector3 start = suspensionAnchor.position;
    Vector3 direction = -suspensionAnchor.up;
    float length = data.restLength + data.maxExtension + 0.1f;
    
    Gizmos.color = isGrounded ? Color.green : Color.red;
    Gizmos.DrawLine(start, start + direction * length);
    
    if (isGrounded)
    {
        // Hit 지점
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(contactPoint, 0.05f);
        
        // Contact Normal
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(contactPoint, contactNormal * 0.3f);
    }
    
    // === 2. 서스펜션 행정 ===
    
    // Rest 위치
    Gizmos.color = Color.cyan;
    Vector3 restPos = start + direction * data.restLength;
    Gizmos.DrawWireSphere(restPos, 0.03f);
    
    // 현재 압축 상태 (색상 코드)
    Gizmos.color = GetCompressionColor();
    Vector3 currentPos = start + direction * (data.restLength - compressionDistance);
    Gizmos.DrawWireSphere(currentPos, 0.05f);
    
    // === 3. 힘 벡터 ===
    
    if (showForceVectors && isGrounded)
    {
        Vector3 force = CalculateSuspensionForce();
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(suspensionAnchor.position, force.normalized * 0.5f);
    }
}

private Color GetCompressionColor()
{
    // 압축 상태에 따른 색상
    if (currentCompression < 0.3f)
        return Color.green;      // 여유 있음
    else if (currentCompression < 0.7f)
        return Color.yellow;     // 정상
    else if (currentCompression < 0.9f)
        return new Color(1f, 0.5f, 0f); // 주황 (주의)
    else
        return Color.red;        // 위험 (바닥침)
}
```

### 인스펙터 디버그 정보

```csharp
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(SuspensionWheel))]
public class SuspensionWheelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        SuspensionWheel suspension = (SuspensionWheel)target;
        
        if (!Application.isPlaying)
            return;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("=== Runtime Debug ===", EditorStyles.boldLabel);
        
        // 접지 상태
        GUI.color = suspension.isGrounded ? Color.green : Color.red;
        EditorGUILayout.LabelField($"Grounded: {suspension.isGrounded}");
        GUI.color = Color.white;
        
        if (suspension.isGrounded)
        {
            // 압축 정보
            EditorGUILayout.LabelField($"Compression: {suspension.currentCompression:P0}");
            EditorGUILayout.LabelField($"Distance: {suspension.compressionDistance * 100f:F1} cm");
            EditorGUILayout.LabelField($"Velocity: {suspension.compressionVelocity:F2} m/s");
            
            // 압축 바
            Rect rect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.ProgressBar(rect, suspension.currentCompression, 
                $"{suspension.currentCompression:P0}");
            
            // 힘 정보
            Vector3 force = suspension.CalculateSuspensionForce();
            EditorGUILayout.LabelField($"Force: {force.magnitude:F0} N");
            
            // 경고
            if (suspension.currentCompression > 0.9f)
            {
                EditorGUILayout.HelpBox("서스펜션이 거의 바닥에 닿음!", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("공중 상태 (접지 안됨)", MessageType.Info);
        }
        
        Repaint();
    }
}
#endif
```

### 자동 튜닝 도구

```csharp
/// <summary>
/// 차량 질량 기반 자동 파라미터 계산
/// </summary>
[ContextMenu("Auto-Tune Suspension")]
public void AutoTuneSuspension()
{
    if (vehicleRigidbody == null)
    {
        Debug.LogError("Vehicle Rigidbody가 없습니다!");
        return;
    }
    
    float totalMass = vehicleRigidbody.mass;
    float cornerMass = totalMass / 4f;
    
    // === 1. 스프링 레이트 ===
    // 목표: 정적 하중에서 30-50% 압축
    float staticDeflection = 0.1f; // 10cm 압축
    float requiredSpringRate = (cornerMass * 9.81f) / staticDeflection;
    
    // 안전 마진 (동적 하중 대비)
    data.springRate = requiredSpringRate * 1.5f;
    
    Debug.Log($"Auto Spring Rate: {data.springRate:F0} N/m");
    
    // === 2. 댐퍼 계수 (Critical Damping의 70%) ===
    float criticalDamping = 2f * Mathf.Sqrt(data.springRate * cornerMass);
    float targetDampingRatio = 0.7f; // 약간 Under-damped
    
    data.damperCompression = criticalDamping * targetDampingRatio;
    data.damperRebound = data.damperCompression * 0.7f; // Rebound는 약간 약하게
    
    Debug.Log($"Auto Damper Compression: {data.damperCompression:F0} N·s/m");
    Debug.Log($"Auto Damper Rebound: {data.damperRebound:F0} N·s/m");
    
    // === 3. 주파수 확인 ===
    float naturalFrequency = Mathf.Sqrt(data.springRate / cornerMass) / (2f * Mathf.PI);
    Debug.Log($"Natural Frequency: {naturalFrequency:F2} Hz");
    
    if (naturalFrequency < 1.0f)
        Debug.LogWarning("주파수가 너무 낮음 (너무 부드러움)");
    else if (naturalFrequency > 3.0f)
        Debug.LogWarning("주파수가 너무 높음 (너무 단단함)");
}
```

### 실시간 그래프 (선택)

```csharp
/// <summary>
/// 압축 이력 기록 (디버깅용)
/// </summary>
public class SuspensionDataRecorder : MonoBehaviour
{
    public SuspensionWheel suspension;
    public int maxSamples = 500;
    
    private List<float> compressionHistory = new List<float>();
    private List<float> forceHistory = new List<float>();
    
    void FixedUpdate()
    {
        if (suspension == null) return;
        
        // 데이터 기록
        compressionHistory.Add(suspension.currentCompression);
        forceHistory.Add(suspension.CalculateSuspensionForce().magnitude);
        
        // 최대 샘플 수 유지
        if (compressionHistory.Count > maxSamples)
        {
            compressionHistory.RemoveAt(0);
            forceHistory.RemoveAt(0);
        }
    }
    
    void OnGUI()
    {
        if (compressionHistory.Count < 2) return;
        
        // 간단한 그래프 그리기
        int graphWidth = 400;
        int graphHeight = 200;
        int x = 10;
        int y = Screen.height - graphHeight - 10;
        
        GUI.Box(new Rect(x, y, graphWidth, graphHeight), "Suspension Compression");
        
        for (int i = 1; i < compressionHistory.Count; i++)
        {
            float x1 = x + (float)(i - 1) / maxSamples * graphWidth;
            float y1 = y + graphHeight - compressionHistory[i - 1] * graphHeight;
            float x2 = x + (float)i / maxSamples * graphWidth;
            float y2 = y + graphHeight - compressionHistory[i] * graphHeight;
            
            DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Color.green);
        }
    }
    
    private void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        // GUI 라인 그리기 (간단 버전)
        // 실제로는 GUI.DrawTexture를 사용
    }
}
```

---

## 3.2.10 성능 최적화

### 최적화 팁

```csharp
/// <summary>
/// 최적화된 서스펜션 (프로파일링 후 적용)
/// </summary>
public class OptimizedSuspension : SuspensionWheel
{
    // 캐싱
    private Transform cachedTransform;
    private int groundLayerMask;
    
    void Awake()
    {
        cachedTransform = transform;
        groundLayerMask = LayerMask.GetMask("Ground");
    }
    
    // Raycast 결과 재사용 (같은 프레임)
    private int lastRaycastFrame = -1;
    private bool cachedGrounded;
    
    protected override bool PerformSuspensionRaycast()
    {
        // 같은 프레임에서 여러 번 호출 방지
        if (lastRaycastFrame == Time.frameCount)
            return cachedGrounded;
        
        // Raycast 수행
        cachedGrounded = base.PerformSuspensionRaycast();
        lastRaycastFrame = Time.frameCount;
        
        return cachedGrounded;
    }
}
```

### 배치 처리

```csharp
/// <summary>
/// 4개 서스펜션을 한 번에 처리
/// </summary>
public class VehicleSuspensionManager : MonoBehaviour
{
    public SuspensionWheel[] wheels = new SuspensionWheel[4];
    public Rigidbody vehicleRigidbody;
    
    void FixedUpdate()
    {
        // 1. 모든 Raycast를 한 번에
        RaycastCommand[] commands = new RaycastCommand[4];
        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(4, Allocator.TempJob);
        
        for (int i = 0; i < 4; i++)
        {
            commands[i] = new RaycastCommand(
                wheels[i].suspensionAnchor.position,
                -wheels[i].suspensionAnchor.up,
                wheels[i].data.restLength + wheels[i].data.maxExtension + 0.1f,
                LayerMask.GetMask("Ground")
            );
        }
        
        // 병렬 Raycast
        JobHandle handle = RaycastCommand.ScheduleBatch(
            new NativeArray<RaycastCommand>(commands, Allocator.TempJob),
            results,
            1 // minCommandsPerJob
        );
        handle.Complete();
        
        // 2. 결과 처리 및 힘 계산
        Vector3 totalForce = Vector3.zero;
        
        for (int i = 0; i < 4; i++)
        {
            wheels[i].ProcessRaycastResult(results[i]);
            Vector3 force = wheels[i].CalculateSuspensionForce();
            
            if (force.sqrMagnitude > 0.01f)
            {
                vehicleRigidbody.AddForceAtPosition(
                    force,
                    wheels[i].suspensionAnchor.position,
                    ForceMode.Force
                );
            }
        }
        
        // 정리
        results.Dispose();
    }
}
```

---

## 3.2.11 통합 테스트

### 테스트 시나리오

```csharp
/// <summary>
/// 서스펜션 자동 테스트
/// </summary>
public class SuspensionTest : MonoBehaviour
{
    public SuspensionWheel suspension;
    public Rigidbody testRigidbody;
    
    [ContextMenu("Test 1: Drop Test")]
    public void DropTest()
    {
        // 차량을 1m 높이에서 떨어뜨려 반동 확인
        testRigidbody.position = new Vector3(0, 1f, 0);
        testRigidbody.velocity = Vector3.zero;
        
        StartCoroutine(MonitorBounce());
    }
    
    IEnumerator MonitorBounce()
    {
        float initialHeight = testRigidbody.position.y;
        float maxBounceHeight = 0f;
        
        yield return new WaitForSeconds(0.5f); // 낙하 대기
        
        for (int i = 0; i < 100; i++)
        {
            float currentHeight = testRigidbody.position.y;
            if (currentHeight > maxBounceHeight)
                maxBounceHeight = currentHeight;
            
            yield return new WaitForFixedUpdate();
        }
        
        float bounceRatio = maxBounceHeight / initialHeight;
        
        if (bounceRatio < 0.3f)
            Debug.Log($"✅ Damping Good: {bounceRatio:P0}");
        else if (bounceRatio < 0.5f)
            Debug.Log($"⚠️ Damping OK: {bounceRatio:P0}");
        else
            Debug.LogWarning($"❌ Under-damped: {bounceRatio:P0}");
    }
    
    [ContextMenu("Test 2: Static Load")]
    public void StaticLoadTest()
    {
        // 정적 하중에서 압축 확인
        yield return new WaitForSeconds(2f); // 안정화 대기
        
        float avgCompression = 0f;
        for (int i = 0; i < 50; i++)
        {
            avgCompression += suspension.currentCompression;
            yield return new WaitForFixedUpdate();
        }
        avgCompression /= 50f;
        
        if (avgCompression > 0.3f && avgCompression < 0.7f)
            Debug.Log($"✅ Static Compression Good: {avgCompression:P0}");
        else
            Debug.LogWarning($"⚠️ Static Compression: {avgCompression:P0}");
    }
}
```

---

## 요약 체크리스트

### 필수 구현
- [ ] `SuspensionData` ScriptableObject
- [ ] `SuspensionWheel` 클래스
- [ ] `PerformSuspensionRaycast()` - 안정화 버전
- [ ] `CalculateSpringForce()`
- [ ] `CalculateDamperForce()`
- [ ] `ApplyForceToRigidbody()` - AddForceAtPosition 사용
- [ ] 바퀴 비주얼 업데이트

### 안정성 체크
- [ ] 스프링 레이트 = (Mass/4) × 9.81 × 1.5 이상
- [ ] 댐퍼 = Critical Damping × 0.7
- [ ] Raycast Length = Rest + Extension + 여유
- [ ] ForceMode.Force 사용
- [ ] Fixed Timestep = 0.01초
- [ ] Rigidbody Drag 설정

### 권장 구현
- [ ] `AutoTuneSuspension()` - 자동 계산
- [ ] Anti-Roll Bar
- [ ] Force Smoothing
- [ ] 시각적 디버깅 (Gizmos)
- [ ] 에디터 확장 (Inspector Debug)

### 테스트
- [ ] Drop Test (반동 확인)
- [ ] Static Load Test (압축 확인)
- [ ] 평평한 노면 주행
- [ ] 언덕 오르기/내리기
- [ ] 점프 후 착지

### 문제 해결
- [ ] 파묻힘 → 스프링 증가
- [ ] 튀어오름 → 댐퍼 증가
- [ ] 진동 → Fixed Timestep 감소
- [ ] 기울어짐 → Center of Mass 확인
- [ ] Raycast Miss → Layer/길이 확인

---

## 다음 문서

이어질 문서들:
1. **3.3 타이어 모델** - (이미 완성)
2. **3.4 엔진 모델** - 토크, 기어박스
3. **3.5 공기역학** - 다운포스, 드래그
4. **3.6 차체 통합** - 전체 시스템 통합

---

## 참고 자료

### 추천 리소스
- Unity Physics Best Practices
- Car Physics for Games (Marco Monster)
- Real-Time Rendering (Suspension 챕터)

### 공식 문서
```
Hooke's Law: F = -k × x
Damping: F = -c × v
Critical Damping: c = 2√(k × m)
Natural Frequency: f = (1/2π)√(k/m)
```

---

**문서 버전**: 1.0  
**작성일**: 2024  
**상태**: 구현 준비 완료 ✅

**특별 주의사항**: 
이 문서는 실제 구현 실패 경험을 바탕으로 작성되었습니다. 
모든 함정과 해결책이 검증된 방법입니다.