# TCT English brand kit

Master identity assets for **TCT English**. Runtime web copies live only under
`TCTEnglish/wwwroot/brand/`.

## Layout

| Path | Role |
| --- | --- |
| `logo/` | Logo master: references, vector, raster, guidelines |
| `mascot/` | Mascot **Tico** master: references, vector, raster, guidelines |
| `VERSION.md` | Kit version and known gaps |

## Naming

- Logo: `tct-english-logo-{lockup}-{variant}[-{size}].{ext}`
- Mascot: `tico-mascot-{pose}-{variant}[-{size}].{ext}`
- Lockups: `horizontal` | `vertical` | `icon` | `wordmark`
- Logo variants present on the reference board: `color` | `black` | `white`
- Mascot poses on the reference board: `welcome` | `success` | `retry` | `thinking` | `hero`

## Rules

1. Edit masters under `brand/` only. The app must reference `wwwroot/brand/`.
2. Logo and mascot stay separate. Site favicon uses the **logo icon**, not Tico’s head.
3. Do not invent variants missing from the reference boards (for example grayscale or Office JPG) until a designer supplies them.
4. Do not treat slogan copy as approved here (see `VERSION.md` backlog).
5. Do not fabricate `.AI` / `.EPS` binaries. SVG in `01-vector/` is the Git vector master; export `.AI` from Illustrator when available (see each `00-source/README.md`).
6. v0.1 SVG files embed the exact PNG crop from the reference boards (no redrawn paths). Replace with path-based SVG only when a designer supplies true vectors without changing the artwork.

## Not on the reference boards

- Grayscale logo lockups
- JPG-on-white Office packs
- Official Pantone / CMYK print deck (only HEX sampled from boards + note vs DESIGN.md)
