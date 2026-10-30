# 2026-10-29-0.10.2

[日本語](README.ja.md)

20 sessions (`result-20261029-054206.json`, `result-20261029-054336.json`, `result-20261029-054511.json`, `result-20261029-054640.json`, `result-20261029-054807.json`, `result-20261029-054952.json`, `result-20261029-055126.json`, `result-20261029-055301.json`, `result-20261029-055436.json`, `result-20261029-055607.json`, `result-20261029-055741.json`, `result-20261029-055915.json`, `result-20261029-060050.json`, `result-20261029-060221.json`, `result-20261029-060355.json`, `result-20261029-060526.json`, `result-20261029-060659.json`, `result-20261029-060831.json`, `result-20261029-061001.json`, `result-20261029-061129.json`), each timing its benchmarks 31 times per compiler. Difference = (TsukimiSharp − UdonSharp) / UdonSharp of the time per Step, using the median of the timed runs (with several sessions, the median of the per-session medians; with two sessions, their mean).

Notes on this measurement (environment, versions): [notes.md](notes.md)

## Environment

- Unity 2022.3.22f1  com.vrchat.worlds 3.10.4  com.tsukimicode.udon-tools 0.10.2-beta.1  CPU AMD Ryzen 5 7600X 6-Core Processor  (12 threads)  RAM 64675 MB  OS Windows 11  (10.0.26200) 64bit

## Results

| Benchmark | UdonSharp ms/Step | TsukimiSharp ms/Step | Difference |
|:--|--:|--:|--:|
| Life | 2.602 | 1.963 | −24.6% |
| Fat | 0.465 | 0.099 | −78.8% |
| Fannkuch | 1.854 | 1.817 | −2.0% |
| NBody | 1.493 | 1.446 | −3.2% |
| Bfs | 1.585 | 1.497 | −5.5% |
| Sort | 1.260 | 1.243 | −1.4% |
| GrayScott | 3.812 | 3.081 | −19.2% |
| Sobel | 10.168 | 5.843 | −42.5% |
| Sieve | 3.394 | 2.688 | −20.8% |
| Cyclic | 7.841 | 4.508 | −42.5% |
| Fir | 2.562 | 1.974 | −23.0% |
| Rgb | 4.091 | 2.900 | −29.1% |
| Noise | 5.795 | 4.747 | −18.1% |
| Ripple | 6.508 | 0.907 | −86.1% |

## Charts

### Difference

![Difference](charts/difference.svg)

For each benchmark, how much less time a Step takes with TsukimiSharp than with UdonSharp. A bar left of 0 (green) means TsukimiSharp is faster; right of 0 (orange), slower; grey means the difference rounds to ±0.0%. The bar is computed from the median of the per-session medians; each black dot is one session. Dots close to the bar mean the sessions agree.

### Box plot (all benchmarks)

![Box plot](charts/boxplot.svg)

The timed runs, per benchmark, for UdonSharp (grey) and TsukimiSharp (blue). The benchmarks take very different times, so the axis is scaled so that the UdonSharp time per Step of each benchmark in the table is 100%. The boxes pool the timed runs of every session: the box holds the middle half of the runs, the thick line in it is the median, and the whiskers reach the farthest run within 1.5 box lengths beyond the box. Runs beyond the whiskers are not drawn; nearly all of them are slow runs, and some of them fall in the same round for both compilers (probably the editor or the OS; not investigated). A blue box clear of the grey box and to its left means TsukimiSharp is faster by more than the noise; overlapping boxes mean the difference is about the size of the noise. The number on the right is the TsukimiSharp time in the table as a percentage of UdonSharp (100% + the difference). Rows are sorted by the difference, the largest speed-up first.

### Box plot per benchmark

![Box plot per benchmark](charts/distribution.svg)

The same box plots, one per benchmark, in milliseconds, for reading the actual time of a Step. The number at the top right is the difference. Each benchmark has its own axis.

Every timed run is in the `result-*.json` files.
