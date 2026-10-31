using Godot;

namespace Mandible;

/// Deterministic math for the simulation. Lockstep multiplayer needs every machine (x64 PC,
/// ARM phone or tablet) to compute bit-identical results, but the platform sin/cos/atan2/exp
/// libraries differ in their last bits. These use only + - * / and sqrt, which IEEE 754
/// defines exactly, so they agree everywhere.
public static class DMath
{
    public const float Pi = 3.14159265f;
    public const float Tau = 6.28318531f;
    const float HalfPi = 1.57079633f;

    /// Wrap to [-pi, pi].
    public static float Wrap(float x)
    {
        float k = (float)System.Math.Floor((x + Pi) / Tau);
        return x - k * Tau;
    }

    // minimax polynomials on [-pi/2, pi/2]
    static float SinCore(float x)
    {
        float x2 = x * x;
        return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * (-0.00019840874f + x2 * 2.7525562e-06f))));
    }

    public static float Sin(float x)
    {
        x = Wrap(x);
        if (x > HalfPi) x = Pi - x;
        else if (x < -HalfPi) x = -Pi - x;
        return SinCore(x);
    }

    public static float Cos(float x) => Sin(x + HalfPi);

    /// atan on [0, 1], max error ~1e-5 rad.
    static float AtanCore(float z)
    {
        float z2 = z * z;
        return z * (0.99997726f + z2 * (-0.33262347f + z2 * (0.19354346f + z2 * (-0.11643287f + z2 * (0.05265332f + z2 * -0.01172120f)))));
    }

    public static float Atan2(float y, float x)
    {
        float ax = System.Math.Abs(x), ay = System.Math.Abs(y);
        if (ax < 1e-20f && ay < 1e-20f) return 0f;
        float a = ax >= ay ? AtanCore(ay / ax) : HalfPi - AtanCore(ax / ay);
        if (x < 0) a = Pi - a;
        return y < 0 ? -a : a;
    }

    public static float Angle(Vector2 v) => Atan2(v.Y, v.X);
    public static Vector2 Dir(float a) => new(Cos(a), Sin(a));

    /// Deterministic replacement for Vector2.Angle().
    public static float AngleD(this Vector2 v) => Atan2(v.Y, v.X);
}
