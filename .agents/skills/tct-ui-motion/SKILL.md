---
name: tct-ui-motion
description: Use when tuning CSS or browser-native motion in TCT English, including flashcard flips, feedback, transitions, reduced motion or animation jank.
---

# Motion for learning tasks

Use the same agent and incumbent UI identity in
[DESIGN.md](../../../DESIGN.md). Start from the learner's action: what change
should motion explain? Frequent study actions need brief feedback and continuity.

- Prefer existing CSS transitions/keyframes, Bootstrap behavior or browser-native
  APIs. Do not introduce Framer Motion, React or GSAP for a motion task.
- Prefer transform and opacity where they express the effect; measure layout and
  paint work before optimizing. Do not apply will-change to every element.
- Keep controls interactive, avoid timer-based correctness, handle interrupted
  transitions and clean up timers/listeners when a widget is disposed.
- Respect prefers-reduced-motion. Preserve information and final state when
  motion is shortened or removed; card content must remain accessible.
- Check keyboard focus, repeated quick actions, mobile performance and reduced
  motion in the rendered page. Trace performance for a reported jank issue.

Read the relevant parts of Emil Kowalski's
[animation guidance](../../../.ai/vendor/emil/animate.md),
[frame budget](../../../.ai/vendor/emil/animation-performance.md) or
[design engineering](../../../.ai/vendor/emil/emil-design-eng.md).
Their React/native recipes and initial-response scripts are outside this adapter;
translate transferable browser principles into the actual Razor/CSS/JS code.
