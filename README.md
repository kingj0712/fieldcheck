# Tidepool

> A small living world. Plankton blooms, fish school and evolve, hunters stalk —
> and you can feed the pool, stir the water, or just watch the populations rise and crash.
> One HTML file, no dependencies, no build step, no purpose beyond being alive.

## Run it

Open `index.html` in any modern browser. That's the whole install.

## What's happening in there

It's a three-tier food web that runs itself:

- **Plankton** blooms on its own — faster when the pool is sparse, slower when it's full. It's
  scarce enough that a dense population genuinely competes for it.
- **Grazer fish** school together, graze plankton, and burn energy as they swim. Well-fed fish
  that find a compatible mate nearby breed; starving fish fade.
- **Predatory fish** are a carnivorous tier that hunts smaller fish and fry. A big "gape" lets a
  fish eat others, but trades against grazing efficiency — so predators boom when prey is thick
  and starve when it thins. Some are present from the start, the tide brings more, and any lineage
  can drift toward hunting on its own.
- **Hunters** are the apex tier: larger, faster, always carnivorous. They chase the nearest fish
  they can sense and lunge when close.
- **Nutrient cycle**: every death — grazed, hunted, or starved — sinks as detritus that decays
  back into plankton, closing the loop. Booms and crashes, no ending.
- A slow **day/night cycle** (~3 minutes) shifts the light; sun shafts appear near midday.

### Evolution & speciation

Every fish carries **seven heritable traits**: speed, size, sense radius, colour, **gape**
(predatoriness), **boldness** (how long it grazes before fleeing), and **schooling** drive.
Offspring recombine both parents' traits with small mutations, and:

- **Sexual reproduction is assortative** — fish only breed with genetically *similar* partners.
  Lineages that drift apart stop interbreeding and become distinct **species**, visible as
  clusters in the HUD's colour spectrum. Juveniles start small and slow, growing to full size
  over a few seconds (which makes fry catchable prey).
- **Colour is functional** — a hue close to the water camouflages a fish from predators, so
  camouflage is selected for against the pull of each species' own colour.
- **Selection is real and observable** — under heavy predation the population evolves *faster*
  (higher average speed) to escape; when plankton is scarce, body size and diet shift. The HUD's
  live trait bars let you watch the whole population's averages slide as it adapts.

## Interacting

| Tool | Key | What it does |
|---|---|---|
| Feed | `1` | Sprinkle plankton where you click or drag |
| Stir | `2` | Push the water — everything nearby scatters |
| Fish | `3` | Release a fresh random fish (a possible new lineage) |
| Hunter | `4` | Release an apex hunter (use sparingly) |

`Space` pauses. `H` or `?` opens the in-app explainer. Works with touch, too.

The HUD tracks each population over time (teal = grazers, orange = predatory fish, red = apex
hunters, faint green = plankton), a **colour spectrum** showing species clusters, and live
**population-average trait bars** you can watch shift as the pool evolves.

## How it's built

Vanilla JavaScript on a single 2D `<canvas>`, ~60 fps with a few hundred agents:

- A **spatial hash grid** keeps neighbor lookups O(1)-ish so flocking, mating, and predation
  searches all stay cheap even with hundreds of fish.
- Plankton glow is a pre-rendered radial-gradient sprite (no per-frame `shadowBlur`).
- Fish are drawn as quadratic-curve teardrops with a phase-driven tail wag; predatory fish
  elongate and grow a jaw as their gape rises, so you can read diet from shape.
- Honors `prefers-reduced-motion` by dropping the decorative bubbles and light shafts.

No frameworks, no assets, no network calls. Everything lives in `index.html`.

## License

MIT © 2026 Jacob King
