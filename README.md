How to run the project:
- Beatmap setup (minimal)
  1. Go to `Scene` **BeatmapRecorder** within the project
  2. Choose `GameObject` Recorder
     - Change **SongID** and **FileName** to correct one
     - Start recording with spacebar using ASDF and enter to save
- Scene setup (minimal)
  1. Create an empty `GameObject` **GameManager** and add **GameManager** component.
  2. Add a child `GameObject` **Conductor** with an **AudioSource** and **SongConductor** component.
     - Uncheck `playOnAwake`.
     - Assign your audio clip into the **AudioSource** or leave it to be set by beatmap.
  3. Create a **SimplePool** object:
     - Assign `prefab` to your **Note** prefab (with a **SpriteRenderer** and **NoteObject** component).
  4. Create **hitLine** and **bottomLine** empty objects at suitable **Y** world positions.
  5. Create **Lane** objects (one per lane) and add **LaneController** to each:
     - Add an empty child as **laneAnchor** positioned at each lane’s X.
     - Add all **LaneController** references to `GameManager.lanes` in order (0..N-1).
  6. Create a Canvas/UI with TMP texts for `scoreTMP`, `comboTMP`, `accuracyTMP`, `judgementTMP`.
  7. Optional: Add **ParticleSystems** for judgement/combo/click/hold and assign them in **GameManager** (VFX section).

---

Design choices:
- **Single Game Manager**: A central **GameManager** orchestrates spawn, input routing, scoring, UI, and VFX to keep logic discoverable.
- **DSP-timed playback**: **SongConductor** uses `PlayScheduled` + DSP clock to provide a stable `SongTime` for judging and spawning, minimizing drift.
- **Object Pooling**: **SimplePool** avoids runtime allocations. Returned notes fade (and scale for normal tiles) before being pooled to reduce pop-in.
- **Long Notes via Stretch**: **NoteObject** stretches a single sprite vertically for long notes (no separate head/body prefabs), simplifying art and pooling.
- **Lightweight UI Feedback**: Small scale “pop” animations for score/combo/accuracy and a quick judgement fade keep feedback punchy without heavy animation assets.
- **Background Pulse**: Optional **BackgroundBeatAnimator** scales a target on beat/subdivisions; time source can be conductor or AudioSource.

--- 

External assets and resources:
- **Song01** and **Song02** from **royalty free song**
- **Hyper casual FX** from **lana studio**
- **Buttons** from **Unity asset store**
