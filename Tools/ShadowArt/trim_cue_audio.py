"""Cut the found-moment sounds (MemorySolveCue) out of the downloaded Soundsnap/Pixabay files.

Each cut: a window of the source, optional channel swap (so a pass moves the way the shadow travels), short fades, the
peak brought to -1 dBFS, written as 44.1 kHz 16-bit WAV into Assets/ShadowWorkshop/PicoFinal/Audio/Cues. Prints each
clip's loudest-400 ms RMS and the cue volume that puts it at TARGET dBFS (next to the found bells).

    python -I Tools/ShadowArt/trim_cue_audio.py "<folder with the downloads>" [out_dir]
"""
import json, os, subprocess, sys, wave
import numpy as np

RATE, TARGET = 44100, -20.0
# out name, source file, start, end (s), channels (1 mono / 2 stereo), swap L/R, fade in, fade out (s)
CUTS = [
    ("Birthday_BlowOut", "165410-Human-Breath-Exhale-Blow_Out_Candles-Many-Long.wav", 0.0, 1.0, 1, False, .01, .25),
    ("Birthday_PartyHorn", "389921-Party-Pack-Horn-Coil-02-Short-04.wav", .04, 1.10, 2, False, .005, .12),
    ("BonVoyage_JetPass", "56898-Jet_Land_2.wav", 34.6, 40.6, 2, True, .6, 2.5),
    ("BonVoyage_CabinChime", "saboteurcomics-airplane-chime-466924.mp3", .05, 2.38, 1, False, .005, .3),
    ("GoodMorning_Sizzle", "424700-FryingPanSizzle1.wav", 0.0, 4.0, 1, False, .3, 1.4),
    ("Home_PotBreak", "493132-BRLFX-TFV1-0316-Breaking_a_terracotta_pot_1.wav", 0.0, 1.2, 2, False, .002, .35),
    ("Home_PuppyBark", "1143705.audio-Anml_Dog_Puppy_02.wav", .38, 1.0, 1, False, .005, .12),
    # WildWest (Pixabay, 2026-11-16): four hoof beats that ride off, and the harmonica's first phrase (up to its breath).
    # Cut in Unity the same way (no ffmpeg on that machine): gallop volume 1.00, harmonica 0.51.
    ("WildWest_Gallop", "dragon-studio-horse-galloping-339737.mp3", 1.05, 3.05, 2, False, .02, .6),
    ("WildWest_Harmonica", "samuel_copier_country-wild-west-harmonica-267552.mp3", .30, 3.45, 1, False, .01, .6),
]


def decode(path, start, end, channels):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-ss", str(start), "-to", str(end), "-i", path, "-ac", str(channels),
                          "-ar", str(RATE), "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, channels).astype(np.float64)


def main():
    src = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else "Assets/ShadowWorkshop/PicoFinal/Audio/Cues"
    os.makedirs(out, exist_ok=True)
    report = {}
    for name, file, start, end, ch, swap, fin, fout in CUTS:
        x = decode(os.path.join(src, file), start, end, ch)
        if swap and ch == 2:
            x = x[:, ::-1]
        n = len(x); t = np.arange(n) / RATE
        env = np.where(t < fin, .5 - .5 * np.cos(np.pi * np.minimum(1, t / max(fin, 1e-4))), 1) * \
              np.where(t[-1] - t < fout, .5 - .5 * np.cos(np.pi * np.minimum(1, (t[-1] - t) / max(fout, 1e-4))), 1)
        x = x * env[:, None]
        x *= 10 ** (-1 / 20) / max(1e-9, np.max(np.abs(x)))
        w = int(RATE * .4); mono = x.mean(axis=1)
        rms = max(np.sqrt(np.mean(mono[i:i + w] ** 2)) for i in range(0, max(1, n - w), RATE // 50))
        rms_db = 20 * np.log10(rms + 1e-12); volume = float(np.clip(10 ** ((TARGET - rms_db) / 20), .15, 1))
        pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
        path = os.path.join(out, name + ".wav")
        with wave.open(path, "wb") as f:
            f.setnchannels(ch); f.setsampwidth(2); f.setframerate(RATE); f.writeframes(pcm.tobytes())
        report[name] = {"seconds": round(n / RATE, 2), "rms400_db": round(rms_db, 1), "volume": round(volume, 2)}
        print(f"{name:22s} {n / RATE:5.2f} s  loudest 400 ms {rms_db:6.1f} dBFS  -> volume {volume:.2f}")
    print(json.dumps({k: v["volume"] for k, v in report.items()}))


if __name__ == "__main__":
    main()
