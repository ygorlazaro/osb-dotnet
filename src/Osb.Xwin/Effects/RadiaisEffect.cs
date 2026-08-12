namespace Osb.Xwin.Effects;

/// <summary>
/// Porte de XWIN/FONTES/RADIAIS.BAS: nove círculos concêntricos/deslocados que
/// crescem juntos a cada passada, formando um padrão radial simétrico.
/// Fórmulas mantidas idênticas ao original (SCREEN 12, resolução 640x480).
/// </summary>
public sealed class RadiaisEffect : IScreenEffect
{
    public string Name => "Radiais";
    public (double Width, double Height) DesignResolution => (640, 480);

    private static readonly double Sqrt20000 = Math.Sqrt(20000);
    private static readonly double SqrtHalf = Math.Sqrt(0.5);

    private readonly Random _rnd = new();
    private readonly List<CircleShape> _shapes = new();
    private int _color;
    private double _rot;

    public RadiaisEffect() => Reset();

    public void Reset()
    {
        _shapes.Clear();
        _color = _rnd.Next(0, 6) + 1; // original: INT(RND*6)+1
        _rot = 0;
    }

    public void Advance()
    {
        if (_rot > 200)
        {
            Reset();
            return;
        }

        var rot = _rot;
        var c = _color;

        _shapes.Add(new CircleShape(320, 240, rot, 0));
        _shapes.Add(new CircleShape(320, 40 + rot, rot, c + 1));
        _shapes.Add(new CircleShape(320, 440 - rot, rot, c + 2));
        _shapes.Add(new CircleShape(120 + rot, 240, rot, c + 3));
        _shapes.Add(new CircleShape(520 - rot, 240, rot, c + 4));
        _shapes.Add(new CircleShape(320 - Sqrt20000 + SqrtHalf * rot, 240 + Sqrt20000 - SqrtHalf * rot, rot, c + 5));
        _shapes.Add(new CircleShape(320 + Sqrt20000 - SqrtHalf * rot, 240 + Sqrt20000 - SqrtHalf * rot, rot, c + 6));
        _shapes.Add(new CircleShape(320 - Sqrt20000 + SqrtHalf * rot, 240 - Sqrt20000 + SqrtHalf * rot, rot, c + 7));
        _shapes.Add(new CircleShape(320 + Sqrt20000 - SqrtHalf * rot, 240 - Sqrt20000 + SqrtHalf * rot, rot, c + 8));

        // No original o passo é STEP 1; avançamos 2 por tick para uma animação fluida
        // em tempo real (o BASIC rodava sem limitador de quadros, tão rápido quanto a CPU).
        _rot += 2;
    }

    public IReadOnlyList<CircleShape> CurrentShapes => _shapes;
}
