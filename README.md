# tsharp-udon-bench

[日本語](README.ja.md)

Benchmarks that compile the same C# code with UdonSharp and with TsukimiSharp (T#), run both on
Udon in the same Unity scene, and compare the times.

## Benchmarks

Fourteen benchmarks. "Steps" is the number of `Step()` calls in one timed run.

| Benchmark | One Step | Steps |
|:--|:--|--:|
| Life | Conway's Game of Life on a 32×32 torus (B3/S23), two buffers swapped | 30 |
| Fat | A 256-cell ring of bits; every cell recomputes a block of about 28 integer operations | 100 |
| Fannkuch | fannkuch-redux, n = 7, 240 permutations per Step (21 Steps = one full walk) | 21 |
| NBody | The Sun and four gas giants, `float` and `Vector3`, 100 substeps per Step | 30 |
| Bfs | Breadth-first search on a 32×32 grid with 28% walls, after flipping one cell | 30 |
| Sort | Iterative quicksort of 256 `int`s (Hoare partition, explicit stack) | 30 |
| GrayScott | Gray-Scott reaction-diffusion on a 32×32 torus in fixed point | 30 |
| Sobel | Sobel edge detection on a 48×48 grayscale image, blended back into the image | 10 |
| Sieve | Sieve of Eratosthenes up to 4096 on a `bool` array, counting primes and twin primes | 21 |
| Cyclic | Cyclic cellular automaton on a 32×32 torus (8 states), written with small helper methods | 10 |
| Fir | 5-tap FIR low-pass filter over a 1024-sample ring (int constants kept small so that TsukimiSharp unrolls the tap loop) | 30 |
| Rgb | Contrast and brightness on 32×32 packed 0xRRGGBB pixels, then a histogram | 30 |
| Noise | 2-D value noise in fixed point, two octaves | 10 |
| Ripple | Water ripples on a 32×32 grid with two buffers, written with small helper methods; a drop every fourth Step | 21 |

The bodies are in `bodies/<Name>.body.cs`. Open a benchmark below for what it does, where it comes from and its code.

<!-- bodies:start -->
<details>
<summary><b>Life</b></summary>

Conway's Game of Life (B3/S23) on a 32×32 torus whose edges wrap, starting from a random field (seed 12345). Each Step counts the 8 neighbours of every cell and swaps two buffers. Checksum: a position-weighted hash of the cells (the population is computed too, but not compared).

Discussion: The neighbour indices reuse the same products, such as `ym * n` in `ym * n + xm`. TsukimiSharp computes each repeated expression once and moves the products that do not change in the inner loop (`ym * n` and the like) out of it. Every arithmetic operation is an EXTERN call on Udon, so the saved multiplications show up directly in the time.

Source: Martin Gardner, "Mathematical Games: The fantastic combinations of John Conway's new solitaire game 'life'", Scientific American 223(4), October 1970, pp. 120–123.

`bodies/Life.body.cs`

```csharp
// Conway's Game of Life on a size x size torus (edges wrap).
// Cells are 0/1 in a flat int array; two buffers are swapped after each step.
public int size = 32;
public int seed = 12345;
public int checksum;
public int population;

int[] cur;
int[] nxt;

void Start()
{
    Init();
}

public void Init()
{
    int n = size;
    cur = new int[n * n];
    nxt = new int[n * n];
    int s = seed;
    for (int i = 0; i < n * n; i++)
    {
        s = (s * 75 + 74) % 65537;
        cur[i] = (s % 3 == 0) ? 1 : 0;
    }
    checksum = 0;
    population = 0;
}

public void Step()
{
    int n = size;
    for (int y = 0; y < n; y++)
    {
        int ym = (y + n - 1) % n;
        int yp = (y + 1) % n;
        for (int x = 0; x < n; x++)
        {
            int xm = (x + n - 1) % n;
            int xp = (x + 1) % n;
            int c = cur[ym * n + xm] + cur[ym * n + x] + cur[ym * n + xp]
                  + cur[y * n + xm] + cur[y * n + xp]
                  + cur[yp * n + xm] + cur[yp * n + x] + cur[yp * n + xp];
            int alive = cur[y * n + x];
            nxt[y * n + x] = (c == 3 || (c == 2 && alive == 1)) ? 1 : 0;
        }
    }
    int[] t = cur;
    cur = nxt;
    nxt = t;
}

public void ComputeChecksum()
{
    int n = size;
    int h = 0;
    int pop = 0;
    for (int i = 0; i < n * n; i++)
    {
        pop += cur[i];
        h = (h * 31 + cur[i] * (i % 1000 + 1)) % 1000003;
    }
    population = pop;
    checksum = h;
}
```

</details>

<details>
<summary><b>Fat</b></summary>

A ring of 256 bits. For every cell, the Step recomputes the same block of integer arithmetic that only reads fields which do not change during the Step, then sets the next bit from the neighbour and that block: next[i] = (cur[i + 1] + bit) & 1.

Discussion: The block only reads fields that do not change during the Step, so it could be moved out of the loop. On Udon, though, another behaviour can write a field by name with a value of a different type. An expression built from such a field, moved before the loop, would run even when the loop runs zero times and could stop the program where the original does not. TsukimiSharp currently moves such an expression out only when the field has already been read as its declared type before the loop. Here the block's fields are never read before the loop, so the block stays inside and the two compilers end up close.

Source: Written by the author.

`bodies/Fat.body.cs`

```csharp
// A one-dimensional ring of bits where every cell recomputes the same heavy
// arithmetic block. The block only reads fields that do not change during the
// step, so its value is the same for every cell (loop-invariant). The varying
// part is a single shifted read: next[i] = (cur[i + 1] + bit) & 1.
public int size = 256;
public int seed = 2024;
public int ka = 7;
public int kb = 13;
public int kc = 29;
public int kd = 101;
public int checksum;

int[] cur;
int[] nxt;

void Start()
{
    Init();
}

public void Init()
{
    int n = size;
    cur = new int[n];
    nxt = new int[n];
    int s = seed;
    for (int i = 0; i < n; i++)
    {
        s = (s * 75 + 74) % 65537;
        cur[i] = s & 1;
    }
    checksum = 0;
}

public void Step()
{
    int n = size;
    for (int i = 0; i < n; i++)
    {
        int a = ka * kb + kc;
        int b = (a ^ kd) + (ka - kc) * 3;
        int c = (b & 1023) * (kb | 5) - (kd >> 2);
        int d = (c + a) % 977 + (b ^ c) % 13;
        int e = d * d - a + (kc & kd);
        int f = (e >> 3) + (e & 7) * kb;
        int bit = (f + c - b) & 1;
        nxt[i] = (cur[(i + 1) % n] + bit) & 1;
    }
    int[] t = cur;
    cur = nxt;
    nxt = t;
}

public void ComputeChecksum()
{
    int n = size;
    int h = 0;
    for (int i = 0; i < n; i++)
    {
        h = (h * 31 + cur[i] * (i + 1)) % 1000003;
    }
    checksum = h;
}
```

