#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = [
#     "mediapipe>=0.10.31,<0.11",
#     "opencv-python>=4.13,<5",
#     "pydantic>=2.12,<3",
#     "rich>=14.3,<15",
#     "typer>=0.21,<0.22",
# ]
# ///

# --- How to run ---
# 1. Install uv: https://docs.astral.sh/uv/getting-started/installation/
# 2. Run: uv run Tools/AuBridge/face_au_bridge.py
# 3. Then launch Unity with: CounselCue.exe --au
# ------------------

from __future__ import annotations

import socket
import time
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import ClassVar, Final

import typer
from pydantic import BaseModel, ConfigDict, Field
from rich.console import Console

SOURCE: Final = "mediapipe-blendshape-proxy"
DEFAULT_HOST: Final = "127.0.0.1"
DEFAULT_PORT: Final = 18765
CALIBRATION_DURATION_MS: Final = 10_000
MODEL_PATH: Final = Path(__file__).with_name("face_landmarker.task")
console = Console(stderr=True)


@dataclass(frozen=True, slots=True)
class BridgeConfig:
    camera_index: int
    port: int
    preview: bool


class ModelMissingError(FileNotFoundError):
    path: Path

    def __init__(self, path: Path) -> None:
        self.path = path
        super().__init__(str(path))


class CameraOpenError(RuntimeError):
    camera_index: int

    def __init__(self, camera_index: int) -> None:
        self.camera_index = camera_index
        super().__init__(f"camera {camera_index} could not be opened")


class AuValues(BaseModel):
    model_config: ClassVar[ConfigDict] = ConfigDict(frozen=True)

    source: str = SOURCE
    au01: float = Field(default=0.0, ge=0.0, le=1.0)
    au02: float = Field(default=0.0, ge=0.0, le=1.0)
    au04: float = Field(default=0.0, ge=0.0, le=1.0)
    au06: float = Field(default=0.0, ge=0.0, le=1.0)
    au07: float = Field(default=0.0, ge=0.0, le=1.0)
    au12: float = Field(default=0.0, ge=0.0, le=1.0)
    au14: float = Field(default=0.0, ge=0.0, le=1.0)
    au15: float = Field(default=0.0, ge=0.0, le=1.0)
    au17: float = Field(default=0.0, ge=0.0, le=1.0)
    au23: float = Field(default=0.0, ge=0.0, le=1.0)
    au25: float = Field(default=0.0, ge=0.0, le=1.0)
    au26: float = Field(default=0.0, ge=0.0, le=1.0)
    au45: float = Field(default=0.0, ge=0.0, le=1.0)


class AuFrame(AuValues):
    timestamp_ms: int = Field(ge=0)
    tracking: bool
    calibrating: bool
    calibrated: bool
    calibration_progress: float = Field(ge=0.0, le=1.0)


class CalibrationState(BaseModel):
    model_config: ClassVar[ConfigDict] = ConfigDict(frozen=True)

    started_ms: int | None = Field(default=None, ge=0)
    sample_count: int = Field(default=0, ge=0)
    baseline: AuValues = Field(default_factory=AuValues)
    calibrated: bool = False


class CalibrationResult(BaseModel):
    model_config: ClassVar[ConfigDict] = ConfigDict(frozen=True)

    state: CalibrationState
    values: AuValues
    progress: float = Field(ge=0.0, le=1.0)
    calibrating: bool
    calibrated: bool


def _score(scores: Mapping[str, float], name: str) -> float:
    return max(0.0, min(1.0, scores.get(name, 0.0)))


def _mean(scores: Mapping[str, float], left: str, right: str) -> float:
    return (_score(scores, left) + _score(scores, right)) * 0.5


def map_blendshapes(scores: Mapping[str, float]) -> AuValues:
    jaw_open = _score(scores, "jawOpen")
    return AuValues(
        au01=_score(scores, "browInnerUp"),
        au02=_mean(scores, "browOuterUpLeft", "browOuterUpRight"),
        au04=_mean(scores, "browDownLeft", "browDownRight"),
        au06=_mean(scores, "cheekSquintLeft", "cheekSquintRight"),
        au07=_mean(scores, "eyeSquintLeft", "eyeSquintRight"),
        au12=_mean(scores, "mouthSmileLeft", "mouthSmileRight"),
        au14=_mean(scores, "mouthDimpleLeft", "mouthDimpleRight"),
        au15=_mean(scores, "mouthFrownLeft", "mouthFrownRight"),
        au17=_score(scores, "mouthShrugLower"),
        au23=_mean(scores, "mouthPressLeft", "mouthPressRight"),
        au25=jaw_open * 0.65,
        au26=jaw_open,
        au45=_mean(scores, "eyeBlinkLeft", "eyeBlinkRight"),
    )


def _blend_values(current: AuValues, sample: AuValues, weight: float) -> AuValues:
    inverse = 1.0 - weight
    return AuValues(
        au01=current.au01 * inverse + sample.au01 * weight,
        au02=current.au02 * inverse + sample.au02 * weight,
        au04=current.au04 * inverse + sample.au04 * weight,
        au06=current.au06 * inverse + sample.au06 * weight,
        au07=current.au07 * inverse + sample.au07 * weight,
        au12=current.au12 * inverse + sample.au12 * weight,
        au14=current.au14 * inverse + sample.au14 * weight,
        au15=current.au15 * inverse + sample.au15 * weight,
        au17=current.au17 * inverse + sample.au17 * weight,
        au23=current.au23 * inverse + sample.au23 * weight,
        au25=current.au25 * inverse + sample.au25 * weight,
        au26=current.au26 * inverse + sample.au26 * weight,
        au45=current.au45 * inverse + sample.au45 * weight,
    )


