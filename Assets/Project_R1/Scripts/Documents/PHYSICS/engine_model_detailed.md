# 3.4 엔진 모델 (Engine Model)

## 목차
- [3.4.1 개요 및 레이스카 특성](#341-개요-및-레이스카-특성)
- [3.4.2 엔진 데이터 구조](#342-엔진-데이터-구조)
- [3.4.3 토크 커브 시스템](#343-토크-커브-시스템)
- [3.4.4 RPM 시뮬레이션](#344-rpm-시뮬레이션)
- [3.4.5 기어박스 시스템](#345-기어박스-시스템)
- [3.4.6 기어 변속 메커니즘](#346-기어-변속-메커니즘)
- [3.4.7 RPM Wobble & 변속 효과](#347-rpm-wobble--변속-효과)
- [3.4.8 엔진 브레이킹](#348-엔진-브레이킹)
- [3.4.9 구동계 통합](#349-구동계-통합)
- [3.4.10 사운드 연동](#3410-사운드-연동)

---

## 3.4.1 개요 및 레이스카 특성

### 레이스카 엔진 특성

```
일반 자가용 vs 레이스카:

┌──────────────┬─────────────┬──────────────┐
│   특성       │  일반 차량  │  레이스카    │
├──────────────┼─────────────┼──────────────┤
│ 레드라인     │  6000 RPM   │  8500 RPM    │
│ 최대 토크    │  3000 RPM   │  6000 RPM    │
│ RPM 반응     │  느림       │  매우 빠름   │
│ 기어박스     │  자동/DCT   │  시퀀셜      │
│ 변속 시간    │  0.3-0.5초  │  0.05-0.1초  │
│ 엔진 브레이크│  약함       │  강함        │
│ 아이들 RPM   │  800 RPM    │  1500 RPM    │
└──────────────┴─────────────┴──────────────┘
```

### 구현 목표

```
핵심 연출 요소:

1. RPM 자연스러운 변화
   ├─ 관성 시뮬레이션 (가속/감속)
   ├─ 부드러운 보간
   └─ 리미터 효과

2. 기어 변속 느낌
   ├─ 파워컷 (0.08초)
   ├─ RPM Wobble (흔들림)
   ├─ 토크 순간 증가
   └─ 사운드 변화

3. 레이스카 느낌
   ├─ 빠른 반응
   ├─ 높은 레드라인
   ├─ 강한 엔진 브레이크
   └─ 시퀀셜 기어박스
```

---

## 3.4.2 엔진 데이터 구조

### ScriptableObject 정의

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "EngineData", menuName = "Vehicle/Engine Data")]
public class EngineData : ScriptableObject
{
    [Header("=== Engine Characteristics ===")]
    [Tooltip("엔진 타입")]
    public EngineType engineType = EngineType.RaceCar;
    
    [Tooltip("배기량 (cc) - 정보용")]
    public int displacement = 3000;
    
    [Tooltip("실린더 수 - 사운드용")]
    public int cylinderCount = 6;
    
    [Header("=== RPM Range ===")]
    [Tooltip("아이들 RPM")]
    [Range(800f, 2000f)]
    public float idleRPM = 1500f;
    
    [Tooltip("최대 RPM (레드라인)")]
    [Range(6000f, 10000f)]
    public float maxRPM = 8500f;
    
    [Tooltip("RPM 리미터 (레드라인 약간 위)")]
    [Range(6000f, 10000f)]
    public float limiterRPM = 8700f;
    
    [Header("=== Torque Curve ===")]
    [Tooltip("최대 토크 (Nm)")]
    [Range(200f, 1000f)]
    public float maxTorque = 650f;
    
    [Tooltip("최대 토크 RPM")]
    [Range(3000f, 8000f)]
    public float peakTorqueRPM = 6000f;
    
    [Tooltip("토크 커브 (RPM → Torque 비율)")]
    public AnimationCurve torqueCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);
    
    [Header("=== RPM Dynamics ===")]
    [Tooltip("엔진 관성 (kg·m²) - 클수록 느리게 반응")]
    [Range(0.1f, 2.0f)]
    public float engineInertia = 0.3f;
    
    [Tooltip("RPM 상승 속도 배율")]
    [Range(1f, 5f)]
    public float revUpRate = 3.0f;
    
    [Tooltip("RPM 하강 속도 배율")]
    [Range(0.5f, 3f)]
    public float revDownRate = 1.5f;
    
    [Tooltip("엔진 마찰/저항 (RPM 감쇠)")]
    [Range(0.01f, 0.5f)]
    public float engineFriction = 0.15f;
    
    [Header("=== Gear Shift Effects ===")]
    [Tooltip("변속 시간 (초)")]
    [Range(0.05f, 0.3f)]
    public float shiftDuration = 0.08f;
    
    [Tooltip("변속 시 파워컷 비율")]
    [Range(0f, 1f)]
    public float powerCutAmount = 0.9f; // 90% 컷
    
    [Tooltip("RPM Wobble 강도")]
    [Range(0f, 500f)]
    public float rpmWobbleAmount = 200f;
    
    [Tooltip("RPM Wobble 주파수 (Hz)")]
    [Range(5f, 30f)]
    public float rpmWobbleFrequency = 15f;
    
    [Tooltip("RPM Wobble 감쇠 속도")]
    [Range(1f, 10f)]
    public float rpmWobbleDamping = 5f;
    
    [Header("=== Engine Braking ===")]
    [Tooltip("엔진 브레이크 강도")]
    [Range(0f, 1f)]
    public float engineBrakingStrength = 0.6f; // 레이스카는 강함
    
    [Header("=== Limiter ===")]
    [Tooltip("RPM 리미터 활성화")]
    public bool enableLimiter = true;
    
    [Tooltip("리미터 컷 주파수 (Hz)")]
    [Range(10f, 50f)]
    public float limiterCutFrequency = 30f;
}

public enum EngineType
{
    Inline4,        // 직렬 4기통
    V6,             // V6
    V8,             // V8
    RaceCar,        // 레이스카 특화
    Formula         // 포뮬러
}
```

### 프리셋 예시

```
레이스카 GT3 스타일:
━━━━━━━━━━━━━━━━━━━━━━━
idleRPM: 1500
maxRPM: 8500
limiterRPM: 8700
maxTorque: 650 Nm
peakTorqueRPM: 6000
engineInertia: 0.3
shiftDuration: 0.08초
rpmWobble: 200 RPM

포뮬러 스타일:
━━━━━━━━━━━━━━━━━━━━━━━
idleRPM: 2000
maxRPM: 12000
limiterRPM: 12200
maxTorque: 500 Nm
peakTorqueRPM: 10000
engineInertia: 0.15 (매우 가벼움)
shiftDuration: 0.05초
rpmWobble: 300 RPM
```

---

## 3.4.3 토크 커브 시스템

### 이론: 엔진 토크 특성

```
레이스카 토크 커브:

Torque (Nm)
   ↑
650│         ╱────╲
   │       ╱        ╲
500│     ╱            ╲
   │   ╱                ╲
350│ ╱                    ╲
   │╱                      ─
   └──────────────────────────→ RPM
   1500  4000  6000  8000  8500

특징:
- 낮은 RPM: 약함 (30-40%)
- Peak: 6000 RPM
- 고 RPM 유지: 레이스카 특성
- 레드라인 근처: 약간 감소
```

### AnimationCurve 설정

```csharp
/// <summary>
/// 레이스카 토크 커브 생성 (에디터 헬퍼)
/// </summary>
public static AnimationCurve CreateRaceCarTorqueCurve()
{
    AnimationCurve curve = new AnimationCurve();
    
    // 정규화된 RPM (0~1) → 토크 비율 (0~1)
    
    // Idle (0.0 = idleRPM)
    curve.AddKey(new Keyframe(0.0f, 0.3f, 0f, 2f));
    
    // 중간 (0.4 = ~40% RPM)
    curve.AddKey(new Keyframe(0.4f, 0.8f, 1f, 0.5f));
    
    // Peak (0.7 = peakTorqueRPM)
    curve.AddKey(new Keyframe(0.7f, 1.0f, 0f, 0f));
    
    // 고 RPM (0.9)
    curve.AddKey(new Keyframe(0.9f, 0.95f, -0.2f, -0.5f));
    
    // 레드라인 (1.0 = maxRPM)
    curve.AddKey(new Keyframe(1.0f, 0.85f, -1f, 0f));
    
    return curve;
}
```

### 토크 계산

```csharp
/// <summary>
/// 현재 RPM에서의 토크 계산
/// </summary>
/// <param name="currentRPM">현재 RPM</param>
/// <param name="throttle">스로틀 입력 (0~1)</param>
/// <returns>출력 토크 (Nm)</returns>
public float CalculateTorque(float currentRPM, float throttle)
{
    // 1. RPM 정규화 (0~1)
    float normalizedRPM = Mathf.InverseLerp(data.idleRPM, data.maxRPM, currentRPM);
    normalizedRPM = Mathf.Clamp01(normalizedRPM);
    
    // 2. 토크 커브에서 샘플링
    float torqueRatio = data.torqueCurve.Evaluate(normalizedRPM);
    
    // 3. 최대 토크 적용
    float torque = data.maxTorque * torqueRatio;
    
    // 4. 스로틀 적용
    torque *= throttle;
    
    // 5. 리미터 체크
    if (data.enableLimiter && currentRPM >= data.limiterRPM)
    {
        torque = 0f; // 파워컷
    }
    
    return torque;
}
```

**수식 정리:**
```
T(RPM, θ) = T_max × C(RPM_norm) × θ

여기서:
T = 출력 토크
T_max = 최대 토크
C = 토크 커브 함수
RPM_norm = (RPM - idle) / (max - idle)
θ = 스로틀 (0~1)
```

---

## 3.4.4 RPM 시뮬레이션

### 물리 기반 RPM 동역학

```
엔진 회전 운동 방정식:

I × dω/dt = T_engine - T_load - T_friction

여기서:
I = 엔진 관성 (kg·m²)
ω = 각속도 (rad/s)
T_engine = 엔진 토크
T_load = 부하 토크 (변속기, 타이어)
T_friction = 엔진 내부 마찰

RPM 변환:
RPM = ω × (60 / 2π)
```

### 구현

```csharp
public class EngineModel : MonoBehaviour
{
    [Header("References")]
    public EngineData data;
    
    [Header("Runtime State")]
    [ReadOnly] public float currentRPM;
    [ReadOnly] public float targetRPM;
    [ReadOnly] public int currentGear;
    [ReadOnly] public bool isShifting;
    
    // 내부 변수
    private float engineAngularVelocity; // rad/s
    private float rpmWobblePhase;
    private float rpmWobbleAmplitude;
    
    // 입력
    private float throttleInput;
    private float clutchInput; // 0 = engaged, 1 = disengaged
    
    void Start()
    {
        currentRPM = data.idleRPM;
        engineAngularVelocity = RPMToAngularVelocity(currentRPM);
    }
    
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        
        // 1. 목표 RPM 계산
        CalculateTargetRPM();
        
        // 2. RPM 물리 시뮬레이션
        SimulateRPMPhysics(dt);
        
        // 3. RPM Wobble 적용
        ApplyRPMWobble(dt);
        
        // 4. RPM 클램핑 및 리미터
        ClampRPM();
    }
    
    /// <summary>
    /// RPM ↔ 각속도 변환
    /// </summary>
    private float RPMToAngularVelocity(float rpm)
    {
        return rpm * (2f * Mathf.PI / 60f); // rad/s
    }
    
    private float AngularVelocityToRPM(float omega)
    {
        return omega * (60f / (2f * Mathf.PI));
    }
}
```

### RPM 물리 시뮬레이션

```csharp
/// <summary>
/// 물리 기반 RPM 변화 (관성 포함)
/// </summary>
private void SimulateRPMPhysics(float dt)
{
    // === 1. 엔진 토크 ===
    float throttle = isShifting ? 0f : throttleInput;
    float engineTorque = CalculateTorque(currentRPM, throttle);
    
    // === 2. 부하 토크 (변속기에서) ===
    float loadTorque = CalculateLoadTorque();
    
    // === 3. 마찰 토크 (RPM에 비례) ===
    float frictionTorque = engineAngularVelocity * data.engineFriction;
    
    // === 4. 순 토크 ===
    float netTorque = engineTorque - loadTorque - frictionTorque;
    
    // === 5. 각가속도 (α = T / I) ===
    float angularAcceleration = netTorque / data.engineInertia;
    
    // === 6. 각속도 업데이트 ===
    engineAngularVelocity += angularAcceleration * dt;
    
    // === 7. 자연 감쇠 (스로틀 OFF 시) ===
    if (throttleInput < 0.01f && !isShifting)
    {
        float decayRate = data.revDownRate * 10f; // rad/s²
        float decay = decayRate * dt;
        
        if (engineAngularVelocity > RPMToAngularVelocity(data.idleRPM))
        {
            engineAngularVelocity -= decay;
        }
    }
    
    // === 8. RPM 변환 ===
    currentRPM = AngularVelocityToRPM(engineAngularVelocity);
    
    // === 9. 부드러운 보간 (추가 안정성) ===
    float smoothing = isShifting ? 0.3f : 0.1f;
    currentRPM = Mathf.Lerp(currentRPM, targetRPM, smoothing);
}
```

### 목표 RPM 계산

```csharp
/// <summary>
/// 스로틀과 부하에 따른 목표 RPM
/// </summary>
private void CalculateTargetRPM()
{
    if (isShifting)
    {
        // 변속 중: 현재 RPM 유지 (wobble 제외)
        targetRPM = currentRPM;
        return;
    }
    
    if (throttleInput > 0.01f)
    {
        // 가속: 레드라인까지
        targetRPM = Mathf.Lerp(
            currentRPM,
            data.maxRPM,
            throttleInput * data.revUpRate * Time.fixedDeltaTime
        );
    }
    else
    {
        // 감속: 아이들까지
        targetRPM = Mathf.Lerp(
            currentRPM,
            data.idleRPM,
            data.revDownRate * Time.fixedDeltaTime
        );
    }
}
```

---

## 3.4.5 기어박스 시스템

### 기어 데이터

```csharp
[System.Serializable]
public class GearboxData
{
    [Header("=== Gear Ratios ===")]
    [Tooltip("기어비 배열 (1단부터)")]
    public float[] gearRatios = new float[]
    {
        3.5f,  // 1단
        2.5f,  // 2단
        1.9f,  // 3단
        1.5f,  // 4단
        1.2f,  // 5단
        1.0f   // 6단
    };
    
    [Tooltip("후진 기어비")]
    public float reverseGearRatio = 3.8f;
    
    [Tooltip("최종 감속비 (Final Drive)")]
    [Range(2f, 6f)]
    public float finalDriveRatio = 4.1f;
    
    [Header("=== Shift Points ===")]
    [Tooltip("업시프트 RPM")]
    [Range(6000f, 9000f)]
    public float upshiftRPM = 8200f;
    
    [Tooltip("다운시프트 RPM")]
    [Range(3000f, 6000f)]
    public float downshiftRPM = 5000f;
    
    [Tooltip("자동 변속 활성화")]
    public bool autoShift = false;
    
    [Header("=== Sequential (레이스카) ===")]
    [Tooltip("시퀀셜 기어박스")]
    public bool isSequential = true;
    
    [Tooltip("중립 기어 허용")]
    public bool allowNeutral = false;
}
```

### 기어 계산

```csharp
/// <summary>
/// 기어비로부터 휠 RPM → 엔진 RPM 계산
/// </summary>
public float CalculateEngineRPMFromWheelSpeed(float wheelRPM, int gear)
{
    if (gear == 0) // 중립
        return currentRPM; // 엔진 RPM 유지
    
    float gearRatio = gear > 0 
        ? gearboxData.gearRatios[gear - 1] 
        : gearboxData.reverseGearRatio;
    
    float totalRatio = gearRatio * gearboxData.finalDriveRatio;
    
    float engineRPM = wheelRPM * totalRatio;
    
    return engineRPM;
}

/// <summary>
/// 엔진 RPM → 휠 토크 계산
/// </summary>
public float CalculateWheelTorque(float engineTorque, int gear)
{
    if (gear == 0) // 중립
        return 0f;
    
    float gearRatio = gear > 0 
        ? gearboxData.gearRatios[gear - 1] 
        : gearboxData.reverseGearRatio;
    
    float totalRatio = gearRatio * gearboxData.finalDriveRatio;
    
    // 토크는 기어비에 비례해 증폭
    float wheelTorque = engineTorque * totalRatio;
    
    // 효율 (95%)
    wheelTorque *= 0.95f;
    
    return wheelTorque;
}
```

**수식:**
```
엔진 RPM ↔ 휠 RPM:
RPM_engine = RPM_wheel × gear_ratio × final_drive

휠 토크:
T_wheel = T_engine × gear_ratio × final_drive × η

여기서 η = 전달 효율 (0.95)
```

---

## 3.4.6 기어 변속 메커니즘

### 시퀀셜 기어박스 (레이스카)

```csharp
public class SequentialGearbox : MonoBehaviour
{
    public GearboxData data;
    public EngineModel engine;
    
    [ReadOnly] public int currentGear = 1;
    [ReadOnly] public bool isShifting = false;
    
    private float shiftTimer;
    private int targetGear;
    
    void Update()
    {
        // 수동 입력 (시퀀셜)
        if (Input.GetKeyDown(KeyCode.E)) // Shift Up
        {
            RequestShiftUp();
        }
        if (Input.GetKeyDown(KeyCode.Q)) // Shift Down
        {
            RequestShiftDown();
        }
        
        // 자동 변속 (옵션)
        if (data.autoShift && !isShifting)
        {
            CheckAutoShift();
        }
    }
    
    void FixedUpdate()
    {
        if (isShifting)
        {
            UpdateShift(Time.fixedDeltaTime);
        }
    }
    
    /// <summary>
    /// 업시프트 요청
    /// </summary>
    public void RequestShiftUp()
    {
        if (isShifting) return;
        
        int maxGear = data.gearRatios.Length;
        if (currentGear >= maxGear) return;
        
        StartShift(currentGear + 1);
    }
    
    /// <summary>
    /// 다운시프트 요청
    /// </summary>
    public void RequestShiftDown()
    {
        if (isShifting) return;
        
        int minGear = data.allowNeutral ? 0 : 1;
        if (currentGear <= minGear) return;
        
        StartShift(currentGear - 1);
    }
}
```

### 변속 프로세스

```csharp
/// <summary>
/// 기어 변속 시작
/// </summary>
private void StartShift(int newGear)
{
    if (isShifting) return;
    
    targetGear = newGear;
    isShifting = true;
    shiftTimer = 0f;
    
    // 엔진에 변속 시작 알림
    engine.OnShiftStart(currentGear, targetGear);
    
    // 사운드/VFX 트리거
    OnShiftStarted?.Invoke(currentGear, targetGear);
}

/// <summary>
/// 변속 업데이트 (매 FixedUpdate)
/// </summary>
private void UpdateShift(float dt)
{
    shiftTimer += dt;
    
    float progress = shiftTimer / engine.data.shiftDuration;
    
    if (progress >= 1f)
    {
        // 변속 완료
        CompleteShift();
    }
    else
    {
        // 변속 진행 중
        engine.UpdateShiftProgress(progress);
    }
}

/// <summary>
/// 변속 완료
/// </summary>
private void CompleteShift()
{
    currentGear = targetGear;
    isShifting = false;
    
    // 엔진에 변속 완료 알림
    engine.OnShiftComplete(currentGear);
    
    // 사운드/VFX
    OnShiftCompleted?.Invoke(currentGear);
}
```

---

## 3.4.7 RPM Wobble & 변속 효과

### Wobble 시뮬레이션 ⭐

```csharp
/// <summary>
/// 기어 변속 시 RPM 흔들림 효과
/// </summary>
private void ApplyRPMWobble(float dt)
{
    if (!isShifting && rpmWobbleAmplitude < 1f)
    {
        // Wobble 없음
        return;
    }
    
    // === 1. Wobble 진폭 감쇠 ===
    rpmWobbleAmplitude -= data.rpmWobbleDamping * dt;
    rpmWobbleAmplitude = Mathf.Max(rpmWobbleAmplitude, 0f);
    
    // === 2. 사인파 생성 ===
    rpmWobblePhase += data.rpmWobbleFrequency * 2f * Mathf.PI * dt;
    float wobble = Mathf.Sin(rpmWobblePhase) * rpmWobbleAmplitude;
    
    // === 3. RPM에 적용 ===
    currentRPM += wobble;
}

/// <summary>
/// 변속 시작 시 호출 (EngineModel에서)
/// </summary>
public void OnShiftStart(int fromGear, int toGear)
{
    isShifting = true;
    
    // Wobble 트리거
    TriggerRPMWobble();
    
    // RPM 변화 계산
    CalculateShiftRPMChange(fromGear, toGear);
}

/// <summary>
/// RPM Wobble 트리거
/// </summary>
private void TriggerRPMWobble()
{
    // 초기 진폭 설정
    rpmWobbleAmplitude = data.rpmWobbleAmount;
    
    // 위상 랜덤화 (자연스러움)
    rpmWobblePhase = Random.Range(0f, 2f * Mathf.PI);
}
```

### 변속 중 RPM 변화

```csharp
/// <summary>
/// 기어 변속 시 RPM 변화 계산
/// </summary>
private void CalculateShiftRPMChange(int fromGear, int toGear)
{
    if (fromGear == 0 || toGear == 0) return;
    
    // 현재 휠 RPM (차량 속도)
    float wheelRPM = CalculateWheelRPM();
    
    // 기어비 변화
    float oldRatio = gearboxData.gearRatios[fromGear - 1];
    float newRatio = gearboxData.gearRatios[toGear - 1];
    
    // 새 기어에서의 엔진 RPM
    float newEngineRPM = wheelRPM * newRatio * gearboxData.finalDriveRatio;
    
    // 파워컷 시뮬레이션
    StartCoroutine(PowerCutSequence(newEngineRPM));
}

/// <summary>
/// 파워컷 시퀀스 (변속 연출)
/// </summary>
private IEnumerator PowerCutSequence(float targetRPM)
{
    float startRPM = currentRPM;
    float cutDuration = data.shiftDuration;
    float elapsed = 0f;
    
    while (elapsed < cutDuration)
    {
        elapsed += Time.fixedDeltaTime;
        float progress = elapsed / cutDuration;
        
        // === 3단계 변속 과정 ===
        
        if (progress < 0.3f) // Phase 1: 파워컷 (0-30%)
        {
            // RPM이 순간적으로 튀어오름 (클러치 단절)
            float overshoot = 150f * (1f - progress / 0.3f);
            currentRPM = startRPM + overshoot;
        }
        else if (progress < 0.6f) // Phase 2: 동기화 (30-60%)
        {
            // 새 기어비에 맞춰 RPM 급격히 변화
            float syncProgress = (progress - 0.3f) / 0.3f;
            currentRPM = Mathf.Lerp(startRPM, targetRPM, syncProgress);
        }
        else // Phase 3: 재접속 (60-100%)
        {
            // 토크가 다시 연결되며 RPM 안정화
            float reconnectProgress = (progress - 0.6f) / 0.4f;
            currentRPM = Mathf.Lerp(targetRPM, targetRPM, reconnectProgress);
            
            // Wobble이 점차 감소
            rpmWobbleAmplitude *= (1f - reconnectProgress);
        }
        
        yield return new WaitForFixedUpdate();
    }
    
    // 최종 RPM 설정
    currentRPM = targetRPM;
}
```

### 변속 효과 시각화

```
업시프트 (5단 → 6단) 시 RPM 변화:

RPM
  ↑
8000│     ╱╲              ← Wobble
    │    ╱  ╲   ╱\
7500│   ╱    ╲ ╱  ─────  ← 안정화
    │  ╱      V
7000│ ╱                   ← 파워컷
    │╱
6500│─────────────────
    └──────────────────→ Time
    0ms 30ms 60ms 100ms

Phase 1 (0-30ms): 파워컷 + 순간 RPM 상승
Phase 2 (30-60ms): 새 기어비로 RPM 하강
Phase 3 (60-100ms): Wobble 감쇠 + 안정화

다운시프트 (4단 → 3단) 시:

RPM
  ↑
7500│         ╱╲╱─────   ← 안정화
    │        ╱
7000│       ╱  Wobble
    │      ╱╲
6500│     ╱  ╲
    │    ╱
6000│───╱
    └──────────────────→ Time

RPM이 상승하므로 더 격렬한 Wobble
```

---

## 3.4.8 엔진 브레이킹

### 개념

```
엔진 브레이크:
스로틀 OFF 상태에서 엔진이 바퀴를 감속시키는 현象

원리:
1. 엔진 압축 저항
2. 펌핑 손실
3. 마찰

레이스카 특성:
- 높은 압축비 → 강한 엔진 브레이크
- 낮은 기어 → 더 강함
```

### 구현

```csharp
/// <summary>
/// 엔진 브레이크 토크 계산
/// </summary>
public float CalculateEngineBrakingTorque()
{
    // 스로틀이 있으면 엔진 브레이크 없음
    if (throttleInput > 0.1f)
        return 0f;
    
    // === 1. 기본 엔진 브레이크 ===
    
    // RPM에 비례 (고 RPM일수록 강함)
    float rpmFactor = Mathf.InverseLerp(data.idleRPM, data.maxRPM, currentRPM);
    
    // 최대 엔진 브레이크 토크
    float maxBrakeTorque = data.maxTorque * 0.3f; // 최대 토크의 30%
    
    float brakeTorque = maxBrakeTorque * rpmFactor * data.engineBrakingStrength;
    
    // === 2. 기어 배율 ===
    
    // 낮은 기어일수록 강함
    if (currentGear > 0 && currentGear <= gearboxData.gearRatios.Length)
    {
        float gearRatio = gearboxData.gearRatios[currentGear - 1];
        
        // 높은 기어비 = 강한 엔진 브레이크
        float gearMultiplier = Mathf.Clamp(gearRatio / 2f, 0.5f, 2f);
        brakeTorque *= gearMultiplier;
    }
    
    // === 3. 방향 (음수 = 감속) ===
    return -brakeTorque;
}

/// <summary>
/// 휠에 적용할 엔진 브레이크 힘
/// </summary>
public float GetEngineBrakingForce()
{
    float brakeTorque = CalculateEngineBrakingTorque();
    
    // 토크 → 휠 포스 변환
    float wheelRadius = 0.35f; // 타이어 모델에서 가져와야 함
    float brakeForce = brakeTorque / wheelRadius;
    
    // 기어비로 증폭
    if (currentGear > 0)
    {
        float totalRatio = gearboxData.gearRatios[currentGear - 1] 
                          * gearboxData.finalDriveRatio;
        brakeForce *= totalRatio;
    }
    
    return brakeForce;
}
```

**효과 비교:**
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
상황              엔진 브레이크 강도
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
1단, 7000 RPM     ████████████ 100%
3단, 7000 RPM     ██████░░░░░░  50%
5단, 7000 RPM     ███░░░░░░░░░  25%
5단, 3000 RPM     █░░░░░░░░░░░  10%
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

활용:
- 코너 진입 시 다운시프트 + 엔진 브레이크
- 언덕 내리막에서 속도 제어
- 브레이크 절약
```

---

## 3.4.9 구동계 통합

### 전체 파워트레인

```
엔진 → 클러치 → 기어박스 → 디퍼렌셜 → 휠

     [Engine]
        ↓ Torque
     [Clutch]
        ↓ (변속 시 단절)
    [Gearbox]
        ↓ Torque × Ratio
  [Differential]
        ↓ (좌우 분배)
    [Drive Wheels]
```

### 구동 휠에 토크 전달

```csharp
/// <summary>
/// 엔진 토크를 구동 휠에 전달
/// </summary>
public void ApplyDriveTorque(WheelCollider[] driveWheels)
{
    // 1. 현재 엔진 토크 계산
    float engineTorque = CalculateTorque(currentRPM, throttleInput);
    
    // 2. 기어박스 통과
    float wheelTorque = CalculateWheelTorque(engineTorque, currentGear);
    
    // 3. 엔진 브레이크 추가
    if (throttleInput < 0.1f)
    {
        wheelTorque += GetEngineBrakingForce();
    }
    
    // 4. 변속 중 파워컷
    if (isShifting)
    {
        wheelTorque *= (1f - data.powerCutAmount);
    }
    
    // 5. 구동 휠에 분배
    float torquePerWheel = wheelTorque / driveWheels.Length;
    
    foreach (var wheel in driveWheels)
    {
        // WheelCollider.motorTorque에 적용
        wheel.motorTorque = torquePerWheel;
        
        // 또는 커스텀 타이어 모델에 전달
        // wheel.GetComponent<TireModel>().ApplyDriveTorque(torquePerWheel);
    }
}
```

### 휠 RPM 피드백

```csharp
/// <summary>
/// 휠 속도로부터 엔진 RPM 업데이트 (클러치 결합 시)
/// </summary>
public void UpdateRPMFromWheels(WheelCollider[] driveWheels)
{
    if (isShifting || currentGear == 0)
        return; // 중립이거나 변속 중이면 무시
    
    // 1. 평균 휠 RPM 계산
    float avgWheelRPM = 0f;
    foreach (var wheel in driveWheels)
    {
        avgWheelRPM += Mathf.Abs(wheel.rpm);
    }
    avgWheelRPM /= driveWheels.Length;
    
    // 2. 기어비로 엔진 RPM 계산
    float expectedEngineRPM = CalculateEngineRPMFromWheelSpeed(avgWheelRPM, currentGear);
    
    // 3. 부드럽게 동기화 (클러치 슬립 시뮬레이션)
    float clutchFactor = 1f - clutchInput; // 0 = 단절, 1 = 완전 결합
    currentRPM = Mathf.Lerp(currentRPM, expectedEngineRPM, clutchFactor * 0.5f);
}
```

---

## 3.4.10 사운드 연동

### 엔진 사운드 파라미터

```csharp
/// <summary>
/// 사운드 시스템에 전달할 엔진 상태
/// </summary>
public struct EngineSoundData
{
    public float rpm;           // 현재 RPM
    public float rpmNormalized; // 0~1 (idle~max)
    public float throttle;      // 스로틀 입력
    public float load;          // 엔진 부하 (0~1)
    public int gear;            // 현재 기어
    public bool isShifting;     // 변속 중
    public bool isLimiting;     // 리미터 작동
}

/// <summary>
/// 사운드 시스템용 데이터 생성
/// </summary>
public EngineSoundData GetSoundData()
{
    return new EngineSoundData
    {
        rpm = currentRPM,
        rpmNormalized = Mathf.InverseLerp(data.idleRPM, data.maxRPM, currentRPM),
        throttle = throttleInput,
        load = CalculateEngineLoad(),
        gear = currentGear,
        isShifting = isShifting,
        isLimiting = currentRPM >= data.limiterRPM
    };
}

/// <summary>
/// 엔진 부하 계산 (사운드용)
/// </summary>
private float CalculateEngineLoad()
{
    float currentTorque = CalculateTorque(currentRPM, throttleInput);
    float maxPossibleTorque = data.maxTorque;
    
    return Mathf.Clamp01(currentTorque / maxPossibleTorque);
}
```

### 사운드 피치 매핑

```csharp
/// <summary>
/// RPM → 오디오 피치 변환
/// </summary>
public float GetEngineSoundPitch()
{
    // RPM 정규화
    float t = Mathf.InverseLerp(data.idleRPM, data.maxRPM, currentRPM);
    
    // 피치 커브 (로그 스케일)
    // Idle: 0.8, Max: 2.2
    float minPitch = 0.8f;
    float maxPitch = 2.2f;
    
    // 약간 비선형 (고 RPM에서 더 빠르게 증가)
    float pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.Pow(t, 0.8f));
    
    // Wobble 추가
    if (rpmWobbleAmplitude > 1f)
    {
        float wobblePitch = (rpmWobbleAmplitude / data.rpmWobbleAmount) * 0.1f;
        pitch += Mathf.Sin(rpmWobblePhase) * wobblePitch;
    }
    
    return pitch;
}
```

---

## 3.4.11 RPM 리미터

### 레이스카 리미터 구현

```csharp
/// <summary>
/// RPM 리미터 업데이트
/// </summary>
private void UpdateRPMLimiter(float dt)
{
    if (!data.enableLimiter) return;
    
    if (currentRPM >= data.limiterRPM)
    {
        isLimiting = true;
        limiterPhase += data.limiterCutFrequency * 2f * Mathf.PI * dt;
        
        // 사인파로 파워 온/오프
        float cutSignal = Mathf.Sin(limiterPhase);
        
        if (cutSignal > 0f)
        {
            // 파워 컷
            throttleMultiplier = 0f;
        }
        else
        {
            // 파워 복귀
            throttleMultiplier = 1f;
        }
        
        // RPM 클램핑
        currentRPM = Mathf.Min(currentRPM, data.limiterRPM + 50f);
    }
    else
    {
        isLimiting = false;
        throttleMultiplier = 1f;
    }
}
```

**리미터 효과:**
```
RPM
  ↑
8700│─╲  ╱─╲  ╱─╲  ╱─  ← 리미터 (30Hz 진동)
    │  ╲╱  ╲╱  ╲╱
8500│────────────────   ← 레드라인
    │
8000│
    │
    └────────────────→ Time

사운드: "Bang-bang-bang" (파워컷 소리)
체감: 차가 떨리는 느낌
```

---

## 3.4.12 디버깅 및 시각화

### 인스펙터 디버그

```csharp
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(EngineModel))]
public class EngineModelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EngineModel engine = (EngineModel)target;
        
        if (!Application.isPlaying)
            return;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("=== Runtime Status ===", EditorStyles.boldLabel);
        
        // RPM 게이지
        EditorGUILayout.LabelField($"RPM: {engine.currentRPM:F0}");
        
        Rect rect = EditorGUILayout.GetControlRect(false, 30f);
        float rpmRatio = Mathf.InverseLerp(
            engine.data.idleRPM, 
            engine.data.maxRPM, 
            engine.currentRPM
        );
        
        // RPM 바 (색상 변화)
        Color barColor = Color.Lerp(Color.green, Color.red, rpmRatio);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * rpmRatio, rect.height), barColor);
        EditorGUI.ProgressBar(rect, rpmRatio, $"{engine.currentRPM:F0} RPM");
        
        // 기어
        EditorGUILayout.LabelField($"Gear: {engine.currentGear}");
        
        // 변속 상태
        if (engine.isShifting)
        {
            EditorGUILayout.HelpBox("SHIFTING...", MessageType.Info);
        }
        
        // 리미터
        if (engine.isLimiting)
        {
            EditorGUILayout.HelpBox("RPM LIMITER!", MessageType.Warning);
        }
        
        // 토크
        float torque = engine.CalculateTorque(engine.currentRPM, engine.throttleInput);
        EditorGUILayout.LabelField($"Torque: {torque:F0} Nm");
        
        // 토크 커브 프리뷰
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Torque Curve:", EditorStyles.boldLabel);
        
        Rect curveRect = EditorGUILayout.GetControlRect(false, 100f);
        DrawTorqueCurve(curveRect, engine.data);
        
        Repaint();
    }
    
    private void DrawTorqueCurve(Rect rect, EngineData data)
    {
        // 배경
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
        
        // 커브 그리기
        int steps = 100;
        Vector3 prevPoint = Vector3.zero;
        
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float rpm = Mathf.Lerp(data.idleRPM, data.maxRPM, t);
            float torqueRatio = data.torqueCurve.Evaluate(t);
            
            Vector3 point = new Vector3(
                rect.x + rect.width * t,
                rect.y + rect.height * (1f - torqueRatio),
                0f
            );
            
            if (i > 0)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(prevPoint, point);
            }
            
            prevPoint = point;
        }
        
        // 현재 RPM 마커
        if (Application.isPlaying)
        {
            EngineModel engine = (EngineModel)target;
            float currentT = Mathf.InverseLerp(data.idleRPM, data.maxRPM, engine.currentRPM);
            float markerX = rect.x + rect.width * currentT;
            
            Handles.color = Color.yellow;
            Handles.DrawLine(
                new Vector3(markerX, rect.y, 0f),
                new Vector3(markerX, rect.y + rect.height, 0f)
            );
        }
    }
}
#endif
```

### Gizmos 시각화

```csharp
void OnDrawGizmos()
{
    if (!Application.isPlaying) return;
    
    // RPM 게이지 (월드 공간)
    Vector3 position = transform.position + Vector3.up * 2f;
    
    // 배경 원
    Gizmos.color = Color.black;
    DrawWireDisc(position, Vector3.forward, 0.5f);
    
    // RPM 아크
    float rpmRatio = Mathf.InverseLerp(data.idleRPM, data.maxRPM, currentRPM);
    Color rpmColor = Color.Lerp(Color.green, Color.red, rpmRatio);
    
    Gizmos.color = rpmColor;
    DrawArc(position, Vector3.forward, Vector3.up, rpmRatio * 270f, 0.5f);
    
    // 기어 텍스트
    #if UNITY_EDITOR
    UnityEditor.Handles.Label(position, $"Gear: {currentGear}\n{currentRPM:F0} RPM");
    #endif
}

private void DrawArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius)
{
    // 원호 그리기 헬퍼 (Gizmos용)
    int segments = 20;
    Vector3 prevPoint = center + from * radius;
    
    for (int i = 1; i <= segments; i++)
    {
        float currentAngle = (angle / segments) * i;
        Vector3 rotation = Quaternion.AngleAxis(currentAngle, normal) * from;
        Vector3 point = center + rotation * radius;
        
        Gizmos.DrawLine(prevPoint, point);
        prevPoint = point;
    }
}
```

---

## 3.4.13 최적화

### 성능 팁

```csharp
/// <summary>
/// 최적화된 엔진 모델
/// </summary>
public class OptimizedEngineModel : EngineModel
{
    // 토크 커브 캐싱
    private float[] torqueLookupTable;
    private const int LOOKUP_SIZE = 100;
    
    void Start()
    {
        base.Start();
        
        // 시작 시 토크 룩업 테이블 생성
        BuildTorqueLookupTable();
    }
    
    private void BuildTorqueLookupTable()
    {
        torqueLookupTable = new float[LOOKUP_SIZE];
        
        for (int i = 0; i < LOOKUP_SIZE; i++)
        {
            float t = (float)i / (LOOKUP_SIZE - 1);
            torqueLookupTable[i] = data.torqueCurve.Evaluate(t);
        }
    }
    
    /// <summary>
    /// 빠른 토크 계산 (룩업 테이블 사용)
    /// </summary>
    public override float CalculateTorque(float rpm, float throttle)
    {
        // RPM 정규화
        float t = Mathf.InverseLerp(data.idleRPM, data.maxRPM, rpm);
        t = Mathf.Clamp01(t);
        
        // 룩업 테이블 인덱스
        float index = t * (LOOKUP_SIZE - 1);
        int i0 = Mathf.FloorToInt(index);
        int i1 = Mathf.Min(i0 + 1, LOOKUP_SIZE - 1);
        
        // 선형 보간
        float frac = index - i0;
        float torqueRatio = Mathf.Lerp(torqueLookupTable[i0], torqueLookupTable[i1], frac);
        
        // 토크 계산
        float torque = data.maxTorque * torqueRatio * throttle;
        
        // 리미터
        if (data.enableLimiter && rpm >= data.limiterRPM)
            torque = 0f;
        
        return torque;
    }
}
```

---

## 요약 체크리스트

### 필수 구현
- [ ] `EngineData` ScriptableObject
- [ ] `EngineModel` 클래스
- [ ] 토크 커브 시스템
- [ ] RPM 물리 시뮬레이션
- [ ] 기어박스 시스템
- [ ] 기어 변속 메커니즘
- [ ] 구동 휠 토크 전달

### 레이스카 특화
- [ ] RPM Wobble (변속 시 흔들림)
- [ ] 파워컷 시퀀스
- [ ] 빠른 RPM 반응 (낮은 관성)
- [ ] RPM 리미터
- [ ] 강한 엔진 브레이킹
- [ ] 시퀀셜 기어박스

### 사운드 연동
- [ ] `EngineSoundData` 구조체
- [ ] 피치 계산
- [ ] 변속 사운드 트리거
- [ ] 리미터 사운드

### 디버깅
- [ ] 에디터 확장 (RPM 게이지)
- [ ] 토크 커브 시각화
- [ ] Gizmos (월드 공간 게이지)
- [ ] 실시간 파라미터 조정

### 최적화
- [ ] 토크 룩업 테이블
- [ ] 불필요한 계산 제거
- [ ] 조건부 업데이트

---

## 다음 문서

이어질 문서들:
1. **3.5 공기역학** - 다운포스, 드래그, 슬립스트림
2. **3.6 차체 통합** - 모든 시스템을 Rigidbody에 통합
3. **4.0 카메라 시스템** - 물리 연동 카메라 워킹

---

**문서 버전**: 1.0  
**작성일**: 2024  
**상태**: 구현 준비 완료 ✅  
**특화**: 레이스카 중심, RPM Wobble 포함