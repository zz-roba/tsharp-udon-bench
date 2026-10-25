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
