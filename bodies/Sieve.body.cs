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
