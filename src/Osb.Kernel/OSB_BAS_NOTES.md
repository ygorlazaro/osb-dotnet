# OSB.BAS original (2000) — registro de funcionalidades

Fonte: [`original-src/OSB.BAS`](../../original-src/OSB.BAS) (QBasic/VBDOS, OSB 0.2).
Este documento cataloga o comportamento do interpretador original para guiar a
reimplementação. Legenda de status:

- ✅ **já no kernel** — funcionalidade portada e funcionando em `Kernel.cs`;
- ⏳ **planejado** — entrará nas próximas fatias (escrita ext4 → `MD`/`RD`, depois
  `COPY DEL REN`, contas, etc.);
- 🔜 **mais tarde** — depende de camadas futuras (processos/`.EXE`, OSH, GUI X).

---

## 1. Boot

O `SUB Boot` lê dois arquivos de configuração e então chama a interface:

### 1.1 `C:\OSB.CFG` (raiz do disco) — seções lidas no boot
| Seção         | Valor                                | Observação                        |
|---------------|--------------------------------------|-----------------------------------|
| `[FORECOLOR]` | número (0–15)                        | cor da letra; `COLOR Letra, Fundo` |
| `[BACKCOLOR]` | número (0–15)                        | cor de fundo                       |
| `[SYSTEM]`    | diretório do sistema (ex. `C:\OSB`)  | define `Drive$` (2 primeiras chars) e faz `CHDRIVE`+`CHDIR` |
| `[LOGO]`      | `TRUE`/outro                        | se `TRUE`, `SHELL "LOGO OSB.IMG"`  |
| `[NUM]`       | `TRUE`/outro                        | se `TRUE`, `SHELL "NUM +"` (NumLock) |
| `[MESSAGE]`   | texto                                | mensagem de boas-vindas após `CLS` |

### 1.2 `<sistema>\Conf\System.Cfg`
| Seção    | Valor          | Ação                              |
|----------|----------------|-----------------------------------|
| `[MOUSE]`| `TRUE`/outro   | `SHELL <sistema>\System\Mouse.com`|
| `[CLOCK]`| `TRUE`/outro   | `SHELL <sistema>\System\Clock.com /E` |

### 1.3 Sequência exibida no boot
`CLS` → mensagens de inicialização → `PRINT "Data: "; DATE$` / `"Hora: "; TIME$` →
linha em branco → `MESSAGE$` → `Starts 0` (executa `Conf\Start.cfg`) → interface.

### 1.4 `SUB Starts` — scripts de inicialização
- `Conf\Start.cfg` = lista de comandos (um por linha) executados no boot via `Command(a$)`.
- **Decisão do projeto:** sem `.BAT`/`AUTOEXEC.BAT`; papel equivalente para OSH/OSLANG (`.OSL`).
- A versão original reinvoca `Starts` depois de cada comando (variável `Chamado`);
  o comportamento exato é confuso no QBasic — **a porta limpa**: só no boot.

---

## 2. Prompt da interface

```
PRINT CURDIR$          ' diretório atual (ex.: A:\OSB)
LINE INPUT "[@] " ...  ' quebra de linha + prompt [@]
```

