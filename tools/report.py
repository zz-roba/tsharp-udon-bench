#!/usr/bin/env python3
"""Turn the files of one measurement into charts and a summary.

    python3 tools/report.py results/<run>            # one folder under results/
    python3 tools/report.py results/<run> --readme   # also put the summary into the top README files

The folder holds the files Unity wrote to unity/Logs/UdonBench/:
    result-<time>.json   one per session, that is one Play mode run (several sessions are pooled)
    notes.md             optional notes on the measurement; the run README links to it

It writes, inside the folder:
    charts/difference.svg     time per Step, TsukimiSharp against UdonSharp, per benchmark
    charts/boxplot.svg        the timed runs, all benchmarks in one box plot (UdonSharp in the table = 100%)
    charts/distribution.svg   the timed runs, a box plot per benchmark in ms
    README.md / README.ja.md  the tables and the charts

Only the Python standard library is used. The output depends only on the input files, so
running it again on the same files gives the same bytes.
"""
import glob
import json
import os
import re
import statistics
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

ORDER = ["Life", "Fat", "Fannkuch", "NBody", "Bfs", "Sort", "GrayScott",
         "Sobel", "Sieve", "Cyclic", "Fir", "Rgb", "Noise", "Ripple"]
LANE = {"UdonSharp": "U", "U#": "U", "TsukimiSharp": "T", "T#": "T"}

COLOR_U = "#7a7f8c"
COLOR_T = "#2f7fbf"
COLOR_FASTER = "#2f9e6e"
COLOR_SLOWER = "#d0703a"
COLOR_EVEN = "#555555"
INK = "#222222"
LIGHT = {"#7a7f8c": "#dcdee3", "#2f7fbf": "#cde0f1"}   # box fills: solid, so every renderer shows the same
GRID = "#dddddd"
FONT = "font-family=\"Helvetica, Arial, sans-serif\""


def die(msg):
    sys.stderr.write("report: " + msg + "\n")
    sys.exit(1)


def f2(v):
    return ("%.2f" % v).rstrip("0").rstrip(".") if abs(v) < 1e6 else "%.0f" % v


def pct(v):
    if abs(v) < 0.05:
        return "±0.0%"
    return ("%+.1f%%" % v).replace("-", "−")


def diff_color(v):
    """Green when TsukimiSharp is faster, orange when slower, grey when pct() shows ±0.0%."""
    if abs(v) < 0.05:
        return COLOR_EVEN
    return COLOR_FASTER if v < 0 else COLOR_SLOWER


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


# ------------------------------------------------------------------ input

def load(folder):
    results = sorted(glob.glob(os.path.join(folder, "result-*.json")))
    if not results:
        die("no result-*.json in " + folder)
    runs = []
    for path in results:
        with open(path, encoding="utf-8-sig") as f:
            data = json.load(f)
        per = {}
        for row in data["rows"]:
            lane = LANE.get(row["lane"])
            if lane is None:
                die("%s: unknown lane %r" % (path, row["lane"]))
            steps = row["stepsPerRun"]
            per.setdefault(row["bench"], {})[lane] = {
                "ms": [m / steps for m in row["msRuns"]],
                "ok": bool(row.get("ok", False)),
                "steps": steps,
            }
        runs.append({"file": os.path.basename(path), "environment": data.get("environment", ""), "bench": per})

    return runs


