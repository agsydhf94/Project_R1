# Unity 차량 물리 엔진 설정 가이드

## 목차
1. [프로젝트 설정](#1-프로젝트-설정)
2. [ScriptableObject 데이터 생성](#2-scriptableobject-데이터-생성)
3. [차량 프리팹 구조 생성](#3-차량-프리팹-구조-생성)
4. [컴포넌트 설정](#4-컴포넌트-설정)
5. [테스트 씬 구성](#5-테스트-씬-구성)
6. [문제 해결](#6-문제-해결)

---

## 1. 프로젝트 설정

### 1.1 Physics Settings
**Edit → Project Settings → Physics**

```
Gravity: (0, -9.81, 0)  ← 기본값 유지
Default Material: None
Bounce Threshold: 2
Sleep Threshold: 0.005
Default Contact Offset: 0.01
Default Solver Iterations: 6
Default Solver Velocity Iterations: 1
Queries Hit Backfaces: ✓
Queries Hit Triggers: ✗
Enable Adaptive Force: ✗
Enable PCM: ✗
```

### 1.2 Time Settings
**Edit → Project Settings → Time**

```
Fixed Timestep: 0.01  ← 중요! (100Hz)
Maximum Allowed Timestep: 0.1
```

### 1.3 Layer 설정
**Edit → Project Settings → Tags and Layers**

새 레이어 추가:
```
Layer 8: Ground
Layer 9: Vehicle
Layer 10: WheelCollider (선택사항)
```

### 1.4 Physics Layer Collision Matrix
**Edit → Project Settings → Physics → Layer Collision Matrix**

```
Vehicle와 Ground: ✓ (충돌)
Vehicle와 Vehicle: ✓ (차량 간 충돌)
```

---

## 2. ScriptableObject 데이터 생성

### 2.1 차량 데이터 생성

**Project 창 우클릭:**
```
Create → ProjectR1 → Physics → Vehicle Data
```

생성된 파일: `VehicleData_SportCoupe`

**인스펙터 설정:**
```
Vehicle Name: Sport Coupe
Mass: 1500 kg
Drive Type: RWD
Center Of Mass: (0, -0.3, 0)  ← Y값 음수로!
Drag: 0.05
Angular Drag: 0.5
Enable ESP: ✓
ESP Strength: 0.5
Enable TCS: ✓
TCS Threshold: 0.3
```

### 2.2 서스펜션 데이터 생성

**Project 창 우클릭:**
```
Create → ProjectR1 → Physics → Suspension Data
```

생성된 파일: `SuspensionData_SportCoupe`

**기본 설정 (1500kg 차량 기준):**
```
Rest Length: 0.5 m
Max Compression: 0.15 m
Max Extension: 0.1 m
Wheel Radius: 0.35 m

Spring Rate: 35000 N/m  ← Auto-Tune 권장
Spring Multiplier: 1.0

Damper Compression: 3500 N·s/m
Damper Rebound: 2500 N·s/m

Anti Roll Bar Stiffness: 5000 N/m

Ground Layer: Everything
Min Compression: 0.01 m
Force Smoothing: 0.1

Show Debug Rays: ✓
Show Force Vectors: ✓
```

**자동 튜닝 (권장):**
1. 인스펙터에서 우클릭
2. Context Menu → "Auto-Tune Suspension" 선택
3. 차량 질량(1500) 입력

### 2.3 타이어 데이터 생성

**Project 창 우클릭:**
```
Create → ProjectR1 → Physics → Tire Data
```

생성된 파일: `TireData_SportCoupe`

**Grid Legends 스타일 설정:**
```
=== Pacejka Parameters ===
Peak Force: 8000 N
Shape Factor: 1.65
Stiffness Factor: 10

=== Surface Grip ===
Dry Grip: 1.0
Wet Grip: 0.7
Gravel Grip: 0.5
Grass Grip: 0.3
Dirt Grip: 0.4
Ice Grip: 0.15

=== Tire Compound ===
Compound: Dry

=== Load Sensitivity ===
Load Sensitivity: 1.5
Reference Load: 4000 N

=== Temperature ===
Enable Temperature: ✓
Optimal Temperature: 80°C
Temperature Range: 20°C
Heating Rate: 1.0°C/s
Cooling Coefficient: 0.1

=== Properties ===
Radius: 0.35 m
Width: 0.25 m
Rotational Inertia: 1.5 kg·m²
```

### 2.4 엔진 데이터 생성

**Project 창 우클릭:**
```
Create → ProjectR1 → Physics → Engine Data
```

생성된 파일: `EngineData_SportCoupe`

**설정:**
```
Max RPM: 7500
Idle RPM: 1000
Redline RPM: 7000

Max Torque: 400 Nm
Max Torque RPM: 4500
Max Power: 350 HP
Max Power RPM: 6500

Torque Curve: [우클릭 → Generate Default Torque Curve]

Engine Inertia: 0.3 kg·m²
Engine Braking: 40
Throttle Response: 1.0

Simulate Fuel: ✗ (테스트 시 비활성화)
```

### 2.5 공기역학 데이터 생성

**Project 창 우클릭:**
```
Create → ProjectR1 → Physics → Aerodynamics Data
```

생성된 파일: `AerodynamicsData_SportCoupe`

**설정:**
```
Drag Coefficient: 0.35
Frontal Area: 2.2 m²

Enable Downforce: ✓
Front Downforce Coefficient: 1.5
Rear Downforce Coefficient: 2.0
Front Downforce Position: 1.5 (앞)
Rear Downforce Position: -1.5 (뒤)

Air Density: 1.225 kg/m³
Show Debug Forces: ✓
```

---

## 3. 차량 프리팹 구조 생성

### 3.1 기본 GameObject 생성

**Hierarchy 창 우클릭:**
```
Create Empty → 이름: "SportCoupe_Vehicle"
```

**Transform 설정:**
```
Position: (0, 1, 0)  ← 바닥에서 1m 위
Rotation: (0, 0, 0)
Scale: (1, 1, 1)
```

### 3.2 차체 (Body) 설정

**SportCoupe_Vehicle 하위에 Cube 생성:**
```
이름: Body
Position: (0, 0.6, 0)
Scale: (1.8, 1.2, 4.5)  ← 차량 크기
```

**Body에 MeshRenderer:**
- Material: 임시 Material
- Cast Shadows: On

**Body에 BoxCollider:**
```
Size: (1.8, 1.2, 4.5)
Is Trigger: ✗
```

### 3.3 바퀴 구조 생성

**각 바퀴마다 다음 구조 생성:**

```
SportCoupe_Vehicle/
├─ Wheels/
   ├─ FrontLeft/
   │  ├─ SuspensionAnchor (Empty)
   │  │  └─ WheelVisual (Cylinder)
   │  
   ├─ FrontRight/
   │  ├─ SuspensionAnchor (Empty)
   │  │  └─ WheelVisual (Cylinder)
   │
   ├─ RearLeft/
   │  ├─ SuspensionAnchor (Empty)
   │  │  └─ WheelVisual (Cylinder)
   │
   └─ RearRight/
      ├─ SuspensionAnchor (Empty)
      └─ WheelVisual (Cylinder)
```

**위치 설정 (예: FrontLeft):**
```
FrontLeft:
- Position: (-0.9, 0.35, 1.5)  ← 좌측, 바퀴 높이, 앞
- Rotation: (0, 0, 0)

SuspensionAnchor:
- Position: (0, 0.15, 0)  ← 서스펜션 상단
- Rotation: (0, 0, 0)

WheelVisual:
- Position: (0, -0.5, 0)  ← 휠 하단 (로컬)
- Rotation: (0, 0, 90)  ← 옆으로 눕히기
- Scale: (0.7, 0.2, 0.7)  ← 타이어 크기
```

**대칭 위치:**
```
FrontLeft:  (-0.9, 0.35,  1.5)
FrontRight: ( 0.9, 0.35,  1.5)
RearLeft:   (-0.9, 0.35, -1.5)
RearRight:  ( 0.9, 0.35, -1.5)
```

### 3.4 시스템 GameObjects 생성

**SportCoupe_Vehicle 하위에 Empty 생성:**

```
SportCoupe_Vehicle/
├─ Systems/
   ├─ Engine (Empty)
   ├─ Gearbox (Empty)
   ├─ Aerodynamics (Empty)
   │  ├─ FrontDownforcePoint (Empty at 0, 0.5, 1.5)
   │  └─ RearDownforcePoint (Empty at 0, 0.5, -1.5)
   ├─ Steering (Empty)
   └─ Brakes (Empty)
```

---

## 4. 컴포넌트 설정

### 4.1 루트 GameObject (SportCoupe_Vehicle)

**Add Component:**
1. **Rigidbody**
   ```
   Mass: 1500 (나중에 VehicleData에서 자동 설정)
   Drag: 0.05
   Angular Drag: 0.5
   Use Gravity: ✓
   Is Kinematic: ✗
   Interpolate: Interpolate
   Collision Detection: Continuous
   ```

2. **VehicleController** (Scripts/Physics/Core)
   ```
   Vehicle Data: [VehicleData_SportCoupe 드래그]
   
   Wheel References:
   - Front Left Suspension: [FrontLeft의 SuspensionWheel]
   - Front Left Tire: [FrontLeft의 TireModel]
   - Front Right Suspension: [FrontRight의 SuspensionWheel]
   - Front Right Tire: [FrontRight의 TireModel]
   - Rear Left Suspension: [RearLeft의 SuspensionWheel]
   - Rear Left Tire: [RearLeft의 TireModel]
   - Rear Right Suspension: [RearRight의 SuspensionWheel]
   - Rear Right Tire: [RearRight의 TireModel]
   
   System References:
   - Engine Model: [Systems/Engine의 EngineModel]
   - Gearbox System: [Systems/Gearbox의 GearboxSystem]
   - Aerodynamics Model: [Systems/Aerodynamics의 AerodynamicsModel]
   - Steering System: [Systems/Steering의 SteeringSystem]
   - Brake System: [Systems/Brakes의 BrakeSystem]
   ```

### 4.2 각 바퀴 설정 (예: FrontLeft)

**FrontLeft GameObject에 Add Component:**

1. **SuspensionWheel** (Scripts/Physics/Core)
   ```
   Data: [SuspensionData_SportCoupe 드래그]
   Vehicle Rigidbody: [루트의 Rigidbody - 자동 할당됨]
   Wheel Transform: [WheelVisual 드래그]
   Suspension Anchor: [SuspensionAnchor 드래그]
   ```

2. **TireModel** (Scripts/Physics/Core)
   ```
   Data: [TireData_SportCoupe 드래그]
   Vehicle Rigidbody: [루트의 Rigidbody - 자동 할당됨]
   Wheel Transform: [WheelVisual 드래그]
   ```

**나머지 3개 바퀴도 동일하게 설정!**

### 4.3 시스템 설정

**Systems/Engine GameObject:**
```
Add Component: EngineModel
Data: [EngineData_SportCoupe 드래그]
```

**Systems/Gearbox GameObject:**
```
Add Component: GearboxSystem

Forward Gear Ratios: [6]
- [0] 3.5
- [1] 2.5
- [2] 1.8
- [3] 1.3
- [4] 1.0
- [5] 0.8

Reverse Gear Ratio: -3.0
Final Drive Ratio: 3.5

Automatic Transmission: ✓
Upshift RPM: 6500
Downshift RPM: 3000
Shift Time: 0.2
```

**Systems/Aerodynamics GameObject:**
```
Add Component: AerodynamicsModel
Data: [AerodynamicsData_SportCoupe 드래그]
Vehicle Rigidbody: [루트의 Rigidbody]
Front Downforce Point: [FrontDownforcePoint 드래그]
Rear Downforce Point: [RearDownforcePoint 드래그]
```

**Systems/Steering GameObject:**
```
Add Component: SteeringSystem

Max Steering Angle: 35°
Steering Speed: 150°/s
Speed Sensitive Steering: ✓
Min Steering Multiplier: 0.3
Steering Reduction Speed: 20 m/s

Enable Ackermann: ✓
Wheelbase: 2.7 m
Track Width: 1.8 m
```

**Systems/Brakes GameObject:**
```
Add Component: BrakeSystem

Max Brake Torque: 2500 Nm
Brake Balance: 0.6 (60% 앞)

Enable ABS: ✓
ABS Slip Threshold: 0.15
ABS Reduction Ratio: 0.7

Handbrake Torque: 1500 Nm
Handbrake Rear Only: ✓
```

---

## 5. 테스트 씬 구성

### 5.1 Ground 생성

**Hierarchy 우클릭:**
```
3D Object → Plane
이름: Ground
Transform:
- Position: (0, 0, 0)
- Scale: (10, 1, 10)  ← 100m × 100m

Layer: Ground  ← 중요!
Tag: Road
```

**Ground에 Physics Material (선택사항):**
```
Project 우클릭 → Create → Physics Material
이름: Road_PhysMat

Dynamic Friction: 1.0
Static Friction: 1.0
Bounciness: 0
Combine: Average
```

Ground의 MeshCollider에 Material 할당

### 5.2 카메라 설정 (임시)

**Main Camera:**
```
Position: (0, 3, -10)
Rotation: (10, 0, 0)

Target: SportCoupe_Vehicle (Follow 스크립트는 나중에)
```

### 5.3 Lighting

**Directional Light:**
```
Rotation: (50, -30, 0)
Intensity: 1
```

---

## 6. 테스트 및 디버깅

### 6.1 첫 테스트

1. **Play 버튼 클릭**
2. **기대 동작:**
   - 차량이 바닥에 떨어짐
   - 서스펜션이 압축되며 안정화
   - 차량이 떨리지 않고 정지

3. **입력 테스트:**
   - `W` 키: 가속 (엔진 RPM 상승)
   - `S` 키: 브레이크
   - `A/D` 키: 조향
   - `Space` 키: 핸드브레이크

### 6.2 디버그 정보 확인

**Scene 뷰:**
- 초록색 레이: 서스펜션 Raycast (접지)
- 빨간색 레이: 서스펜션 Raycast (공중)
- 노란색 구: 접촉점
- 파란색 화살표: 다운포스
- 빨간색 화살표: 드래그

**Console 확인:**
```
[VehicleController] Sport Coupe 초기화 완료!
[SuspensionData] Auto-tuned for 1500kg vehicle:
  Spring Rate: 35000 N/m
  Damper Compression: 3500 N·s/m
```

### 6.3 문제 해결

**문제 1: 차량이 땅에 파묻힘**
```
해결:
1. SuspensionData의 Spring Rate 증가 (× 1.5)
2. Rest Length 증가 (+0.1m)
3. 바퀴 위치 확인 (Y 좌표가 차체보다 아래)
```

**문제 2: 차량이 튀어오름**
```
해결:
1. Damper Compression/Rebound 증가 (× 1.5)
2. Force Smoothing 활성화 (0.2)
3. Fixed Timestep 확인 (0.01)
```

**문제 3: 조향이 안됨**
```
해결:
1. SteeringSystem이 FrontLeft/Right에 할당되었는지 확인
2. WheelTransform이 올바르게 설정되었는지 확인
3. Max Steering Angle > 0 확인
```

**문제 4: 가속이 안됨**
```
해결:
1. VehicleData의 Drive Type 확인 (RWD/FWD/AWD)
2. 엔진 토크가 0이 아닌지 확인
3. 기어가 중립(N)이 아닌지 확인
4. 타이어가 접지되어 있는지 확인
```

**문제 5: 성능 문제 (낮은 FPS)**
```
해결:
1. Fixed Timestep을 0.02로 증가 (테스트용)
2. Debug Rays/Forces 비활성화
3. Show Debug Info 비활성화
```

---

## 7. 고급 설정

### 7.1 입력 시스템 교체

현재는 기본 Input Manager 사용 중.

**New Input System 사용 시:**
1. Package Manager → Input System 설치
2. VehicleController.cs의 `UpdateInput()` 수정
3. Input Actions 생성

### 7.2 프리팹 저장

모든 설정이 완료되면:
```
1. Hierarchy에서 SportCoupe_Vehicle 선택
2. Project 창으로 드래그
3. Prefab으로 저장
```

### 7.3 여러 차량 타입 생성

**프리셋 값:**

**Hatchback (해치백):**
```
Mass: 1200kg
Spring Rate: 25000 N/m
Peak Force: 6000 N
Max Torque: 250 Nm
Max Steering Angle: 40°
```

**Supercar (슈퍼카):**
```
Mass: 1400kg
Spring Rate: 50000 N/m
Peak Force: 12000 N
Max Torque: 600 Nm
Max Steering Angle: 30°
Front/Rear Downforce: 3.0/4.0
```

**Race Car (레이스카):**
```
Mass: 1100kg
Spring Rate: 70000 N/m
Peak Force: 15000 N
Max Torque: 450 Nm
Max Steering Angle: 28°
Front/Rear Downforce: 5.0/7.0
```

---

## 8. 체크리스트

설정 완료 확인:

### ScriptableObjects
- [ ] VehicleData 생성 및 설정
- [ ] SuspensionData 생성 (Auto-Tune 실행)
- [ ] TireData 생성 및 설정
- [ ] EngineData 생성 (Torque Curve 생성)
- [ ] AerodynamicsData 생성

### GameObject 구조
- [ ] 루트 GameObject (Rigidbody + VehicleController)
- [ ] Body (Collider + Mesh)
- [ ] 4개 바퀴 (SuspensionWheel + TireModel)
- [ ] 시스템 GameObjects (Engine, Gearbox 등)

### 참조 연결
- [ ] VehicleController의 모든 참조 연결
- [ ] 각 바퀴의 SuspensionAnchor/WheelTransform 연결
- [ ] 시스템들의 데이터 에셋 할당

### Physics 설정
- [ ] Fixed Timestep = 0.01
- [ ] Layer "Ground" 생성
- [ ] Ground에 Layer와 Tag 할당

### 테스트
- [ ] Play 모드에서 차량이 안정적으로 정지
- [ ] W/S로 가속/브레이크
- [ ] A/D로 조향
- [ ] Console에 에러 없음

---

## 9. 다음 단계

물리 엔진이 완성되면:

1. **카메라 시스템** - 차량 추적 카메라
2. **사운드 시스템** - 엔진음, 타이어 스키드
3. **VFX** - 배기가스, 타이어 자국, 파티클
4. **AI 시스템** - AI 드라이버
5. **트랙/코스** - 레이싱 트랙 디자인
6. **UI/HUD** - 속도계, 타코미터, 미니맵

---

**문서 버전**: 1.0  
**작성일**: 2024  
**테스트 환경**: Unity 2022.3 LTS
