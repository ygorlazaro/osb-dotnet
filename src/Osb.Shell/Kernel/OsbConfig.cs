namespace Osb.Shell.Kernel;

/// <summary>
/// Porte de OSB.CFG (as variáveis globais lidas na SUB Boot do OSB.BAS original).
/// Mantém o mesmo formato de seções [CHAVE] seguidas do valor na linha seguinte,
/// para ficar o mais fiel possível ao arquivo de configuração de 1997.
/// </summary>
public class OsbConfig
{
    public int ForeColor { get; set; } = 15;
    public int BackColor { get; set; } = 1;
    public string SystemDir { get; set; } = "";
    public bool Logo { get; set; } = false;
    public bool Num { get; set; } = true;
    public string Message { get; set; } = "Seja bem-vindo ao OSB 2.0 (portado para .NET)";

    public static OsbConfig Load(string path)
    {
        var cfg = new OsbConfig { SystemDir = Path.GetDirectoryName(path) ?? "." };
        if (!File.Exists(path)) return cfg;

        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            var key = lines[i].Trim().ToUpperInvariant();
            string Next() => i + 1 < lines.Length ? lines[++i] : "";

            switch (key)
            {
                case "[FORECOLOR]": cfg.ForeColor = ParseInt(Next(), 15); break;
                case "[BACKCOLOR]": cfg.BackColor = ParseInt(Next(), 1); break;
                case "[SYSTEM]": cfg.SystemDir = Next(); break;
                case "[LOGO]": cfg.Logo = Next().Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase); break;
                case "[NUM]": cfg.Num = Next().Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase); break;
                case "[MESSAGE]": cfg.Message = Next(); break;
            }
        }
        return cfg;
    }

    public void Save(string path)
    {
        var content = $"[ForeColor]\n{ForeColor}\n\n[BackColor]\n{BackColor}\n\n[System]\n{SystemDir}\n\n[Logo]\n{(Logo ? "True" : "False")}\n\n[Num]\n{(Num ? "True" : "False")}\n\n[Message]\n{Message}\n";
        File.WriteAllText(path, content);
    }

    private static int ParseInt(string s, int fallback) => int.TryParse(s.Trim(), out var v) ? v : fallback;
}
