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
