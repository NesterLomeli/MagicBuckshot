"""
Synthesizes every sound effect in the game (run: python3 Tools/make_sounds.py).

Goal: realistic, heavy weapons in the spirit of Ready or Not, and punchy,
crunchy, slightly gory body sounds in the spirit of Hotline Miami,
made entirely from scratch so they're original and license-free.

Techniques used to push past "beepy" synthesis:
  * Layering — a gunshot is a sharp crack + a filtered noise blast + a
    sub-bass thump + a mechanical click, mixed and saturated together.
  * Saturation (tanh) — soft clipping gives weight and grit.
  * Room reverb — every sound is convolved with a synthetic small-room
    impulse response so it sits in a space instead of floating.
  * Formant filtering — grunts and shouts run a buzzing "vocal cord"
    signal through resonant filters tuned to vowel sounds (uh, eh, ah).
  * Wet impacts — low squelches + bone-crunch grains for flesh hits.
  * Karplus-Strong plucked strings for the banjo music stings.

File names follow the game's convention: "<name>.wav" or "<name>_<n>.wav"
for variations. Replace any file with a real recording of the same name to use it.

Output: Assets/Audio/*.wav (44.1 kHz, 16-bit mono)
"""
import os
import wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Audio")
rng = np.random.default_rng(7)


# ================================================================ building blocks
def n(dur):
    """Number of samples in 'dur' seconds."""
    return int(SR * dur)


def t(dur):
    return np.arange(n(dur)) / SR


def noise(dur):
    return rng.uniform(-1, 1, n(dur))


def env(dur, attack=0.002, decay=0.2):
    """Fast attack, exponential decay."""
    x = t(dur)
    return np.clip(x / max(attack, 1e-6), 0, 1) * np.exp(-x / decay)


