---
version: alpha
name: TCT English
description: Existing employee learning interface for Razor and Bootstrap work.
colors:
  primary: "#4255ff"
  background: "#f6f7fb"
  surface: "#ffffff"
  text: "#303545"
  border: "#e5e7eb"
typography:
  body:
    fontFamily: "Inter, Arial, sans-serif"
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.5
rounded:
  sm: 8px
  md: 12px
  pill: 999px
spacing:
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
components:
  navigation:
    accent: "{colors.primary}"
    surface: "{colors.surface}"
  search:
    background: "{colors.background}"
    radius: "{rounded.pill}"
---

## Overview

TCT English helps employees study vocabulary, practise speaking and track progress.
Product screens make the next learning action easy to find and give clear feedback.
Study, dashboard and admin screens prioritize task clarity. Marketing pages may
use more expressive composition when requested.

This documents the existing identity, not a completed UI redesign. Colors, font
family and radii come from TCTEnglish/wwwroot/css/layout.css; spacing is a
recommended consolidation of Bootstrap spacing. Page-specific styles remain
evidence where a shared token is not implemented. These tokens do not create
CSS variables or change the application by themselves.

## Colors

Use blue for primary actions, active navigation and meaningful emphasis, with
white content surfaces on the pale background. Dark text is for readable content;
the border color is for separation, not body text. Use existing Bootstrap semantic
colors for success, warning and errors with a text/icon cue as well as color.
Verify actual rendered contrast: normal text at least 4.5:1, large text 3:1.
Preserve visible focus indicators.

## Typography

The shared layout loads Inter, falling back to Arial/sans-serif. Keep Vietnamese
diacritics, phonetics and mixed-length learning content readable. Do not add a
display font solely to satisfy an imported skill's stylistic preference.

The token above describes desktop body text. site.css currently sets the root to
14px below 768px and 16px above. Improving mobile readability is a scoped future
UI change needing browser verification, not an already-applied fix. Use a clear
heading hierarchy and avoid truncating essential learning content.

## Layout

Use Razor feature views/shared partials and Bootstrap 5.3 responsive utilities.
The shell has a fixed header and collapsible desktop sidebar (280px expanded).
Account for offsets and mobile behavior. Avoid new framework/build pipelines
for ordinary components. Check around 375px/1440px plus actual breakpoints,
text zoom, long labels and focus visibility. Preserve Tag Helpers, actions,
antiforgery tokens and validation.

## Elevation & Depth

Existing navigation uses subtle offset shadows. Use restrained elevation for
menus/overlays. Prefer spacing, alignment and type over nested cards and effects.

## Shapes

Controls commonly use 8px/12px corners; search is pill shaped. Reuse existing
component shapes. Circles suit avatars and meaningful compact icon controls.

## Spacing

Prefer Bootstrap utilities for ordinary layout and scoped CSS for product detail.
Keep related content close and separate learning sections with larger gaps.
Avoid global resets and inline layout styles in feature views.

## Components

- Navigation: preserve active meaning, collapse behavior and keyboard access.
- Forms: visible labels, inline errors, helper text and submission states.
- Study: quick feedback, duplicate-submit protection and authoritative progress.
  Preserve keyboard and media controls.
- Modals: Bootstrap focus management, Escape and focus return.
- Feedback: loading, success, empty and recoverable errors; never raw exceptions.
- Icons: the layout loads Font Awesome and Material Symbols. Match the existing
  page's family; Bootstrap Icons are not guaranteed globally.
- Motion: explain state changes, keep frequent actions quick, support reduced
  motion and use CSS/browser-native APIs before adding dependencies.

## Do's and Don'ts

Preserve identity, encoded text, typed ViewModels and feature boundaries. Do not
copy another brand as TCT's identity, replace Razor with React/Tailwind, invent
statistics or remove useful study states merely to simplify a screenshot.

Format: [Google DESIGN.md](https://github.com/google-labs-code/design.md).
Reviewed spec/license: .ai/vendor/google-design-md/. Pins: .ai/tooling/sources.lock.json.
