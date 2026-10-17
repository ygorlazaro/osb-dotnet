using System.Linq;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private string ExpandPrompt(string layout, string user, string hostname)
    {
        var result = layout;
        result = result.Replace("%user", user);
        result = result.Replace("%hostname", hostname);
        result = result.Replace("%pwd", Directory.GetCurrentDirectory());
        result = result.Replace("%d", DateTime.Now.ToString("dd/MM/yyyy"));
        result = result.Replace("%t", DateTime.Now.ToString("HH:mm:ss"));
        result = result.Replace("%br", Environment.NewLine);
        
        // Expand %VAR% from user variables
        var vars = _env.Variables.GetForUser(_env.CurrentUsername);
        foreach (var (name, value) in vars)
        {
            result = result.Replace($"%{name}%", value);
        }
        
        return result;
    }
    private string ExpandVariables(string input)
    {
        if (string.IsNullOrEmpty(_env.CurrentUsername)) return input;
        
        var vars = _env.Variables.GetForUser(_env.CurrentUsername);
        var result = input;
        foreach (var (name, value) in vars)
        {
            result = result.Replace($"%{name}%", value);
        }
        return result;
    }
    private static bool TryEvaluateMath(string expr, out double result)
    {
        result = 0;
        try
        {
            var s = expr.Replace(" ", "");
            if (s.Length == 0) return false;
            
            var pos = 0;
            
            double ParseFactor()
            {
                if (pos >= s.Length) return 0;
                
                if (s[pos] == '(')
                {
                    pos++;
                    var result = ParseExpression();
                    if (pos < s.Length && s[pos] == ')') pos++;
                    return result;
                }
                
                if (s[pos] == '-')
                {
                    pos++;
                    return -ParseFactor();
                }
                
                if (s[pos] == '+')
                {
                    pos++;
                    return ParseFactor();
                }
                
                var start = pos;
                while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
                    pos++;
                
                if (start == pos) return 0;
                
                return double.Parse(s[start..pos]);
            }
            
            double ParseTerm()
            {
                var result = ParseFactor();
                while (pos < s.Length && (s[pos] == '*' || s[pos] == '/'))
                {
                    var op = s[pos++];
                    var right = ParseFactor();
                    if (op == '*') result *= right;
                    else result /= right;
                }
                return result;
            }
            
            double ParseExpression()
            {
                var result = ParseTerm();
                while (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
                {
                    var op = s[pos++];
                    var right = ParseTerm();
                    if (op == '+') result += right;
                    else result -= right;
                }
                return result;
            }
            
            result = ParseExpression();
            return pos >= s.Length;
        }
        catch
        {
            return false;
        }
    }
}
