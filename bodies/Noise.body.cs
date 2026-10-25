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
