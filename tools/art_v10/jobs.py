"""v1.0 illustration jobs for the standard art pipeline (docs/art/AI_ART_PIPELINE.md).

Writes one artgen job per new content id under ArtSource/AIConcepts/v10-illustrations/<ID>/job.json.
Subjects follow docs/art/STYLE_GUIDE.md 4.4: objects, action and relations only; colour only where the mechanic needs it.

    python tools/art_v10/jobs.py            # write missing job files (never overwrites an existing job)
    python tools/art_v10/jobs.py --list     # print ids in generation order
    python tools/art_v10/jobs.py --rewrite  # rewrite the job files of the ids revised after review (REVISED)
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JOBS = ROOT / "ArtSource" / "AIConcepts" / "v10-illustrations"

SUBJECTS = [
    ("C104", "three large dice threaded on a short cord like beads and packed in a tight diagonal row rising from lower left to upper right, the two outer dice with red pips and the middle die with blue pips"),
    ("C105", "a shallow sorting tray with a raised divider down the middle, red dice gathered in the left half and blue dice in the right half"),
    ("C106", "a folded paper butterfly opened flat with a crease down its middle, one die sitting on each wing at matching positions"),
    ("C107", "a curling vine with three dice growing along it like melons, each die slightly larger than the one before"),
    ("C108", "one large die wearing a tall plumed marching hat, leading two smaller dice that follow in a line behind it"),
    ("C109", "a sturdy stone pillar standing in a stream with a die resting on top, the water splitting around the pillar"),
    ("C110", "an open drawstring pouch spilling out a small potion bottle, a rolled scroll and a die"),
    ("C111", "a peddler's tall wooden backpack frame loaded with small red and blue potion bottles and rolled scrolls, a small rattle drum hanging from its side"),
    ("C112", "a pan flute with a blank playing card slipped in between its pipes like one extra pipe"),
    ("C113", "a bellows blowing into a small brick forge where a die sits on the glowing coals"),
    ("C114", "the tip of a wand touching a blank playing card, a burst of small stars rising from the touched spot"),
    ("C115", "a willow twig stuck into a small mound of earth and sprouting fresh leaves, a die lying beside the mound"),
    ("C116", "a pennant flag on a pole planted beside a small tent, two dice lined up in front of the tent"),
    ("C117", "a glove with its index finger extended touching a die, the touched face of the die turned gold"),
    ("C118", "a die being lifted out of a small pot of red paint by a pair of tongs, red paint coating its top face and dripping from underneath it; at most three faces of the die are visible and every visible face has pips"),
    ("C119", "an ink dropper releasing a drop above a die, the ink soaking from one face across the edge onto the neighbouring face"),
    ("C120", "a seedling in a flower pot being pulled upward by a pair of tweezers, a die half buried in the soil beside it"),
    ("C121", "a hand plane shaving long curls off the top of a large die, a much smaller die sitting beside it"),
    ("C122", "five small dice huddled tightly together in a ring like a flock, one more die rolling toward the group"),
    ("C123", "two low steps, a pair of dice sitting on the lower step and a trio of dice sitting on the upper step"),
    ("C124", "a ladle pouring molten metal into a hollow die through an opening in its top"),
    ("C125", "a polishing cloth rubbing one face of a die to a shine, a small round tin of polish beside it"),
    ("C126", "a die resting on a plump cushion under a folded blanket, a steaming cup beside it"),
    ("C127", "a branch where a flowering twig is bound onto a bare stem with twine, a die hanging from the grafted twig"),
    ("C128", "a glassblower's pipe with a transparent glass die forming at its end"),
    ("C129", "a short sword planted upright through a coiled scaly tail, a die balanced on the sword's pommel"),
    ("C130", "a fish leaping up a small waterfall with a die carried on its back"),
    ("C131", "a carved wooden toy goat carrying a bundle tied with rope on its back, a broken chain link at its feet"),
    ("C132", "a pressure cooker with its lid valve jetting steam, a die rattling on top of the lid"),
    ("C133", "a rolled blank poster pinned to a post by a dagger, a pouch of gold coins hanging below it"),
    ("C134", "an empty potion bottle held upside down with its last drop falling onto a die"),
    ("C135", "a wooden storage crate packed full of bottles and scrolls, its lid held half shut by a padlock"),
    ("C136", "a round shield with a die strapped to its centre and a spear leaning behind it"),
    ("C137", "a star-shaped trophy on a small pedestal with a die mounted at the centre of the star"),
    ("C138", "a slingshot stretched back with a gold coin loaded in its pouch, two more gold coins already flying off"),
    ("C139", "a tall upholstered throne with a die seated on it wearing a small crown, each armrest a short stack of three gold coins"),
    ("C140", "a small bin with two blank cards dropping into its open top and a gold coin popping out of a slot at the bottom"),
    ("C141", "a large night-blooming flower in full bloom in a small pot, one petal already falling, a die at the foot of the pot"),
    ("C142", "a dragon kite with a plain paper kite head and no face, its tail made of five dice in a wavy row, the kite string trailing below"),
    ("C143", "two identical dice sitting side by side on a small two-seat swing hanging from one bar"),
    ("C144", "a crown resting across two identical dice standing side by side"),
    ("C145", "a transparent glass die wrapped in a layer of bubble wrap"),
    ("C146", "a folded piece of brocade with a needle and red thread stitching one red flower onto it, a red die beside it"),
    ("C147", "a small block of tiered theatre seats with dice sitting in the seats like an audience"),
    ("C148", "a dice cup turned upside down and held shut by a small padlock, two dice resting beside it"),
    ("C149", "a single curved sabre lying diagonally across the canvas from lower left to upper right, one die balanced on its tip"),
    ("C150", "three bells hanging in a row from small to large, a die hanging as the clapper of the largest bell"),
    ("C151", "an empty picture frame standing on an easel, a die at the foot of the easel"),
    ("C152", "a tuft of grass pushing up through a cracked paving stone and lifting a die on its blades"),
    ("C153", "a double door swinging open with a cut red ribbon falling from the doorway and a die rolling out"),
    ("I08", "a paper talisman strip torn in half, the two halves drifting apart, a broken chain link between them"),
    ("I09", "a light bulb whose filament is shaped like a lightning bolt, a die reflected in its glass"),
    ("I10", "a curling blue wave lifting three dice on its crest"),
    ("I11", "a calligraphy brush loaded with gold paint drawing a gold stroke across the top face of a die"),
    ("I12", "a segmented tray being pulled open like an accordion, one extra empty slot appearing at its end beside the dice already in it"),
    ("U08", "a carving chisel struck by a small mallet cutting a new pip into the face of a die, chips flying"),
    ("U09", "a flat file rasping across the top of a die, filings falling off"),
    ("U10", "a screwdriver driving a small weight plug into one face of a die"),
    ("U11", "a die standing on a small round mirror lying flat on the table, the mirror showing the die's reflection beneath it; at most three faces of the die are visible and every visible face has pips"),
    ("O09", "a sealed envelope stuffed with gold coins sliding under a heavy door"),
    ("O10", "an abacus with a small stack of gold coins beside it and a quill pen resting across its top"),
    ("O11", "a small stone pedestal table with a blank playing card floating above it, three small stars around the card"),
]


# Round r02, after the independent review evidence/v10/art/ART_REVIEW_R1.md. The r01 subject of each id stays in
# its r01/prompt.txt; the reason for the change is recorded here and in ArtSource/Art10/README.md.
REVISED = {
    "C104": "r01 came out on a 1536x1024 canvas; fewer, larger dice so the subject fits the square canvas; red / blue shown as pip colour (STYLE_GUIDE 4.1). r02 was again 1536x1024, so r03 asks for a diagonal row",
    "C106": "REWORK C3: same subject as the shipped C93 (die against a standing mirror); the mirror motif is left to U11",
    "C111": "r01 came out on a 1536x1024 canvas; the shoulder pole is inherently wide, replaced by an upright backpack frame. r02 had amber at 17% (yellow bottles), so r03 names the bottle colours",
    "C118": "REWORK B4: the r01 subject asked for the underside, which produced an impossible die",
    "C139": "REWORK B2: gold was 42.5% of the picture; the volume moves from coins to the throne",
    "C140": "MINOR: playing-card suits; controllers are drawn as blank cards in this set. r02 carried a faint semi-transparent haze over a quarter of the canvas (lightFringeShare 0.86) and touched the top edge; r03 is a re-roll of the same subject",
    "C142": "MINOR: read as dice on a string like C104; fewer dice and a kite head",
    "C143": "REWORK C3: near-duplicate of the shipped C80 (two dice under a red bow); also a 1312x1199 canvas",
    "C149": "REWORK A3: subject only 39% of the canvas width; the blade now lies diagonally",
    "C153": "REWORK B4: red pips on a card whose mechanic has no colour (edit of r01, pips only)",
    "U11": "REWORK B4 / C1: impossible die and a caliper that read as measuring",
    "C134": "MINOR: a tag with a suit symbol on the bottle neck (edit of r01)",
    "C146": "MINOR: one ivory pip among ink pips on the red die (edit of r01)",
}
# Edit jobs: only one thing changes; the target is the r01 image of the same id (AI_ART_PIPELINE 3.4).
EDITS = {
    "C153": "change the pips of the rolling die from red to dark ink",
    "C134": "remove the hanging tag from the neck of the bottle",
    "C146": "make all three pips on the front face of the red die dark ink",
}


def job_for(content_id: str, subject: str) -> dict:
    job = {"jobId": "V10-ART-" + content_id, "category": "illustration", "contentId": content_id, "subject": subject}
    if content_id in EDITS:
        job["edit"] = {"target": f"ArtSource/AIConcepts/v10-illustrations/{content_id}/r01/original.png", "change": EDITS[content_id]}
    return job


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--rewrite", action="store_true")
    args = ap.parse_args()
    if args.list:
        print("\n".join(i for i, _ in SUBJECTS))
        return
    assert len(SUBJECTS) == 62 and len({i for i, _ in SUBJECTS}) == 62
    written = 0
    for content_id, subject in SUBJECTS:
        path = JOBS / content_id / "job.json"
        if path.exists() and not (args.rewrite and content_id in REVISED):
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        job = job_for(content_id, subject)
        path.write_text(json.dumps(job, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
        written += 1
    print("jobs written:", written, "of", len(SUBJECTS))


if __name__ == "__main__":
    main()
