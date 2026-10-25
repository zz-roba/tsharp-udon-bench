#!/usr/bin/env python3
"""Write the folded description, discussion, source and code of every benchmark into the README files.

    python3 tools/readme_bodies.py

Fills the part between <!-- bodies:start --> and <!-- bodies:end --> in README.md (English) and
README.ja.md (Japanese). The descriptions, discussions and sources come from tools/benchmarks.json; the code is
copied from bodies/<Name>.body.cs, so the README always shows the body that is measured.
tools/gen.py runs this at the end.
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ORDER = ["Life", "Fat", "Fannkuch", "NBody", "Bfs", "Sort", "GrayScott",
         "Sobel", "Sieve", "Cyclic", "Fir", "Rgb", "Noise", "Ripple"]
START, END = "<!-- bodies:start -->", "<!-- bodies:end -->"


def dedent(text):
    lines = text.rstrip("\n").split("\n")
    widths = [len(l) - len(l.lstrip(" ")) for l in lines if l.strip()]
    cut = min(widths) if widths else 0
    return "\n".join(l[cut:] if l.strip() else "" for l in lines)


def section(info, ja):
    out = []
    for name in ORDER:
        with open(os.path.join(ROOT, "bodies", name + ".body.cs"), encoding="utf-8") as f:
            code = dedent(f.read())
        d = info[name]
        desc, src = (d["ja"], d["source_ja"]) if ja else (d["en"], d["source_en"])
        note = d.get("analysis_ja" if ja else "analysis_en")
        out += ["<details>", "<summary><b>%s</b></summary>" % name, "", desc, ""]
        if note:
            out += [("考察: " if ja else "Discussion: ") + note, ""]
        out += [("出典: " if ja else "Source: ") + src, "",
                "`bodies/%s.body.cs`" % name, "", "```csharp", code, "```", "", "</details>", ""]
    return "\n".join(out).rstrip("\n")


def main():
    with open(os.path.join(ROOT, "tools", "benchmarks.json"), encoding="utf-8") as f:
        info = json.load(f)
    missing = [n for n in ORDER if n not in info]
    if missing:
        sys.exit("readme_bodies: no description for " + ", ".join(missing))
    for fname, ja in (("README.md", False), ("README.ja.md", True)):
        path = os.path.join(ROOT, fname)
        with open(path, encoding="utf-8") as f:
            text = f.read()
        if START not in text or END not in text:
            sys.exit("readme_bodies: %s has no %s ... %s" % (fname, START, END))
        body = section(info, ja)
        text = re.sub(re.escape(START) + ".*?" + re.escape(END),
                      lambda m: START + "\n" + body + "\n" + END, text, flags=re.S)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    print("wrote the benchmark sections of README.md and README.ja.md (%d benchmarks)" % len(ORDER))


if __name__ == "__main__":
    main()
