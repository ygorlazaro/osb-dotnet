// Transcendental functions for MATH.* (OSLANG 0.51).
//
// The kernel has no libm, so each function is a short series on double. They
// are accurate to a few ULP over the range scripts actually use - enough for a
// teaching language, not a numerical library.
//
// Split out of OslangKernel.cs (one partial class of Kernel) to keep that file
// navigable.

internal static unsafe partial class Kernel
{
    private static double OsPi() => 3.14159265358979323846;

    private static double OsExp(double x)
    {
        // e^x = 2^(x * log2 e), built from the binary exponent and a Taylor
        // series on the fractional part (|y| <= ln2/2 converges quickly).
        const double Log2E = 1.44269504088896340736;
        double z = x * Log2E;
        long k = (long)(z >= 0 ? OsFloor(z) : -OsFloor(-z));
        double f = z - (double)k;
        double term = 1.0, sum = 1.0;
        for (int i = 1; i <= 18; i++)
        {
            term *= f / (double)i;
            sum += term;
        }
        double r = sum;
        long n = k;
        while (n > 0) { r *= 2.0; n--; }
        while (n < 0) { r *= 0.5; n++; }
        return r;
    }

    private static double OsLn(double x)
    {
        if (x <= 0) return 0;
        // Normalise into [1, 2) by halving/doubling, then use the atanh series
        // ln(x) = 2 * (t + t^3/3 + t^5/5 + ...) with t = (x-1)/(x+1).
        int e = 0;
        while (x >= 2.0) { x *= 0.5; e++; }
        while (x < 1.0) { x *= 2.0; e--; }
        double t = (x - 1.0) / (x + 1.0);
        double t2 = t * t;
        double sum = 0.0, term = t;
        for (int i = 1; i <= 40; i += 2)
        {
            sum += term / (double)i;
            term *= t2;
        }
        return 2.0 * sum + (double)e * 0.69314718055994530942;   // ln(2)
    }

    private static double OsSin(double x)
    {
        // Reduce to [-pi, pi] so the Taylor series stays in its accurate range.
        double twoPi = 2.0 * OsPi();
        x = x - twoPi * OsFloor(x / twoPi + 0.5);
        double term = x, sum = x;
        double x2 = x * x;
        for (int i = 1; i <= 8; i++)
        {
            term *= -x2 / (double)((2 * i) * (2 * i + 1));
            sum += term;
        }
        return sum;
    }

    private static double OsCos(double x)
    {
        double twoPi = 2.0 * OsPi();
        x = x - twoPi * OsFloor(x / twoPi + 0.5);
        double term = 1.0, sum = 1.0;
        double x2 = x * x;
        for (int i = 1; i <= 8; i++)
        {
            term *= -x2 / (double)((2 * i - 1) * (2 * i));
            sum += term;
        }
        return sum;
    }

    private static double OsAsin(double x)
    {
        if (x <= -1) return -OsPi() / 2;
        if (x >= 1) return OsPi() / 2;
        // asin(x) = atan(x / sqrt(1 - x^2))
        return OsAtan(x / OsSqrt(1.0 - x * x));
    }

    private static double OsAcos(double x)
    {
        return OsPi() / 2 - OsAsin(x);
    }

    private static double OsAtan(double x)
    {
        bool neg = x < 0;
        double a = neg ? -x : x;
        bool invert = a > 1;
        if (invert) a = 1.0 / a;
        // atan series on [0,1] converges slowly, so use the halving identity
        // atan(a) = 2*atan(a / (1 + sqrt(1+a^2))) until a is small.
        for (int i = 0; i < 6; i++) a = a / (1.0 + OsSqrt(1.0 + a * a));
        double sum = 0.0, term = a, a2 = a * a;
        for (int i = 0; i < 12; i++)
        {
            sum += term / (double)(2 * i + 1);
            term *= -a2;
        }
        double r = sum * 64.0;    // 2^6 from the halving loop
        if (invert) r = OsPi() / 2 - r;
        return neg ? -r : r;
    }

    private static double OsAtan2(double y, double x)
    {
        if (x > 0) return OsAtan(y / x);
        if (x < 0) return y >= 0 ? OsAtan(y / x) + OsPi() : OsAtan(y / x) - OsPi();
        if (y > 0) return OsPi() / 2;
        if (y < 0) return -OsPi() / 2;
        return 0;
    }
}