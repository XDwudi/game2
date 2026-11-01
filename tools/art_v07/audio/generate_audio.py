"""Original deterministic 48 kHz synthesis for art0.7.0. No external recordings.

Editable source is this recipe plus ArtSource/Art07/Audio/recipe.json. WAV masters
and runtime exports are byte-identical. Peak headroom is measured after synthesis.
"""
from pathlib import Path
import hashlib
import json
import math
import random
import struct
import wave

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "ArtSource/Art07/Audio"
RUNTIME = ROOT / "DicePrototype/Assets/Art/Art07/Audio"
RATE = 48000

SPECS = [
    ("sfx_ui_hover", .050, "wood", 620), ("sfx_ui_press", .075, "wood", 370),
    ("sfx_ui_denied", .120, "denied", 140),
    ("sfx_dice_roll_01", .540, "roll", 720), ("sfx_dice_roll_02", .600, "roll", 680), ("sfx_dice_roll_03", .480, "roll", 760),
    ("sfx_dice_land_01", .090, "ceramic", 830), ("sfx_dice_land_02", .110, "ceramic", 730), ("sfx_dice_land_03", .125, "ceramic", 650),
    ("sfx_lock", .110, "lock", 470), ("sfx_card_trigger", .130, "paper_note", 440),
    ("sfx_score_step", .150, "note", 330),
    ("sfx_score_burst_l3", .420, "chord3", 220), ("sfx_score_burst_l4", .520, "chord4", 220), ("sfx_score_burst_l5", .600, "chord5", 220),
    ("sfx_coin", .180, "coin", 1100), ("sfx_grow", .230, "grow", 330),
    ("sfx_dye", .210, "brush", 290), ("sfx_copy", .190, "paper", 410),
    ("sfx_dice_break", .300, "glass", 1600), ("sfx_card_break", .280, "tear", 460),
    ("sfx_refresh", .220, "refresh", 360), ("sfx_qualified", .440, "stamp", 220),
    ("sfx_run_win", 1.500, "win", 220), ("sfx_run_fail", 1.350, "fail", 220),
]


def tone(t, hz, decay=18):
    return (math.sin(2*math.pi*hz*t) + .16*math.sin(2*math.pi*hz*2*t))*math.exp(-decay*t)


def synth(name, duration, family, hz):
    rng = random.Random(int(hashlib.sha256(name.encode()).hexdigest()[:16], 16))
    data = []; low_noise = 0
    for i in range(round(duration*RATE)):
        t = i/RATE; low_noise = .75*low_noise + .25*rng.uniform(-1, 1)
        n = low_noise
        if family == "wood": v = .6*tone(t,hz,70) + n*.28*math.exp(-100*t)
        elif family == "denied": v = tone(t,hz,20)*.6 + tone(t,hz*.73,20)*.3
        elif family in ("ceramic", "coin", "glass"):
            decay = 38 if family == "ceramic" else 22
            v = .6*tone(t,hz,decay)+.2*tone(t,hz*1.49,decay)+n*.15*math.exp(-55*t)
            if family == "glass": v += .25*tone(t,hz*1.22,30)+.12*tone(t,hz*1.74,45)
        elif family == "roll":
            v=0
            for j, start in enumerate((0,.065,.145,.245,.355,.440)):
                q=t-start
                if q>=0: v += (.7-j*.075)*(tone(q,hz*(1+j*.09),60)+n*.5*math.exp(-60*q))
        elif family == "lock": v=.5*tone(t,hz,50)+(.25*tone(t-.035,hz*1.8,60) if t>.035 else 0)+n*.2*math.exp(-55*t)
        elif family in ("paper", "brush", "tear", "refresh", "paper_note"):
            shape = math.sin(math.pi*t/duration)**1.5
            v = n*shape*(1.1 if family=="tear" else .8)
            if family in ("refresh","paper_note"): v += .25*tone(t,hz,22)
        elif family == "note": v = tone(t,hz,13)*.75
        elif family == "grow": v=tone(t,hz,18)*.5 + (tone(t-.09,hz*1.5,15)*.5 if t>.09 else 0)
        elif family.startswith("chord"):
            intervals=(0,4,7) if family=="chord3" else (0,4,7,12) if family=="chord4" else (0,4,7,9,12)
            v=sum(tone(t,hz*2**(s/12),8) for s in intervals)/len(intervals)
        elif family == "stamp": v = .5*tone(t,90,40)+n*.4*math.exp(-60*t)+sum(tone(t,hz*r,9) for r in (1,1.25,1.5))*.18
        elif family in ("win","fail"):
            notes=(0,4,7,12) if family=="win" else (7,3,0,-5)
            v=0
            for j,s in enumerate(notes):
                q=t-j*.22
                if q>=0: v += tone(q,hz*2**(s/12),5)*.4
        else: raise ValueError(family)
        # Short smooth attack and release, with no DC jump at a PCM endpoint.
        fade = min(1,t/.003, (duration-t)/.012)
        data.append(v*max(0,fade))
    peak=max(abs(x) for x in data) or 1
    gain=.24/peak
    return [x*gain for x in data]


