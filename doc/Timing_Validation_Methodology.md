# Timing Precision Validation — 방법론

LeapDex의 핵심 클레임은 "programmed(사용자가 설정한) 딜레이대로 TTL이 정확히 deliver된다"는 것. 이 문서는 그걸 어떻게 검증할지 정리한 것.

## 실제 실험 구조

```
instruction cue → (설정 가능한 2-3s) → go cue
                                          ↑
                            TTL은 go cue 500ms 전에 발사됨
                            (이 delay 값은 사용자가 자유롭게 설정 가능)
```

## 검증하려는 것

"Unity가 의도한 delay(예: 500ms)만큼 정확히 떨어진 시점에 TTL을 내보내는가?" — 즉 **Unity의 소프트웨어 타이밍 jitter**를 정량화하는 것.

## 왜 순진한 접근이 실패하는가

**시도해볼 법한 (하지만 틀린) 방법**: PowerLab이 TTL을 입력받아서 Output1(즉시)과 Output2(내부적으로 계산한 delay만큼 후)를 쏘게 하고, Output1↔Output2 간격을 측정.

**왜 안 되는가**: PowerLab이 자체적으로 만드는 delay(Output1→Output2)는 PowerLab의 전용 하드웨어 클럭으로 계산되기 때문에 jitter가 거의 없음. 이건 "PowerLab이 정확한 측정 도구다"라는 것만 증명할 뿐, **Unity의 타이밍 정확도는 전혀 검증하지 못함**. 왜냐하면 TTL이 PowerLab에 "들어오는 순간" 이미 Unity 쪽에서 발생했을 jitter는 다 발생을 마친 상태이기 때문. PowerLab 입장에서는 그 시점 이후로 뭘 하든(Output1, Output2 생성 등) 전부 정확하게 계산되니, Output들끼리 비교해봐야 "이미 지나간 오차"는 안 보임.

## 올바른 방법

Unity가 **독립적으로 생성하는 두 이벤트**를 PowerLab의 서로 다른 채널에 각각 기록해서, 그 둘의 간격을 직접 잰다:

1. **이벤트 A**: 실제 자극/기록용 TTL (go cue 500ms 전에 발사)
2. **이벤트 B**: go cue 마커 (go cue 시점에 발사)

두 이벤트 모두 Unity가 만들어서 TriggerBox를 거쳐 PowerLab에 각각 입력으로 들어감. PowerLab은 여기서 **수동적으로 두 이벤트의 도착 시점만 정밀 기록**하는 역할만 함 (PowerLab 자체의 기록 오차는 무시할 수 있는 수준).

**계산**: 매 trial마다 (이벤트 B 도착 시점 − 이벤트 A 도착 시점) = 실제 간격. 이걸 의도한 programmed delay(500ms)와 비교.
→ 이 차이(bias)와 trial 간 변동(SD) = **Unity의 실제 소프트웨어 타이밍 jitter**

이 방법이 맞는 이유: PowerLab은 여기서 "정확한 딜레이를 만드는 장치"로 쓰이는 게 아니라, 순수히 "두 개의 독립적 이벤트가 언제 도착했는지 정밀하게 기록하는 스톱워치"로만 쓰임. 측정 도구 자체의 오차는 무시할 수 있을 만큼 작으니, 관찰되는 간격의 변동은 전부 Unity가 만들어내는 실제 jitter를 반영함.

## 실험 설계

- **반복 trial**: 하나의 programmed delay에 대해 n=100+ 반복 → mean latency(bias), SD(jitter), min/max 산출
- **여러 delay 값**: 0, 50, 100, 300, 500ms 등 다양한 delay에 대해 반복 → delay-dependent bias가 있는지 확인
- **두 조건 비교**:
  1. **Simulated condition** — Unity 스크립트로 trigger 조건을 시뮬레이션 (카메라/hand tracking 연산 없이, 순수 소프트웨어+하드웨어 체인의 타이밍만 측정)
  2. **Real-use condition** — 실제 참가자가 reach trial을 수행하는 동안, 카메라 기반 hand tracking이 실시간으로 돌아가는 상태에서 동일하게 측정
  - 두 조건의 latency/jitter 분포를 통계적으로 비교 (예: t-test, 분산 비교)하여 **트래킹 연산 부하가 타이밍 정확도에 영향을 주는지** 정량화
  - Unity는 보통 프레임 기반 Update loop로 동작하므로, 트래킹 연산 부하가 프레임레이트를 떨어뜨리면 그만큼 타이밍 해상도/jitter도 나빠질 수 있음 → 가능하면 **프레임레이트 로그도 함께 제시**해서, real-use 조건의 jitter 증가가 프레임레이트 저하 때문임을 뒷받침

## 대상 modality

TMS를 메인 데모로 사용. EEG/PNS는 동일한 TTL 메커니즘이 정상적으로 각 장비에 출력되는 것만 기술적으로 증빙 (예: oscilloscope trace).
