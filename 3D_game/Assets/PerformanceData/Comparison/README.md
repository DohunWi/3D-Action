# 성능 최적화 Before / After 비교

측정일: 2026-06-27
- **v0.0.0 (Before)**: `bfb43ec` (최적화 직전, 로거만 신규 버전 이식)
- **v1.0.0 (After)**: 최적화 완료 (URP Shadow/SSAO, VFX Overdraw, GC 풀링·캐싱)
- 동일 동선·시간(~80–90초), 동일 측정 도구(`PerformanceLogger`)

## 핵심 수치

| 지표 | v0.0.0 | v1.0.0 | 개선 |
|------|--------|--------|------|
| 평균 FPS (릴리즈) | 94 | 152 | **+62%** |
| 평균 FrameTime (릴리즈) | 11.1 ms | 6.9 ms | **-38%** |
| p95 FrameTime (릴리즈) | 12.8 ms | 7.5 ms | **-41%** |
| GC/frame 평균 (개발) | 454 B | 190 B | **-58%** |
| GC/frame 최대 (개발) | 815 KB | 417 KB | -49% |

## 측정 방법론 주의사항

- **FPS / FrameTime** 은 릴리즈 빌드 수치가 정확 (Profiler 오버헤드 없음).
- **GC/frame** 은 릴리즈 빌드에서 측정 불가 — `ProfilerRecorder "GC Allocated In Frame"`
  카운터가 Profiler 비활성(릴리즈) 상태에서 항상 0을 반환하기 때문.
  따라서 GC 비교는 **개발 빌드** 수치로 수행 (Before/After 동일 조건이므로 delta 유효).
- 개발 빌드 GC 잔여분(After ~190B)은 측정 도구 자체(TMP HUD·PerformanceLogger)의
  할당이며, 게임 로직 GC 는 Profiler "Top GC contributors" 분석상 사실상 0B.

## 디렉토리 구조

```
PerformanceData/
├── v0.0.0_Release/   v0.0.0_release.csv   (94 FPS / 11.1ms)
├── v0.0.0_DevBuild/  v0.0.0_dev.csv       (GCAvg 454B)
├── v1.0.0_Release/   v1.0.0_release.csv   (152 FPS / 6.9ms)
├── v1.0.0_DevBuild/  v1.0.0_dev.csv       (GCAvg 190B)
└── Comparison/
    ├── compare_v0.0.0_release_vs_v1.0.0_release.png   (FPS·FrameTime)
    └── compare_v0.0.0_dev_vs_v1.0.0_dev.png           (GC per frame)
```

## 재생성 방법

```bash
cd Assets/PerformanceData
# 릴리즈 비교 (FPS/FrameTime)
python3 visualize.py "v0.0.0_Release/v0.0.0_release.csv=v0.0.0" "v1.0.0_Release/v1.0.0_release.csv=v1.0.0"
# 개발 비교 (GC)
python3 visualize.py "v0.0.0_DevBuild/v0.0.0_dev.csv=v0.0.0" "v1.0.0_DevBuild/v1.0.0_dev.csv=v1.0.0"
```