def _subtract_baseline(values: AuValues, baseline: AuValues) -> AuValues:
    return AuValues(
        au01=max(0.0, values.au01 - baseline.au01),
        au02=max(0.0, values.au02 - baseline.au02),
        au04=max(0.0, values.au04 - baseline.au04),
        au06=max(0.0, values.au06 - baseline.au06),
        au07=max(0.0, values.au07 - baseline.au07),
        au12=max(0.0, values.au12 - baseline.au12),
        au14=max(0.0, values.au14 - baseline.au14),
        au15=max(0.0, values.au15 - baseline.au15),
        au17=max(0.0, values.au17 - baseline.au17),
        au23=max(0.0, values.au23 - baseline.au23),
        au25=max(0.0, values.au25 - baseline.au25),
        au26=max(0.0, values.au26 - baseline.au26),
        au45=max(0.0, values.au45 - baseline.au45),
    )


def strict_video_timestamp_ms(previous_timestamp_ms: int, now_ns: int) -> int:
    return max(now_ns // 1_000_000, previous_timestamp_ms + 1)


def update_neutral_calibration(
    state: CalibrationState,
    values: AuValues,
    timestamp_ms: int,
) -> CalibrationResult:
    if state.calibrated:
        return CalibrationResult(
            state=state,
            values=_subtract_baseline(values, state.baseline),
            progress=1.0,
            calibrating=False,
            calibrated=True,
        )

    started_ms = timestamp_ms if state.started_ms is None else state.started_ms
    sample_count = state.sample_count + 1
    baseline = _blend_values(state.baseline, values, 1.0 / sample_count)
    elapsed_ms = max(0, timestamp_ms - started_ms)
    progress = min(1.0, elapsed_ms / CALIBRATION_DURATION_MS)
    calibrated = progress >= 1.0
    next_state = CalibrationState(
        started_ms=started_ms,
        sample_count=sample_count,
        baseline=baseline,
        calibrated=calibrated,
    )
    return CalibrationResult(
        state=next_state,
        values=_subtract_baseline(values, baseline) if calibrated else values,
        progress=progress,
        calibrating=not calibrated,
        calibrated=calibrated,
    )


def run_bridge(config: BridgeConfig) -> None:
    import cv2
    import mediapipe as mp

    if not MODEL_PATH.is_file():
        raise ModelMissingError(MODEL_PATH)

    capture = cv2.VideoCapture(config.camera_index, cv2.CAP_DSHOW)
    if not capture.isOpened():
        raise CameraOpenError(config.camera_index)

    options = mp.tasks.vision.FaceLandmarkerOptions(
        base_options=mp.tasks.BaseOptions(model_asset_path=str(MODEL_PATH)),
        running_mode=mp.tasks.vision.RunningMode.VIDEO,
        num_faces=1,
        min_face_detection_confidence=0.5,
        min_face_presence_confidence=0.5,
        min_tracking_confidence=0.5,
        output_face_blendshapes=True,
    )
    console.print(f"[green]AU bridge listening for Unity at udp://{DEFAULT_HOST}:{config.port}[/green]")
    calibration_state = CalibrationState()
    previous_timestamp_ms = -1
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sender:
            with mp.tasks.vision.FaceLandmarker.create_from_options(options) as landmarker:
                while True:
                    success, frame = capture.read()
                    if not success:
                        continue
                    timestamp_ms = strict_video_timestamp_ms(
                        previous_timestamp_ms,
                        time.monotonic_ns(),
                    )
                    previous_timestamp_ms = timestamp_ms
                    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                    image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
                    result = landmarker.detect_for_video(image, timestamp_ms)
                    tracking = len(result.face_blendshapes) > 0
                    values = AuValues()
                    calibration_progress = 0.0
                    calibrating = not calibration_state.calibrated
                    if tracking:
                        scores = {
                            category.category_name: float(category.score)
                            for category in result.face_blendshapes[0]
                        }
                        calibration = update_neutral_calibration(
                            calibration_state,
                            map_blendshapes(scores),
                            timestamp_ms,
                        )
                        calibration_state = calibration.state
                        values = calibration.values
                        calibration_progress = calibration.progress
                        calibrating = calibration.calibrating
                    payload = AuFrame(
                        source=values.source,
                        au01=values.au01,
                        au02=values.au02,
                        au04=values.au04,
                        au06=values.au06,
                        au07=values.au07,
                        au12=values.au12,
                        au14=values.au14,
                        au15=values.au15,
                        au17=values.au17,
                        au23=values.au23,
                        au25=values.au25,
                        au26=values.au26,
                        au45=values.au45,
                        timestamp_ms=timestamp_ms,
                        tracking=tracking,
                        calibrating=calibrating,
                        calibrated=calibration_state.calibrated,
                        calibration_progress=calibration_progress,
                    )
                    _ = sender.sendto(
                        payload.model_dump_json().encode("utf-8"),
                        (DEFAULT_HOST, config.port),
                    )
                    if config.preview:
                        cv2.imshow("CounselCue AU bridge", frame)
                        if cv2.waitKey(1) & 0xFF == 27:
                            return
    finally:
        capture.release()
        cv2.destroyAllWindows()


def main(
    camera_index: int = 0,
    port: int = DEFAULT_PORT,
    preview: bool = False,
) -> None:
    run_bridge(BridgeConfig(camera_index=camera_index, port=port, preview=preview))


if __name__ == "__main__":
    typer.run(main)
