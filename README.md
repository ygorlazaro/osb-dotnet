# OSB - Operating System Basic (porte para .NET 10)

Porte do **OSB**, o "sistema operacional" (na verdade um shell/ambiente operacional
sobre o DOS) que o Ygor criou entre os 14 e 16 anos em Basic (BC7), quase 30 anos
atrás. O código BASIC original está preservado em `/original-src` para referência
e comparação.

O porte tem dois programas, exatamente como o original:

- **`Osb.Shell`** — o kernel: o interpretador de comandos que roda no terminal.
- **`Osb.Xwin`** — o XWIN: a interface gráfica alternativa, também em modo texto puro.

O `Osb.Shell` **lança o `Osb.Xwin` como um processo separado** quando você digita
`X`, do mesmo jeito que o `COMMAND.COM` do MS-DOS 6.22 chamava o `WIN.COM`: o XWIN
toma conta da tela até você sair, e o controle volta pro prompt do OSB exatamente
de onde parou.

**Nenhum dos dois programas usa X11, Wayland, ou qualquer toolkit gráfico.** Tudo
roda com a Console API pura do .NET + sequências ANSI, igual o VBDOS original não
precisava de nada além da BIOS de vídeo do DOS. O XWIN simula "pixels" dentro do
terminal usando o truque do meio-bloco Unicode (▀): cada caractere vira 2 pixels
(cor de primeiro plano em cima, cor de fundo embaixo) — a mesma técnica usada há
décadas em telas de texto ANSI/BBS.

## Osb.Shell (o kernel)

O núcleo do OSB — o kernel de boot e o interpretador de comandos (`OSB.BAS`) — foi
reescrito em C#/.NET 10 como um shell de linha de comando, mantendo os mesmos
comandos e o mesmo espírito do original:

| Comando | Status |
|---|---|
| `DIR`, `CD`, `MD`, `RD`, `COPY`, `ERASE`, `REN`, `TYPE`, `TREE`, `SIZE`, `PRINT` | Portado, operando no sistema de arquivos real, com resolução de caminho **case-insensitive** (`cd minhapasta` acha `MinhaPasta`, do jeito que o DOS fazia) |
| `CLS`/`CLEAR`, `VER`, `ABOUT`, `HELP`, `<cmd> /?`, `RPT`, `.<comando>`, `./<comando>` | Portado |
| `COLOR`, `CONFIG` | Portado (grava em `OSB.CFG` na pasta de build e em `CONF/` local, não em `~/.osb`) |
| `HOSTNAME`, `USER` | Portado — `HOSTNAME` exibe/alterar o nome da máquina; `USER` autentica e gerencia contas locais |
| `DATE`, `TIME` | Portado como somente leitura (mudar a data/hora do SO exige privilégios de admin) |
| `CAL` | Portado — mês atual, `CAL <mês>`, `CAL <mês> <ano>`, e `CAL <ano>` pro ano inteiro |
| `KISS <arquivo>` | Portado — editor de texto de verdade (setas navegam, Ctrl+S salva, ESC sai), com **sintaxe highlighting OSLANG** para arquivos `.osl` e `.oslang` |
| `TOUR` | Portado — tour passo a passo do OSB |
| `TODO` | Portado — gerenciador de tarefas |
| `HANGMAN` | Portado — jogo da forca, jogável |
| `X` | Portado — carrega o XWIN |
| `EXIT` | Portado |

O interpretador de comandos foi reescrito pra separar o **verbo** do comando dos
seus argumentos (em vez de checar se o texto inteiro *começa com* o nome do
comando). Isso corrigiu um bug real: digitar `CALC` por engano abria o
calendário (`CAL`), porque a checagem antiga era só um prefixo.

## Osb.Xwin (a interface gráfica, em modo texto)

Uma janela de menu em modo texto (equivalente ao `MAIN.FRM`/`COMANDOS.FRM` do VBDOS
original) com **botões clicáveis pelo mouse** (protocolo SGR, suportado por
qualquer terminal xterm-compatível) e atalhos de teclado pro mesmo item — no
espírito do Windows 1.0 rodando sobre o DOS: uma lista de "programas" que abre
com um clique, sem precisar de X11, Wayland ou qualquer servidor gráfico por
baixo (o clique chega como texto — uma sequência ANSI — na entrada padrão, e o
próprio terminal do usuário é quem faz esse trabalho).

