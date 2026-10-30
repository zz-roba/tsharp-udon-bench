# notes

- TsukimiSharp 0.10.2 (a release build in the VPM listing archive format, not a development build). It was
  built from the same source as the released 0.10.2 under the pre-release version number 0.10.2-beta.1, which
  is the version the environment line in each result file shows; the released package differs only in the
  version number. VRChat Worlds SDK 3.10.4 (UdonSharp and ClientSim from it), Unity 2022.3.22f1, Play mode in
  the editor with ClientSim. The environment line in each result file was written by the driver.
- The project is a VRChat world project on the author's machine with `Assets/UdonBench` of this repository
  copied in (the same generated sources, driver and scene setup as `unity/`), not the `unity/` project itself.
  The TsukimiSharp programs were compiled again with this version before the run.
- Machine: AMD Ryzen 5 7600X (6 cores, 12 threads), 64 GB RAM, Windows 11 (10.0.26200). The same machine as
  `2026-10-25-0.10.1`.
- 21 sessions back to back (2026-10-29, 05:40 to 06:13), each with 3 warm-up runs and 31 rounds per benchmark
  (each round runs both compilers once). As decided before the run, the first session after the editor
  started was dropped; the 20 sessions here are the rest. Nothing else heavy ran on the machine meanwhile.
- Compare the milliseconds only within one folder. The UdonSharp column of this run is 4 to 6% faster than
  that of `2026-10-25-0.10.1` for every benchmark, although UdonSharp did not change: the state of the machine
  differs between days. The difference between the two compilers measured in the same session is the number
  to compare across runs.

- TsukimiSharp：0.10.2（VPM の listing の形に組んだ出荷用の物。開発版ではない）。出した 0.10.2 と同じソースから、出す前の版番号 0.10.2-beta.1 で組んだ物で、各結果ファイルの環境の行に出ている版はこの番号。出した 0.10.2 との違いは版番号だけ。VRChat Worlds SDK 3.10.4（UdonSharp と ClientSim はこれに入っている物）・Unity 2022.3.22f1・エディターの Play（ClientSim）。各結果ファイルの環境の行はドライバーが書いた。
- プロジェクトは作者の機械の VRChat のワールドのプロジェクトで、このリポの `Assets/UdonBench` を写して使った（生成したソース・ドライバー・場面の作り方は `unity/` と同じ）。`unity/` のプロジェクトそのものではない。計測の前に、TsukimiSharp のプログラムをこの版でコンパイルし直した。
- 機械：AMD Ryzen 5 7600X（6 コア 12 スレッド）・メモリ 64 GB・Windows 11（10.0.26200）。`2026-10-25-0.10.1` と同じ機械。
- 計測は 21 回続けて回した（2026-10-29 の 05:40〜06:13）。各回はベンチマークごとにウォームアップ 3 回・31 ラウンド（各ラウンドで両方を 1 回ずつ）。回す前に決めたとおり、エディターを起こした後の最初の 1 回は捨て、残りの 20 回をここに置いた。その間、機械では重い仕事を走らせていない。
- ミリ秒どうしを比べるのは、同じフォルダーの中だけにする。この計測の UdonSharp の列は、UdonSharp が変わっていないのに、どのベンチマークでも `2026-10-25-0.10.1` より 4〜6% 速い。日によって機械の状態が違うため。計測をまたいで比べるのは、同じ回の中で測った 2 つのコンパイラーの差の方。
