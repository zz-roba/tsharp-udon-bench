# notes

- TsukimiSharp 0.10.1 as released (the package installed from the VPM listing archive, not a development build).
  VRChat Worlds SDK 3.10.4 (UdonSharp and ClientSim from it), Unity 2022.3.22f1, Play mode in the editor with
  ClientSim. The environment line in each result file was written by the driver.
- The project is a VRChat world project on the author's machine with `Assets/UdonBench` of this repository
  copied in (the same generated sources, driver and scene setup as `unity/`), not the `unity/` project itself.
- Machine: AMD Ryzen 5 7600X (6 cores, 12 threads), 64 GB RAM, Windows 11 (10.0.26200).
- 21 sessions back to back (2026-10-25, 02:15 to 02:55), each with 3 warm-up runs and 31 rounds per benchmark
  (each round runs both compilers once). As decided before the run, the first session after the editor
  started was dropped; the 20 sessions here are the rest. Nothing else heavy ran on the machine meanwhile.

- TsukimiSharp：出した 0.10.1（VPM の listing に載せた物そのもの。開発版ではない）。VRChat Worlds SDK 3.10.4（UdonSharp と ClientSim はこれに入っている物）・Unity 2022.3.22f1・エディターの Play（ClientSim）。各結果ファイルの環境の行はドライバーが書いた。
- プロジェクトは作者の機械の VRChat のワールドのプロジェクトで、このリポの `Assets/UdonBench` を写して使った（生成したソース・ドライバー・場面の作り方は `unity/` と同じ）。`unity/` のプロジェクトそのものではない。
- 機械：AMD Ryzen 5 7600X（6 コア 12 スレッド）・メモリ 64 GB・Windows 11（10.0.26200）。
- 計測は 21 回続けて回した（2026-10-25 の 02:15〜02:55）。各回はベンチマークごとにウォームアップ 3 回・31 ラウンド（各ラウンドで両方を 1 回ずつ）。回す前に決めたとおり、エディターを起こした後の最初の 1 回は捨て、残りの 20 回をここに置いた。その間、機械では重い仕事を走らせていない。
