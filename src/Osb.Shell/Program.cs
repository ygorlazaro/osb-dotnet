using Osb.Shell.Kernel;

// OSB - Operating System Basic
// Porte para .NET 8 do "sistema operacional" (na verdade, um shell/ambiente operacional)
// originalmente escrito em Basic (BC7) por Ygor Lazaro entre os 14 e 16 anos.
//
// Este porte mantém a mesma estrutura de comandos e o mesmo espírito do original,
// rodando agora como um shell de linha de comando multiplataforma.

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; };

var env = new OsbEnvironment();
var shell = new OsbShell(env);

BootSequence.Run(env, shell);
shell.Run();
