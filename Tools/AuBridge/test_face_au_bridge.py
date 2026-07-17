#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = [
#     "pydantic>=2.12,<3",
#     "pytest>=8.4,<9",
#     "rich>=14.3,<15",
#     "typer>=0.21,<0.22",
# ]
# ///

# --- How to run ---
# 1. Install uv: https://docs.astral.sh/uv/getting-started/installation/
# 2. Run: uv run Tools/AuBridge/test_face_au_bridge.py
# ------------------

from __future__ import annotations

import pytest

from face_au_bridge import (
    AuValues,
    CalibrationState,
    map_blendshapes,
    strict_video_timestamp_ms,
    update_neutral_calibration,
)


def test_map_blendshapes_returns_zero_when_face_is_neutral() -> None:
    # Given
    scores: dict[str, float] = {}

    # When
    result = map_blendshapes(scores)

    # Then
    assert result.model_dump(exclude={"source"}) == {
        "au01": 0.0,
        "au02": 0.0,
        "au04": 0.0,
        "au06": 0.0,
        "au07": 0.0,
        "au12": 0.0,
        "au14": 0.0,
        "au15": 0.0,
        "au17": 0.0,
        "au23": 0.0,
        "au25": 0.0,
        "au26": 0.0,
        "au45": 0.0,
    }


def test_map_blendshapes_averages_bilateral_signals() -> None:
    # Given
    scores = {
        "browOuterUpLeft": 0.2,
        "browOuterUpRight": 0.6,
        "mouthSmileLeft": 0.3,
        "mouthSmileRight": 0.7,
        "eyeBlinkLeft": 0.4,
        "eyeBlinkRight": 0.8,
    }

    # When
    result = map_blendshapes(scores)

    # Then
    assert result.au02 == pytest.approx(0.4)
    assert result.au12 == pytest.approx(0.5)
    assert result.au45 == pytest.approx(0.6)


def test_map_blendshapes_labels_values_as_proxy_not_facs_ground_truth() -> None:
    # Given
    scores = {"jawOpen": 0.8}

    # When
    result = map_blendshapes(scores)

    # Then
    assert result.source == "mediapipe-blendshape-proxy"
    assert result.au25 == pytest.approx(0.52)
    assert result.au26 == pytest.approx(0.8)


def test_neutral_calibration_reports_progress_before_ten_seconds() -> None:
    # Given
    state = CalibrationState()
    values = AuValues(au04=0.2)

    # When
    result = update_neutral_calibration(state, values, timestamp_ms=5_000)

    # Then
    assert result.calibrating is True
    assert result.progress == pytest.approx(0.0)
    assert result.values.au04 == pytest.approx(0.2)


def test_neutral_calibration_removes_personal_baseline_after_ten_seconds() -> None:
    # Given
    first = update_neutral_calibration(
        CalibrationState(),
        AuValues(au04=0.2),
        timestamp_ms=0,
    )

    # When
    completed = update_neutral_calibration(
        first.state,
        AuValues(au04=0.2),
        timestamp_ms=10_001,
    )
    changed = update_neutral_calibration(
        completed.state,
        AuValues(au04=0.7),
        timestamp_ms=10_101,
    )

    # Then
    assert completed.calibrated is True
    assert completed.values.au04 == pytest.approx(0.0)
    assert changed.values.au04 == pytest.approx(0.5)


def test_video_timestamp_increases_when_frames_share_a_millisecond() -> None:
    # Given
    previous_timestamp_ms = 42

    # When
    timestamp_ms = strict_video_timestamp_ms(previous_timestamp_ms, now_ns=42_900_000)

    # Then
    assert timestamp_ms == 43


if __name__ == "__main__":
    raise SystemExit(pytest.main([__file__]))
