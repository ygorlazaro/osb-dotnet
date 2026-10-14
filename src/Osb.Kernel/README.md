# Osb.Kernel — o kernel bootável do OSB

Primeira versão **bootável** do OSB (reboot do projeto original de 2000 em QBasic),
escrita em **C# puro freestanding** no estilo do projeto [Cosmos](https://github.com/CosmosOS/Cosmos):
sem sistema operacional hospedeiro, sem framework externo — compilada com **NativeAOT**
(sem runtime, sem BCL) e um **bootloader próprio Multiboot2**.

## O que esta versão faz

- Boota via **GRUB (Multiboot2)** em x86-64 (long mode), a partir de um ISO (BIOS).
- Kernel C# compilado nativamente com `ilc` (NativeAOT) contra um **mini-CoreLib próprio**
  (`MiniCoreLib.cs`) — as únicas "bibliotecas" necessárias para o compilador C# funcionar
  sem um OS.
- Saída de texto em **VGA text mode** (`0xB8000`) espelhada na **COM1 (serial)**.
- **IDT (256 gates) + PIC 8259A** remapeado (IRQ0-15 → vetores **0x30-0x3F**): exceções de CPU
  viram panic legível (vetor + RIP), e o **teclado PS/2 é dirigido por interrupção**
  (IRQ1 → vetor 0x31, fila circular de scancodes, EOI explícito) — sem polling. O remap
  para 0x30 deixa o vetor **0x21** livre para a syscall DOS-like `INT 21h`.
- **Console serial (COM1, IRQ4 → vetor 0x34)**: além do teclado PS/2, a shell também lê
  caracteres recebidos na serial. Isso torna a sessão **headless** utilizável
  (`./build.sh run` usa `-serial stdio`): o que você digita no terminal chega à shell.
  A saída serial usa CRLF.
- **Timer PIT** (8254, canal 0) a **100 Hz via IRQ0** (vetor 0x30): contador global de ticks,
  uptime em segundos e os comandos `TICKS`/`SLEEP <segundos>` (espera dirigida por ticks).
- **Clock RTC** (CMOS, IRQ8 → vetor 0x38): modo 24h + binário, interrupção "update-ended"
  (~1 Hz) que mantém o relógio de software do kernel; comandos `TIME` e `DATE` exibem
  hora e data reais.
- **Agendador round-robin preemptivo** sobre uma **tabela de tarefas** (8 slots): o slot 0 é
  o **console/shell** (roda na pilha de boot) e os demais são tarefas/processos criados por
  comandos (`DEMO`, `SPAWN`), cada um com **16 KiB de pilha de kernel** própria. Alternância
  por **preempção**: o handler do PIT (IRQ0) troca de contexto a cada tick (troca de pilha via
  `osb_switch`), sem `yield`; slots livres são pulados. O frame do ISR interrompido fica
  "estacionado" na pilha de cada tarefa até ela ser reescalonada.
- **Processos com espaço de endereço próprio** (Fatia 2):
  - **Alocador de frames físico** (bitmap de 4 KiB) sobre a mmap do Multiboot2; pool a partir
    de `0x200000` até o topo da RAM (aqui 64 MiB; ~15 872 frames). A janela `0x400000-0x5FFFFF`
    (o `pd[2]` de todo espaço) é reservada. Comando `MEM` mostra uso/faixa.
  - Cada processo tem `PML4+PDPT+PD` **próprios** (3 frames). O kernel é copiado como mapa de
    identidade (`pd[0..1]`, U=0) e `pd[2]` aponta para um **bloco físico privado de 2 MiB**:
    o código vai em `0x400000` e a pilha de usuário em `0x480000` — o *mesmo endereço virtual*
    em todo processo, mas com memória física distinta. O escalonador faz `mov cr3` a cada troca
    e ajusta `TSS.RSP0` para a pilha de kernel do processo.
  - **`spawn`/`exit`/`wait`**: um processo terminado vira **zumbi** (com código de saída) até um
    `WAIT` coletá-lo e liberar frames (`reaping`); `KILL` mata e libera na hora.