def lowpass(sig, cutoff):
    """One-pole low-pass. 'cutoff' may be a number or an array (for sweeps)."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=float), sig.shape)
    alpha = 1 - np.exp(-2 * np.pi * cutoff / SR)
    out = np.empty_like(sig)
    y = 0.0
    for i in range(len(sig)):
        y += alpha[i] * (sig[i] - y)
        out[i] = y
    return out


def highpass(sig, cutoff):
    return sig - lowpass(sig, cutoff)


def bandpass(sig, freq, q=4.0):
    """Resonant band-pass (RBJ biquad). Used for vowel formants and resonances."""
    w0 = 2 * np.pi * freq / SR
    alpha = np.sin(w0) / (2 * q)
    b0, b2 = alpha, -alpha
    a0, a1, a2 = 1 + alpha, -2 * np.cos(w0), 1 - alpha
    b0, b2, a1, a2 = b0 / a0, b2 / a0, a1 / a0, a2 / a0
    out = np.empty_like(sig)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(sig)):
        x0 = sig[i]
        y0 = b0 * x0 + b2 * x2 - a1 * y1 - a2 * y2
        out[i] = y0
        x2, x1, y2, y1 = x1, x0, y1, y0
    return out


def sweep(f0, f1, dur, shape="sine"):
    """Oscillator gliding exponentially from f0 to f1 Hz."""
    x = t(dur)
    f = f0 * (f1 / f0) ** (x / dur)
    phase = 2 * np.pi * np.cumsum(f) / SR
    if shape == "square":
        return np.sign(np.sin(phase))
    if shape == "saw":
        return 2 * ((phase / (2 * np.pi)) % 1) - 1
    return np.sin(phase)


def saturate(sig, drive=2.0):
    """Soft clipping: adds grit and makes things feel louder and heavier."""
    return np.tanh(sig * drive) / np.tanh(drive)


def metal(dur, partials, decay):
    """Inharmonic struck-metal tone (armour, shells, gun parts)."""
    x = t(dur)
    return sum(np.sin(2 * np.pi * f * x) * np.exp(-x / (decay * (1 - i * 0.12))) / (i + 1)
               for i, f in enumerate(partials))


def mix(*parts):
    """Adds signals of different lengths, each given as (signal, start_seconds)."""
    end = max(n(start) + len(sig) for sig, start in parts)
    out = np.zeros(end)
    for sig, start in parts:
        i = n(start)
        out[i:i + len(sig)] += sig
    return out


_ir_cache = {}


def room(sig, size=0.35, wet=0.25, tone=4000):
    """
    Small-room reverb: convolve with a synthetic impulse response (a burst of
    filtered, exponentially decaying noise plus a couple of early reflections).
    """
    key = (size, tone)
    if key not in _ir_cache:
        ir = noise(size) * np.exp(-t(size) / (size / 5))
        ir = lowpass(ir, tone)
        ir[n(0.011)] += 0.5    # early reflections off nearby walls
        ir[n(0.019)] += 0.35
        ir /= np.sqrt(np.sum(ir ** 2))
        _ir_cache[key] = ir
    ir = _ir_cache[key]
    size_out = len(sig) + len(ir) - 1
    nfft = 1 << (size_out - 1).bit_length()
    tail = np.fft.irfft(np.fft.rfft(sig, nfft) * np.fft.rfft(ir, nfft), nfft)[:size_out]
    dry = np.concatenate([sig, np.zeros(len(tail) - len(sig))])
    return dry + wet * tail * (np.max(np.abs(sig)) / (np.max(np.abs(tail)) or 1))


def voice(f0, f1, dur, vowel="uh", rough=0.25, breath=0.15):
    """
    A grunt/shout: a buzzing glottal pulse train (with pitch jitter) shaped by
    three vowel formants, plus breath noise. Sounds far more human than a raw
    oscillator.
    """
    formants = {
        "uh": [(600, 1.0), (1000, 0.6), (2500, 0.25)],
        "ah": [(750, 1.0), (1150, 0.7), (2600, 0.3)],
        "eh": [(530, 1.0), (1850, 0.6), (2500, 0.3)],
        "oh": [(450, 1.0), (800, 0.6), (2600, 0.2)],
    }[vowel]
    x = t(dur)
    pitch = f0 * (f1 / f0) ** (x / dur) * (1 + rough * 0.06 * lowpass(noise(dur), 30) * 8)
    phase = np.cumsum(pitch) / SR
    pulses = (phase % 1 < 0.08).astype(float)              # glottal clicks
    source = lowpass(pulses, 3000) + breath * noise(dur)
    out = sum(bandpass(source, f, 6) * g for f, g in formants)
    shape = np.clip(x / 0.02, 0, 1) * np.clip((dur - x) / 0.06, 0, 1)
    return saturate(out * shape / (np.max(np.abs(out)) or 1), 1.5)


def note(name):
    names = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}
    pitch, octave = name[:-1], int(name[-1])
    return 440 * 2 ** ((names[pitch] + 12 * (octave + 1) - 69) / 12)


def pluck(freq, dur, decay=0.996):
    """Karplus-Strong plucked string — sounds like a banjo when bright and short."""
    total, period = n(dur), int(SR / freq)
    buf = rng.uniform(-1, 1, period)
    out = np.empty(total)
    for i in range(total):
        j = i % period
        out[i] = buf[j]
        buf[j] = decay * 0.5 * (buf[j] + buf[(j + 1) % period])
    return out + 0.5 * highpass(out, 1500)                  # twangy brightness


def save(name, sig, gain=0.9):
    sig = np.asarray(sig, dtype=float)
    sig = highpass(sig, 25)                                  # remove DC / rumble
    sig = sig / (np.max(np.abs(sig)) or 1) * gain
    fade = min(len(sig), n(0.015))                           # click-free ending
    sig[-fade:] *= np.linspace(1, 0, fade)
    data = (np.clip(sig, -1, 1) * 32767).astype(np.int16)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ================================================================ guns
def shotgun_shot(v):
    """Layered blast: crack + boom + sub thump + action rattle, saturated, in a room."""
    crack = highpass(noise(0.015), 2500) * env(0.015, 0.0002, 0.004) * 2.0
    boom = lowpass(noise(0.35), np.linspace(9000, 700, n(0.35))) * env(0.35, 0.0005, 0.07 + 0.01 * v) * 3.0
    body = bandpass(noise(0.2), 180 + 20 * v, 1.5) * env(0.2, 0.001, 0.05) * 2.5
    sub = sweep(95 - 5 * v, 38, 0.3) * env(0.3, 0.001, 0.08) * 0.9
    rattle = metal(0.08, [2300, 3700, 5100], 0.015) * 0.15
    blast = saturate(mix((crack, 0), (boom, 0), (body, 0), (sub, 0), (rattle, 0.004)), 3.0)
    return room(blast, 0.45, 0.35, 3500)


def gun_click(freq=3200):
    """Small mechanical click: tiny noise snap with a metallic ring."""
    snap = highpass(noise(0.004), 3000) * env(0.004, 0.0001, 0.001)
    ring = metal(0.04, [freq, freq * 1.47, freq * 2.3], 0.006) * 0.4
    return mix((snap, 0), (ring, 0))


def shotgun_open():
    """Break-action opening: latch click, hinge creak, barrels dropping."""
    latch = gun_click(2800)
    hinge = bandpass(noise(0.09), 1400, 3) * env(0.09, 0.01, 0.03) * 0.6
    drop = mix((metal(0.12, [900, 1650, 2700], 0.03) * 0.6, 0),
               (lowpass(noise(0.05), 800) * env(0.05, 0.001, 0.015), 0))
    return room(mix((latch, 0), (hinge, 0.015), (drop, 0.07)), 0.25, 0.2)


def shotgun_close():
    """Shells sliding in and the barrels snapping shut — heavy and satisfying."""
    shells = [(gun_click(1900 + 200 * i), 0.03 * i) for i in range(2)]
    snap = mix((highpass(noise(0.01), 1500) * env(0.01, 0.0002, 0.003) * 1.5, 0),
               (metal(0.15, [700, 1300, 2150, 3400], 0.035), 0),
               (sweep(160, 70, 0.08) * env(0.08, 0.001, 0.02), 0))
    return room(saturate(mix(*shells, (snap, 0.11)), 1.8), 0.25, 0.2)


def staff_shot(v):
    """Magic bolt: a punchy downward zap with a crackle and a sparkle on top."""
    zap = lowpass(sweep(1700 + 150 * v, 300, 0.1, "saw"), 5000) * env(0.1, 0.001, 0.03)
    crackle = highpass(noise(0.05), 4000) * env(0.05, 0.0005, 0.01) * 0.5
    thump = sweep(140, 60, 0.07) * env(0.07, 0.001, 0.02)
    return room(saturate(mix((zap, 0), (crackle, 0), (thump, 0)), 1.8), 0.25, 0.15)


def staff_charge():
    """Reload: a rising, pulsing hum of magic building up."""
    dur = 0.7
    hum = lowpass(sweep(150, 700, dur, "saw"), 2500) * (0.6 + 0.4 * np.sin(2 * np.pi * 16 * t(dur)))
    return room(hum * np.linspace(0.2, 1, n(dur)), 0.3, 0.2)


def chime(freqs, dur=0.7, gap=0.06):
    return room(mix(*[(np.sin(2 * np.pi * f * t(dur)) * env(dur, 0.002, 0.25), i * gap)
                      for i, f in enumerate(freqs)]), 0.5, 0.3)


# ================================================================ bodies and gore
def flesh_hit(v, heavy=False):
    """Wet impact: low squelch + bone-crunch grains + slap. The Hotline Miami crunch."""
    squelch = bandpass(noise(0.12), 250 + 60 * v, 2) * env(0.12, 0.001, 0.035) * 3
    squelch += lowpass(noise(0.12), 900) * env(0.12, 0.001, 0.025)
    slap = highpass(noise(0.015), 1800) * env(0.015, 0.0002, 0.004) * 0.8
    parts = [(squelch, 0), (slap, 0)]
    grains = 7 if heavy else 3
    for _ in range(grains):                                   # bone crunch
        g = bandpass(noise(0.012), rng.uniform(1500, 3500), 3) * env(0.012, 0.0002, 0.003)
        parts.append((g * 1.2, rng.uniform(0.0, 0.06 if heavy else 0.03)))
    return saturate(mix(*parts), 2.0)


def body_fall(heavy_armour=False):
    """Body hitting the floor (with armour clatter for knights)."""
    thud = lowpass(noise(0.25), 220) * env(0.25, 0.003, 0.06) * 2.5
    parts = [(thud, 0)]
    if heavy_armour:
        for i in range(5):
            parts.append((metal(0.25, [rng.uniform(350, 700), rng.uniform(1100, 1700), rng.uniform(2200, 3200)], 0.06) * 0.45,
                          0.02 + i * rng.uniform(0.04, 0.08)))
    return mix(*parts)


def footstep(v):
    """Boot on stone: heel thud, toe scuff, tiny room."""
    heel = lowpass(noise(0.06), 450 + 60 * v) * env(0.06, 0.001, 0.014)
    toe = bandpass(noise(0.04), 1800 + 200 * v, 2) * env(0.04, 0.002, 0.008) * 0.5
    return room(mix((heel, 0), (toe, 0.025)), 0.2, 0.18)


def shell_drop(v):
    """Shotgun shell bouncing: two or three plasticky-brass tinks, quieter each time."""
    parts = []
    tt = 0.0
    for i in range(3):
        tink = metal(0.12, [2100 + 150 * v, 3400, 5200], 0.025) * (0.8 ** i)
        parts.append((tink, tt))
        tt += 0.09 - i * 0.025
    return room(mix(*parts), 0.25, 0.2)


def impact(v):
    """Pellet hitting stone: sharp crack, a little debris, ricochet ping."""
    crack = highpass(noise(0.02), 2000) * env(0.02, 0.0003, 0.005)
    debris = [(bandpass(noise(0.01), rng.uniform(2000, 5000), 3) * env(0.01, 0.0002, 0.003) * 0.5,
               rng.uniform(0.01, 0.08)) for _ in range(4)]
    ping = sweep(3200 + 300 * v, 2400, 0.15) * env(0.15, 0.001, 0.04) * 0.15
    return room(mix((crack, 0), (ping, 0.005), *debris), 0.25, 0.25)


# ================================================================ enemies
def knight_windup(v):
    """Armour rattles and a hard 'HUH!' as the knight raises his sword."""
    rattle = mix(*[(metal(0.12, [rng.uniform(500, 900), rng.uniform(1300, 2000), 2900], 0.03) * 0.4, i * 0.035)
                   for i in range(3)])
    shout = voice(125 + 10 * v, 105, 0.25, "uh", rough=0.6)
    return room(mix((rattle, 0), (shout, 0.03)), 0.35, 0.25)


def sword_swing(v):
    """Whoosh: band-passed air that sweeps up in pitch as the blade passes."""
    dur = 0.24
    shape = np.sin(np.pi * t(dur) / dur) ** 3
    air = noise(dur)
    whoosh = lowpass(air, np.linspace(500, 5500 + 500 * v, n(dur))) * shape
    whistle = bandpass(air, 2200 + 300 * v, 8) * shape * 2
    return room(whoosh + whistle, 0.25, 0.2)


def hurt_knight(v):
    clang = metal(0.25, [480 + 40 * v, 1250, 2050, 3100], 0.06) * 0.7
    return room(mix((flesh_hit(v), 0), (clang, 0), (voice(150 + 10 * v, 115, 0.18, "uh", 0.5), 0.02)), 0.3, 0.2)


def hurt_wizard(v):
    return room(mix((flesh_hit(v), 0), (voice(230 + 20 * v, 170, 0.18, "oh", 0.4), 0.015)), 0.3, 0.2)


def death_knight(v):
    return room(mix((flesh_hit(v, heavy=True) * 1.2, 0),
                    (voice(140 - 8 * v, 70, 0.5, "ah", rough=0.7), 0.02),
                    (body_fall(True), 0.18)), 0.4, 0.25)


def death_wizard(v):
    fizzle = lowpass(sweep(1500, 120, 0.45, "square"), 3000) * env(0.45, 0.002, 0.15) * 0.4
    return room(mix((flesh_hit(v, heavy=True), 0),
                    (voice(260 + 15 * v, 120, 0.45, "oh", rough=0.5), 0.02),
                    (fizzle, 0.03), (body_fall(False), 0.2)), 0.4, 0.25)


def death_player():
    """Skeeter goes down: a wet hit and a strangled 'AAGH'."""
    return room(mix((flesh_hit(2, heavy=True) * 1.2, 0),
                    (voice(210, 95, 0.6, "ah", rough=0.6, breath=0.25), 0.02),
                    (body_fall(False), 0.3)), 0.45, 0.3)


def alert(v):
    """A sharp shouted 'HEY!' (pitch jumps up, 'eh' vowel)."""
    return room(voice(190 + 20 * v, 260 + 20 * v, 0.24, "eh", rough=0.5), 0.3, 0.2)


def curious(v):
    """A questioning 'hm?' that rises at the end."""
    return room(voice(140 + 15 * v, 230 + 15 * v, 0.32, "uh", rough=0.2, breath=0.25), 0.3, 0.2)


def wizard_cast(v):
    """Wizard spell: a rising hum that pops into a crackling release."""
    build = lowpass(sweep(250 + 50 * v, 800, 0.22, "saw"), 2500) * np.linspace(0.2, 1, n(0.22))
    release = mix((highpass(noise(0.12), 2500) * env(0.12, 0.001, 0.03), 0),
                  (sweep(900, 300, 0.15) * env(0.15, 0.001, 0.05), 0))
    return room(mix((build * 0.6, 0), (release, 0.2)), 0.35, 0.3)


# ================================================================ props and world
def break_wood(v):
    """Barrel smashed: low crunch, splintering crackles, staves clattering."""
    parts = [(lowpass(noise(0.18), 350) * env(0.18, 0.001, 0.05) * 2, 0)]
    for _ in range(10):
        parts.append((bandpass(noise(0.02), rng.uniform(800, 3500), 2.5) * env(0.02, 0.0005, 0.005),
                      rng.uniform(0, 0.12)))
    for i in range(3):
        parts.append((bandpass(noise(0.05), rng.uniform(300, 600), 3) * env(0.05, 0.001, 0.015) * 0.8,
                      0.15 + i * rng.uniform(0.05, 0.09)))
    return room(saturate(mix(*parts), 1.6), 0.35, 0.25)


def break_glass(v):
    """Bottles shattering: crack, bright spray, tinkling shards, a splash."""
    parts = [(highpass(noise(0.03), 2500) * env(0.03, 0.0003, 0.008) * 1.5, 0),
             (lowpass(noise(0.25), 1200) * env(0.25, 0.01, 0.06) * 0.3, 0.02)]   # liquid splash
    for _ in range(14):
        f = rng.uniform(2500, 7500)
        parts.append((np.sin(2 * np.pi * f * t(0.18)) * env(0.18, 0.0005, 0.04) * 0.35, rng.uniform(0, 0.3)))
    return room(mix(*parts), 0.35, 0.3)


def portal_whoosh():
    dur = 1.0
    shape = np.sin(np.pi * t(dur) / dur)
    air = lowpass(noise(dur), np.linspace(250, 5000, n(dur))) * shape
    shimmer = sweep(300, 1500, dur) * 0.25 * shape
    return room(air + shimmer, 0.6, 0.35)


def banjo(notes, spacing, ring=0.9, last_ring=None):
    parts = []
    for i, nm in enumerate(notes):
        dur = last_ring if (last_ring and i == len(notes) - 1) else ring
        parts.append((pluck(note(nm), dur), i * spacing))
    return room(mix(*parts), 0.5, 0.2)



# ================================================================ realistic weapons (Ready or Not style)
# Real gunshots are an extremely sharp pressure spike (an "N-wave"), a
# violent burst of broadband noise lasting a few tens of milliseconds, a low
# "punch" in the chest, and then — indoors — a long, dark reverb tail built
# from hard early reflections off nearby walls. Compression brings that tail
# up so the shot feels big and the room feels real.

def n_wave(dur=0.0009, amp=1.0):
    """Supersonic pressure spike: instant rise, linear fall through zero, snap back."""
    return np.linspace(amp, -amp, max(2, n(dur)))


def compress(sig, threshold=0.25, ratio=5.0, attack=0.001, release=0.06):
    """Envelope-follower compressor: tames the peak so the body and tail come forward."""
    a_att = np.exp(-1 / (SR * attack))
    a_rel = np.exp(-1 / (SR * release))
    out = np.empty_like(sig)
    level = 0.0
    for i, x in enumerate(sig):
        mag = abs(x)
        coeff = a_att if mag > level else a_rel
        level = coeff * level + (1 - coeff) * mag
        if level > threshold:
            gain = (threshold + (level - threshold) / ratio) / level
        else:
            gain = 1.0
        out[i] = x * gain
    return out


_indoor_ir = {}


def indoor(sig, tail=0.9, wet=0.55, darkness=2500):
    """
    Indoor room acoustics: hard early reflections (walls, floor, ceiling at
    different distances) followed by a dense, darkening reverb tail.
    """
    key = (tail, darkness)
    if key not in _indoor_ir:
        ir = np.zeros(n(tail))
        for delay, gain in [(0.004, 0.7), (0.009, 0.55), (0.013, 0.5), (0.021, 0.4),
                            (0.029, 0.35), (0.037, 0.3), (0.052, 0.22), (0.068, 0.18)]:
            ir[n(delay)] += gain * rng.choice([-1, 1])
        dense = noise(tail) * np.exp(-t(tail) / (tail / 6))
        dense = lowpass(dense, np.linspace(darkness * 1.6, darkness * 0.3, n(tail)))  # air absorbs highs over time
        ir += dense * 0.6
        ir /= np.sqrt(np.sum(ir ** 2))
        _indoor_ir[key] = ir
    ir = _indoor_ir[key]
    size_out = len(sig) + len(ir) - 1
    nfft = 1 << (size_out - 1).bit_length()
    verb = np.fft.irfft(np.fft.rfft(sig, nfft) * np.fft.rfft(ir, nfft), nfft)[:size_out]
    dry = np.concatenate([sig, np.zeros(len(verb) - len(sig))])
    return dry + wet * verb * (np.max(np.abs(sig)) / (np.max(np.abs(verb)) or 1))


def rn_shotgun(v):
    """12-gauge pump shotgun discharge indoors."""
    spike = n_wave(0.0008, 1.0)
    echo_spike = n_wave(0.0008, -0.5)                      # ground reflection a moment later
    blast = noise(0.12) * env(0.12, 0.0001, 0.03 + 0.003 * v) * 2.5
    blast = lowpass(blast, np.linspace(7000, 1000, n(0.12)))
    bark = bandpass(noise(0.15), 650 + 80 * v, 0.8) * env(0.15, 0.0002, 0.045) * 4.5   # the gun's "bark"
    punch = bandpass(noise(0.2), 85 + 10 * v, 1.2) * env(0.2, 0.0005, 0.05) * 2.4     # chest thump
    sub = sweep(70, 35, 0.35) * env(0.35, 0.001, 0.07) * 0.3
    action = metal(0.06, [2600, 4100, 6300], 0.01) * 0.12   # gun parts ringing from the recoil
    dry = mix((spike, 0), (echo_spike, 0.0011), (blast, 0), (bark, 0), (punch, 0), (sub, 0), (action, 0.002))
    dry = saturate(compress(dry, 0.2, 6, 0.0005, 0.05), 2.2)
    slap = lowpass(dry, 1500) * 0.25                        # slap-back off the far wall
    return indoor(mix((dry, 0), (slap, 0.075 + 0.01 * v)), tail=1.1, wet=0.6, darkness=2200)


def rn_staff(v):
    """
    The Gatling Staff, played straight: each bolt has a real rifle-like crack
    and thump with a faint magical zing on top, in the same indoor space.
    """
    spike = n_wave(0.0006, 0.8)
    blast = lowpass(noise(0.05) * env(0.05, 0.0001, 0.009), np.linspace(7000, 1600, n(0.05)))
    punch = bandpass(noise(0.08), 140 + 15 * v, 1.2) * env(0.08, 0.0005, 0.025) * 3.0
    zing = sweep(2600 + 200 * v, 1500, 0.06) * env(0.06, 0.001, 0.02) * 0.18
    dry = saturate(compress(mix((spike, 0), (blast, 0), (punch, 0), (zing, 0.002)), 0.25, 5, 0.0005, 0.03), 2.0)
    return indoor(dry, tail=0.6, wet=0.4, darkness=2600)


def rn_dry_fire():
    """Hammer falling on an empty chamber: spring twang + sharp metal click."""
    click = mix((n_wave(0.0004, 0.6), 0), (metal(0.05, [3900, 6100, 8300], 0.006) * 0.6, 0))
    spring = bandpass(noise(0.05), 2200, 10) * env(0.05, 0.001, 0.02) * 0.3
    return indoor(mix((click, 0), (spring, 0.001)), tail=0.35, wet=0.25, darkness=4000)


def foley_click(freq, amp=1.0):
    """A precise metal-on-metal contact: transient + short ring + body knock."""
    return mix((n_wave(0.0004, amp), 0),
               (metal(0.05, [freq, freq * 1.52, freq * 2.37, freq * 3.1], 0.007) * 0.5 * amp, 0),
               (lowpass(noise(0.03), 900) * env(0.03, 0.0005, 0.006) * 0.6 * amp, 0))


def rn_shotgun_open():
    """(Unused since the switch to a pump gun; kept for a break-action variant.) Barrels hinge open."""
    lever = foley_click(2400, 0.9)
    spring = bandpass(noise(0.07), 1700, 6) * env(0.07, 0.002, 0.025) * 0.35
    hinge = bandpass(noise(0.12), 900, 4) * env(0.12, 0.01, 0.04) * 0.4
    stop = foley_click(1200, 1.0)                            # barrels hit the hinge stop
    extract = mix((foley_click(3000, 0.5), 0), (bandpass(noise(0.04), 600, 2) * env(0.04, 0.001, 0.01) * 0.5, 0))
    return indoor(mix((lever, 0), (spring, 0.004), (hinge, 0.03), (stop, 0.1), (extract, 0.13)),
                  tail=0.4, wet=0.25, darkness=3500)


def rn_shotgun_close():
    """(Unused since the switch to a pump gun; kept for a break-action variant.) Shells in, action slammed shut."""
    def insert(start_freq):
        slide = bandpass(noise(0.07), start_freq, 3) * np.linspace(0.2, 1, n(0.07)) * 0.35   # plastic hull sliding
        seat = mix((foley_click(1500, 0.6), 0), (lowpass(noise(0.03), 500) * env(0.03, 0.0005, 0.008), 0))
        return mix((slide, 0), (seat, 0.065))
    slam = mix((foley_click(900, 1.3), 0),
               (bandpass(noise(0.12), 180, 1.5) * env(0.12, 0.0005, 0.03) * 2.0, 0),
               (metal(0.2, [650, 1180, 1990, 3020], 0.04) * 0.45, 0.001))
    latch = foley_click(2800, 0.7)
    dry = mix((insert(1100), 0), (insert(1250), 0.14), (slam, 0.32), (latch, 0.335))
    return indoor(saturate(dry, 1.5), tail=0.45, wet=0.25, darkness=3500)


def rn_shell(v):
    """Plastic shotgun hull with a brass base bouncing on a hard floor: hollow, woody 'tok-tok'."""
    parts = []
    tt = 0.0
    for i in range(4):
        amp = 0.75 ** i
        brass = metal(0.07, [1650 + 120 * v, 2900, 4400], 0.012) * 0.35 * amp
        hull = bandpass(noise(0.03), 900 + 80 * v, 5) * env(0.03, 0.0005, 0.008) * amp
        parts.append((mix((hull, 0), (brass, 0)), tt))
        tt += max(0.035, 0.11 - i * 0.03)
    roll = bandpass(noise(0.18), 1300, 3) * env(0.18, 0.01, 0.06) * 0.08     # hull rolling to a stop
    parts.append((roll, tt))
    return indoor(mix(*parts), tail=0.35, wet=0.2, darkness=4000)


def rn_impact(v):
    """Buckshot hitting plaster/stone: sharp crack, crumbling debris, falling dust."""
    crack = mix((n_wave(0.0005, 0.9), 0),
                (bandpass(noise(0.03), 2400 + 400 * v, 1.5) * env(0.03, 0.0002, 0.006), 0))
    crumble = [(bandpass(noise(0.015), rng.uniform(1200, 4500), 2.5) * env(0.015, 0.0003, 0.004) * rng.uniform(0.2, 0.5),
                rng.uniform(0.008, 0.12)) for _ in range(9)]
    dust = highpass(noise(0.3), 3000) * env(0.3, 0.02, 0.08) * 0.06
    parts = [(crack, 0), (dust, 0.01)] + crumble
    if v == 2:                                               # occasional ricochet whine
        parts.append((sweep(3800, 2100, 0.25) * env(0.25, 0.005, 0.08) * 0.12, 0.004))
    return indoor(mix(*parts), tail=0.5, wet=0.35, darkness=3000)


def rn_staff_spin(down=True):
    """Gatling barrels spinning: a whirring motor and rattling cylinder, winding down or up."""
    dur = 0.6
    f0, f1 = (55, 12) if down else (12, 55)
    x = t(dur)
    rate = f0 * (f1 / f0) ** (x / dur)                       # barrel clicks per second
    phase = np.cumsum(rate) / SR
    ticks = (np.diff(np.floor(phase), prepend=0) > 0).astype(float)
    rattle = lowpass(ticks, 6000) * 3 + bandpass(ticks, 2400, 6) * 2
    whir = bandpass(noise(dur), 300, 2) * (rate / 55) * 0.6
    return mix((rattle + whir, 0))


def rn_staff_reload_start():
    """Spin down, unlatch the spent crystal cell."""
    return indoor(mix((rn_staff_spin(True), 0), (foley_click(1800, 0.9), 0.55), (foley_click(1100, 0.7), 0.62)),
                  tail=0.4, wet=0.25, darkness=3500)


def rn_staff_reload_end():
    """Fresh cell slammed home, latch, and a low hum as it charges."""
    hum = sweep(110, 160, 0.5) * env(0.5, 0.05, 0.25) * 0.3
    return indoor(mix((foley_click(800, 1.3), 0),
                      (bandpass(noise(0.1), 200, 1.5) * env(0.1, 0.0005, 0.025) * 1.5, 0),
                      (foley_click(2600, 0.7), 0.05), (hum, 0.06)), tail=0.45, wet=0.25, darkness=3500)


def rn_staff_equip():
    return indoor(mix((foley_click(1400, 0.8), 0), (rn_staff_spin(False) * 0.7, 0.04)), tail=0.4, wet=0.25, darkness=3500)


def rn_staff_dry():
    """Out of charge: the barrels turn but only click."""
    return indoor(mix(*[(foley_click(2000 + 150 * i, 0.5), i * 0.05) for i in range(3)]), tail=0.3, wet=0.2)



def rn_pump(v):
    """
    Racking a pump shotgun — the classic "chk-chk":
      back stroke:  action unlocks (click), fore-end slides back (metal friction),
                    the spent hull is kicked out, the bolt hits the rear stop;
      forward stroke: fore-end slams forward, a fresh shell is chambered and the
                    bolt locks with a heavy, resonant clack.
    """
    unlock = foley_click(2300 + 100 * v, 0.7)
    slide_back = bandpass(noise(0.08), 1900 + 150 * v, 2.5) * np.linspace(0.3, 1, n(0.08)) * 0.45
    eject = mix((foley_click(3300, 0.45), 0), (bandpass(noise(0.03), 700, 2) * env(0.03, 0.001, 0.008) * 0.4, 0))
    rear_stop = mix((foley_click(1300, 1.1), 0), (bandpass(noise(0.06), 230, 1.5) * env(0.06, 0.0005, 0.015) * 1.2, 0))
    slide_fwd = bandpass(noise(0.05), 1700 + 150 * v, 2.5) * np.linspace(0.4, 1, n(0.05)) * 0.4
    lock = mix((foley_click(1050, 1.4), 0),
               (bandpass(noise(0.1), 190, 1.4) * env(0.1, 0.0005, 0.025) * 1.8, 0),
               (metal(0.18, [720, 1290, 2110, 3180], 0.035) * 0.4, 0.001))
    dry = mix((unlock, 0), (slide_back, 0.005), (eject, 0.06), (rear_stop, 0.085),
              (slide_fwd, 0.15), (lock, 0.195))
    return indoor(saturate(dry, 1.6), tail=0.45, wet=0.25, darkness=3500)


def rn_shell_insert(v):
    """Thumbing a shell into the tube: plastic hull scrape, spring pushback, a soft seat click."""
    scrape = bandpass(noise(0.07), 1100 + 120 * v, 3) * np.linspace(0.2, 1, n(0.07)) * 0.4
    spring = bandpass(noise(0.04), 2600 + 200 * v, 12) * env(0.04, 0.001, 0.015) * 0.25
    seat = mix((foley_click(1600 + 100 * v, 0.55), 0),
               (lowpass(noise(0.03), 500) * env(0.03, 0.0005, 0.008) * 0.8, 0))
    return indoor(mix((scrape, 0), (spring, 0.06), (seat, 0.065)), tail=0.3, wet=0.2, darkness=3500)


def rn_shotgun_handling():
    """Tilting the gun to load / drawing it: cloth rustle and a light knock on the receiver."""
    rustle = lowpass(noise(0.25), 1800) * np.sin(np.pi * t(0.25) / 0.25) ** 2 * 0.35
    knock = foley_click(1400, 0.5)
    return indoor(mix((rustle, 0), (knock, 0.12)), tail=0.3, wet=0.2, darkness=3500)



# ================================================================ Skeeter's barks and menu
def old_chuckle(v):
    """A raspy old-timer's "heh-heh-heh": short voiced bursts with breathy gaps."""
    parts = []
    t0 = 0.0
    for i in range(3 + v % 2):
        pitch = 150 - 12 * i - 8 * v
        parts.append((voice(pitch, pitch * 0.85, 0.085, "eh", rough=0.8, breath=0.45), t0))
        t0 += 0.11 + 0.01 * i
    return room(mix(*parts), 0.25, 0.15)


