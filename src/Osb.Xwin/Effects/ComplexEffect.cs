namespace Osb.Xwin.Effects;

/// <summary>
/// Porte de XWIN/FONTES/COMPLEX.BAS: uma "flor" de círculos dispostos radialmente,
/// que cresce colorida e depois é apagada (desenhada de novo em preto), repetindo.
/// </summary>
public sealed class ComplexEffect : IScreenEffect
{
    public string Name => "Complex";
    public (double Width, double Height) DesignResolution => (640, 480);

    private const double Deg2Rad = Math.PI / 180.0;

    private readonly Random _rnd = new();
    private readonly List<CircleShape> _shapes = [];

    private int _c = 1;      // original: FOR c = 1 TO 10  (c=10 é a passada que "apaga" em preto)
    private int _b = 1;      // original: FOR b = 1 TO 200 STEP 5
    private int _a = 1;      // original: FOR a = 1 TO 360 STEP 20
    private int _colorForRing;

    public ComplexEffect() => Reset();

    public void Reset()
    {
        _shapes.Clear();
        _c = 1;
        _b = 1;
        _a = 1;
        _colorForRing = _rnd.Next(0, 16);
    }

    public void Advance()
    {
        if (_c > 10) { Reset(); return; }

        var angle = _a * Deg2Rad;
        var color = _c == 10 ? 0 : _colorForRing;
        var x = _b * Math.Sin(angle) + 300;
        var y = _b * Math.Cos(angle) + 225;
        _shapes.Add(new CircleShape(x, y, _b, color));

        _a += 20;
        if (_a > 360)
        {
            _a = 1;
            _b += 5;
            if (_b > 200)
            {
                _b = 1;
                _c++;
                _colorForRing = _rnd.Next(0, 16); // nova cor pra cada "anel" de raio b
            }
        }
    }

    public IReadOnlyList<CircleShape> CurrentShapes => _shapes;
}