- ✅ **implementado:** o `PROMPT` padrão do kernel agora é `$P$_[@] ` (diretório, `$_`
  = quebra de linha, literal `[@] `). `CURDIR$` não traz `\` no final (exceto raiz).

---

## 3. Normalização e atalhos do comando (SUB `Command`)

1. `LTRIM$(RTRIM$(UCASE$(Comando$)))` — sem espaços nas pontas, tudo maiúsculo.
   - ✅ já é assim (case-insensitive; nomes de **arquivo** continuam case-sensitive).
2. **`RPT`** — repete o último comando válido (armazenado em `Copia$`). 🔜
3. **`X:`** — só drive (ex.: `C:`) troca o diretório corrente para a raiz desse drive.
   - ✅ parcial: apenas `A:` existe; troca de drive ainda não é um comando. ⏳
4. **`.`** como prefixo — executa um *programa externo* (ex.: `.PROG.EXE`). Precisa de PATH. ⏳
5. **`/?`** no final — mostra a ajuda do comando (ex.: `DIR /?`). ✅ parcial: HELP existe
   (sem argumento); abrir `Help.com <comando>`/`HELP\OSB.HLP` ainda não. ⏳
6. Comando desconhecido → `"Comando ou nome de arquivo inválido!"`
   - ✅ no kernel: `"Bad command or file name."` (texto a alinhar ao original).

---

## 4. Tabela de comandos do original

| Comando | Comportamento no OSB.BAS | Status |
|---|---|---|
| `ABOUT` | Executa `About.com`; imprime contatos (site, e-mail, ICQ, MSN) e autor | ✅ básico (texto); 🔜 arquivo externo |
| `APLIC` | Lista/executa aplicativos (`Aplic.com`) | 🔜 |
| `CAL` | Calendário (`Cal.com`, opcional `CAL <nome>`) | 🔜 |
| `CD` | `CD ..`, `CD \`, `CD <dir>`; sem argumento → ajuda (`CD` aceita `CD..` no original) | ✅ (`CD ..`, `CD \`, `CD dir`); `CD..` colado ainda não ⏳ |
| `CLS` | Limpa a tela | ✅ |
| `COLOR` | Chama `Color.com`, relê OSB.CFG, aplica cores | ⏳ |
| `CONFIG` | Chama `System\Config.Com`, depois `CLS` | 🔜 |
| `COPY` | Pergunta origem/destino, copia linha a linha, mostra nº de linhas | ⏳ (escrita ext4) |
| `DATE` | Mostra formato `DD/MM/AAAA`, data atual, aceita nova data | ✅ leitura; setar data ⏳ |
| `DIR` | `FILES` (atual) / `FILES <path>` | ✅ + `/W`; colado `DIR/W` ✅ |
| `ERASE` | Confirma (`S`/`N`), `KILL <arquivo>`; `.` equivale a `*.*` | ⏳ (escrita) |
| `EXIT` | Confirma; para relógio/mouse/arquivos, imprime encerramento, `END` | ✅ parcial (`POWEROFF`/`SHUTDOWN`/`EXIT`); ACPI ainda preso ao QEMU correto |
| `GAMES` | Lista `Conf\Games.Cfg`, roda `Games\<nome>` | 🔜 |
| `HELP` | Ajuda do comando | ✅ básico; `HELP <cmd>` ⏳ |
| `KISS` | `Aplic\Kiss.com [args]` | 🔜 |
| `MD` | `MKDIR <dir>`; sem argumento → ajuda | ⏳ (escrita ext4) |
| `PRINT` | Imprime arquivo na impressora (`LPRINT`) | 🔜 |
| `RD` | `RMDIR <dir>`; sem argumento → ajuda | ⏳ (escrita ext4) |
| `REN` | Pergunta nome antigo/novo, `NAME` | ⏳ (escrita ext4) |
| `SIZE` | `LOF/1024` → "N KiloBytes" | ⏳ |
| `TIME` | Mostra formato `HH:MM:SS`, hora atual, aceita nova hora | ✅ leitura; setar hora ⏳ |
| `TREE` | `Aplic\Tree.com` | 🔜 |
| `TYPE` | Imprime arquivo com **paginação a cada 20 linhas** (`-----Pressione qualquer tecla----`), mostra total de linhas | ✅ sem paginação; paginação ⏳ |
| `VER` | `OSB Versao 0.2` + site + dica ABOUT | ✅ texto equivalente |
| `X` | Escolhe interface gráfica (lista `Conf\Interf.cfg`, `0` = nenhuma) | 🔜 |

---

## 5. Programas externos citados no OSB.BAS

Só fazem sentido quando existirem `.EXE`/`.COM` no disco (Fatias 3+/L3):

`About.com`, `Aplic.com`, `Cal.com`, `Color.com`, `Config.com`, `Games.com`,
`Help.com`, `Kiss.com`, `Logo.com` (com `OSB.IMG`), `NUM.com`, `System\Mouse.com`,
`System\Clock.com` (`/E` liga, `/U` desliga), `Aplic\Tree.com`.

## 6. Arquivos de configuração/ajuda

| Arquivo | Conteúdo |
|---|---|
| `OSB.CFG` (raiz) | cores, diretório do sistema, logo, NumLock, mensagem |
| `Conf\System.Cfg` | `[MOUSE]`, `[CLOCK]` |
| `Conf\Start.Cfg` | comandos do boot |
| `Conf\Games.Cfg` | nomes de jogos |
| `Conf\Interf.Cfg` | interfaces X disponíveis |
| `Help\OSB.HLP` | textos de ajuda por comando |

A ideia do OSB era **autoconfigurável**: o `Color.com` grava o `OSB.CFG` e o boot o relê.
No kernel atual o análogo é `SET` (+ `Config` futuro) sobre a tabela de ambiente.

## 7. Tratamento de erros (transcrito do ON ERROR GOTO)

O original mapeia erros do QBasic para mensagens em português, por exemplo:

| Err | Mensagem |
|---|---|
| 52 | Nome ou número de arquivo não encontrado (retorna ao prompt) |
| 53 | Arquivo não encontrado |
| 57 | Erro de dispositivo I/O — é necessário formatar o disco (`CHDRIVE "c:"`) |
| 58 | Arquivo já existente |
| 61 | Disco cheio. Substitua o disco e tente novamente |
| 64 | Nome de arquivo inválido |
| 67 | Excesso de arquivos |
| 68 | Dispositivo não disponível |
| 70 | Permissão negada |
| 71 | Disco não está pronto → pergunta drive `(A)nular, (R)epetir` |
| 72 | Erro de disco |
| 75 | Erro de acesso ao caminho/arquivo |
| 76 | Erro do caminho |
| outros | "Erro inesperado..." + contato (site/e-mail) + nº do erro |

> Decisão do projeto: as mensagens do kernel podem ser em português como o original,
> mas o **código de erro** deve ser o do contexto (ex.: "Path not found") traduzível
> depois via i18n; a tabela acima é a referência de redação.