"""
Lucid Knight — Performance Data Visualizer
사용법:
  python visualize.py                          # 가장 최근 CSV 자동 선택
  python visualize.py before_release.csv        # 특정 파일 단독 분석
  python visualize.py before_release.csv after_release.csv   # Before/After 비교

라벨/범례는 파일명에서 자동 추론한다. 파일명에 before/after, release/dev 가
들어있으면 "Before (Release)" 같은 보기 좋은 라벨로 변환된다.
명시적으로 지정하려면  파일경로=라벨  형식을 쓸 수 있다:
  python visualize.py before_dev.csv=v0.0.0 after_dev.csv=v1.0.0
"""

import sys
import glob
import os
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.font_manager as fm
import numpy as np

# ── 한국어 폰트 설정 (macOS: AppleGothic, Windows: Malgun Gothic) ─────────────
def setup_korean_font():
    candidates = ["AppleGothic", "NanumGothic", "Malgun Gothic", "Arial Unicode MS"]
    available  = {f.name for f in fm.fontManager.ttflist}
    for font in candidates:
        if font in available:
            plt.rcParams["font.family"] = font
            return
    # 지원 폰트 없으면 영문 fallback (깨짐 방지)
    plt.rcParams["font.family"] = "DejaVu Sans"

setup_korean_font()
plt.rcParams["axes.unicode_minus"] = False  # 마이너스 기호 깨짐 방지

# ── 설정 ──────────────────────────────────────────────────────────────────────
SCRIPT_DIR   = os.path.dirname(os.path.abspath(__file__))
SPIKE_MS     = 33.3
TARGET_60FPS = 16.6
TARGET_90FPS = 11.1

COLOR_BEFORE = "#e05252"
COLOR_AFTER  = "#52aae0"
COLOR_SPIKE  = "#ff4444"
COLOR_GC     = "#f0a030"
# ─────────────────────────────────────────────────────────────────────────────


def pretty_label(raw: str) -> str:
    """파일명에서 보기 좋은 라벨 추론 (before_release → 'Before (Release)')"""
    s = raw.lower()
    if   "before" in s: phase = "Before"
    elif "after"  in s: phase = "After"
    else:               return raw            # 추론 불가 시 원본 유지
    if   "release" in s: return f"{phase} (Release)"
    elif "dev"     in s: return f"{phase} (Dev)"
    return phase


def load_csv(path: str) -> pd.DataFrame:
    df = pd.read_csv(path, parse_dates=["Timestamp"])

    # 초기 로딩 구간 제거 (구간 최대 FrameTime > 1000ms = 씬 초기화)
    df = df[df["MaxFrameMs"] < 1000].reset_index(drop=True)

    df["GC_KB"]     = df["GCAvgPerFrame_B"] / 1024        # 프레임당 평균 GC 할당
    df["GCMax_KB"]  = df["GCMaxPerFrame_B"] / 1024        # 프레임당 최대 GC 할당
    df["Elapsed_s"] = (df["Timestamp"] - df["Timestamp"].iloc[0]).dt.total_seconds()
    return df


def spike_count(df: pd.DataFrame) -> int:
    # 구간 최대 FrameTime이 임계값을 넘은 구간 수
    return int((df["MaxFrameMs"] > SPIKE_MS).sum())


def print_summary(label: str, df: pd.DataFrame):
    spikes = spike_count(df)
    print(f"\n{'─'*44}")
    print(f"  {label}")
    print(f"{'─'*44}")
    print(f"  측정 시간          : {df['Elapsed_s'].iloc[-1]:.0f}초")
    print(f"  평균 FPS           : {df['AvgFPS'].mean():.0f}")
    print(f"  평균 FrameTime     : {df['AvgFrameMs'].mean():.1f} ms")
    print(f"  p95 FrameTime      : {df['P95FrameMs'].mean():.1f} ms")
    print(f"  최대 FrameTime     : {df['MaxFrameMs'].max():.1f} ms")
    print(f"  스파이크 구간      : {spikes}개 (>{SPIKE_MS}ms)")
    print(f"  GC/프레임 평균     : {df['GCAvgPerFrame_B'].mean():.0f} B")
    print(f"  GC/프레임 최대     : {df['GCMaxPerFrame_B'].max():.0f} B")
    print(f"  Mono Heap 평균     : {df['MonoHeapMB'].mean():.0f} MB")
    print(f"  Total Mem 평균     : {df['TotalMemMB'].mean():.0f} MB")