</details>

<details>
<summary><b>Fannkuch</b></summary>

fannkuch-redux with n = 7. Walk every permutation of 0..6 in a fixed order; for each, keep reversing the first p[0] + 1 elements until p[0] is 0 and count the reversals. The checksum is the sum of the counts with alternating sign, and the largest count is kept too. One Step handles 240 permutations; a full walk of 7! takes 21 Steps and gives checksum 228 and largest count 16.

Discussion: Array reads and writes in short loops, with hardly any repeated expression or call to a small method. There is little for TsukimiSharp to remove, so the difference is small.

Source: The Computer Language Benchmarks Game, fannkuch-redux (https://benchmarksgame-team.pages.debian.net/benchmarksgame/description/fannkuchredux.html); Kenneth R. Anderson and Duane Rettig, "Performing Lisp Analysis of the FANNKUCH Benchmark", ACM SIGPLAN Lisp Pointers VII(4), 1994, pp. 2–12.

`bodies/Fannkuch.body.cs`

```csharp
// fannkuch-redux: walk every permutation of 0..n-1 in a fixed order; for each,
// keep reversing the first (p[0] + 1) elements until p[0] == 0 and count the
// reversals. checksum = sum of flip counts with alternating sign (even index +,
// odd index -), maxFlips = the largest count. One Step handles `chunk`
// permutations; when all n! are done the totals are published and the walk
// starts over.
public int n = 7;
public int chunk = 240;
public int checksum;
public int maxFlips;
public int cycles;

int[] perm1;
int[] perm;
int[] count;
int r;
int permIndex;
int sum;
int best;

void Start()
{
    Init();
}

public void Init()
{
    perm1 = new int[n];
    perm = new int[n];
    count = new int[n];
    ResetCycle();
    checksum = 0;
    maxFlips = 0;
    cycles = 0;
}

void ResetCycle()
{
    for (int i = 0; i < n; i++) perm1[i] = i;
    r = n;
    permIndex = 0;
    sum = 0;
    best = 0;
}

public void Step()
{
    for (int k = 0; k < chunk; k++)
    {
        while (r != 1)
        {
            count[r - 1] = r;
            r--;
        }

        for (int i = 0; i < n; i++) perm[i] = perm1[i];
        int flips = 0;
        int first = perm[0];
        while (first != 0)
        {
            int lo = 0;
            int hi = first;
            while (lo < hi)
            {
                int t = perm[lo];
                perm[lo] = perm[hi];
                perm[hi] = t;
                lo++;
                hi--;
            }
            flips++;
            first = perm[0];
        }
        if (flips > best) best = flips;
        if (permIndex % 2 == 0) sum += flips;
        else sum -= flips;

        bool finished = false;
        while (true)
        {
            if (r == n)
            {
                finished = true;
                break;
            }
            int p0 = perm1[0];
            for (int i = 0; i < r; i++) perm1[i] = perm1[i + 1];
            perm1[r] = p0;
            count[r] = count[r] - 1;
            if (count[r] > 0) break;
            r++;
        }
        permIndex++;

        if (finished)
        {
            checksum = sum;
            maxFlips = best;
            cycles++;
            ResetCycle();
        }
    }
}

public void ComputeChecksum()
{
    // checksum and maxFlips are published when a full walk ends (see Step).
}
```

</details>

<details>
<summary><b>NBody</b></summary>

The Sun and the four gas giants with float and Vector3, symplectic Euler with dt = 0.01, 100 substeps per Step. The initial positions, velocities and masses are those of the Benchmarks Game n-body, rounded to float. Checksum: the total energy (compared with an absolute tolerance of 1e-4).

Discussion: Almost every operation here is one EXTERN call (Vector3 operators, float arithmetic, array reads and writes, Mathf.Sqrt). Both compilers make the same calls the same number of times, so there is hardly any difference.

Source: The Computer Language Benchmarks Game, n-body (https://benchmarksgame-team.pages.debian.net/benchmarksgame/description/nbody.html).

`bodies/NBody.body.cs`

```csharp
// n-body: the Sun and the four gas giants, symplectic Euler with dt = 0.01
// (units: AU, days/365.24, solar masses scaled so G = 1). float + Vector3.
// One Step advances `substeps` times. checksum = total energy.
public int substeps = 100;
public float dt = 0.01f;
public float checksum;

Vector3[] pos;
Vector3[] vel;
float[] mass;

void Start()
{
    Init();
}

public void Init()
{
    float pi = 3.141592653589793f;
    float solarMass = 4f * pi * pi;
    float daysPerYear = 365.24f;
    pos = new Vector3[5];
    vel = new Vector3[5];
    mass = new float[5];

    pos[0] = Vector3.zero;
    vel[0] = Vector3.zero;
    mass[0] = solarMass;

    pos[1] = new Vector3(4.84143144246472090e+00f, -1.16032004402742839e+00f, -1.03622044471123109e-01f);
    vel[1] = new Vector3(1.66007664274403694e-03f, 7.69901118419740425e-03f, -6.90460016972063023e-05f) * daysPerYear;
    mass[1] = 9.54791938424326609e-04f * solarMass;

    pos[2] = new Vector3(8.34336671824457987e+00f, 4.12479856412430479e+00f, -4.03523417114321381e-01f);
    vel[2] = new Vector3(-2.76742510726862411e-03f, 4.99852801234917238e-03f, 2.30417297573763929e-05f) * daysPerYear;
    mass[2] = 2.85885980666130812e-04f * solarMass;

    pos[3] = new Vector3(1.28943695621391310e+01f, -1.51111514016986312e+01f, -2.23307578892655734e-01f);
    vel[3] = new Vector3(2.96460137564761618e-03f, 2.37847173959480950e-03f, -2.96589568540237556e-05f) * daysPerYear;
    mass[3] = 4.36624404335156298e-05f * solarMass;

    pos[4] = new Vector3(1.53796971148509165e+01f, -2.59193146099879641e+01f, 1.79258772950371181e-01f);
    vel[4] = new Vector3(2.68067772490389322e-03f, 1.62824170038242295e-03f, -9.51592254519715870e-05f) * daysPerYear;
    mass[4] = 5.15138902046611451e-05f * solarMass;

    // Give the Sun the momentum that makes the total zero.
    Vector3 p = Vector3.zero;
    for (int i = 0; i < 5; i++) p = p + vel[i] * mass[i];
    vel[0] = p * (-1f / solarMass);
    checksum = 0f;
}

public void Step()
{
    for (int s = 0; s < substeps; s++)
    {
        for (int i = 0; i < 5; i++)
        {
            for (int j = i + 1; j < 5; j++)
            {
                Vector3 d = pos[i] - pos[j];
                float d2 = d.sqrMagnitude;
                float mag = dt / (d2 * Mathf.Sqrt(d2));
                vel[i] = vel[i] - d * (mass[j] * mag);
                vel[j] = vel[j] + d * (mass[i] * mag);
            }
        }
        for (int i = 0; i < 5; i++)
        {
            pos[i] = pos[i] + vel[i] * dt;
        }
    }
}

public void ComputeChecksum()
{
    float e = 0f;
    for (int i = 0; i < 5; i++)
    {
        e += 0.5f * mass[i] * vel[i].sqrMagnitude;
        for (int j = i + 1; j < 5; j++)
        {
            Vector3 d = pos[i] - pos[j];
            e -= mass[i] * mass[j] / Mathf.Sqrt(d.sqrMagnitude);
        }
    }
    checksum = e;
}
```

</details>

<details>
<summary><b>Bfs</b></summary>

A 32×32 grid with 28% walls (seed 4242). Each Step flips one cell between wall and floor (never the start or the goal), then runs a breadth-first search over the 4-neighbourhood from the top-left cell, with an int array as the queue. The distance to the bottom-right cell and the number of reached cells are folded into a hash.

Discussion: Both compilers make nearly the same EXTERN calls; most of the difference is in the instructions that move values around (PUSH, COPY). TsukimiSharp writes the result of an EXTERN call straight into the local variable and moves `width - 1` and `height - 1` out of the loop. The gain is small.

Source: E. F. Moore, "The shortest path through a maze", Proceedings of an International Symposium on the Theory of Switching, Part II (Annals of the Computation Laboratory of Harvard University 30), Harvard University Press, 1959, pp. 285–292.

`bodies/Bfs.body.cs`

```csharp
// Breadth-first search on a width x height grid with walls, from the top-left
// cell to every reachable cell (4-neighbourhood). Each Step first flips one
// cell between wall and floor (never the start or the goal), then searches.
// The distance to the bottom-right cell and the number of reached cells are
// folded into a running hash.
public int width = 32;
public int height = 32;
public int seed = 4242;
public int wallPercent = 28;
public int checksum;
public int lastDistance;

int[] wall;
int[] dist;
int[] queue;
int rng;
int acc;

void Start()
{
    Init();
}

int NextRandom()
{
    rng = (rng * 75 + 74) % 65537;
    return rng;
}

public void Init()
{
    int n = width * height;
    wall = new int[n];
    dist = new int[n];
    queue = new int[n];
    rng = seed;
    for (int i = 0; i < n; i++)
    {
        wall[i] = (NextRandom() % 100 < wallPercent) ? 1 : 0;
    }
    // Keep the start, the goal and their neighbours open.
    wall[0] = 0;
    wall[1] = 0;
    wall[width] = 0;
    wall[n - 1] = 0;
    wall[n - 2] = 0;
    wall[n - 1 - width] = 0;
    acc = 0;
    checksum = 0;
    lastDistance = 0;
}

public void Step()
{
    int n = width * height;
    int flip = NextRandom() % n;
    if (flip != 0 && flip != n - 1) wall[flip] = 1 - wall[flip];

    for (int i = 0; i < n; i++) dist[i] = -1;
    int head = 0;
    int tail = 0;
    dist[0] = 0;
    queue[tail] = 0;
    tail++;
    while (head < tail)
    {
        int c = queue[head];
        head++;
        int cx = c % width;
        int cy = c / width;
        int nd = dist[c] + 1;
        if (cx > 0)
        {
            int nb = c - 1;
            if (wall[nb] == 0 && dist[nb] < 0) { dist[nb] = nd; queue[tail] = nb; tail++; }
        }
        if (cx < width - 1)
        {
            int nb = c + 1;
            if (wall[nb] == 0 && dist[nb] < 0) { dist[nb] = nd; queue[tail] = nb; tail++; }
        }
        if (cy > 0)
        {
            int nb = c - width;
            if (wall[nb] == 0 && dist[nb] < 0) { dist[nb] = nd; queue[tail] = nb; tail++; }
        }
        if (cy < height - 1)
        {
            int nb = c + width;
            if (wall[nb] == 0 && dist[nb] < 0) { dist[nb] = nd; queue[tail] = nb; tail++; }
        }
    }
    lastDistance = dist[n - 1];
    acc = (acc * 31 + (lastDistance + 1) * 1009 + tail) % 1000003;
}

public void ComputeChecksum()
{
    checksum = acc;
}
```

</details>

<details>
<summary><b>Sort</b></summary>

Fill 256 ints with pseudo-random values, then sort them in place with an iterative quicksort: Hoare partition, the middle element as the pivot, an explicit stack, the smaller side first. A sortedness check and a few sampled elements are folded into a hash.

Discussion: The sort is already written the hand-tuned way: an explicit stack instead of recursion and the partition written inline in the loop. With few repeated expressions and no method calls, there is little for TsukimiSharp to remove, and both run at the same speed.

Source: C. A. R. Hoare, "Quicksort", The Computer Journal 5(1), 1962, pp. 10–16.

`bodies/Sort.body.cs`

```csharp
// Fill an int array with pseudo-random values, then sort it in place with an
// iterative quicksort (Hoare partition, middle element as pivot, explicit
// stack, smaller side first). A few sampled elements and a sortedness check
// are folded into a running hash.
public int length = 256;
public int seed = 777;
public int checksum;

int[] a;
int[] stack;
int rng;
int acc;

void Start()
{
    Init();
}

public void Init()
{
    a = new int[length];
    stack = new int[128];
    rng = seed;
    acc = 0;
    checksum = 0;
}

public void Step()
{
    int n = length;
    for (int f = 0; f < n; f++)
    {
        rng = (rng * 75 + 74) % 65537;
        a[f] = rng;
    }

    int sp = 0;
    stack[0] = 0;
    stack[1] = n - 1;
    sp = 2;
    while (sp > 0)
    {
        sp -= 2;
        int lo = stack[sp];
        int hi = stack[sp + 1];
        if (lo >= hi) continue;

        int pivot = a[(lo + hi) / 2];
        int i = lo - 1;
        int j = hi + 1;
        while (true)
        {
            i++;
            while (a[i] < pivot) i++;
            j--;
            while (a[j] > pivot) j--;
            if (i >= j) break;
            int t = a[i];
            a[i] = a[j];
            a[j] = t;
        }

        // [lo, j] and [j + 1, hi]: push the larger range first so the smaller is handled next.
        if (j - lo < hi - j - 1)
        {
            stack[sp] = j + 1; stack[sp + 1] = hi; sp += 2;
            stack[sp] = lo; stack[sp + 1] = j; sp += 2;
        }
        else
        {
            stack[sp] = lo; stack[sp + 1] = j; sp += 2;
            stack[sp] = j + 1; stack[sp + 1] = hi; sp += 2;
        }
    }

    int bad = 0;
    for (int k = 0; k + 1 < n; k++)
    {
        if (a[k] > a[k + 1]) bad++;
    }
    acc = (acc * 31 + a[n / 2] + a[n / 4] * 7 + bad * 100000) % 1000003;
}

public void ComputeChecksum()
{
    checksum = acc;
}
```

</details>

<details>
<summary><b>GrayScott</b></summary>

Gray-Scott reaction-diffusion on a 32×32 torus in fixed point (4096 = 1.0, so every value is an int and the result is the same bit for bit everywhere), 5-point Laplacian, dt = 1, Du = 0.16, Dv = 0.08, F = 0.035, k = 0.065, values kept in [0, 1].

Discussion: As in Life, the 5-point Laplacian reuses the same index products. TsukimiSharp computes them once and moves the ones that do not change in the inner loop out of it.

Source: John E. Pearson, "Complex Patterns in a Simple System", Science 261(5118), 1993, pp. 189–192.

`bodies/GrayScott.body.cs`

```csharp
// Gray-Scott reaction-diffusion on a size x size torus in fixed point
// (4096 = 1.0, so every value is an int and the result is the same bit for
// bit everywhere).
// du/dt = Du * lap(u) - u*v*v + F * (1 - u)
// dv/dt = Dv * lap(v) + u*v*v - (F + k) * v      (dt = 1, 5-point Laplacian)
public int size = 32;
public int diffU = 655;   // 0.16
public int diffV = 328;   // 0.08
public int feed = 143;    // 0.035
public int kill = 266;    // 0.065
public int checksum;

int[] u;
int[] v;
int[] u2;
int[] v2;

void Start()
{
    Init();
}

public void Init()
{
    int n = size;
    u = new int[n * n];
    v = new int[n * n];
    u2 = new int[n * n];
    v2 = new int[n * n];
    for (int i = 0; i < n * n; i++)
    {
        u[i] = 4096;
        v[i] = 0;
    }
    int lo = n / 2 - 4;
    int hi = n / 2 + 4;
    for (int y = lo; y < hi; y++)
    {
        for (int x = lo; x < hi; x++)
        {
            u[y * n + x] = 2048;
            v[y * n + x] = 1024 + (x * 37 + y * 11) % 256;
        }
    }
    checksum = 0;
}

public void Step()
{
    int n = size;
    for (int y = 0; y < n; y++)
    {
        int ym = (y + n - 1) % n;
        int yp = (y + 1) % n;
        for (int x = 0; x < n; x++)
        {
            int xm = (x + n - 1) % n;
            int xp = (x + 1) % n;
            int i = y * n + x;
            int uc = u[i];
            int vc = v[i];
            int lu = u[ym * n + x] + u[yp * n + x] + u[y * n + xm] + u[y * n + xp] - 4 * uc;
            int lv = v[ym * n + x] + v[yp * n + x] + v[y * n + xm] + v[y * n + xp] - 4 * vc;
            int uv = (uc * vc) >> 12;
            int uvv = (uv * vc) >> 12;
            int nu = uc + ((diffU * lu) >> 12) - uvv + ((feed * (4096 - uc)) >> 12);
            int nv = vc + ((diffV * lv) >> 12) + uvv - (((feed + kill) * vc) >> 12);
            if (nu < 0) nu = 0;
            if (nu > 4096) nu = 4096;
            if (nv < 0) nv = 0;
            if (nv > 4096) nv = 4096;
            u2[i] = nu;
            v2[i] = nv;
        }
    }
    int[] t = u;
    u = u2;
    u2 = t;
    t = v;
    v = v2;
    v2 = t;
}

public void ComputeChecksum()
{
    int n = size;
    int h = 0;
    for (int i = 0; i < n * n; i++)
    {
        h = (h * 31 + v[i] + u[i] * 3) % 1000003;
    }
    checksum = h;
}
```

</details>

<details>
<summary><b>Sobel</b></summary>

Sobel edge detection on a 48×48 grayscale image (0..255). Each Step computes |gx| + |gy|, capped at 255, for every inner pixel, then blends the edges back into the image ((img × 3 + edge) / 4) so that the next Step sees a different picture.

Discussion: Written plainly, the 3×3 neighbourhood repeats index expressions such as `(y - 1) * w` and `x - 1` many times per pixel. UdonSharp computes every one as written; TsukimiSharp computes each once and moves the row products out of the inner loop. In the compiled programs, the body of the inner loop contains 17 multiplications (82 EXTERN calls in all) with UdonSharp and 4 (44 in all) with TsukimiSharp. Every addition and multiplication is an EXTERN call on Udon, so this is where most of the time goes.

Source: Irwin Sobel and Gary Feldman, "A 3×3 Isotropic Gradient Operator for Image Processing", presented at the Stanford Artificial Intelligence Project, 1968.

`bodies/Sobel.body.cs`

```csharp
// Sobel edge detection on a size x size grayscale image (values 0..255).
// Each Step computes the edge magnitude |gx| + |gy| (capped at 255) for every
// inner pixel, then blends the edges back into the image so that the next
// Step sees a different picture. Border pixels of the edge image stay 0.
public int size = 48;
public int seed = 777;
public int checksum;

int[] img;
int[] edge;

void Start()
{
    Init();
}

public void Init()
{
    int n = size;
    img = new int[n * n];
    edge = new int[n * n];
    int s = seed;
    for (int i = 0; i < img.Length; i++)
    {
        s = (s * 75 + 74) % 65537;
        img[i] = s % 256;
    }
    checksum = 0;
}

public void Step()
{
    int w = size;
    for (int y = 1; y < w - 1; y++)
    {
        for (int x = 1; x < w - 1; x++)
        {
            int gx = -img[(y - 1) * w + (x - 1)] + img[(y - 1) * w + (x + 1)]
                     - 2 * img[y * w + (x - 1)] + 2 * img[y * w + (x + 1)]
                     - img[(y + 1) * w + (x - 1)] + img[(y + 1) * w + (x + 1)];
            int gy = -img[(y - 1) * w + (x - 1)] - 2 * img[(y - 1) * w + x] - img[(y - 1) * w + (x + 1)]
                     + img[(y + 1) * w + (x - 1)] + 2 * img[(y + 1) * w + x] + img[(y + 1) * w + (x + 1)];
            if (gx < 0) gx = -gx;
            if (gy < 0) gy = -gy;
            int m = gx + gy;
            if (m > 255) m = 255;
            edge[y * w + x] = m;
        }
    }
    for (int i = 0; i < img.Length; i++)
    {
        img[i] = (img[i] * 3 + edge[i]) / 4;
    }
}

public void ComputeChecksum()
{
    int h = 0;
    for (int i = 0; i < img.Length; i++)
    {
        h = (h * 31 + img[i] * (i % 1000 + 1)) % 1000003;
    }
    checksum = h;
}
```

</details>

<details>
<summary><b>Sieve</b></summary>

The sieve of Eratosthenes on a bool array of 4096 entries, then a walk over the primes that counts them, counts the twin primes and hashes their positions. Each Step runs the whole sieve again; the hash carries over from Step to Step.

Discussion: Every loop condition reads `composite.Length`, and UdonSharp reads `.Length` with an EXTERN call on every pass (the compiled program has four such calls, one in each loop condition). The array does not change inside the loops, so TsukimiSharp reads `.Length` once, before the first loop.

Source: The sieve of Eratosthenes (Eratosthenes of Cyrene, 3rd century BC), as described in Nicomachus of Gerasa, Introduction to Arithmetic, Book I, ch. 13 (trans. Martin Luther D'Ooge, Macmillan, 1926, pp. 204–206).

`bodies/Sieve.body.cs`

```csharp
// The sieve of Eratosthenes over a bool array, then a walk over the primes
// that counts them, counts twin primes, and hashes their positions.
// Each Step runs the whole sieve again; the hash carries over from Step to Step.
public int limit = 4096;
public int checksum;
public int primeCount;

bool[] composite;
int acc;
int round;

void Start()
{
    Init();
}

public void Init()
{
    composite = new bool[limit];
    acc = 0;
    round = 0;
    checksum = 0;
    primeCount = 0;
}

public void Step()
{
    for (int i = 0; i < composite.Length; i++)
    {
        composite[i] = false;
    }
    composite[0] = true;
    composite[1] = true;
    for (int i = 2; i * i < composite.Length; i++)
    {
        if (!composite[i])
        {
            for (int j = i * i; j < composite.Length; j += i)
            {
                composite[j] = true;
            }
        }
    }
    int count = 0;
    int twins = 0;
    int h = round;
    for (int i = 2; i < composite.Length; i++)
    {
        if (!composite[i])
        {
            count++;
            if (i > 2 && !composite[i - 2])
            {
                twins++;
            }
            h = (h * 31 + i) % 1000003;
        }
    }
    primeCount = count;
    acc = (acc * 7 + count * 13 + twins * 5 + h) % 1000003;
    round++;
}

public void ComputeChecksum()
{
    checksum = acc;
}
```

</details>

<details>
<summary><b>Cyclic</b></summary>

A cyclic cellular automaton on a 32×32 torus: 8 states, 8 neighbours, threshold 2. A cell moves on to the next state (wrapping to 0) when at least 2 of its neighbours already hold that state. Written with small helper methods (Wrap, At, CountState).

Discussion: The rule is written with three small methods (Wrap, At, CountState), 28 calls per cell. A method call in UdonSharp copies the arguments, pushes and pops the return address and jumps: 8 to 14 instructions per call in the compiled program, depending on the number of arguments. TsukimiSharp inlines all three, so the cost of the calls disappears.

Source: Robert Fisch, Janko Gravner and David Griffeath, "Cyclic cellular automata in two dimensions", in K. S. Alexander and J. C. Watkins (eds.), Spatial Stochastic Processes (Progress in Probability 19), Birkhäuser, 1991, pp. 171–185.

`bodies/Cyclic.body.cs`

```csharp
// A cyclic cellular automaton on a size x size torus: every cell holds a
// state 0..states-1 and moves on to the next state (wrapping to 0) when at
// least `threshold` of its 8 neighbours already hold that next state.
// Written with small helper methods (Wrap, At, CountState), the way the
// rule is usually spelled out.
public int size = 32;
public int states = 8;
public int threshold = 2;
public int seed = 31337;
public int checksum;

int[] cur;
int[] nxt;

void Start()
{
    Init();
}

public void Init()
{
    int n = size;
    cur = new int[n * n];
    nxt = new int[n * n];
    int s = seed;
    for (int i = 0; i < cur.Length; i++)
    {
        s = (s * 75 + 74) % 65537;
        cur[i] = s % states;
    }
    checksum = 0;
}

int Wrap(int v)
{
    return (v + size) % size;
}

int At(int x, int y)
{
    return cur[Wrap(y) * size + Wrap(x)];
}

int CountState(int x, int y, int target)
{
    int c = 0;
    if (At(x - 1, y - 1) == target) c++;
    if (At(x, y - 1) == target) c++;
    if (At(x + 1, y - 1) == target) c++;
    if (At(x - 1, y) == target) c++;
    if (At(x + 1, y) == target) c++;
    if (At(x - 1, y + 1) == target) c++;
    if (At(x, y + 1) == target) c++;
    if (At(x + 1, y + 1) == target) c++;
    return c;
}

public void Step()
{
    int n = size;
    for (int y = 0; y < n; y++)
    {
        for (int x = 0; x < n; x++)
        {
            int s = At(x, y);
            int next = (s + 1) % states;
            nxt[y * n + x] = CountState(x, y, next) >= threshold ? next : s;
        }
    }
    int[] t = cur;
    cur = nxt;
    nxt = t;
}

public void ComputeChecksum()
{
    int h = 0;
    for (int i = 0; i < cur.Length; i++)
    {
        h = (h * 31 + cur[i] * (i % 1000 + 1)) % 1000003;
    }
    checksum = h;
}
```

</details>

<details>
<summary><b>Fir</b></summary>

A 5-tap FIR low-pass filter with the binomial weights 1 4 6 4 1 (sum 16) over a ring buffer of 1024 int samples. Each Step writes 64 new noise samples into the ring, then filters the whole ring into a second array.

Discussion: The inner loop over the 5 taps has a constant count. TsukimiSharp unrolls it, which removes the comparison and the `k++` of every pass. This unrolling is skipped when the program contains an int constant at least as large as its code size in bytes, so this benchmark keeps its int constants small (see the comment in the body).

Source: Written by the author.

`bodies/Fir.body.cs`

```csharp
// A 5-tap FIR low-pass filter (binomial weights 1 4 6 4 1, sum 16)
// over a ring buffer of integer samples, the way audio-style code is written.
// Each Step writes a block of new noise samples into the ring, then filters
// the whole ring into filtered[].
// Every int constant is kept small on purpose (PRNG modulus 1021, hash moduli
// 1009 and 997): the TsukimiSharp optimizer unrolls the 5-tap loop only when no int
// constant in the program is as large as the program's code size in bytes.
public int length = 1024;
public int block = 64;
public int seed = 99;
public int checksum;

int[] samples;
int[] filtered;
int[] taps;
int head;
int rng;

void Start()
{
    Init();
}

public void Init()
{
    samples = new int[length];
    filtered = new int[length];
    taps = new int[5];
    taps[0] = 1;
    taps[1] = 4;
    taps[2] = 6;
    taps[3] = 4;
    taps[4] = 1;
    rng = seed;
    head = 0;
    for (int i = 0; i < samples.Length; i++)
    {
        rng = (rng * 75 + 74) % 1021;
        samples[i] = rng % 201 - 100;
    }
    checksum = 0;
}

public void Step()
{
    int n = samples.Length;
    for (int i = 0; i < block; i++)
    {
        rng = (rng * 75 + 74) % 1021;
        samples[(head + i) % n] = rng % 201 - 100;
    }
    head = (head + block) % n;
    for (int i = 0; i < n; i++)
    {
        int acc = 0;
        for (int k = 0; k < 5; k++)
        {
            acc += taps[k] * samples[(i + k) % n];
        }
        filtered[i] = acc / 16;
    }
}

public void ComputeChecksum()
{
    int h1 = 0;
    int h2 = 0;
    for (int i = 0; i < filtered.Length; i++)
    {
        int v = filtered[i] + 100;
        h1 = (h1 * 31 + v * (i % 100 + 1)) % 1009;
        h2 = (h2 * 37 + v) % 997;
    }
    checksum = h1 * 1000 + h2;
}
```

</details>

<details>
<summary><b>Rgb</b></summary>

Contrast and brightness on 32×32 packed 0xRRGGBB pixels, written with small helpers (Channel and Adjust take the channel number as an argument; Clamp255 and Pack do not). The brightness shift changes from Step to Step. Each Step also builds a histogram of the gray values.

Discussion: When TsukimiSharp inlines the helpers that take the channel number as a literal (Channel, Adjust), expressions such as `8 * c` and `3 + c` fold into constants, which removes EXTERN calls per pixel. Clamp255 and Pack are not inlined and stay as calls.

Source: Written by the author.

`bodies/Rgb.body.cs`

```csharp
// Colour adjustment on packed 0xRRGGBB pixels, written with small helpers
// (Channel and Adjust take the channel number as an argument, Clamp255 and
// Pack do not; channel 0 = blue, 1 = green, 2 = red). Each Step lowers the contrast
// a little and shifts the brightness per channel (the shift changes from
// Step to Step), writes the pixels back, and builds a histogram of the gray values.
public int size = 32;
public int seed = 4711;
public int checksum;

int[] pixels;
int[] hist;
int round;

void Start()
{
    Init();
}

public void Init()
{
    int n = size;
    pixels = new int[n * n];
    hist = new int[256];
    int s = seed;
    for (int i = 0; i < pixels.Length; i++)
    {
        s = (s * 75 + 74) % 65537;
        int r = s % 256;
        s = (s * 75 + 74) % 65537;
        int g = s % 256;
        s = (s * 75 + 74) % 65537;
        int b = s % 256;
        pixels[i] = (r << 16) | (g << 8) | b;
    }
    round = 0;
    checksum = 0;
}

int Channel(int rgb, int c)
{
    return (rgb >> (8 * c)) & 255;
}

int Clamp255(int v)
{
    if (v < 0) return 0;
    if (v > 255) return 255;
    return v;
}

int Adjust(int v, int c, int shift)
{
    int gain = 3 + c;
    return Clamp255(((v - 128) * 15) / 16 + 128 + shift * gain);
}

int Pack(int r, int g, int b)
{
    return (r << 16) | (g << 8) | b;
}

public void Step()
{
    int shift = round % 7 - 3;
    for (int i = 0; i < hist.Length; i++)
    {
        hist[i] = 0;
    }
    for (int i = 0; i < pixels.Length; i++)
    {
        int p = pixels[i];
        int r = Adjust(Channel(p, 2), 2, shift);
        int g = Adjust(Channel(p, 1), 1, shift);
        int b = Adjust(Channel(p, 0), 0, shift);
        pixels[i] = Pack(r, g, b);
        int gray = (r * 77 + g * 150 + b * 29) >> 8;
        hist[gray] = hist[gray] + 1;
    }
    round++;
}

public void ComputeChecksum()
{
    int h = 0;
    for (int i = 0; i < hist.Length; i++)
    {
        h = (h * 31 + hist[i] * (i + 1)) % 1000003;
    }
    for (int i = 0; i < pixels.Length; i++)
    {
        h = (h * 31 + (pixels[i] % 65521) * (i % 1000 + 1)) % 1000003;
    }
    checksum = h;
}
```

</details>

<details>
<summary><b>Noise</b></summary>

2-D value noise in fixed point (256 = 1.0): a pseudo-random value at every lattice point, blended with a smoothstep curve, two octaves summed. Written with small helper methods (Lattice, Fade, Lerp, Sample). Each Step renders a 24×24 tile at a scrolling offset and folds it into a hash.

Discussion: TsukimiSharp inlines Lattice and Fade. Lerp (called inside another call) and Sample (called from two places and large) stay as calls, so the gain is smaller than in Cyclic.

Source: Written by the author.

`bodies/Noise.body.cs`

```csharp
// 2-D value noise in fixed point (256 = 1.0): a pseudo-random value at every
// lattice point, blended with a smoothstep curve, two octaves summed.
// Written with small helper methods (Lattice, Fade, Lerp, Sample), the way
// terrain or texture code usually is. Each Step renders a size x size tile at
// an offset that scrolls, and folds the tile into a hash.
public int size = 24;
public int seed = 1234;
public int checksum;

int[] field;
int offset;
int acc;

void Start()
{
    Init();
}

public void Init()
{
    field = new int[size * size];
    offset = 0;
    acc = 0;
    checksum = 0;
}

int Lattice(int x, int y)
{
    int h = (x * 7919 + y * 104729 + seed) % 65537;
    h = (h * 75 + 74) % 65537;
    return h & 255;
}

int Fade(int t)
{
    return (t * t * (768 - 2 * t)) >> 16;
}

int Lerp(int a, int b, int t)
{
    return a + (((b - a) * t) >> 8);
}

int Sample(int x, int y)
{
    int xi = x >> 8;
    int yi = y >> 8;
    int tx = Fade(x & 255);
    int ty = Fade(y & 255);
    int a = Lattice(xi, yi);
    int b = Lattice(xi + 1, yi);
    int c = Lattice(xi, yi + 1);
    int d = Lattice(xi + 1, yi + 1);
    return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
}

public void Step()
{
    int n = size;
    for (int y = 0; y < n; y++)
    {
        for (int x = 0; x < n; x++)
        {
            int v = Sample(x * 40 + offset, y * 40) + Sample(x * 80 + offset * 2, y * 80 + 4096) / 2;
            field[y * n + x] = v;
        }
    }
    int h = acc;
    for (int i = 0; i < field.Length; i++)
    {
        h = (h * 31 + field[i] * (i % 1000 + 1)) % 1000003;
    }
    acc = h;
    offset = (offset + 37) % 4096;
}

public void ComputeChecksum()
{
    checksum = acc;
}
```

</details>

<details>
<summary><b>Ripple</b></summary>

Water ripples on a 32×32 grid with two buffers. Each inner cell takes half the sum of its four neighbours, subtracts its own value from the Step before, and loses 1/32 of the result. The 3×3 neighbourhood is described by Weight (1 for the four cells above, below, left and right, 0 for the others), and Tap skips the neighbours whose weight is 0. Every fourth Step a drop falls at a pseudo-random place. Checksum: two hashes modulo small primes (1009 and 1013), combined.

Discussion: The kernel is written the general way, with small helper methods (Index, Offset, Weight, Tap) that could describe any small kernel: 23 calls per cell (Index once, Tap and Weight nine times each, Offset for the four taps with weight 1). UdonSharp makes every call as written, and a call costs 8 to 14 instructions depending on the number of arguments, counted as in Cyclic. TsukimiSharp inlines the helpers. dx and dy are constants at every call, so Weight folds to a constant, the five taps with weight 0 no longer read the array, and Offset becomes a constant too. In the compiled TsukimiSharp program the inner loop has no calls left: 119 instructions, 29 of them EXTERN calls (five array reads and one write). Four multiplications by 1 and four additions of 0 are still there, so the work of a cell comes close to, but not quite down to, the sum of four neighbours divided by 2. The form was chosen on purpose, but it is a common one: someone used to an ordinary compiler splits code into small functions, expecting them to be inlined. On UdonSharp, every function split off costs its calls.

Source: Written for this benchmark. The update rule follows Hugo Elias, "2D Water" (web page, archived 2 December 1998: https://web.archive.org/web/19981202135801/http://freespace.virgin.net/hugo.elias/graphics/x_water.htm).

`bodies/Ripple.body.cs`

```csharp
// Water ripples on a 32 x 32 grid (the two-buffer wave effect). The 3x3
// neighbourhood is described by a Weight function, and Tap(i, dx, dy) reads
// the neighbour of cell i at offset (dx, dy), skipping the neighbours whose
// weight is zero, so the same helpers could describe any small kernel.
// A drop falls every fourth Step at a pseudo-random place.
const int Size = 32;

public int seed = 4242;
public int checksum;

int[] cur;
int[] prev;
int round;
int rng;

void Start()
{
    Init();
}

public void Init()
{
    cur = new int[Size * Size];
    prev = new int[Size * Size];
    rng = seed % 1021;
    round = 0;
    checksum = 0;
}

int Index(int x, int y)
{
    return y * Size + x;
}

int Offset(int dx, int dy)
{
    return dy * Size + dx;
}

int Weight(int dx, int dy)
{
    if (dx * dx + dy * dy == 1) return 1;
    return 0;
}

int Tap(int i, int dx, int dy)
{
    int w = Weight(dx, dy);
    if (w == 0) return 0;
    return cur[i + Offset(dx, dy)] * w;
}

public void Step()
{
    if (round % 4 == 0)
    {
        rng = (rng * 75 + 74) % 1021;
        int px = 2 + rng % (Size - 4);
        int py = 2 + (rng / 7) % (Size - 4);
        int d = Index(px, py);
        cur[d] = cur[d] + 400;
    }
    for (int y = 1; y < Size - 1; y++)
    {
        for (int x = 1; x < Size - 1; x++)
        {
            int i = Index(x, y);
            int sum = 0;
            sum += Tap(i, -1, -1);
            sum += Tap(i, 0, -1);
            sum += Tap(i, 1, -1);
            sum += Tap(i, -1, 0);
            sum += Tap(i, 0, 0);
            sum += Tap(i, 1, 0);
            sum += Tap(i, -1, 1);
            sum += Tap(i, 0, 1);
            sum += Tap(i, 1, 1);
            int v = sum / 2 - prev[i];
            v = v - v / 32;
            prev[i] = v;
        }
    }
    int[] t = cur;
    cur = prev;
    prev = t;
    round++;
}

public void ComputeChecksum()
{
    int h1 = 0;
    int h2 = 0;
    for (int i = 0; i < cur.Length; i++)
    {
        int v = cur[i] % 1000 + 1000;
        h1 = (h1 * 31 + v) % 1009;
        h2 = (h2 * 37 + v * (i % 97 + 1)) % 1013;
    }
    checksum = h1 * 1013 + h2;
}
```

</details>
<!-- bodies:end -->

## Method

### Two versions from one body

Each benchmark is one body file that holds the class members only. `tools/gen.py` puts a class
header around the same body to make the UdonSharp version and the TsukimiSharp version. Only the
header differs; the members are the same byte for byte.

```csharp
// UdonSharp version
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LifeU : UdonSharpBehaviour { ...body... }

// TsukimiSharp version
public partial class LifeT : TsukimiBehaviour { ...body... }
```

The bodies are written plainly, in a form that both compilers accept. They use C# 7.3 and
instance methods only, with no local functions, no recursion and no types of their own.

### Checking

The same bodies also run under plain .NET (`reference/`, with small stand-ins for `Vector3` and
`Mathf`), and those values are the expected values. After every timed run in Unity the `checksum`
is read and compared: bit for bit for the integer benchmarks, within an absolute 1e-4 for NBody
(`float`).

### Timing

The driver (`UdonBenchDriver`) calls the two programs of each benchmark with `SendCustomEvent` in
Play mode.

- One run: `Init` (not timed), then a `Stopwatch` around the Step calls, then `ComputeChecksum`
  (not timed).
- Three warm-up runs, then 31 rounds. Each round runs both programs once, in the order AB, BA, BA,
  AB, ...
- The median of the 31 runs divided by the number of Steps is the time per Step. With several
  sessions (one result file each), the table uses the median of the per-session medians (with two
  sessions, their mean).

ms/Step is the time one call of `Step()` takes. It is not the time of a frame. The `Step` calls of
one timed run follow each other within the same frame; the time covers running `Step` on Udon and
one call of `SendCustomEvent`, and nothing else of the frame (rendering, other scripts). For work
that is called once per frame, it is the part of the frame that the work uses in the editor.

### Environment

Unity 2022.3.22f1, VRChat Worlds SDK 3.10.4, Play mode in the Unity editor (ClientSim).

## Results

Made by `tools/report.py` from a measurement under `results/`.

<!-- results:start -->
Latest: [`results/2026-10-29-0.10.2`](results/2026-10-29-0.10.2/README.md) (environment and notes there)

Difference = (TsukimiSharp − UdonSharp) / UdonSharp of the time per Step. −40% means a Step takes 0.6 times as long.

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

### Difference

![Difference](results/2026-10-29-0.10.2/charts/difference.svg)

For each benchmark, how much less time a Step takes with TsukimiSharp than with UdonSharp. A bar left of 0 (green) means TsukimiSharp is faster; right of 0 (orange), slower; grey means the difference rounds to ±0.0%. The bar is computed from the median of the per-session medians; each black dot is one session. Dots close to the bar mean the sessions agree.

### Box plot (all benchmarks)

![Box plot](results/2026-10-29-0.10.2/charts/boxplot.svg)

The timed runs, per benchmark, for UdonSharp (grey) and TsukimiSharp (blue). The benchmarks take very different times, so the axis is scaled so that the UdonSharp time per Step of each benchmark in the table is 100%. The boxes pool the timed runs of every session: the box holds the middle half of the runs, the thick line in it is the median, and the whiskers reach the farthest run within 1.5 box lengths beyond the box. Runs beyond the whiskers are not drawn; nearly all of them are slow runs, and some of them fall in the same round for both compilers (probably the editor or the OS; not investigated). A blue box clear of the grey box and to its left means TsukimiSharp is faster by more than the noise; overlapping boxes mean the difference is about the size of the noise. The number on the right is the TsukimiSharp time in the table as a percentage of UdonSharp (100% + the difference). Rows are sorted by the difference, the largest speed-up first.

### Box plot per benchmark

![Box plot per benchmark](results/2026-10-29-0.10.2/charts/distribution.svg)

The same box plots, one per benchmark, in milliseconds, for reading the actual time of a Step. The number at the top right is the difference. Each benchmark has its own axis.

Every timed run is in the `result-*.json` files.
<!-- results:end -->

## Running it

1. Add the listing `https://zz-roba.github.io/tsukimi-vpm/index.json` to VCC or ALCOM and open
   `unity/` as an existing project.
2. Run the menus **UdonBench/1 Create Program Assets** and **UdonBench/2 Create Bench Scene**.
3. Enter Play mode with ClientSim enabled. The run starts by itself. When it is done, the table
   is printed to the Console and written to `unity/Logs/UdonBench/result-<time>.txt`.
4. Copy the `result-*.json` files (any number of sessions) from `unity/Logs/UdonBench/` into `results/<name>/` and run
   `python3 tools/report.py results/<name> --readme`. It writes the charts (`charts/*.svg`) and
   the tables, and replaces the Results section of this README. Commit all of it.

After changing a body, regenerate with `python3 tools/gen.py` (the expected values need the .NET 8
SDK).

## License

MIT ([LICENSE](LICENSE)).
