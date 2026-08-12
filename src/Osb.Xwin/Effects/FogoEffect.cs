namespace Osb.Xwin.Effects;

/// <summary>
/// Porte de XWIN/FONTES/FOGO.BAS: um foguete sobe (com a mesma física do original -
/// gravidade, atrito aleatório) e explode em partículas que caem com gravidade,
/// ricocheteando nas bordas ("Elastic=1" no original).
///
/// O original usava um truque de animação de paleta VGA (via OUT nas portas 968/969)
/// pra fazer as partículas "esfriarem" sem redesenhar nada - isso é uma técnica de
/// hardware do DOS que não existe fora dele, então aqui simulamos o esfriamento
/// simplesmente trocando de uma cor "viva" pra uma cor "escura" da mesma família
/// conforme a partícula envelhece. A física de posição/velocidade é a mesma.
/// </summary>
public sealed class FogoEffect : IScreenEffect
{
    public string Name => "Fogo";

    // Original: SCREEN 13 (320x200).
    public (double Width, double Height) DesignResolution => (320, 200);

    private const int ParticleCount = 220; // original: MaxExpRad = 500 (reduzido para o terminal)
    private const double Gravity = 0.01;   // igual ao original

    private readonly Random _rnd = new();
    private readonly List<PointShape> _points = [];

    private enum Phase { Ascending, Exploding }
    private Phase _phase;

    // Estado do foguete subindo
    private double _x, _y, _yd, _xd;
    private int _brightColor, _darkColor;

    // Estado da explosão
    private readonly double[] _ex = new double[ParticleCount];
    private readonly double[] _ey = new double[ParticleCount];
    private readonly double[] _exd = new double[ParticleCount];
    private readonly double[] _eyd = new double[ParticleCount];
    private int _age;
    private const int MaxAge = 140;

    // Pares cor viva/escura da mesma família, pra simular o esfriamento sem paleta.
    private static readonly (int Bright, int Dark)[] ColorPairs =
    [
        (12, 4), (14, 6), (10, 2), (11, 3), (13, 5), (9, 1), (15, 7)
    ];

    public FogoEffect() => Reset();

    public void Reset()
    {
        _points.Clear();
        LaunchRocket();
    }

    private void LaunchRocket()
    {
        _phase = Phase.Ascending;
        _x = _rnd.Next(1, 320);
        _y = 199;
        _yd = _rnd.Next(2, 5);                                    // original: INT(RND*3)+2
        _xd = _x > 160 ? -_rnd.NextDouble() * 5 : _rnd.NextDouble() * 5; // original: Inc=7 (elástico); reduzido
        var (bright, dark) = ColorPairs[_rnd.Next(ColorPairs.Length)];
        _brightColor = bright;
        _darkColor = dark;
    }

    public void Advance()
    {
        if (_phase == Phase.Ascending)
        {
            AdvanceAscending();
        }
        else
        {
            AdvanceExploding();
        }
    }

    private void AdvanceAscending()
    {
        // Física idêntica ao original: Y -= YD; YD -= RND*.09; X += Xd (com ricochete elástico).
        _y -= _yd;
        _yd -= _rnd.NextDouble() * 0.09;
        if (_x + _xd > 320 || _x + _xd < 0)
        {
            _xd = -_xd;
        }

        _x += _xd;

        _points.Clear();
        _points.Add(new PointShape(_x, _y, _brightColor));

        if (_yd <= 0)
        {
            StartExplosion();
        }
    }

    private void StartExplosion()
    {
        _phase = Phase.Exploding;
        _age = 0;
        for (var i = 0; i < ParticleCount; i++)
        {
            _ex[i] = _x;
            _ey[i] = _y;
            _exd[i] = _rnd.NextDouble() - _rnd.NextDouble(); // original: RND - RND
            _eyd[i] = _rnd.NextDouble() - _rnd.NextDouble();
        }
    }

    private void AdvanceExploding()
    {
        _points.Clear();
        var color = _age < MaxAge / 2 ? _brightColor : _darkColor;

        for (var i = 0; i < ParticleCount; i++)
        {
            if (_ex[i] + _exd[i] > 320 || _ex[i] + _exd[i] < 0)
            {
                _exd[i] = -_exd[i];
            }

            _ex[i] += _exd[i];
            if (_ey[i] + _eyd[i] > 200 || _ey[i] + _eyd[i] < 0)
            {
                _eyd[i] = -_eyd[i];
            }

            _ey[i] += _eyd[i];
            _eyd[i] += Gravity * _rnd.NextDouble();

            _points.Add(new PointShape(_ex[i], _ey[i], color));
        }

        _age++;
        if (_age > MaxAge)
        {
            LaunchRocket();
        }
    }

    public IReadOnlyList<PointShape> CurrentPoints => _points;
}