def plot_single(df: pd.DataFrame, label: str, color: str, axes):
    ax_fps, ax_ft, ax_gc, ax_heap = axes
    t = df["Elapsed_s"]

    # --- 평균 FPS ---
    ax_fps.plot(t, df["AvgFPS"], color=color, lw=1.5, label=f"{label}")
    ax_fps.fill_between(t, df["AvgFPS"], alpha=0.15, color=color)

    # --- Frame Time (평균선 + 최대 밴드) ---
    ax_ft.plot(t, df["AvgFrameMs"], color=color, lw=1.4, label=f"{label} avg")
    ax_ft.fill_between(t, df["AvgFrameMs"], df["MaxFrameMs"],
                       color=color, alpha=0.12, label=f"{label} avg→max")
    spikes = df[df["MaxFrameMs"] > SPIKE_MS]
    ax_ft.scatter(spikes["Elapsed_s"], spikes["MaxFrameMs"],
                  color=COLOR_SPIKE, s=35, zorder=5, label=f"스파이크 ({len(spikes)})")

    # --- GC per frame (B) — 0에 붙어야 "GC 0B" 달성 ---
    gc_color = COLOR_GC if color == COLOR_BEFORE else "#a0d0a0"
    ax_gc.plot(t, df["GCMaxPerFrame_B"], color=gc_color, lw=1.5, label=f"{label} max/frame")
    ax_gc.plot(t, df["GCAvgPerFrame_B"], color=gc_color, lw=1.0, ls="--", alpha=0.7,
               label=f"{label} avg/frame")

    # --- Total Memory MB ---
    ax_heap.plot(t, df["TotalMemMB"], color=color, lw=1.5, label=label)


