# results

One folder per measurement, named `<date>-<short label>` (the machine or the build):

- `result-<time>.json` (timing, one per session in Play mode; several sessions are pooled), as Unity wrote them.
  The `.txt` files may be added too.
- `charts/*.svg`, `README.md` and `README.ja.md`, written by
  `python3 tools/report.py results/<folder> --readme`.
- `notes.md` (optional): notes on the measurement (environment, versions). The folder's README links to it.

計測 1 回につきフォルダーを 1 つ（`<日付>-<短い名前>`。機械か版）作り、Unity が書いた `result-*.json`（何回分でも）を入れて、`python3 tools/report.py results/<フォルダー> --readme` を回します。グラフと表ができます。計測の覚え書き（環境・版）があれば `notes.md` に書くと、そのフォルダーの README からリンクされます。全部コミットします。
