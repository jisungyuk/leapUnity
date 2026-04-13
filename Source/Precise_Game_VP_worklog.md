# Precise_Game_VP Worklog

This file summarizes the final working updates made to `Precise_Game_VP.py`.

## Core Task Fixes

- Audio playback was aligned to the same monitor/audio-device handling used in `Precise_Game_V7.py`, so the go cue sound plays on the intended display output.
- MVC calibration was changed so the three MVC slot buttons respond directly to click and start recording immediately.
- Direct MVC max/min entry boxes were added to the main UI to save setup time.
- Main trial logic was corrected so a trial must detect the first real hit before it can be classified as a miss.
- The ball/brick interaction was adjusted so lightly activating the bar no longer ends the trial before the ball is actually launched.

## Stimulation / Triggering

- GO cue TTL remains active in both practice and stim sessions for reaction-time measurement.
- Fast Response Output is kept off for non-stim modes and prepared before sampling for stim mode.
- Stim-session triggering was stabilized so:
  - Output 1 is restored as the immediate go-aligned output.
  - Output 2 is used for the delayed trigger based on the planned RT percentage.
- The FRO cold-start issue was addressed using the recorded `OffOn.vbs` transition, while restoring the known-good template state afterward so Output 1 does not drift to Output 2 timing.
- Practice mode is configured so FRO is off, but GO TTL still fires.
- Stim mode is configured so FRO is checked before sampling starts, and GO TTL still fires.
- The COM3 trigger pulse width was increased to improve detectability for LabChart/FRO triggering.

## Stim Plan / Session Management

- The 120-trial stimulation plan remains block-based, with 20-trial stim blocks.
- Stimulation plan Excel files are saved with unique timestamp/session-number names to avoid file-lock collisions.
- Stim-session trial counting/progress handling was updated to track completed trials more clearly.

## UI / Layout

- The main menu layout was reorganized so controls fit on screen more reliably.
- Stim controls were moved upward so the second-trigger controls are visible.
- Free play was removed from the active task menu and main flow because it is no longer needed.
- Practice and stim in-game HUD text was reorganized to reduce overlap and keep status text inside the window bounds.

## Target Layout / Scoring Rules

- Target spacing was adjusted:
  - `FAR` was moved down from the top.
  - `NEAR` was also moved down so `NO TARGET`, `FAR`, and `NEAR` are more evenly distributed.
- For `NO TARGET`, a valid upward launch still counts as success.
- For `NEAR` and `FAR`, success feedback now requires the ball to land inside the target circle range before the target turns green.

## Current Intended Final Behavior

- Practice mode:
  - GO TTL on
  - FRO off
  - scoring enabled
- Stim session:
  - GO TTL on
  - FRO checked/prepared before sampling
  - Output 1 immediate, Output 2 delayed
  - 20-trial block display with non-overlapping HUD
- MVC:
  - direct slot-click recording
  - direct manual max/min entry available in the main UI

## Main Files Involved

- `Precise_Game_VP.py`
- `G:\Shared drives\Cunningham Lab\Studies\LabChart Settings Files\Supplimantary Folder\First.vbs`
- `G:\Shared drives\Cunningham Lab\Studies\LabChart Settings Files\Supplimantary Folder\Second.vbs`
- `G:\Shared drives\Cunningham Lab\Studies\LabChart Settings Files\Supplimantary Folder\Thrid.vbs`
- `G:\Shared drives\Cunningham Lab\Studies\LabChart Settings Files\Supplimantary Folder\OffOn.vbs`

