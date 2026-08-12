namespace Osb.Xwin.Effects;

/// <summary>
/// Porte de XWIN/FONTES/LINHAS.BAS: polígonos "3D" cujos vértices se movem em
/// funções seno (um Lissajous por vértice), dando a impressão de formas girando
/// no espaço. Os coeficientes de cada uma das 8 figuras são os mesmos dos DATA
/// originais do BASIC (transcritos ao pé da letra logo abaixo).
/// </summary>
public sealed class LinhasEffect : IScreenEffect
{
    public string Name => "Linhas";

    // O original usa WINDOW SCREEN (-32,24)-(32,-24): um sistema de coordenadas
    // "de mundo" de 64x48 unidades, que aqui tratamos como a "resolução de design".
    public (double Width, double Height) DesignResolution => (64, 48);

    private const double Pi = 3.141592653589793;

    private readonly record struct Shape((double Xu, double Yu, double Zu)[] Vertices, double[] Phase);

    // Cada figura: (A = número de vértices, [XU,YU,ZU] por vértice, [P] fase por vértice).
    // Transcrito diretamente das linhas DATA de LINHAS.BAS.
    private static readonly Shape[] Shapes =
    {
        new(new (double, double, double)[] { (1, 2, 2), (1, 2, 2), (2, 3, 2), (2, 1, 2) }, new[] { 1.1, 1.1, 2.1, 1.6 }),
        new(new (double, double, double)[] { (1, 4, 1), (2, 1, 1), (3, 3, 2) }, new[] { 2.7, 2.2, 3.2 }),
        new(new (double, double, double)[] { (3, 4, 1), (5, 3, 2), (1, 4, 2), (4, 3, 5) }, new[] { 1.0, 1, 0, 0 }),
        new(new (double, double, double)[] { (2, 4, 3), (3, 4, 1), (1, 3, 2) }, new[] { 1.0, 0, 0 }),
        new(new (double, double, double)[] { (2, 3, 4), (3, 2, 1) }, new[] { 0.0, 1 }),
        new(new (double, double, double)[] { (1, 3, 2), (3, 4, 2), (3, 4, 1) }, new[] { 0.0, 0, 0 }),
        new(new (double, double, double)[] { (2, 1, 2), (2, 2, 1), (1, 2, 2), (2, 2, 1) }, new[] { 1.5, 2.5, 1.5, 2.5 }),
        new(new (double, double, double)[] { (3, 1, 2), (2, 3, 2), (1, 2, 2) }, new[] { 0.0, 0, 0 }),
    };

    private readonly List<LineShape> _lines = new();
    private int _shapeIndex;
    private double _k;

    public LinhasEffect() => Reset();

    public void Reset()
    {
        _lines.Clear();
        _shapeIndex = 0;
        _k = -1;
    }

    public void Advance()
    {
        var shape = Shapes[_shapeIndex];
        var n = shape.Vertices.Length;
        var px = new double[n];
        var py = new double[n];

        for (int i = 0; i < n; i++)
        {
            var (xu, yu, zu) = shape.Vertices[i];
            var p = shape.Phase[i];
            var x = Math.Sign(xu) * Math.Sin((_k + p) * Pi * xu) * 12;
            var y = Math.Sign(yu) * Math.Sin((_k + p) * Pi * yu) * 12;
            var z = Math.Sign(zu) * Math.Sin((_k + p) * Pi * zu) * 12;
            // Projeção usada no original: soma-se Z a X e a Y (uma projeção isométrica simples).
            px[i] = x + z + 32; // desloca de coordenada de mundo (-32..32) para pixel (0..64)
            py[i] = 24 - (y + z); // inverte Y (tela cresce pra baixo) e desloca (-24..24) -> (0..48)
        }

        _lines.Clear();
        for (int i = 0; i < n - 1; i++)
            _lines.Add(new LineShape(px[i], py[i], px[i + 1], py[i + 1], i + 1));
        _lines.Add(new LineShape(px[n - 1], py[n - 1], px[0], py[0], n));

        _k += 0.02; // original: STEP .007 - acelerado para ficar fluido em tempo real
        if (_k > 1)
        {
            _k = -1;
            _shapeIndex = (_shapeIndex + 1) % Shapes.Length;
        }
    }

    public IReadOnlyList<LineShape> CurrentLines => _lines;
}
