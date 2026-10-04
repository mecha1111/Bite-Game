"""Deterministic active successful-catch SFX. No licensed sample dependencies."""
import math, random, struct, wave
from pathlib import Path
RATE = 48000
OUT = Path(__file__).resolve().parents[1] / 'assets/audio'
SOUNDS = {
    'bite_snap': (370, .055, .45, 'snap'),
    'catch_gulp': (240, .14, .32, 'gulp'),
    'perfect_thump': (98, .105, .42, 'pressure'),
}
OUT.mkdir(parents=True, exist_ok=True)
for name, (freq, duration, amplitude, kind) in SOUNDS.items():
    rng = random.Random(name)
    samples, phase, filtered = [], 0.0, 0.0
    for i in range(round(duration * RATE)):
        t, p = i / RATE, i / (duration * RATE)
        attack = min(1, t / .0015)
        envelope = attack * (1-p)**(3 if kind in ('snap', 'whiff') else 2.5)
        sweep = freq * (1 - (.30 if kind == 'gulp' else .045) * p)
        phase += 2 * math.pi * sweep / RATE
        noise = rng.uniform(-1, 1)
        filtered += .20 * (noise - filtered)
        if kind == 'ping':
            value = .82 * math.sin(phase) + .18 * math.sin(phase * 2.013)
        elif kind == 'pressure':
            value = .68 * math.sin(phase) + .24 * math.sin(phase * 2) + .08 * filtered
        elif kind == 'snap':
            value = .50 * noise + .30 * filtered + .20 * math.sin(phase)
        elif kind == 'whiff':
            value = .6 * filtered + .25 * noise + .15 * math.sin(phase)
        else:
            value = .55 * math.sin(phase) + .25 * math.sin(phase * 1.5) + .2 * filtered
        samples.append(round(max(-1, min(1, amplitude * envelope * value)) * 32767))
    with wave.open(str(OUT / (name + '.wav')), 'wb') as f:
        f.setparams((1, 2, RATE, len(samples), 'NONE', 'not compressed'))
        f.writeframes(struct.pack('<' + 'h'*len(samples), *samples))
    print(name, len(samples), 'samples')
