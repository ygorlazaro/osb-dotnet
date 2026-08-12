namespace Osb.Xwin.Effects;

/// <summary>
/// Porte de XWIN/FONTES/CICULOS.BAS: dois "trens" de círculos crescentes, um
/// seguindo COS/SIN e outro SIN/COS, formando um padrão espiral entrelaçado.
/// </summary>
public sealed class CiculosEffect : IScreenEffect
{
    public string Name => "Círculos";
    public (double Width, double Height) DesignResolution => (640, 480);

    private readonly Random _rnd = new();
    private readonly List<CircleShape> _shapes = [];
    private int _colorA;
    private int _colorB;
    private double _rot;

    private const double MaxRot = 62.83; // original: FOR rot = 0 TO 62.83

    public CiculosEffect() => Reset();

    public void Reset()
    {
        _shapes.Clear();
        _colorA = _rnd.Next(0, 15) + 1; // INT(RND*15)+1
        _colorB = _rnd.Next(0, 15) + 1;
        _rot = 0;
    }

    public void Advance()
    {
        if (_rot > MaxRot)
        {
            Reset();
            return;
        }

        var rot = _rot;
        var radius = rot * 3;

        _shapes.Add(new CircleShape(320 + 100 * Math.Cos(rot), 240 + 100 * Math.Sin(rot), radius, _colorA));
        _shapes.Add(new CircleShape(320 + 100 * Math.Sin(rot), 240 + 100 * Math.Cos(rot), radius, _colorB));

        // Original: STEP .01 (6283 passos por passada). Usamos um passo maior para
        // manter a animação leve em tempo real; a forma final é a mesma.
        _rot += 0.04;
    }

    public IReadOnlyList<CircleShape> CurrentShapes => _shapes;
}