def make_figure(files: list[str]):
    datasets = []
    for f in files:
        # "경로=라벨" 형식 지원 (라벨 미지정 시 파일명에서 추론)
        if "=" in f:
            raw_path, explicit_label = f.split("=", 1)
        else:
            raw_path, explicit_label = f, None
        path = raw_path if os.path.isabs(raw_path) else os.path.join(SCRIPT_DIR, raw_path)
        df   = load_csv(path)
        stem  = os.path.splitext(os.path.basename(path))[0]
        label = explicit_label or pretty_label(stem)
        datasets.append((label, df, os.path.dirname(path), stem))

    fig, axes = plt.subplots(4, 1, figsize=(14, 13),
                             gridspec_kw={"hspace": 0.85})
    fig.patch.set_facecolor("#1a1a2e")
    for ax in axes:
        ax.set_facecolor("#16213e")
        ax.tick_params(colors="white")
        ax.xaxis.label.set_color("white")
        ax.yaxis.label.set_color("white")
        ax.title.set_color("white")
        for spine in ax.spines.values():
            spine.set_edgecolor("#444466")

    ax_fps, ax_ft, ax_gc, ax_heap = axes
    colors = [COLOR_BEFORE, COLOR_AFTER]

    for (label, df, _, _), color in zip(datasets, colors):
        plot_single(df, label, color, axes)
        print_summary(label, df)

    # 범례는 플롯 밖 오른쪽에 배치 (데이터 가림 방지)
    leg_kw = dict(facecolor="#1a1a2e", labelcolor="white", fontsize=8,
                  loc="upper left", bbox_to_anchor=(1.01, 1.0),
                  framealpha=0.9, edgecolor="#444466")

    # --- 평균 FPS ---
    ax_fps.set_title("Average FPS  (구간 평균)", fontsize=12, pad=6)
    ax_fps.set_ylabel("FPS", fontsize=10)
    ax_fps.axhline(60, color="#aaaaff", lw=1, ls="--", alpha=0.7, label="60 fps target")
    ax_fps.axhline(90, color="#aaffaa", lw=1, ls="--", alpha=0.7, label="90 fps VR target")
    ax_fps.legend(**leg_kw)
    ax_fps.set_ylim(bottom=0)
    ax_fps.tick_params(labelbottom=False)  # x축 레이블 숨김

    # --- Frame Time ---
    ax_ft.set_title("Frame Time (ms)  — avg(선) / max(밴드), lower is better", fontsize=12, pad=6)
    ax_ft.set_ylabel("ms", fontsize=10)
    ax_ft.axhline(TARGET_60FPS, color="#aaaaff", lw=1, ls="--", alpha=0.8, label=f"{TARGET_60FPS} ms  (60 fps)")
    ax_ft.axhline(TARGET_90FPS, color="#aaffaa", lw=1, ls="--", alpha=0.8, label=f"{TARGET_90FPS} ms  (90 fps VR)")
    ax_ft.axhline(SPIKE_MS,     color=COLOR_SPIKE, lw=1, ls=":",  alpha=0.6, label=f"spike threshold  ({SPIKE_MS} ms)")
    ax_ft.legend(**leg_kw)
    ax_ft.set_ylim(bottom=0)
    ax_ft.tick_params(labelbottom=False)

    # --- GC per frame ---
    ax_gc.set_title("GC Allocated per Frame (Bytes)  — 0에 붙을수록 GC 0B 달성", fontsize=12, pad=6)
    ax_gc.set_ylabel("Bytes", fontsize=10)
    ax_gc.axhline(0, color="#888888", lw=1, ls="-", alpha=0.5)
    ax_gc.legend(**leg_kw)
    ax_gc.tick_params(labelbottom=False)

    # 릴리즈 빌드는 Profiler 비활성 → GC 카운터가 항상 0 (측정 불가) 안내
    if all(df["GCMaxPerFrame_B"].max() == 0 for _, df, _, _ in datasets):
        ax_gc.text(0.5, 0.5,
                   "릴리즈 빌드: Profiler 비활성으로 GC 측정 불가\nGC 비교는 개발 빌드(Dev) 리포트 참고",
                   transform=ax_gc.transAxes, ha="center", va="center",
                   color="#ffcc66", fontsize=11, alpha=0.9)

    # --- Total Memory (맨 아래만 x축 레이블 표시) ---
    ax_heap.set_title("Total Allocated Memory (MB)", fontsize=12, pad=6)
    ax_heap.set_ylabel("MB", fontsize=10)
    ax_heap.set_xlabel("Elapsed (s)", fontsize=11)
    ax_heap.legend(**leg_kw)

    # --- 타이틀 + 개선율 요약 ---
    if len(datasets) == 2:
        (lbl_b, df_b, _, _), (lbl_a, df_a, _, _) = datasets[0], datasets[1]
        title = f"Lucid Knight — Performance Report  [{lbl_b} → {lbl_a}]"

        def pct(before, after):
            return (after - before) / before * 100 if before else 0.0
        fps_b, fps_a = df_b["AvgFPS"].mean(),     df_a["AvgFPS"].mean()
        ft_b,  ft_a  = df_b["AvgFrameMs"].mean(), df_a["AvgFrameMs"].mean()
        deltas = (f"FPS {fps_b:.0f} → {fps_a:.0f} ({pct(fps_b, fps_a):+.0f}%)    "
                  f"FrameTime {ft_b:.1f} → {ft_a:.1f} ms ({pct(ft_b, ft_a):+.0f}%)")
        # GC는 측정된 경우(개발 빌드)만 표시
        if df_b["GCMaxPerFrame_B"].max() > 0 or df_a["GCMaxPerFrame_B"].max() > 0:
            gc_b, gc_a = df_b["GCAvgPerFrame_B"].mean(), df_a["GCAvgPerFrame_B"].mean()
            deltas += f"    GC/frame {gc_b:.0f} → {gc_a:.0f} B ({pct(gc_b, gc_a):+.0f}%)"
        fig.text(0.5, 0.975, deltas, ha="center", va="top",
                 color="#9fe6b0", fontsize=11, fontweight="bold")
    else:
        title = f"Lucid Knight — Performance Report  [{datasets[0][0]}]"
    fig.suptitle(title, fontsize=13, color="white", y=1.012)

    # --- 저장 ---
    # 단일: CSV와 같은 서브디렉토리에 저장
    # 비교: PerformanceData/Comparison 에 저장 (파일명에 두 데이터셋 stem 반영)
    if len(datasets) == 1:
        out_dir  = datasets[0][2]
        out_name = f"report_{datasets[0][3]}.png"
    else:
        out_dir  = os.path.join(SCRIPT_DIR, "Comparison")
        os.makedirs(out_dir, exist_ok=True)
        out_name = f"compare_{datasets[0][3]}_vs_{datasets[1][3]}.png"

    out_path = os.path.join(out_dir, out_name)
    fig.savefig(out_path, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
    print(f"\n✅ 저장 완료: {out_path}")
    plt.close(fig)


def resolve_files(args: list[str]) -> list[str]:
    if args:
        # 상대 경로면 SCRIPT_DIR 기준으로 해석, 서브디렉토리도 허용
        # "경로=라벨" 형식이면 라벨 부분은 검증에서 제외하고 그대로 전달
        resolved = []
        for a in args[:2]:
            raw_path, sep, label = a.partition("=")
            path = raw_path if os.path.isabs(raw_path) else os.path.join(SCRIPT_DIR, raw_path)
            if not os.path.exists(path):
                sys.exit(f"❌ 파일을 찾을 수 없습니다: {path}")
            resolved.append(f"{path}={label}" if sep else path)
        return resolved

    # 인수 없으면 서브디렉토리 포함 가장 최근 CSV 자동 선택
    csvs = sorted(glob.glob(os.path.join(SCRIPT_DIR, "**", "perf_*.csv"), recursive=True))
    if not csvs:
        sys.exit("❌ CSV 파일을 찾을 수 없습니다. PerformanceData 폴더에 CSV가 있는지 확인하세요.")
    latest = csvs[-1]
    print(f"자동 선택: {os.path.relpath(latest, SCRIPT_DIR)}")
    return [latest]


if __name__ == "__main__":
    files = resolve_files(sys.argv[1:])
    make_figure(files)
