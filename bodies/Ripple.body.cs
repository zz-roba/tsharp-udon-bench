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
