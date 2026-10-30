"""
Extra sounds added later (market stands and the cigarette). Kept in a separate
script so re-running it doesn't change any of the sounds from make_sounds.py.
Uses the same building blocks (noise, filters, envelopes, room reverb).
"""
import numpy as np
import make_sounds as ms
from make_sounds import noise, env, lowpass, highpass, bandpass, metal, mix, room, saturate, t, n, save, SR

rng = np.random.default_rng(42)


def break_fruit(v):
    """Fruit stand smashed: wooden crack, then wet squelches and thuds of fruit."""
    parts = [(lowpass(noise(0.12), 400) * env(0.12, 0.001, 0.04) * 1.6, 0)]
    for i in range(5):
        f0 = rng.uniform(140, 260)
        x = t(0.09)
        squelch = np.sin(2 * np.pi * (f0 - 900 * x) * x) * env(0.09, 0.002, 0.03)          # pitch-dropping "plop"
        wet = bandpass(noise(0.09), rng.uniform(700, 1500), 2) * env(0.09, 0.001, 0.02) * 0.6
        parts.append((squelch + wet, 0.03 + i * rng.uniform(0.04, 0.08)))
    return room(mix(*parts), 0.3, 0.2)


def swords_fall(v):
    """Swords knocked off a rack: three bright metal clangs and a scrape."""
    parts = []
    for i in range(3):
        base = rng.uniform(900, 1500)
        parts.append((metal(0.5, [base, base * 2.76, base * 5.4, base * 8.9], 0.12) * 0.5, i * rng.uniform(0.07, 0.12)))
    parts.append((bandpass(noise(0.3), 3000, 3) * env(0.3, 0.02, 0.1) * 0.2, 0.25))      # skid
    return room(mix(*parts), 0.35, 0.3)


def potion_poof(v):
    """Potions bursting into a cloud: glass pop then a soft rising whoosh."""
    x = t(0.6)
    swell = lowpass(noise(0.6), 600 + 2400 * x / 0.6) * np.sin(np.pi * np.clip(x / 0.6, 0, 1)) * 0.8
    pop = highpass(noise(0.02), 3000) * env(0.02, 0.0003, 0.006) * 1.2
    sparkle = [(np.sin(2 * np.pi * rng.uniform(3000, 6000) * t(0.2)) * env(0.2, 0.001, 0.05) * 0.2, rng.uniform(0.02, 0.3))
               for _ in range(6)]
    return room(mix((pop, 0), (swell, 0.01), *sparkle), 0.4, 0.3)


def cig_light():
    """Zippo-style lighter: lid click, flint scratch, flame catching."""
    click = metal(0.08, [2400, 5100], 0.01) * 0.6
    flint = highpass(noise(0.05), 2000) * env(0.05, 0.001, 0.015) * 0.7
    x = t(0.5)
    flame = lowpass(noise(0.5), 900) * np.clip(x / 0.05, 0, 1) * np.exp(-x / 0.25) * 0.5
    inhale = lowpass(noise(0.45), 1400) * np.sin(np.pi * np.clip(t(0.45) / 0.45, 0, 1)) * 0.25
    return room(mix((click, 0), (flint, 0.12), (flame, 0.14), (inhale, 0.4)), 0.25, 0.15)


def cig_exhale():
    """Long, relaxed breath out."""
    x = t(1.0)
    shape = np.clip(x / 0.08, 0, 1) * np.exp(-x / 0.45)
    return lowpass(noise(1.0), 1100 - 500 * x) * shape


def door_burst():
    """The great door blown open: a deep boom, a big wooden crack, then planks clattering down."""
    x = t(0.6)
    boom = np.sin(2 * np.pi * (70 - 30 * x) * x) * np.exp(-x / 0.18) * 1.2
    crack = lowpass(noise(0.25), 1800) * env(0.25, 0.001, 0.06) * 1.4
    parts = [(boom, 0), (crack, 0)]
    for i in range(6):
        parts.append((bandpass(noise(0.06), rng.uniform(250, 700), 3) * env(0.06, 0.001, 0.02), 0.18 + i * rng.uniform(0.05, 0.1)))
    return room(saturate(mix(*parts), 1.5), 0.5, 0.35)


def coins_spill():
    """A chest of gold coins bursting: dozens of small bright metallic tinks."""
    parts = [(lowpass(noise(0.1), 500) * env(0.1, 0.001, 0.03), 0)]
    for _ in range(28):
        f = rng.uniform(3500, 7000)
        parts.append((metal(0.12, [f, f * 1.48], 0.025) * rng.uniform(0.15, 0.4), rng.uniform(0, 0.55)))
    return room(mix(*parts), 0.3, 0.25)


def slowmo_in():
    """Dropping into bullet time: a deep reversed whoosh sliding down in pitch, ending in a low thud."""
    x = t(0.9)
    swell = lowpass(noise(0.9), 3000 * np.exp(-x * 3) + 150) * np.clip(x / 0.6, 0, 1) ** 2 * 0.8
    drop = np.sin(2 * np.pi * np.cumsum(220 * np.exp(-x * 2.5) + 40) / SR) * np.clip(x / 0.3, 0, 1) * np.exp(-np.clip(x - 0.6, 0, None) / 0.2) * 0.6
    thud = np.sin(2 * np.pi * 45 * t(0.4)) * env(0.4, 0.002, 0.12)
    return room(mix((swell + drop, 0), (thud, 0.75)), 0.6, 0.35)


def slowmo_out():
    """Snapping back to normal speed: a quick rising whoosh."""
    x = t(0.45)
    rise = lowpass(noise(0.45), 200 + 4000 * (x / 0.45) ** 2) * np.sin(np.pi * np.clip(x / 0.45, 0, 1)) * 0.8
    return room(rise, 0.4, 0.25)


def heartbeat():
    """Two muffled low thumps: lub-dub."""
    def thump(f, amp):
        return lowpass(np.sin(2 * np.pi * f * t(0.18)) * env(0.18, 0.004, 0.05), 200) * amp
    return mix((thump(55, 1.0), 0), (thump(48, 0.7), 0.16))


def main():
    for v in range(2):
        save(f"break_fruit_{v + 1}", break_fruit(v), 0.8)
        save(f"swords_fall_{v + 1}", swords_fall(v), 0.7)
        save(f"potion_poof_{v + 1}", potion_poof(v), 0.75)
    save("cig_light", cig_light(), 0.7)
    save("door_burst", door_burst(), 0.95)
    save("coins_spill", coins_spill(), 0.7)
    save("slowmo_in", slowmo_in(), 0.9)
    save("slowmo_out", slowmo_out(), 0.7)
    save("heartbeat", heartbeat(), 0.85)
    save("cig_exhale", cig_exhale(), 0.55)
    print("wrote extra sounds")


if __name__ == "__main__":
    main()
