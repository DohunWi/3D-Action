"""
Lucid Knight — Performance Data Visualizer
사용법:
  python visualize.py                          # 가장 최근 CSV 자동 선택
  python visualize.py perf_before.csv          # 특정 파일 단독 분석
  python visualize.py perf_before.csv perf_after.csv  # Before/After 비교
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


def load_csv(path: str) -> pd.DataFrame:
    df = pd.read_csv(path, parse_dates=["Timestamp"])

    # 초기 로딩 프레임 제거 (Frame Time > 1000ms는 씬 초기화)
    df = df[df["FrameTime_ms"] < 1000].reset_index(drop=True)

    df["GC_MB"]      = df["GC_AllocBytes"] / 1_048_576
    df["Elapsed_s"]  = (df["Timestamp"] - df["Timestamp"].iloc[0]).dt.total_seconds()
    df["FPS_smooth"] = df["FPS"]  # raw 값 사용 — 이동평균은 스파이크를 숨김
    return df


def gc_growth_rate(df: pd.DataFrame) -> float:
    """MB/s 단위 GC 누적 속도 (선형 회귀)"""
    x = df["Elapsed_s"].values
    y = df["GC_MB"].values
    coef = np.polyfit(x, y, 1)
    return coef[0]


def spike_count(df: pd.DataFrame) -> int:
    return int((df["FrameTime_ms"] > SPIKE_MS).sum())


def print_summary(label: str, df: pd.DataFrame):
    gc_rate = gc_growth_rate(df)
    spikes  = spike_count(df)
    print(f"\n{'─'*40}")
    print(f"  {label}")
    print(f"{'─'*40}")
    print(f"  측정 시간       : {df['Elapsed_s'].iloc[-1]:.0f}초")
    print(f"  FPS 평균/최소   : {df['FPS'].mean():.0f} / {df['FPS'].min():.0f}")
    print(f"  Frame Time 최대 : {df['FrameTime_ms'].max():.1f} ms")
    print(f"  스파이크 횟수   : {spikes}회 (>{SPIKE_MS}ms)")
    print(f"  GC 누적 속도    : {gc_rate:.2f} MB/s")
    print(f"  Heap 평균       : {df['HeapMB'].mean():.0f} MB")


def plot_single(df: pd.DataFrame, label: str, color: str, axes):
    ax_fps, ax_ft, ax_gc, ax_heap = axes
    t = df["Elapsed_s"]

    # --- FPS ---
    ax_fps.plot(t, df["FPS_smooth"], color=color, lw=1.5, label=f"{label}")
    ax_fps.fill_between(t, df["FPS_smooth"], alpha=0.15, color=color)

    # --- Frame Time ---
    ax_ft.plot(t, df["FrameTime_ms"], color=color, lw=1.2, alpha=0.8, label=label)
    spikes = df[df["FrameTime_ms"] > SPIKE_MS]
    ax_ft.scatter(spikes["Elapsed_s"], spikes["FrameTime_ms"],
                  color=COLOR_SPIKE, s=40, zorder=5, label=f"스파이크 ({len(spikes)}회)")

    # --- GC Mono MB ---
    ax_gc.plot(t, df["GC_MB"], color=COLOR_GC if color == COLOR_BEFORE else "#a0d0a0",
               lw=1.5, label=label)

    # --- Heap MB ---
    ax_heap.plot(t, df["HeapMB"], color=color, lw=1.5, label=label)


def make_figure(files: list[str]):
    datasets = []
    for f in files:
        path = f if os.path.isabs(f) else os.path.join(SCRIPT_DIR, f)
        df   = load_csv(path)
        label = os.path.splitext(os.path.basename(path))[0]
        datasets.append((label, df, os.path.dirname(path)))

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

    for (label, df, _), color in zip(datasets, colors):
        plot_single(df, label, color, axes)
        print_summary(label, df)

    leg_kw = dict(facecolor="#1a1a2e", labelcolor="white", fontsize=9)

    # --- FPS ---
    ax_fps.set_title("FPS  (raw)", fontsize=12, pad=6)
    ax_fps.set_ylabel("FPS", fontsize=10)
    ax_fps.axhline(60, color="#aaaaff", lw=1, ls="--", alpha=0.7, label="60 fps target")
    ax_fps.axhline(90, color="#aaffaa", lw=1, ls="--", alpha=0.7, label="90 fps VR target")
    ax_fps.legend(**leg_kw)
    ax_fps.set_ylim(bottom=0)
    ax_fps.tick_params(labelbottom=False)  # x축 레이블 숨김

    # --- Frame Time ---
    ax_ft.set_title("Frame Time (ms)  — lower is better", fontsize=12, pad=6)
    ax_ft.set_ylabel("ms", fontsize=10)
    ax_ft.axhline(TARGET_60FPS, color="#aaaaff", lw=1, ls="--", alpha=0.8, label=f"{TARGET_60FPS} ms  (60 fps)")
    ax_ft.axhline(TARGET_90FPS, color="#aaffaa", lw=1, ls="--", alpha=0.8, label=f"{TARGET_90FPS} ms  (90 fps VR)")
    ax_ft.axhline(SPIKE_MS,     color=COLOR_SPIKE, lw=1, ls=":",  alpha=0.6, label=f"spike threshold  ({SPIKE_MS} ms)")
    ax_ft.legend(**leg_kw)
    ax_ft.set_ylim(bottom=0)
    ax_ft.tick_params(labelbottom=False)

    # --- GC Mono ---
    ax_gc.set_title("GC Mono Usage (MB)  — rising = GC bomb incoming", fontsize=12, pad=6)
    ax_gc.set_ylabel("MB", fontsize=10)
    ax_gc.legend(**leg_kw)
    ax_gc.tick_params(labelbottom=False)

    # --- Heap (맨 아래만 x축 레이블 표시) ---
    ax_heap.set_title("Total Heap (MB)", fontsize=12, pad=6)
    ax_heap.set_ylabel("MB", fontsize=10)
    ax_heap.set_xlabel("Elapsed (s)", fontsize=11)
    ax_heap.legend(**leg_kw)

    # --- 타이틀 ---
    mode   = "Before vs After" if len(datasets) == 2 else datasets[0][0]
    title  = f"Lucid Knight — Performance Report  [{mode}]"
    fig.suptitle(title, fontsize=13, color="white", y=1.005)

    # --- 저장 ---
    # 단일: CSV와 같은 서브디렉토리에 저장
    # 비교: PerformanceData 루트에 저장
    if len(datasets) == 1:
        out_dir  = datasets[0][2]
        out_name = f"report_{datasets[0][0]}.png"
    else:
        out_dir  = SCRIPT_DIR
        out_name = "report_comparison.png"

    out_path = os.path.join(out_dir, out_name)
    fig.savefig(out_path, dpi=150, bbox_inches="tight", facecolor=fig.get_facecolor())
    print(f"\n✅ 저장 완료: {out_path}")
    plt.close(fig)


def resolve_files(args: list[str]) -> list[str]:
    if args:
        # 상대 경로면 SCRIPT_DIR 기준으로 해석, 서브디렉토리도 허용
        resolved = []
        for a in args[:2]:
            path = a if os.path.isabs(a) else os.path.join(SCRIPT_DIR, a)
            if not os.path.exists(path):
                sys.exit(f"❌ 파일을 찾을 수 없습니다: {path}")
            resolved.append(path)
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
