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
- **Timer PIT** (8254, canal 0) a **100 Hz via IRQ0** (vetor 0x30): contador global de ticks,
  uptime em segundos e os comandos `TICKS`/`SLEEP <segundos>` (espera dirigida por ticks).
- **Clock RTC** (CMOS, IRQ8 → vetor 0x38): modo 24h + binário, interrupção "update-ended"
  (~1 Hz) que mantém o relógio de software do kernel; comandos `TIME` e `DATE` exibem
  hora e data reais.
- **Agendador round-robin preemptivo** com **N tarefas**: a shell (tarefa 0), uma tarefa
  "demo" (tarefa 1, pilha própria de 16 KiB) e uma tarefa de **usuário** (tarefa 2, que roda
  em **ring 3**). Alternância por **preempção**: o handler do PIT (IRQ0) troca de contexto a
  cada tick (troca de pilha via `osb_switch`), sem `yield`; tarefas concluídas (Done) são
  puladas pelo escalonador. O frame do ISR interrompido fica "estacionado" na pilha de cada
  tarefa até ela ser reescalonada.
- **Ring 3 (modo usuário)**: GDT construída em runtime com segmentos de usuário (DPL3) + **TSS**
  (`ltr`; `RSP0` = topo da pilha de kernel da tarefa de usuário); paging marca só a janela
  `0x400000-0x5FFFFF` como **U/S=1** (o kernel permanece U=0, inacessível ao usuário);
  o IDT expõe o vetor `0x21` como gate **DPL3**. O programa de teste (blob em `user.S`) é
  copiado para `0x400000` e entra em CPL3 via `iretq`. **Syscalls DOS-like** via `INT 21h`
  (função em `AH`): `AH=09h` imprime string terminada em `$` (RSI) e `AH=4Ch` termina.
  Falhas vindas de CPL3 (ex.: `cli` em ring 3 → **#GP**) são **recuperáveis**: o kernel
  termina só aquela tarefa e segue rodando.
- **Desligamento/reboot**: `POWEROFF` faz ACPI S5 (porta PM1a_CNT 0x604) — no QEMU o
  emulador encerra limpo (útil: nada fica preso capturando teclado/mouse do host);
  `REBOOT` pulsa o reset via controlador 8042.
- Entrada de **teclado PS/2** (scancodes set 1) com um mini-shell:
  `VER`, `ABOUT`, `HELP`, `CLS`, `ECHO <texto>`, `TICKS`, `SLEEP <s>`, `TIME`, `DATE`,
  `RING3`, `RING3 BAD`, `HALT`, `POWEROFF`, `REBOOT`.

## Arquitetura

```
src/Osb.Kernel/
├── Osb.Kernel.csproj   # projeto C# "sem .NET": NoStdLib, nada referenciado
├── MiniCoreLib.cs      # mini-CoreLib: System.Object/String/primitivas + stubs que o
│                       # ilc exige do "system module" (ThrowHelpers, Buffer, TypeCast...)
├── Kernel.cs           # kernel: VGA + serial + IDT/PIC + teclado IRQ1 + RTC IRQ8
│                       # + agendador RR preemptivo (3 tarefas) + GDT/TSS + ring 3
│                       # + syscalls INT 21h + shell OSB
├── boot.S              # bootstrap Multiboot2: 32-bit -> long mode -> chama kmain
│                       # (+ helpers: outb/inb, mem*, lidt, sti/cli, halt, lgdt/ltr,
│                       #  enter_user)
├── sched.S             # troca de contexto (osb_switch) + trampolines (demo/user)
├── user.S              # programas de usuário (ring 3): syscall + exit; teste cli->#GP
├── interrupts.S        # gerado no build: 256 stubs de ISR + isr_common + tabela de stubs
├── linker.ld           # layout ELF (carregado em 0x100000)
├── grub.cfg            # menu do GRUB (multiboot2)
├── ROADMAP.md          # plano: multiusuário/ext4, fatias verticais, ring 3+
└── build.sh            # build completo -> osb.iso
```

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
teclado/mouse do host. O kernel espelha a VGA na serial, então você vê tudo no terminal.
Para sair: digite `POWEROFF` no prompt do OSB (ACPI S5 → o QEMU encerra sozinho).

Se rodar com janela (`run-gui` ou QEMU manual), o grab de input pode prender o mouse/
teclado da sessão: **Ctrl+Alt+G** libera o grab e **Ctrl+Alt+2** abre o monitor do QEMU
(depois `quit`). O comando `POWEROFF` também encerra o emulador e libera o input.

## Próximos passos sugeridos

- **Ring 3 já funciona** (Fatia 1): o próximo passo é **processos com espaço de endereço
  próprio** (CR3 por processo) e `spawn`/`exit`/`wait` — ver `ROADMAP.md`, Fatia 2.
- **Multiusuário** (Fatias 3/6): contas, `LOGIN`, sessões/consoles, permissões vindas do ext4.
- **ext4** (Fatias 4/5): driver ATA PIO + leitura/escrita de uma imagem `mke2fs` — o disco
  vira `C:` no idioma DOS.
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
- **Ring 3**: a GDT é montada em `Kernel.cs` (segmentos user code `0x18` / data `0x20`,
  ambos DPL3, + descritor TSS em `0x28`), carregada com `osb_lgdt`/`osb_ltr`. O `RSP0` do
  TSS aponta para o topo da pilha de kernel **da tarefa de usuário** — é para lá que a
  CPU troca quando chega uma interrupção/syscall de CPL3. Na entrada do usuário usamos
  seletores com RPL3 (`0x1B`/`0x23`). O programa em ring 3 é um blob de `user.S` copiado
  para a página U/S=1 em `0x400000` (e um segundo em `0x401000`, que executa `cli` para
  provar o `#GP`). O paging deixa **só** `0x400000-0x5FFFFF` como U=1; o kernel é U=0.
- **Falta de recurso ao falhar (user fault)**: uma exceção com CS.RPL==3 é tratada como
  falha do programa (imprime vetor/err/RIP, marca a tarefa como Done e escala para outra);
  exceções de CPL0 continuam sendo panic fatal.