def old_grunt(v):
    """A low, satisfied "hmph" / "heh" grunt."""
    vowel = "uh" if v == 0 else "oh"
    return room(voice(118 - 8 * v, 92, 0.24, vowel, rough=0.75, breath=0.35), 0.25, 0.15)


def ui_move():
    """Soft click when moving through the menu."""
    return room(foley_click(2600, 0.5), 0.2, 0.1)



def birds():
    """Morning birdsong: little rising/falling chirps over a soft breeze."""
    dur = 3.5
    parts = [(lowpass(noise(dur), 400) * 0.05, 0)]
    for _ in range(14):
        f0 = rng.uniform(2600, 4200)
        chirp = sweep(f0, f0 * rng.uniform(0.7, 1.4), 0.07) * env(0.07, 0.005, 0.03)
        start = rng.uniform(0, dur - 0.3)
        for k in range(rng.integers(1, 4)):                   # some birds chirp twice or three times
            parts.append((chirp * 0.35, start + k * 0.1))
    return room(mix(*parts), 0.5, 0.2)


# ================================================================ write everything
def main():
    for v in range(3):
        save(f"shotgun_shot_{v + 1}", rn_shotgun(v), 0.97)
        save(f"staff_shot_{v + 1}", rn_staff(v), 0.55)
        save(f"impact_{v + 1}", rn_impact(v), 0.6)
        save(f"death_knight_{v + 1}", death_knight(v))
    save("shotgun_dry", rn_dry_fire(), 0.6)
    for v in range(2):
        save(f"shotgun_pump_{v + 1}", rn_pump(v), 0.85)
    for v in range(3):
        save(f"shotgun_insert_{v + 1}", rn_shell_insert(v), 0.7)
    save("shotgun_reload_start", rn_shotgun_handling(), 0.6)
    save("shotgun_equip", rn_shotgun_handling(), 0.6)
    save("staff_dry", rn_staff_dry(), 0.5)
    save("staff_reload_start", rn_staff_reload_start(), 0.7)
    save("staff_reload_end", rn_staff_reload_end(), 0.8)
    save("staff_equip", rn_staff_equip(), 0.7)

    for v in range(4):
        save(f"footstep_{v + 1}", footstep(v), 0.5)
    for v in range(2):
        save(f"shell_{v + 1}", rn_shell(v), 0.35)
        save(f"wizard_cast_{v + 1}", wizard_cast(v), 0.7)
        save(f"knight_windup_{v + 1}", knight_windup(v), 0.85)
        save(f"sword_swing_{v + 1}", sword_swing(v), 0.8)
        save(f"hurt_knight_{v + 1}", hurt_knight(v), 0.85)
        save(f"hurt_wizard_{v + 1}", hurt_wizard(v), 0.85)
        save(f"death_wizard_{v + 1}", death_wizard(v))
        save(f"alert_{v + 1}", alert(v), 0.8)
        save(f"curious_{v + 1}", curious(v), 0.7)
        save(f"break_wood_{v + 1}", break_wood(v), 0.85)
        save(f"break_glass_{v + 1}", break_glass(v), 0.75)
    save("death_player", death_player(), 0.95)

    for v in range(3):
        save(f"bark_chuckle_{v + 1}", old_chuckle(v), 0.8)
    for v in range(2):
        save(f"bark_grunt_{v + 1}", old_grunt(v), 0.8)
    save("ui_move", ui_move(), 0.5)
    save("ambience_birds", birds(), 0.6)
    save("exit_open", portal_whoosh(), 0.7)
    save("pickup_weapon", mix((chime([note("G5"), note("B5"), note("D6"), note("G6")], 0.7, 0.07), 0),
                              (portal_whoosh() * 0.5, 0)), 0.8)

    # Music stings stay banjo: sad descending "wah-wah" on death, a roll on clear, a strum to start.
    save("sting_death", banjo(["D4", "C#4", "C4", "B3"], 0.38, 0.6, 1.4), 0.8)
    save("sting_clear", banjo(["G4", "B4", "D5", "G5", "D5", "G5"], 0.09, 0.6, 1.0), 0.8)
    save("sting_start", banjo(["G3", "D4", "G4", "B4", "D5"], 0.035, 1.2), 0.7)
    save("menu_start", mix((rn_pump(0), 0), (banjo(["G3", "B3", "D4", "G4"], 0.03, 1.0), 0.12)), 0.8)
    save("victory", banjo(["G4", "G4", "B4", "D5", "B4", "D5", "G5", "D5", "G5"], 0.13, 0.7, 1.6), 0.8)

    print("wrote", len(os.listdir(OUT)), "sounds")


if __name__ == "__main__":
    main()
