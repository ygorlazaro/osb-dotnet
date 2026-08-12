namespace Osb.Xwin.Effects;

/// <summary>Um círculo desenhado (equivalente a uma chamada CIRCLE do QBasic).</summary>
public readonly record struct CircleShape(double CenterX, double CenterY, double Radius, int Color);

/// <summary>Uma linha desenhada (equivalente a uma chamada LINE do QBasic).</summary>
public readonly record struct LineShape(double X1, double Y1, double X2, double Y2, int Color);

/// <summary>Um ponto desenhado (equivalente a PSET do QBasic).</summary>
public readonly record struct PointShape(double X, double Y, int Color);

/// <summary>
/// Contrato comum para as animações de tela portadas de XWIN/FONTES/*.BAS (a pasta "AT" -
/// "Animações de Tela" - do OSB original). Cada implementação porta a matemática exata
/// do BASIC original; a única adaptação é trocar o loop bloqueante "DO WHILE INKEY$=..."
/// por um passo por vez (Advance), chamado pelo loop de renderização em modo texto.
/// </summary>
public interface IScreenEffect
{
    string Name { get; }

    /// <summary>Espaço de coordenadas original em que a animação foi desenhada (ex: 640x480).</summary>
    (double Width, double Height) DesignResolution { get; }

    void Reset();

    /// <summary>Avança um passo da animação.</summary>
    void Advance();

    /// <summary>Círculos do quadro/passada atual (CIRCLE). Vazio se o efeito não usa círculos.</summary>
    IReadOnlyList<CircleShape> CurrentShapes => [];

    /// <summary>Linhas do quadro/passada atual (LINE). Vazio se o efeito não usa linhas.</summary>
    IReadOnlyList<LineShape> CurrentLines => [];

    /// <summary>Pontos do quadro/passada atual (PSET). Vazio se o efeito não usa pontos soltos.</summary>
    IReadOnlyList<PointShape> CurrentPoints => [];
}

