# Tidepool

> A small living world. Plankton blooms, fish school and evolve, hunters stalk —
> and you can feed the pool, stir the water, or just watch the populations rise and crash.
> One HTML file, no dependencies, no build step, no purpose beyond being alive.

## Run it

Open `index.html` in any modern browser. That's the whole install.

## What's happening in there

- **Plankton** blooms on its own — faster when the pool is empty, slower when it's full.
- **Fish** flock (separation / alignment / cohesion), graze on plankton, and burn energy as
  they swim. Eat enough and they spawn young; starve and they fade away.
- **Evolution**: every fish carries heritable traits — speed, size, sense radius, and color —
  which mutate slightly in each offspring. Lean times favor small, efficient fish; heavy
  predation favors the quick. Watch the color of the pool drift over generations.
- **Hunters** chase the nearest fish they can sense, lunging when they get close. They starve
  without a catch, and a fallen hunter returns to the pool as plankton.
- **The tide** never lets the world die for good: if fish or hunters vanish entirely, new ones
  quietly drift in after a while. No ending, just cycles.
- A slow **day/night cycle** (~3 minutes) shifts the light; sun shafts appear near midday.

## Interacting

| Tool | Key | What it does |
|---|---|---|
| Feed | `1` | Sprinkle plankton where you click or drag |
| Stir | `2` | Push the water — everything nearby scatters |
| Fish | `3` | Release a fish at the cursor |
| Hunter | `4` | Release a hunter (use sparingly) |

`Space` pauses. `H` or `?` opens the in-app explainer. Works with touch, too.

The HUD tracks each population, with a sparkline of the last few minutes
(each series scaled to its own range — teal is fish, red is hunters, faint green is plankton).

## How it's built

Vanilla JavaScript on a single 2D `<canvas>`, ~60 fps with a few hundred agents:

- A **spatial hash grid** keeps neighbor lookups O(1)-ish so flocking stays cheap.
- Plankton glow is a pre-rendered radial-gradient sprite (no per-frame `shadowBlur`).
- Fish are drawn as quadratic-curve teardrops with a phase-driven tail wag; heading follows velocity.
- Honors `prefers-reduced-motion` by dropping the decorative bubbles and light shafts.

No frameworks, no assets, no network calls. Everything lives in `index.html`.

## License

MIT © 2026 Jacob King