def summarize(runs):
    names = [n for n in ORDER if any(n in r["bench"] for r in runs)]
    names += sorted({n for r in runs for n in r["bench"]} - set(names))
    out = []
    for name in names:
        pooled = {"U": [], "T": []}
        diffs = []
        ok = True
        steps = None
        for r in runs:
            b = r["bench"].get(name)
            if not b or "U" not in b or "T" not in b:
                continue
            for lane in ("U", "T"):
                pooled[lane] += b[lane]["ms"]
                ok = ok and b[lane]["ok"]
                steps = b[lane]["steps"]
            mu = statistics.median(b["U"]["ms"])
            mt = statistics.median(b["T"]["ms"])
            diffs.append((mt - mu) / mu * 100.0)
        if not diffs:
            continue
        medians_u = [statistics.median(r["bench"][name]["U"]["ms"]) for r in runs if name in r["bench"]]
        medians_t = [statistics.median(r["bench"][name]["T"]["ms"]) for r in runs if name in r["bench"]]
        mu = statistics.median(medians_u)
        mt = statistics.median(medians_t)
        out.append({
            "name": name, "steps": steps, "ok": ok,
            "u": mu, "t": mt, "diff": (mt - mu) / mu * 100.0, "diffs": diffs,
            "pooled": pooled,
        })
    return out


# ------------------------------------------------------------------ svg

class Svg:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.parts = []

    def add(self, s):
        self.parts.append(s)

    def rect(self, x, y, w, h, fill, extra=""):
        self.add('<rect x="%s" y="%s" width="%s" height="%s" fill="%s"%s/>' % (f2(x), f2(y), f2(max(w, 0)), f2(max(h, 0)), fill, extra))

    def line(self, x1, y1, x2, y2, stroke, width=1, extra=""):
        self.add('<line x1="%s" y1="%s" x2="%s" y2="%s" stroke="%s" stroke-width="%s"%s/>' % (f2(x1), f2(y1), f2(x2), f2(y2), stroke, f2(width), extra))

    def circle(self, x, y, r, fill, extra=""):
        self.add('<circle cx="%s" cy="%s" r="%s" fill="%s"%s/>' % (f2(x), f2(y), f2(r), fill, extra))

    def text(self, x, y, s, size=12, anchor="start", fill=INK, weight="normal"):
        self.add('<text x="%s" y="%s" font-size="%s" text-anchor="%s" fill="%s" font-weight="%s" %s>%s</text>'
                 % (f2(x), f2(y), size, anchor, fill, weight, FONT, esc(s)))

    def save(self, path):
        body = "\n".join(self.parts)
        svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">\n'
               '<rect width="100%%" height="100%%" fill="#ffffff"/>\n%s\n</svg>\n') % (self.w, self.h, self.w, self.h, body)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(svg)


def nice_step(span, target):
    raw = span / max(target, 1)
    mag = 10 ** int(("%e" % raw).split("e")[1])
    for m in (1, 2, 2.5, 5, 10):
        if raw <= m * mag:
            return m * mag
    return 10 * mag


