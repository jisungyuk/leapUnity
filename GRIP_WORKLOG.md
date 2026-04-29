# GRIP Game Mode — Work Log

## 2026-04-29

### 세션 목표
Ultraleap 손 추적 기반 GRIP(Reach & Grasp) 게임 모드 구현.
기존 RWR 게임을 건드리지 않고 독립적인 GRIP 씬/스크립트로 구성.

---

### 구현 내용

#### 1. CapsuleHand provider 수정
- GRIP_Game 씬에서 CapsuleHand 두 개의 `_leapProvider`가 `LeapServiceProvider`(raw)를 바라보고 있었음
- offset이 적용된 가상 손 모델을 보여주기 위해 `HandOffsetProvider`로 변경
- 씬 파일(GRIP_Game.unity) 직접 편집으로 수정

#### 2. 원통(타겟) 물리 설정
- `GameObject.CreatePrimitive(Cylinder)` 생성 시 `CapsuleCollider.isTrigger = true` 설정
- Physical Hands가 원통을 물리적으로 밀어내는 현상 방지
- `Rigidbody.isKinematic = true`로 중력/물리 영향 차단

#### 3. 원통 불투명 처리
- 타겟 원통 Material을 Opaque로 변경 (`SetOpaqueColor` 메서드 추가)
- 손가락이 원통을 통과해 보이는 현상 제거
- 색상 alpha 1.0 적용 (idle: 회색 / active: 노랑 / good: 초록 / bad: 빨강)

#### 4. Grab 판정 로직
- Physical Hands 기반 GrabHelper 대신 코드 기반 proximity 감지로 전환
- **조건**: 엄지(`thumbTip`) AND 검지(`indexTip`) 둘 다 `targetRadius` 이내일 때 grab 판정
- XZ 평면 기준 거리 계산

#### 5. 손 Freeze + 물체 커플링 (핵심 인터랙션)
Meta Interaction SDK의 원리를 참고하여 구현:

**FreezeProvider.cs** (신규 스크립트)
- `PostProcessProvider`를 상속, CapsuleHand의 `_leapProvider`로 연결
- `Freeze(Vector3 pivot)`: 현재 프레임을 스냅샷, grab pivot(MCP 위치) 기록
- `UpdateTransform(Vector3 delta, Quaternion rot)`: 매 프레임 스냅샷 frame의 모든 뼈 위치를 변환
- `Unfreeze()`: 원래 live 데이터로 복귀
- CapsuleHand에만 연결 — LeapFingerInput, Gettinghanddata는 HandOffsetProvider에서 직접 live 데이터 수신

**Grab 이후 동작 흐름**:
1. `CheckProximity()` true → `FreezeHand()` 호출
2. `FreezeProvider.Freeze(grabMcpPos)` — 손 모델 스냅샷
3. 매 프레임 `UpdateCylinderFollow()`:
   - `mcpDelta = indexMcp.position - grabMcpPos`
   - `rotDelta = currentPalmRot * Quaternion.Inverse(grabHandRot)`
   - `FreezeProvider.UpdateTransform(mcpDelta, rotDelta)` → 손 모델 이동
   - 원통 position/rotation 동일 수식으로 업데이트
4. 손과 원통이 하나의 rigid body처럼 함께 이동/회전

#### 6. Palm rotation 기반 회전 추적
- `Gettinghanddata.cs`에 `leftPalmRot`, `rightPalmRot` 추가 (Leap `hand.Rotation`)
- `indexMcp.rotation` 대신 palm rotation 사용 → 손 전체 회전 올바르게 반영

#### 7. 성공/실패 판정 (EvaluateOutcome)
- **성공 조건**: `isGrabbed == true` AND `IsGripSuccessful()`
  - 엄지-검지 간격 < `targetRadius * 2` (원통 지름보다 좁아야 함)
  - 두 손가락 끝이 원통 볼륨 안에 있어야 함 (XZ ≤ radius, Y ≤ halfHeight)
- 타이머 기반 종료: `executionDuration` 만료 시 판정, 조기 종료 없음

---

### 변경된 파일

| 파일 | 변경 내용 |
|------|-----------|
| `Assets/Script/FreezeProvider.cs` | 신규 — hand frame freeze/transform provider |
| `Assets/Script/TrialGameController_GRIP.cs` | 신규 — GRIP trial state machine |
| `Assets/Script/GameSessionController_GRIP.cs` | 신규 (이전 세션) |
| `Assets/Script/Gettinghanddata.cs` | leftPalmRot/rightPalmRot 추가 |
| `Assets/Scenes/GRIP_Game.unity` | CapsuleHand provider 수정, FreezeProvider 연결 |

---

### 씬 구성 (GRIP_Game.unity)

```
LeapServiceProvider
  └─ HandOffsetProvider
       ├─ FreezeProvider  →  CapsuleHand (Left/Right) [시각 전용]
       ├─ LeapFingerInput  →  indexTip, thumbTip, indexMcp transforms
       └─ Gettinghanddata  →  MCP 위치, palm rotation (live)
```

---

### 다음 작업 후보
- 실험 데이터 로깅 (GRIP trial CSV 저장) 검증
- GRIP_Session, GRIP_Target 씬 UI 점검
- 손가락 포즈 세밀 조정 (SyntheticHand 방식 탐색)
