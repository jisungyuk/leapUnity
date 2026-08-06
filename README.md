# LeapDex

A markerless hand and finger-tracking system built in Unity using Leap Motion (Ultraleap), for delivering precisely movement-timed transcranial magnetic stimulation (TMS) during human motor neuroscience experiments.

## Why

Studying how motor cortical circuits behave *during* movement, rather than at rest, means stimulating (or probing) the brain at a precise moment relative to a subject's ongoing hand movement. Our lab had EMG and force transducers, but no kinematic tracking and no way to synchronize movement timing with a TMS stimulator. This closes that gap.

## What it does

- Tracks hand and finger position in real time with a markerless Leap Motion sensor (no gloves, no markers).
- Runs configurable reaching and reach-to-grasp trial paradigms, including a Real World Reaching (RWR) mode for physical targets and a grasp-interaction (GRIP) mode.
- Detects movement onset/phase in real time and triggers a TMS pulse (Unity → TriggerBox → PowerLab → TMS stimulator) at a configurable delay relative to that movement event.
- Automatically configures paired-pulse TMS timing (testing + conditioning stimulus) in LabChart before each trial via a custom COM-automation bridge, working around Unity/Mono's incomplete native COM support with a VBScript relay.
- Logs full per-trial hand kinematics and stimulus timing to CSV.

## Hardware/software this expects

- Unity 2021.3.45f1
- An Ultraleap/Leap Motion sensor
- A TMS stimulator + Brain Products TriggerBox + ADInstruments PowerLab/LabChart

## Status

Built and maintained solo; actively used in ongoing research. This is a lab research tool tied to specific hardware, not a general-purpose package.