def ticks(lo, hi, target=5):
    step = nice_step(hi - lo, target)
    start = (lo // step) * step
    out = []
    v = start
    while v <= hi + step * 1e-9:
        if v >= lo - step * 1e-9:
            out.append(round(v, 10))
        v += step
    return out


def chart_difference(rows, path, runs_count):
    left, right, top, row_h = 110, 70, 58, 26
    plot_w = 520
    h = top + row_h * len(rows) + 46
    w = left + plot_w + right
    lo = min([0.0] + [min(r["diffs"] + [r["diff"]]) for r in rows])
    hi = max([0.0] + [max(r["diffs"] + [r["diff"]]) for r in rows])
    pad = (hi - lo) * 0.16 or 5
    lo, hi = lo - pad, hi + pad
    tk = ticks(lo, hi, 6)
    lo, hi = min(lo, tk[0]), max(hi, tk[-1])
    sx = lambda v: left + (v - lo) / (hi - lo) * plot_w
    s = Svg(w, h)
    s.text(left, 24, "Time per Step: TsukimiSharp vs UdonSharp", 15, weight="bold")
    per_run = runs_per_session(rows)
    s.text(left, 42, "median of %d timed runs per compiler%s; left of 0 = TsukimiSharp is faster" % (
        per_run, "" if runs_count == 1 else " in each session"), 11, fill="#555555")
    for t in tk:
        x = sx(t)
        s.line(x, top - 6, x, top + row_h * len(rows), GRID)
        s.text(x, top + row_h * len(rows) + 16, pct(t).replace(".0%", "%"), 10, "middle", "#555555")
    s.line(sx(0), top - 6, sx(0), top + row_h * len(rows), INK, 1.2)
    for i, r in enumerate(rows):
        y = top + i * row_h
        cy = y + row_h / 2
        s.text(left - 10, cy + 4, r["name"], 12, "end")
        x0, x1 = sx(0), sx(r["diff"])
        color = diff_color(r["diff"])
        s.rect(min(x0, x1), y + 5, abs(x1 - x0), row_h - 10, color)
        if len(r["diffs"]) > 1:
            for d in r["diffs"]:
                s.circle(sx(d), cy, 2.6, INK, ' fill-opacity="0.75"')
        label = pct(r["diff"]) + ("" if r["ok"] else "  (checksum mismatch)")
        if r["diff"] < 0:
            s.text(sx(min(r["diffs"] + [r["diff"]])) - 7, cy + 4, label, 11, "end")
        else:
            s.text(sx(max(r["diffs"] + [r["diff"]])) + 7, cy + 4, label, 11, "start")
    note = ("bar = from the median of the per-session medians; dots = each session"
            if any(len(r["diffs"]) > 1 for r in rows) else "bar = from the median of the timed runs")
    s.text(left, h - 10, note, 10, fill="#555555")
    s.save(path)


def runs_per_session(rows):
    """Timed runs per compiler in one session (the same for every benchmark of a measurement)."""
    if not rows:
        return 0
    r = rows[0]
    return len(r["pooled"]["U"]) // max(len(r["diffs"]), 1)


def sessions_note(rows, ja):
    """Name how many sessions timed each benchmark, when that is not the same for all of them."""
    by = {}
    for r in rows:
        by.setdefault(len(r["diffs"]), []).append(r["name"])
    if len(by) < 2:
        return ""
    if ja:
        return "ベンチマークごとの計測の回数: " + "・".join(
            "%d 回（%s）" % (k, "・".join(by[k])) for k in sorted(by)) + "。"
    return "Sessions per benchmark: " + "; ".join(
        "%d (%s)" % (k, ", ".join(by[k])) for k in sorted(by)) + "."


def box_stats(data):
    """Tukey box: quartiles, whiskers to the farthest values within 1.5 IQR, the rest are outliers."""
    data = sorted(data)
    if len(data) == 1:
        v = data[0]
        return {"q1": v, "med": v, "q3": v, "lo": v, "hi": v, "out": []}
    q1, med, q3 = statistics.quantiles(data, n=4, method="inclusive")
    iqr = q3 - q1
    fence_lo, fence_hi = q1 - 1.5 * iqr, q3 + 1.5 * iqr
    inside = [v for v in data if fence_lo <= v <= fence_hi]
    return {"q1": q1, "med": med, "q3": q3, "lo": min(inside), "hi": max(inside),
            "out": [v for v in data if v < fence_lo or v > fence_hi]}


def draw_box(s, sx, y, half, b, color):
    s.line(sx(b["lo"]), y, sx(b["q1"]), y, color, 1.2)
    s.line(sx(b["q3"]), y, sx(b["hi"]), y, color, 1.2)
    s.line(sx(b["lo"]), y - half * 0.55, sx(b["lo"]), y + half * 0.55, color, 1.2)
    s.line(sx(b["hi"]), y - half * 0.55, sx(b["hi"]), y + half * 0.55, color, 1.2)
    s.rect(sx(b["q1"]), y - half, sx(b["q3"]) - sx(b["q1"]), half * 2, LIGHT[color], ' stroke="%s"' % color)
    s.line(sx(b["med"]), y - half, sx(b["med"]), y + half, color, 2.4)


def legend(s, x, y):
    s.rect(x, y - 9, 10, 10, COLOR_U)
    s.text(x + 15, y, "UdonSharp", 11)
    s.rect(x + 90, y - 9, 10, 10, COLOR_T)
    s.text(x + 105, y, "TsukimiSharp", 11)


BOX_NOTE = "box: middle half   line: median   whiskers: farthest run within 1.5 box lengths of the box (runs beyond are not drawn)"


def chart_distribution(rows, path):
    cols = 4
    pw, ph = 230, 118
    gx, gy = 18, 22
    margin_l, margin_t = 16, 64
    nrows = (len(rows) + cols - 1) // cols
    w = margin_l * 2 + cols * pw + (cols - 1) * gx
    h = margin_t + nrows * ph + (nrows - 1) * gy + 20
    s = Svg(w, h)
    s.text(margin_l, 24, "Timed runs, ms per Step", 15, weight="bold")
    s.text(margin_l, 42, BOX_NOTE, 11, fill="#555555")
    legend(s, w - margin_l - 190, 24)
    for i, r in enumerate(rows):
        cx0 = margin_l + (i % cols) * (pw + gx)
        cy0 = margin_t + (i // cols) * (ph + gy)
        s.rect(cx0, cy0, pw, ph, "#fafafa", ' stroke="#e4e4e4"')
        s.text(cx0 + 8, cy0 + 17, r["name"], 12, weight="bold")
        s.text(cx0 + pw - 8, cy0 + 17, pct(r["diff"]), 11, "end", diff_color(r["diff"]), "bold")
        boxes = {lane: box_stats(r["pooled"][lane]) for lane in ("U", "T")}
        lo = min(b["lo"] for b in boxes.values())
        hi = max(b["hi"] for b in boxes.values())
        span = (hi - lo) or hi * 0.1 or 1
        lo, hi = lo - span * 0.06, hi + span * 0.06
        tk = ticks(lo, hi, 4)
        px0, px1 = cx0 + 14, cx0 + pw - 14
        sx = lambda v, lo=lo, hi=hi, px0=px0, px1=px1: px0 + (v - lo) / (hi - lo) * (px1 - px0)
        base = cy0 + ph - 22
        for t in tk:
            if lo <= t <= hi:
                s.line(sx(t), cy0 + 26, sx(t), base, GRID)
                s.text(sx(t), base + 13, f2(t), 9, "middle", "#666666")
        for k, (lane, color) in enumerate((("U", COLOR_U), ("T", COLOR_T))):
            draw_box(s, sx, cy0 + 42 + k * 28, 9, boxes[lane], color)
    s.save(path)


def chart_boxplot(rows, path):
    """All benchmarks in one chart. Each run is divided by the UdonSharp time per Step of its benchmark
    in the table (r["u"]), so the number on the right (100 + difference) and the order of the rows
    (by the difference) match the table. The boxes themselves pool the timed runs of every session.

    Runs beyond the whiskers are not drawn; every run stays in the result files. So far they have all
    been slow runs, some of them in the same round for both compilers; the cause has not been
    investigated.
    """
    norm = []
    for r in rows:
        base = r["u"]
        n = {lane: [v / base * 100.0 for v in r["pooled"][lane]] for lane in ("U", "T")}
        norm.append((r, n, {lane: box_stats(n[lane]) for lane in ("U", "T")}))
    norm.sort(key=lambda e: (e[0]["diff"], e[0]["name"]))
    left, right, top, row_h = 100, 70, 76, 44
    plot_w = 560
    h = top + row_h * len(norm) + 44
    w = left + plot_w + right
    lo = min([100.0] + [b[lane]["lo"] for _, _, b in norm for lane in ("U", "T")])
    hi = max([100.0] + [b[lane]["hi"] for _, _, b in norm for lane in ("U", "T")])
    lo, hi = (lo // 10) * 10 - 5, (hi // 10 + 1) * 10 + 5
    sx = lambda v: left + (min(max(v, lo), hi) - lo) / (hi - lo) * plot_w
    s = Svg(w, h)
    s.text(left, 24, "Time per Step (UdonSharp = 100%, as in the table)", 15, weight="bold")
    s.text(left, 42, BOX_NOTE, 11, fill="#555555")
    legend(s, left, 62)
    bottom = top + row_h * len(norm)
    for i in range(1, len(norm), 2):
        s.rect(left, top + i * row_h, plot_w, row_h, "#f4f4f4")
    t = (lo // 10 + 1) * 10
    while t < hi:
        major = int(round(t)) % 50 == 0
        s.line(sx(t), top - 4, sx(t), bottom, "#c8c8c8" if major else "#e6e6e6")
        if major or (hi - lo) <= 80:
            s.text(sx(t), bottom + 16, "%d%%" % round(t), 10, "middle", "#555555")
        t += 10
    s.line(sx(100), top - 4, sx(100), bottom, INK, 1.4)
    for i, (r, n, b) in enumerate(norm):
        y = top + i * row_h
        s.text(left - 10, y + row_h / 2 + 4, r["name"], 13, "end")
        for k, (lane, color) in enumerate((("U", COLOR_U), ("T", COLOR_T))):
            cy = y + 13 + k * 18
            draw_box(s, sx, cy, 7, b[lane], color)
        s.text(left + plot_w + 64, y + row_h / 2 + 5, "%.0f%%" % (100.0 + r["diff"]), 13, "end",
               diff_color(r["diff"]), "bold")
    s.save(path)


# ------------------------------------------------------------------ markdown

def table(rows, ja):
    if ja:
        head = "| ベンチマーク | UdonSharp ms/Step | TsukimiSharp ms/Step | 差 |"
    else:
        head = "| Benchmark | UdonSharp ms/Step | TsukimiSharp ms/Step | Difference |"
    lines = [head, "|:--|--:|--:|--:|"]
    for r in rows:
        mark = "" if r["ok"] else (" （照合が不一致）" if ja else " (checksum mismatch)")
        lines.append("| %s | %.3f | %.3f | %s%s |" % (r["name"], r["u"], r["t"], pct(r["diff"]), mark))
    return "\n".join(lines)


def untimed(runs, rows, ja):
    """Lines naming the benchmarks of the list that are not in the table (none when every one is)."""
    timed = {r["name"] for r in rows}
    seen = {n for r in runs for n in r["bench"]}
    absent = [n for n in ORDER if n not in seen]
    one_lane = [n for n in ORDER + sorted(seen - set(ORDER)) if n in seen and n not in timed]
    out = []
    if absent:
        out += [("この計測に無いベンチマーク: %s。" % "・".join(absent)) if ja else
                ("Not in this measurement: %s." % ", ".join(absent)), ""]
    if one_lane:
        out += [("片方のコンパイラーでしか計時していないので表に無いベンチマーク: %s。" % "・".join(one_lane)) if ja else
                ("Timed with one compiler only, so not in the table: %s." % ", ".join(one_lane)), ""]
    return out


def environment(runs):
    envs = sorted({r["environment"] for r in runs if r["environment"]})
    return envs


def charts_with_guide(prefix, ja):
    """The three charts, each followed by how to read it."""
    if ja:
        return [
            "### 差", "",
            "![差](%scharts/difference.svg)" % prefix, "",
            "ベンチマークごとに、TsukimiSharp で 1 Step の時間が UdonSharp より何 % 短いか。"
            "棒が 0 より左（緑）なら TsukimiSharp が速く、右（橙）なら遅い。灰は差が ±0.0% に丸まる物。"
            "棒は回ごとの中央値の中央値から出した差で、黒い点が 1 回ずつの差。"
            "点が棒の近くに集まっていれば、回をまたいでも同じ差が出ている。", "",
            "### 箱ひげ図（全部のベンチマーク）", "",
            "![箱ひげ図](%scharts/boxplot.svg)" % prefix, "",
            "計時を、ベンチマークごとに UdonSharp（灰）と TsukimiSharp（青）に分けて描いた。"
            "ベンチマークによって時間の桁が違うので、横軸は表の UdonSharp の 1 Step あたりの時間を 100% にそろえてある。"
            "箱は全部の回の計時を混ぜて描き、真ん中の半分の計時を囲む。箱の中の太線は中央値、両側のひげは、箱の端から箱の幅の 1.5 倍の内側でいちばん遠い計時。"
            "それより外の計時（外れ値）は描いていない。ほとんどが遅い側で、両方のコンパイラーの同じラウンドに出ている物もある"
            "（エディターか OS によると見ているが、確かめていない）。"
            "青の箱が灰の箱から離れて左にあれば、揺れよりはっきり大きく TsukimiSharp が速い。箱が重なっていれば、差は揺れと同じくらい。"
            "右端の数は、表の TsukimiSharp の時間を UdonSharp に対する % で表した物（100% + 差）。"
            "上から差の順（TsukimiSharp が速い順）。", "",
            "### ベンチマークごとの箱ひげ図", "",
            "![ベンチマークごとの箱ひげ図](%scharts/distribution.svg)" % prefix, "",
            "上と同じ箱ひげ図を、ベンチマークごとに 1 枚ずつ、ミリ秒の軸で描いた。1 Step が実際に何ミリ秒かかるかを見るときに使う。"
            "右上の数は差。軸はベンチマークごとに違う。", "",
            "生の計時は `result-*.json` に全部入っている。", "",
        ]
    return [
        "### Difference", "",
        "![Difference](%scharts/difference.svg)" % prefix, "",
        "For each benchmark, how much less time a Step takes with TsukimiSharp than with UdonSharp. "
        "A bar left of 0 (green) means TsukimiSharp is faster; right of 0 (orange), slower; grey means "
        "the difference rounds to ±0.0%. The bar is computed from the median of the per-session medians; "
        "each black dot is one session. Dots close to the bar mean the sessions agree.", "",
        "### Box plot (all benchmarks)", "",
        "![Box plot](%scharts/boxplot.svg)" % prefix, "",
        "The timed runs, per benchmark, for UdonSharp (grey) and TsukimiSharp (blue). The benchmarks take "
        "very different times, so the axis is scaled so that the UdonSharp time per Step of each benchmark "
        "in the table is 100%. The boxes pool the timed runs of every session: the box holds the middle half "
        "of the runs, the thick line in it is the median, and the whiskers reach the farthest run within "
        "1.5 box lengths beyond the box. Runs beyond the whiskers are not drawn; nearly all of them are "
        "slow runs, and some of them fall in the same round for both compilers (probably the editor or the "
        "OS; not investigated). A blue box clear of the grey box and to its left means TsukimiSharp is "
        "faster by more than the noise; overlapping boxes mean the difference is about the size of the "
        "noise. The number on the right is the TsukimiSharp time in the table as a percentage of "
        "UdonSharp (100% + the difference). Rows are sorted by the difference, the largest speed-up first.", "",
        "### Box plot per benchmark", "",
        "![Box plot per benchmark](%scharts/distribution.svg)" % prefix, "",
        "The same box plots, one per benchmark, in milliseconds, for reading the actual time of a Step. "
        "The number at the top right is the difference. Each benchmark has its own axis.", "",
        "Every timed run is in the `result-*.json` files.", "",
    ]


def run_readme(folder, runs, rows, ja):
    name = os.path.basename(os.path.normpath(folder))
    n = runs_per_session(rows)
    notes = os.path.isfile(os.path.join(folder, "notes.md"))
    out = ["# " + name, ""]
    if ja:
        out += ["[English](README.md)", "",
                "%d 回の計測（%s）。各回で、その回のベンチマークをコンパイラーごとに %d 回ずつ計時。"
                "差 =（TsukimiSharp − UdonSharp）/ UdonSharp（1 Step あたりの時間。計時の中央値を使い、"
                "複数回のときは回ごとの中央値の中央値。2 回なら平均）。" % (
                    len(runs), "・".join("`%s`" % r["file"] for r in runs), n), ""]
        if sessions_note(rows, True):
            out += [sessions_note(rows, True), ""]
        if notes:
            out += ["この計測の覚え書き（環境・版）: [notes.md](notes.md)", ""]
        if environment(runs):
            out += ["## 環境", ""] + ["- " + e for e in environment(runs)] + [""]
        out += ["## 結果", "", table(rows, True), ""] + untimed(runs, rows, True)
        out += ["## グラフ", ""] + charts_with_guide("", True)
    else:
        out += ["[日本語](README.ja.md)", "",
                "%d session%s (%s), each timing its benchmarks %d times per compiler. "
                "Difference = (TsukimiSharp − UdonSharp) / UdonSharp of the time per Step, using the median "
                "of the timed runs (with several sessions, the median of the per-session medians; with two "
                "sessions, their mean)." % (
                    len(runs), "" if len(runs) == 1 else "s", ", ".join("`%s`" % r["file"] for r in runs), n), ""]
        if sessions_note(rows, False):
            out += [sessions_note(rows, False), ""]
        if notes:
            out += ["Notes on this measurement (environment, versions): [notes.md](notes.md)", ""]
        if environment(runs):
            out += ["## Environment", ""] + ["- " + e for e in environment(runs)] + [""]
        out += ["## Results", "", table(rows, False), ""] + untimed(runs, rows, False)
        out += ["## Charts", ""] + charts_with_guide("", False)
    path = os.path.join(folder, "README.ja.md" if ja else "README.md")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out))


MARK_START = "<!-- results:start -->"
MARK_END = "<!-- results:end -->"


def top_readme(folder, runs, rows, ja):
    path = os.path.join(ROOT, "README.ja.md" if ja else "README.md")
    with open(path, encoding="utf-8") as f:
        text = f.read()
    if MARK_START not in text or MARK_END not in text:
        die(path + ": the results markers are missing")
    rel = os.path.relpath(os.path.abspath(folder), ROOT).replace(os.sep, "/")
    if ja:
        body = ["最新: [`%s`](%s/README.ja.md)（環境と覚え書きはそちら）" % (rel, rel), "",
                "差 =（TsukimiSharp − UdonSharp）/ UdonSharp（1 Step あたりの時間）。"
                "−40% は 1 Step の時間が 0.6 倍になったこと。", "",
                table(rows, True), ""] + untimed(runs, rows, True) + charts_with_guide(rel + "/", True)
    else:
        body = ["Latest: [`%s`](%s/README.md) (environment and notes there)" % (rel, rel), "",
                "Difference = (TsukimiSharp − UdonSharp) / UdonSharp of the time per Step. "
                "−40% means a Step takes 0.6 times as long.", "",
                table(rows, False), ""] + untimed(runs, rows, False) + charts_with_guide(rel + "/", False)
    new = re.sub(re.escape(MARK_START) + ".*?" + re.escape(MARK_END),
                 lambda m: MARK_START + "\n" + "\n".join(body).rstrip("\n") + "\n" + MARK_END, text, flags=re.S)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new)


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        die("usage: python3 tools/report.py results/<run> [--readme]")
    folder = args[0]
    runs = load(folder)
    rows = summarize(runs)
    if not rows:
        die("no benchmark has both lanes")
    charts = os.path.join(folder, "charts")
    os.makedirs(charts, exist_ok=True)
    chart_difference(rows, os.path.join(charts, "difference.svg"), len(runs))
    chart_distribution(rows, os.path.join(charts, "distribution.svg"))
    chart_boxplot(rows, os.path.join(charts, "boxplot.svg"))
    for ja in (False, True):
        run_readme(folder, runs, rows, ja)
        if "--readme" in argv:
            top_readme(folder, runs, rows, ja)
    bad = [r["name"] for r in rows if not r["ok"]]
    if bad:
        sys.stderr.write("report: checksum mismatch in " + ", ".join(bad) + "\n")
    print("wrote %s (%d benchmarks, %d sessions)" % (charts, len(rows), len(runs)))


if __name__ == "__main__":
    main(sys.argv[1:])