def write_wav(path, samples, channels=1):
    path.parent.mkdir(parents=True,exist_ok=True)
    raw=struct.pack('<'+'h'*len(samples), *(round(max(-1,min(1,x))*32767) for x in samples))
    with wave.open(str(path),'wb') as w:
        w.setnchannels(channels);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(raw)


def main():
    SOURCE.mkdir(parents=True,exist_ok=True);RUNTIME.mkdir(parents=True,exist_ok=True)
    assets=[]
    for name,duration,family,hz in SPECS:
        samples=synth(name,duration,family,hz)
        target=SOURCE/(name+'.wav');write_wav(target,samples);(RUNTIME/target.name).write_bytes(target.read_bytes())
        assets.append(dict(assetId=name,sourcePath=str(target.relative_to(ROOT)).replace('\\','/'),runtimePath=str((RUNTIME/target.name).relative_to(ROOT)).replace('\\','/'),sampleRate=RATE,channels=1,bits=16,durationSeconds=duration,peak=max(abs(x) for x in samples),sha256=hashlib.sha256(target.read_bytes()).hexdigest(),status='produced_not_engine_verified'))
    # Original 8-bar/90 BPM sparse D-minor table loop; exact bar length permits gapless looping.
    duration=8*4*60/90; count=round(duration*RATE); music=[0.0]*count
    for bar,root in enumerate((146.832,130.813,116.541,130.813,146.832,174.614,130.813,146.832)):
        for beat,ratio,gain in ((0,.5,.055),(1.5,1,.025),(2,1.5,.024),(3,2,.017)):
            start=round((bar*4+beat)*60/90*RATE)
            length=min(round(.9*RATE),count-start)
            for j in range(length):
                t=j/RATE;env=min(1,t/.025)*min(1,(length-j)/(.045*RATE))
                music[start+j]+=tone(t,root*ratio,4)*gain*env
    name='music_table_loop';target=SOURCE/(name+'.wav');write_wav(target,music);(RUNTIME/target.name).write_bytes(target.read_bytes())
    assets.append(dict(assetId=name,sourcePath=str(target.relative_to(ROOT)).replace('\\','/'),runtimePath=str((RUNTIME/target.name).relative_to(ROOT)).replace('\\','/'),sampleRate=RATE,channels=1,bits=16,durationSeconds=duration,bpm=90,peak=max(abs(x) for x in music),sha256=hashlib.sha256(target.read_bytes()).hexdigest(),status='produced_not_engine_verified'))
    recipe=dict(artVersion='art0.7.0',producer='Original deterministic synthesis by project tooling',license='Original project audio; no third-party recordings or reference-game audio',script='tools/art_v07/audio/generate_audio.py',logicalCueCount=16,sfxWavCount=25,musicWavCount=1,assets=assets)
    (SOURCE/'recipe.json').write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'Generated {len(assets)} original WAV masters/exports, SFX peak <= 0.24; actual listening and engine mixer verification pending.')


if __name__=='__main__': main()