Os `.FRM` originais (`MAIN.FRM`, `COMANDOS.FRM` etc.) foram salvos em formato binário
pelo VBDOS e não puderam ser recuperados como código-fonte — então a lógica exata
deles se perdeu; o menu foi recriado do zero. As animações de tela em
`XWIN/FONTES/*.BAS`, porém, eram texto puro, e foram portadas com a mesma
matemática do `.BAS` original:

- **Radiais** (`RADIAIS.BAS`) — nove círculos crescendo em posições espelhadas
- **Círculos** (`CICULOS.BAS`) — duas espirais de círculos entrelaçadas
- **Complex** (`COMPLEX.BAS`) — uma "flor" de círculos que cresce e depois se apaga
- **Linhas** (`LINHAS.BAS`) — sólidos "3D" tipo Lissajous (os 8 conjuntos de dados originais foram copiados direto do `DATA` do BASIC)
- **Fogo** (`FOGO.BAS`) — fogos de artifício, com a mesma física de gravidade/ricochete do original

Água, Cubo, Cones e Dragões aparecem no menu como "em breve" — os `.BAS` deles
também são texto puro (em `original-src/XWIN/FONTES`), então é questão de portar a
matemática de cada um, um de cada vez.

## Como rodar

Requer o [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# Compila os dois primeiro (o Osb.Shell precisa achar o Osb.Xwin já compilado
# para o comando X funcionar):
cd src/Osb.Xwin && dotnet build && cd ../..
cd src/Osb.Shell && dotnet run
```

Na primeira execução, o OSB cria automaticamente uma instalação local no diretório da build do `Osb.Shell`, com `OSB.CFG` na raiz desse diretório e a pasta `CONF/` contendo arquivos como `OSB.HLP`, `HOSTNAME.CFG` e `USER.CFG`.

O sistema de ajuda agora carrega `CONF/OSB.HLP` em tempo de execução, em vez de deixar os textos de ajuda embutidos no código.

Dentro do OSB, digite `X` pra abrir o XWIN. O `Osb.Shell` procura o
`Osb.Xwin.dll` já compilado ao lado (`src/Osb.Xwin/bin/Debug|Release/`, em
qualquer subpasta de TFM); se você compilou em outro lugar, defina a variável
de ambiente `OSB_XWIN_PATH` apontando para o `.dll`.

## Estrutura

```
src/Osb.Shell/
  Program.cs           - ponto de entrada
  Kernel/
    OsbEnvironment.cs   - equivalente a "instalar" o OSB em C:\OSB (aqui, pasta de build local com `CONF/`)
    OsbConfig.cs        - leitura/gravação do OSB.CFG
    OsbShell.cs         - o interpretador de comandos (porte de SUB Command)
    BootSequence.cs     - porte de SUB Boot
    XwinLauncher.cs     - acha e lança o Osb.Xwin como subprocesso (comando X)
    PathResolver.cs     - resolve caminhos ignorando maiúsculas/minúsculas
    ColorPicker.cs, ConfigUtility.cs, About.cs, HelpTexts.cs, DosColors.cs
    OslangHighlighter.cs - syntax highlighting ANSI para OSLANG no KISS e TYPE
  Apps/
    Calendar.cs         - porte do CAL.COM (mês, mês+ano, ano inteiro)
    TextEditor.cs       - porte do KISS (editor de texto com syntax highlighting)
  Games/
    Hangman.cs          - porte do HANGMAN.BAS

src/Osb.Xwin/
  Program.cs            - ponto de entrada (modo texto)
  MainMenu.cs            - menu principal clicável (equivalente ao MAIN.FRM)
  TextMode/
    TextCanvas.cs        - "framebuffer" em modo texto (truque do meio-bloco ▀)
    AnsiPalette.cs        - paleta VGA de 16 cores em sequências ANSI
    AnimationRunner.cs     - roda um efeito até apertar uma tecla
    MouseInput.cs           - leitura de cliques de mouse via protocolo SGR
  Effects/
    IScreenEffect.cs             - contrato comum (círculos, linhas e pontos)
    RadiaisEffect.cs, CiculosEffect.cs, ComplexEffect.cs  - efeitos com CIRCLE
    LinhasEffect.cs                                        - efeito com LINE (sólidos 3D)
    FogoEffect.cs                                           - efeito com PSET (partículas)

src/Osb.Lang/
  Lexing/                - lexer OSLANG
  Parsing/               - parser OSLANG
  Compilation/           - análise semântica e compilação
  Runtime/               - interpretador e runtime
  OSLANG-0.61-SPEC.md    - especificação da linguagem OSLANG 0.61

oslang-vscode/           - extensão VS Code para OSLANG (syntax highlighting, intellisense, document symbols, folding)

original-src/            - código BASIC original, para referência
```

## OSLANG 0.62

A linguagem de script do OSB agora suporta **intercâmbio de dados** e **rede** através de namespaces padrão:

- **OSL.JSON** — serialização, desserialização e manipulação de JSON (`PARSE`, `STRINGIFY`, `PRETTY`, `READ`, `WRITE`)
- **OSL.CSV** — leitura e escrita de CSV (`PARSE`, `STRINGIFY`, `READ`, `WRITE`)
- **OSL.XML** — parsing e navegação de XML (`PARSE`, `STRINGIFY`, `READ`, `WRITE`, `NAME`, `VALUE`, `ATTRIBUTES`, `CHILDREN`, `CHILD`, `HAS`)
- **OSL.CNF** — API de configuração do OSB (`READ`, `WRITE`, `GET`, `SET`, `HAS`, `DELETE`, `KEYS`, `SAVE`)
- **OSB.NET** — comunicação de rede (`PING`, `DOWN`)

Todos mantêm total compatibilidade com versões anteriores.

## OSLANG 0.61

A linguagem de script do OSB evoluiu para a versão **0.61**, com novos recursos
mantendo total compatibilidade com versões anteriores:

- **ENUM** — tipos enumerados com valores numéricos ou string
- **ENUM SETS** — combinação de valores enum com `|`
- **SWITCH / CASE / DEFAULT** — desvio condicional multi-way
- **BREAK** — sai de `SWITCH`, `FOR`, `WHILE` e `DO WHILE`
- **String interpolation** — templates com `${expressao}`
- **Multiline strings** — strings entre `"""` com indentação automática
- **Escape sequences** — `\n`, `\t`, `\\` em strings
- **Arrow functions** — funções lambda com `X => expr`
- **FOREACH** — iteração de arrays com método ou bloco
- **MATH** — funções trigonométricas (`SIN`, `COS`, `TAN`), `PI`, `RANDOM`
- **Array methods** — `FINDINDEX`, `FLAT`, `PUSH`, `POP`, `SORT`, `JOIN`, `CONTAINS`
- **STRING methods** — `PADSTART`, `PADEND`, `REPEAT`, `NORMALIZE`
- **DATE/TIME** — tipos nativos `DATE` e `TIME` com `DATE.NOW()` e `DATE.FORMAT()`
- **I18N** — sistema de internacionalização com `I18N.GET()`, `I18N.SETLANGUAGE()`, etc.
- **FILE/DIR** — namespaces para manipulação de arquivos e diretórios
- **TRY/CATCH** — tratamento de erros em runtime
- **EVENT/ON/RAISE** — sistema de eventos
- **CLASS/INTERFACE** — programação orientada a objetos com herança, interfaces, construtores

Veja a especificação completa em `src/Osb.Lang/OSLANG-0.61-SPEC.md`.

## Extensões e Ferramentas

### VS Code Extension (oslang-vscode/)

A extensão **OSLANG 1.1.0** para VS Code oferece:

- **Syntax Highlighting** — coloração para `.osl` e `.oslang` com suporte a:
  - Keywords, tipos, números, strings, comentários
  - Operadores (aritméticos, comparação, lógicos, arrow `=>`)
  - Métodos de builtins (MATH, STRING, ARRAY, I18N, FILE, DIR, DATE)
  - Interpolação de strings `${...}` com highlighting aninhado
  - Enum sets com `|`
- **IntelliSense** — autocompletar contextual:
  - Keywords e builtins
  - Classes, interfaces, funções, métodos, variáveis e enums do arquivo atual
  - Métodos de contexto (`MATH.`, `STRING.`, `ARRAY.`, `I18N.`, `FILE.`, `DIR.`, `DATE.`)
- **Hover** — documentação inline para 60+ funções e keywords
- **Signature Help** — ajuda de assinatura para funções com múltiplos overloads
- **Document Symbols** — outline view com classes, interfaces, funções, enums, métodos e propriedades
- **Code Folding** — dobramento de blocos `CLASS`, `FUNCTION`, `IF`, `FOR`, `WHILE`, `DO`, `SWITCH`, `ENUM`, `TRY`
- **Snippets** — 40+ snippets para código comum (enum, arrow functions, foreach, try-catch, etc.)

Instalação: copie a pasta `oslang-vscode/` para `~/.vscode/extensions/oslang-vscode/`
ou use `code --install-extension oslang-vscode/oslang-0.61.vsix`.

### KISS Editor

O editor de texto embutido no OSB agora aplica syntax highlighting OSLANG
também para arquivos `.cfg`, `.i18n`, `.hlp` e `.wds`, além de `.osl`.

### TYPE Command

O comando `TYPE` agora aplica syntax highlighting OSLANG para arquivos `.osl`,
`.cfg`, `.i18n`, `.hlp` e `.wds`.

## Status completo (o que foi portado x o que falta)

### Osb.Shell (kernel / OSB.BAS)

| Original | Status |
|---|---|
| DIR, CD, MD, RD, COPY, ERASE, REN, TYPE, TREE, SIZE, PRINT | Portado, com resolução de caminho case-insensitive |
| CLS, VER, ABOUT, HELP, `/?`, RPT, `.comando`, `./comando` | Portado |
| COLOR, CONFIG | Portado |
| DATE, TIME | Portado (somente leitura) |
| CAL | Portado — mês, mês+ano, e ano inteiro (`CAL 2015`) |
| X (chamar o XWIN) | Portado |
| CAL | Portado |
| KISS (editor de texto) | Portado — editor com syntax highlighting OSLANG |
| TODO | Portado |
| TOUR | Portado |
| HANGMAN | Portado |
| GERMS (tetris) | **Ainda não migramos** — `.BAS` não sobreviveu em texto |
| PROG (teste de digitação) | **Ainda não migramos** |

### Osb.Xwin (interface gráfica, agora em modo texto)

| Original | Status |
|---|---|
| MAIN.FRM / COMANDOS.FRM (menu principal) | Portado como menu de texto **clicável (mouse via SGR) + atalhos de teclado** — o `.FRM` original é binário (VBDOS), então a lógica exata de layout/eventos se perdeu; recriamos o comportamento (não o desenho) |
| AT → RADIAIS.BAS | Portado, matemática idêntica |
| AT → CICULOS.BAS | Portado, matemática idêntica |
| AT → COMPLEX.BAS | Portado, matemática idêntica |
| AT → LINHAS.BAS | Portado, matemática idêntica (inclusive os DATA originais dos 8 sólidos) |
| AT → FOGO.BAS | Portado, física idêntica (gravidade, ricochete) — só o truque de fade por paleta VGA foi trocado por troca de cor viva/escura, porque paleta animada é um recurso de hardware do DOS que não existe fora dele |
| Suporte a mouse (equivalente ao driver de mouse do DOS que o VBDOS usava) | Portado via protocolo SGR (funciona em qualquer terminal xterm-compatível) |
| AT → AGUA.BAS | **Ainda não migramos** — `.BAS` existe em texto, é o próximo candidato natural |
| AT → CUBO.BAS | **Ainda não migramos** — `.BAS` existe em texto |
| AT → CONES.BAS | **Ainda não migramos** — `.BAS` existe em texto |
| AT → DRAGONS.BAS | **Ainda não migramos** — `.BAS` existe em texto |
| CONF.FRM (config visual do XWIN) | **Ainda não migramos** — `.FRM` binário, sem fonte |
| VISUAL.FRM (escolher animação padrão) | **Ainda não migramos** — `.FRM` binário |
| CURSORS.FRM (cursor customizado) | **Ainda não migramos** — `.FRM` binário; conceito nem se aplica em modo texto |
| PRINTER.FRM | **Ainda não migramos** — `.FRM` binário |
| DATAFRM.FRM | **Ainda não migramos** — `.FRM` binário |
| PROG.FRM (config do teste de digitação) | **Ainda não migramos** — `.FRM` binário |
| GAMES → APROX (jogo, só existe como `.EXE` + `.FRM` binários) | **Ainda não migramos** — sem fonte recuperável |
| APLIC → MJB (aplicativo, `.FRM`/`.BAS` binários) | **Ainda não migramos** — sem fonte recuperável |
| APLIC → XWinText (editor de texto, `.FRM` binários) | **Ainda não migramos** — sem fonte recuperável |

**Resumo**: das 9 animações de tela originais, 5 estão portadas (Radiais, Círculos,
Complex, Linhas, Fogo); faltam 4 (Água, Cubo, Cones, Dragões), e todas têm `.BAS`
em texto puro disponível em `original-src/XWIN/FONTES`, então são o próximo passo
mais direto. Os apps/jogos da GUI (MJB, XWinText, Aprox) e as telas de configuração
do XWIN (CONF, VISUAL, CURSORS, PRINTER, DATAFRM, PROG) só existem como `.FRM`
binários do VBDOS — a lógica deles não sobreviveu, teria que ser reconstruída do
zero (não portada) se quiser esse nível de paridade.

## Licença

MIT, mesma licença do projeto original (ver `LICENSE.txt`).