- **Gestão de tarefas (job control)**, no espírito DOS+multitarefa:
  - `DEMO [&]` inicia uma tarefa de demonstração; `SPAWN [BAD] [&]` (sinônimo: `RING3`) inicia
    um processo ring 3 com espaço de endereço próprio.
  - **`&` no fim** roda o comando em **background** e devolve o prompt (ex.: `SPAWN &`).
  - **Sem `&`** roda em **foreground**: a shell espera o job terminar e **coleta o código de
    saída**; **Ctrl+C** aborta e volta ao prompt (type-ahead preservado).
  - `TASK` (ou `PS`) lista PID/nome/estado (run/zombie)/bloco de memória — um `ps` simples;
    `KILL <pid>` mata; `WAIT <pid>` espera e coleta; `TAB <pid>` traz ao foreground.
- **Ring 3 (modo usuário)**: GDT construída em runtime com segmentos de usuário (DPL3) + **TSS**
  (`ltr`; `RSP0` = topo da pilha de kernel do processo ring 3, ajustado no escalonador); paging
  marca só a janela `0x400000-0x5FFFFF` como **U/S=1** (o kernel permanece U=0, inacessível ao
  usuário); o IDT expõe o vetor `0x21` como gate **DPL3**. Cada processo recebe seu **próprio
  bloco** de 2 MiB nessa janela e entra em CPL3 via `iretq`. **Syscalls DOS-like** via
  `INT 21h` (função em `AH`): `AH=09h` imprime string terminada em `$` (RSI), `AH=30h` getpid,
  `AH=4Ch` exit(código em AL), `AH=80h` spawn(id do programa), `AH=81h` wait(pid). Falhas vindas
  de CPL3 (ex.: `cli` em ring 3 → **#GP**) são **recuperáveis**: o kernel termina só aquele
  processo (vira zumbi, código `-1`) e segue rodando.
- **Desligamento/reboot**: `POWEROFF` faz ACPI S5 (porta PM1a_CNT 0x604) — no QEMU o
  emulador encerra limpo (útil: nada fica preso capturando teclado/mouse do host);
  `REBOOT` pulsa o reset via controlador 8042.
- Entrada de **teclado PS/2** e **console serial** com um mini-shell:
  `VER`, `ABOUT`, `HELP`, `CLS`, `ECHO <texto>`, `TICKS`, `SLEEP <s>`, `TIME`, `DATE`, `MEM`,
  `DEMO [&]`, `SPAWN [BAD] [&]`, `RING3 [BAD] [&]`, `TASK`/`PS`, `KILL <pid>`, `WAIT <pid>`,
  `TAB <pid>`, `HALT`, `POWEROFF`, `REBOOT`.
- **Histórico e autocomplete na shell**:
  - `HISTORY [n]` lista o histórico numerado (os **100 últimos** por padrão);
  - `(:N)` re-executa a entrada N do histórico (ex.: `(:45)`);
  - as **setas Up/Down** (ou `ESC [ A` / `ESC [ B` no serial) navegam pelo
    histórico, preservando a linha em edição;
  - **TAB** completa nomes de comando (primeiro token), flags (`/S` de `LN`)
    e nomes de arquivo/diretório da ext4 em `A:` (relativos, absolutos, com
    `\` automático para diretórios).
- **Sistema de login** (estilo unix):
  - o boot cai no prompt `login:`; **root entra sem senha**; contas com senha
    pedem `Password:` (sem eco);
  - `USER` lista as contas; `USER /ADD <nome> [senha]`, `USER /CHANGE <nome>
    [senha]`, `USER /DEL <nome> [/HOME]`, `USER /SUDO` / `USER /UNSUDO`;
  - `LOGIN [nome]` troca de usuário; `EXIT`/`LOGOUT` volta ao `login:`;
  - `CHOWN <usuário> <caminho>` (root), `CHMOD <octal> <caminho>` (dono/root),
    `SUDO <comando>` (quem está no SUDOERS, confirmando a própria senha);
  - persistência em `A:\OSB\CONF\PASSWD` (`nome:senha:uid:home`, modo 0600) e
    `A:\OSB\CONF\SUDOERS`;
  - tudo sob `A:\OSB` é do root (uid 0), exceto `A:\OSB\HOME\<usuário>` (do
    usuário, criado no `/ADD`, é o PWD inicial do login) e `A:\OSB\TEMP`
    (modo 0777, compartilhado). `DIR` mostra o nome do dono.
- **Ajuda de comandos**: todo comando interno aceita `/?` (ou `-?`) e exibe sua
  página de ajuda (uso, flags, exemplos); `HELP <comando>` mostra a mesma página,
  e `HELP` sozinho lista todos os comandos. Comandos externos recebem o `/?` e
  tratam a ajuda por conta própria.
- **Pipes**: `A | B` captura a saída de A e a anexa após o primeiro `\n` da linha
  de B; `GREP [/I] <padrão> [arquivo]` filtra linhas (com `[arquivo]` lê o arquivo;
  sem, filtra o stdin do pipe). Pipelines rodam em primeiro plano.

## Arquitetura

```
src/Osb.Kernel/
├── Osb.Kernel.csproj   # projeto C# "sem .NET": NoStdLib, nada referenciado
├── MiniCoreLib.cs      # mini-CoreLib: System.Object/String/primitivas + stubs que o
│                       # ilc exige do "system module" (ThrowHelpers, Buffer, TypeCast...)
├── Kernel.cs           # apenas a orquestração: Main/kmain (ordem de boot) + banner
├── Hardware/           # porta de saída/entrada do kernel
│   ├── Vga.cs          # modo texto 80x25 (espelha tudo no COM1)
│   ├── Serial.cs       # COM1: saída + ring de entrada alimentado pela IRQ4
│   ├── Keyboard.cs     # scancodes PS/2 (IRQ1) + mapa Shift
│   ├── ConsoleControl.cs # sinais do console (Ctrl+C) entre IRQ e shell
│   └── NativeMethods.cs  # P/Invoke "osbnative" (inb/outb, lgdt/lidt, switch, CR3...)
├── Memory/             # memória física
│   ├── Layout.cs       # constantes (VGA, IDT/PIC, GDT, ring 3, ATA, ext4, tasks)
│   └── PhysicalMemory.cs # bitmap de frames + detecção do mapa Multiboot2
├── Cpu/                # temporizadores, memória de controle e entrada de IRQ
│   ├── Pit.cs          # 8254 canal 0 @ 100 Hz + contador de ticks
│   ├── Rtc.cs          # CMOS/RTC: relógio de software (IRQ8), TIME/DATE
│   ├── Gdt.cs          # segmentos kernel/user + TSS (RSP0)
│   ├── Interrupts.cs   # 8259A, IDT e o despachante (osb_handle_interrupt)
│   └── Syscalls.cs     # porta INT 21h (print/getpid/exit/spawn/wait)
├── Processes/          # tarefas e memória por processo
│   ├── Scheduler.cs    # tabela de slots, round-robin preemptivo, jobs
│   └── AddressSpace.cs # CR3 privado por processo (bloco de 2 MiB)
├── Storage/            # persistência
│   ├── Ata.cs          # ATA PIO (setor de 512 bytes)
│   ├── Ext4.cs         # ext4 simplificado (blocos, inodes, diretórios, resolução)
│   └── Cwd.cs          # drive + diretório atual (namespace DOS)
├── Shell/              # o prompt e os comandos
│   ├── Shell.cs        # laço de entrada (serial/teclado) + despacho de comandos
│   ├── Environment.cs  # $VAR / SET
│   ├── Prompt.cs       # $PROMPT no estilo DOS ($P$G...)
│   ├── JobControl.cs   # foreground/background, espera e reap
│   ├── History.cs      # anel de histórico + HISTORY/(:N)/setas Up/Down
│   ├── Completion.cs   # TAB: comandos, /flags e nomes de arquivo
│   ├── Users.cs        # contas, sessão, permissões (PASSWD/SUDOERS)
│   ├── ShellText.cs    # comparações/parse/padding de texto
│   └── Commands/       # SystemCommands, EnvironmentCommands, FileCommands,
│                       # UserCommands (USER/LOGIN/CHOWN/CHMOD/SUDO), TaskCommands
├── OslangKernel.cs     # interpretador OSLANG 0.72 (lexer/parser/runtime) no kernel
├── boot.S              # bootstrap Multiboot2: 32-bit -> long mode -> chama kmain
│                       # (+ helpers: outb/inb, mem*, lidt, sti/cli, halt, lgdt/ltr,
│                       #  enter_user, load_cr3, page tables do kernel)
├── sched.S             # troca de contexto (osb_switch) + trampolines (demo/user)
├── user.S              # programas ring 3: HELLO (teste de isolamento) + BAD (cli->#GP)
├── interrupts.S        # gerado no build: 256 stubs de ISR + isr_common + tabela de stubs
├── linker.ld           # layout ELF (carregado em 0x100000)
├── grub.cfg            # menu do GRUB (multiboot2)
├── ROADMAP.md          # plano: multiusuário/ext4, fatias verticais, ring 3+
└── build.sh            # build completo -> osb.iso
```

Cada subsistema é uma classe `internal static` no seu próprio namespace
(`Osb.Kernel.Hardware`, `Osb.Kernel.Memory`, `Osb.Kernel.Cpu`,
`Osb.Kernel.Processes`, `Osb.Kernel.Storage`, `Osb.Kernel.Shell`): o estado fica
junto de quem o usa e o `Kernel.cs` só ordena a subida da máquina.

Pipeline de build:

```
C# (mini-CoreLib)  ──csc──▶  OsbKernel.dll (IL)
OsbKernel.dll      ──ilc──▶  OsbKernel.o   (objeto x86-64 freestanding)
boot.S + sched.S + user.S + interrupts.S ─clang▶ boot.o + sched.o + user.o + interrupts.o
(...objects) + OsbKernel.o ──ld.lld + linker.ld─▶ kernel.elf (Multiboot2 ELF)
kernel.elf         ──grub-mkrescue─▶       osb.iso
```

## Pré-requisitos

- .NET SDK 10.x
- clang, lld (ld.lld), xorriso, grub-mkrescue (grub-pc-bin), nasm (opcional)
- qemu-system-x86_64 (para testar)

## Build e execução

```sh
cd src/Osb.Kernel
chmod +x build.sh
./build.sh            # gera src/Osb.Kernel/osb.iso
./build.sh run        # build + boot no QEMU (serial no terminal, sem janela)
./build.sh run-gui    # idem, mas abre a janela VGA (cuidado com o grab de input)
./build.sh check      # valida que kernel.elf é Multiboot2
```

`run` usa `-display none -serial stdio`: sem janela, então o QEMU **não captura** o
teclado/mouse do host. O kernel espelha a VGA na serial e **também lê a serial como
entrada**, então você digita diretamente na shell pelo terminal — é só digitar um comando
e Enter. Para sair: `POWEROFF` no prompt do OSB (ACPI S5 → o QEMU encerra sozinho).

Se rodar com janela (`run-gui` ou QEMU manual), o grab de input pode prender o mouse/
teclado da sessão: **Ctrl+Alt+G** libera o grab e **Ctrl+Alt+2** abre o monitor do QEMU
(depois `quit`). O comando `POWEROFF` também encerra o emulador e libera o input.

## Próximos passos sugeridos

- **Processos com espaço de endereço próprio já funcionam** (Fatia 2): CR3 por processo,
  `spawn`/`exit`/`wait` e isolamento comprovado — ver `ROADMAP.md`, Fatia 2.
- **Multiusuário** (Fatias 3/6): contas, `LOGIN`, sessões/consoles, permissões vindas do ext4.
- **ext4** (Fatias 4/5): leitura já operacional (`DIR`/`CD`/`TYPE`); falta a
  **escrita** (bitmaps de bloco/inode → `MD`/`RD`) e depois `COPY DEL REN`. O disco
  é `A:` no idioma DOS; é o que permitirá comandos externos (`.EXE`) e scripts.
- **Conforto da shell já implementado:** variáveis de ambiente `$VAR` (nomes case-insensitive),
  `SET [NAME]` / `SET NAME=value`, `PROMPT` DOS (`$P$G` e tokens `$D/$T/$N/...`) com
  defaults de boot `PATH PROMPT USER HOME TEMP COMSPEC OS VER LASTDRIVE`.
- ACPI + MMIO, framebuffer via `multiboot2` (modo gráfico) em vez de VGA text.
- **Endurecimento**: IST para exceções, SMAP/SMEP, validar ponteiros de syscall com mais rigor.

## Notas

- O ilc é localizado automaticamente no cache NuGet
  (`runtime.linux-x64.microsoft.dotnet.ilcompiler`).
- `MiniCoreLib.cs` é a fronteira do "runtime": tudo que o C# exige sem um OS.
  Manter pequena; nada de GC/alocação — o kernel usa buffers fixos/stackalloc.
- `interrupts.S` é gerado deterministicamente pelo `build.sh` (256 stubs: cada um
  empurra `[vetor][dummy]`, descartando o error code da CPU nos vetores 8/10-14/17).
  O `isr_common` salva **GPRs + XMM0-15** (o handler SysV pode sujar registradores
  vetorizados do código interrompido), realinha a stack em 16 bytes, chama
  `osb_handle_interrupt` e faz `iretq`.
- O shell "dorme" com `sti; hlt` quando a fila de scancodes está vazia; a IRQ1
  entrega o scancode e a IRQ0 (100 Hz) acorda o idle a cada tick.
- **Qualquer IRQ do slave (IRQ8-15) exige o cascateamento no master**: sem IRQ2
  desmascarado no 8259A master (OCW1 = `0xF8` = IRQ0/IRQ1/IRQ2), a IRQ8 do RTC nunca
  chega à CPU. `0xF8`, e não `0xFB` (que mascara justamente IRQ0 e IRQ1).
- **Preempção**: `SchedulerTick` roda dentro do handler da IRQ0 (portanto com IF=0, logo
  atômica) e faz `osb_switch` para a outra tarefa. O EOI é enviado **antes** da troca,
  para o PIC poder entregar o próximo tick já na tarefa nova. Quando a tarefa é
  reescalonada, ela retoma de dentro do próprio `SchedulerTick`, desenrola o handler,
  restaura GPRs+XMM do frame e faz `iretq` — o contexto interrompido volta intacto.
- `osb_switch` só salva os registradores **callee-saved** (rbx, rbp, r12-r15). Isso basta
  porque o `isr_common` já salva **todos** os GPRs e XMM no frame de interrupção antes de
  chamar o handler: o que a troca de contexto precisa preservar são só os callee-saved
  vivos entre o `iretq` e o ponto de troca.
- A tarefa demo tem pilha fixa de 16 KiB em `.bss`; a IRQ pode interrompê-la e roda na
  própria pilha da tarefa (sem TSS), então o espaço precisa cobrir ISR + handler.
- **SSE requer CR0.EM=0**: com `CR0.EM=1` toda instrução SSE do código C# (ex.
  `movups` de um stackalloc) sofre `#UD`. O `boot.S` força CR0 (PG|NE|WP, EM/TS=0)
  e CR4 (PAE|OSFXSR|OSXMMEXCPT) na entrada do long mode.
- **PIC remapeado para 0x30-0x3F** (e não 0x20-0x2F): libera o vetor **0x21** para a
  syscall `INT 21h`, sem colidir com a IRQ1 (teclado). Constantes `PicBase=0x30`,
  `TimerVector=0x30`, `KbdVector=0x31`, `RtcVector=0x38`.
- **Ring 3 + processos**: a GDT é montada em `Cpu/Gdt.cs` (segmentos user code `0x18` / data `0x20`,
  ambos DPL3, + descritor TSS em `0x28`), carregada com `osb_lgdt`/`osb_ltr`. O `RSP0` do
  TSS aponta para o topo da pilha de kernel **do processo de usuário** — é para lá que a
  CPU troca quando chega uma interrupção/syscall de CPL3. Na entrada do usuário usamos
  seletores com RPL3 (`0x1B`/`0x23`). Cada processo tem page tables próprias (`PML4/PDPT/PD`);
  copiamos o mapa de identidade do kernel (`osb_master_pd()`) e trocamos só `pd[2]` por um bloco
  físico privado de 2 MiB. O programa (`user.S`) é copiado para o início do bloco e o escalonador
  troca o CR3 (`osb_load_cr3`) a cada contexto. Um segundo blob (`BAD`) executa `cli` para provar
  o `#GP` em CPL3.
- **Por que `pd[2]` e a janela reservada**: como o kernel é *identity-mapped* em 0-1 GiB, o
  endereço físico de um bloco recém-alocado só coincide com o virtual fora da janela do usuário.
  Por isso o alocador nunca entrega frames em `0x400000-0x600000` (512 frames): assim o kernel
  consegue inicializar qualquer bloco pela sua identidade, mesmo rodando sob o CR3 de um processo
  que pediu `spawn`. Zumbis que ninguém espera ocupam um slot, mas os frames só voltam no reaping.
- **Falta de recurso ao falhar (user fault)**: uma exceção com CS.RPL==3 é tratada como
  falha do programa (imprime vetor/err/RIP, marca a tarefa como **zumbi** com código `-1` e
  escala para outra); exceções de CPL0 continuam sendo panic fatal.